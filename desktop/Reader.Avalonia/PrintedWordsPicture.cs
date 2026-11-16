using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Reader.Core;

namespace Reader.Ui;

/*
 * A page's printed words (see PageText in Reader.Core), drawn once into a small see-through picture that
 * covers only the words box, and from then on drawn with the page picture exactly as the page picture
 * is: on the flat page, in the open book, on the strips of a turning page and in the slide between pages.
 * So the words zoom, move and curl with the picture and look the same in all of them.
 *
 * Why a picture of the words rather than drawing the letters every frame: the curling page is made of
 * many strips, and letters drawn strip by strip would cost far more than one small picture cut the same
 * way as the page. And why a small picture of its own rather than writing the words into the page picture:
 * the page pictures are kept at several sizes and the full-size ones come and go, so each would have to be
 * redrawn, and switching the words off would mean loading the book again.
 *
 * The letters are laid out once as if the picture were PrintedWords.ReferenceHeight units tall and then
 * scaled, so the lines break in the same places at every size (the editor's preview and Check book use
 * the same layout).
 */

/// <summary>Lays out and measures a page's printed words.</summary>
public static class WordsLayout
{
    /// <summary>The bundled apps carry no font of their own, so the words use the system's, as the pop-up does, a little heavier.</summary>
    public static readonly Typeface Face = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    /// <summary>The words laid out for a page <paramref name="aspect"/> times as wide as tall, in reference units.</summary>
    public static FormattedText Layout(PageText text, string content, double aspect, IBrush ink)
    {
        double size = text.SizeOrDefault * PrintedWords.ReferenceHeight;
        return new FormattedText(content, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, size, ink)
        {
            MaxTextWidth = Math.Max(1, text.Box.W * aspect * PrintedWords.ReferenceHeight),
            TextAlignment = text.AlignOrLeft switch { PageText.Center => TextAlignment.Center, PageText.Right => TextAlignment.Right, _ => TextAlignment.Left },
            LineHeight = size * PrintedWords.LineHeight,
        };
    }

    /// <summary>
    /// How much room the words take: their widest line as a fraction of the picture's width, and their
    /// height as a fraction of its height.
    /// </summary>
    public static (double Width, double Height) Measure(PageText text, string content, double aspect)
    {
        if (string.IsNullOrWhiteSpace(content) || aspect <= 0) return (0, 0);
        FormattedText ft = Layout(text, content, aspect, Brushes.Black);
        return (ft.Width / (aspect * PrintedWords.ReferenceHeight), ft.Height / PrintedWords.ReferenceHeight);
    }

    public static Color Ink(PageText text) =>
        PrintedWords.ParseColor(text.ColorOrDefault) is var (r, g, b) ? Color.FromRgb(r, g, b) : Color.Parse(PageText.DefaultColor);

    /// <summary>The glow behind the letters: warm white behind dark letters, a deep brown behind light ones.</summary>
    public static Color Halo(Color ink) =>
        0.299 * ink.R + 0.587 * ink.G + 0.114 * ink.B > 150 ? Color.FromRgb(36, 26, 18) : Color.FromRgb(255, 254, 248);
}

/// <summary>
/// A page's printed words as a see-through picture covering the words box (and the glow round it), at a
/// few sizes like the page pictures, the biggest first.
/// </summary>
public sealed class PrintedWordsPicture : IDisposable
{
    /// <summary>No side of the biggest copy is longer than this, so it stays a small picture even on a big page.</summary>
    public const int MostPixels = 2048;

    PrintedWordsPicture(RectD region, Bitmap[] levels, string key)
    {
        Region = region;
        Levels = levels;
        Key = key;
    }

    /// <summary>The part of the page the picture covers, in fractions of the page's width and height.</summary>
    public RectD Region { get; }
    public Bitmap[] Levels { get; }
    /// <summary>Everything the picture was made from, to tell when it needs making again.</summary>
    public string Key { get; }

    /// <summary>The bytes the picture holds at all its sizes.</summary>
    public long Bytes => Levels.Sum(b => (long)b.PixelSize.Width * b.PixelSize.Height * 4);

    /// <summary>What a page's words picture is made from: change any of it and it is made again.</summary>
    public static string KeyFor(Page page, double aspect) =>
        page.Text is not PageText t ? "" : string.Join("|", PrintedWords.Content(page), t.Box, t.SizeOrDefault.ToString(CultureInfo.InvariantCulture),
            t.AlignOrLeft, t.ColorOrDefault, aspect.ToString("0.####", CultureInfo.InvariantCulture));

    /// <summary>
    /// The smallest copy that is sharp when the whole page is drawn <paramref name="pagePixelsHigh"/>
    /// pixels tall, or the biggest one when none is.
    /// </summary>
    public Bitmap For(double pagePixelsHigh)
    {
        double wanted = Region.H * pagePixelsHigh * 0.98;
        for (int i = Levels.Length - 1; i >= 0; i--) if (Levels[i].PixelSize.Height >= wanted) return Levels[i];
        return Levels[0];
    }

    /// <summary>
    /// Make the words picture for a page <paramref name="aspect"/> times as wide as tall, as sharp as a
    /// page <paramref name="pixelsHigh"/> pixels tall would need. Null when the page prints nothing.
    /// Draws, so it must be called on the screen's thread.
    /// </summary>
    public static PrintedWordsPicture? Make(Page page, double aspect, double pixelsHigh)
    {
        if (!PrintedWords.Prints(page) || aspect <= 0 || pixelsHigh <= 0) return null;
        PageText t = page.Text!;
        string content = PrintedWords.Content(page);
        const double R = PrintedWords.ReferenceHeight;
        Color inkColour = WordsLayout.Ink(t), haloColour = WordsLayout.Halo(inkColour);
        FormattedText ft = WordsLayout.Layout(t, content, aspect, new ImmutableSolidColorBrush(inkColour));

        // The part of the page to cover, in page heights across and down: the box, or the words if they
        // run out of it, with room round them for the glow, kept on the page.
        double size = t.SizeOrDefault, reach = size * (PrintedWords.HaloReach + 0.1);
        double left = t.Box.X * aspect, top = t.Box.Y;
        double width = Math.Max(t.Box.W * aspect, ft.Width / R), height = Math.Max(t.Box.H, ft.Height / R);
        double x0 = Math.Max(0, left - reach), y0 = Math.Max(0, top - reach);
        double x1 = Math.Min(aspect, left + width + reach), y1 = Math.Min(1, top + height + reach);
        if (x1 - x0 <= 0 || y1 - y0 <= 0) return null;

        double density = Math.Min(pixelsHigh, MostPixels / Math.Max(x1 - x0, y1 - y0));
        var px = new PixelSize(Math.Max(1, (int)Math.Ceiling((x1 - x0) * density)), Math.Max(1, (int)Math.Ceiling((y1 - y0) * density)));
        var origin = new Point(left * R, top * R);
        Geometry? outline = ft.BuildGeometry(origin);
        Matrix toPixels = Matrix.CreateTranslation(-x0 * R, -y0 * R) * Matrix.CreateScale(density / R, density / R);

        // The soft glow: the letters drawn fat at a quarter of the size, then stretched up smoothly, which
        // blurs them. Light enough to lift the letters off a busy wash without looking like a label.
        var glowSize = new PixelSize(Math.Max(1, px.Width / 4), Math.Max(1, px.Height / 4));
        using var glow = new RenderTargetBitmap(glowSize);
        using (DrawingContext g = glow.CreateDrawingContext())
        using (g.PushTransform(toPixels * Matrix.CreateScale(glowSize.Width / (double)px.Width, glowSize.Height / (double)px.Height)))
        {
            if (outline is not null)
            {
                var haloBrush = new ImmutableSolidColorBrush(haloColour);
                g.DrawGeometry(haloBrush, new ImmutablePen(haloBrush, size * R * 0.34, lineJoin: PenLineJoin.Round, lineCap: PenLineCap.Round), outline);
            }
        }

        var picture = new RenderTargetBitmap(px);
        using (DrawingContext ctx = picture.CreateDrawingContext())
        {
            using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            using (ctx.PushOpacity(0.7))
                ctx.DrawImage(glow, new Rect(0, 0, px.Width, px.Height));
            using (ctx.PushTransform(toPixels))
            {
                // A thin, firmer edge of the same light right round each letter, then the letters.
                if (outline is not null)
                    ctx.DrawGeometry(null, new ImmutablePen(new ImmutableSolidColorBrush(haloColour, 0.6), size * R * 0.1, lineJoin: PenLineJoin.Round), outline);
                ctx.DrawText(ft, origin);
            }
        }

        // Smaller copies for when the page is drawn small, as for the page pictures.
        var levels = new List<Bitmap> { picture };
        Bitmap from = picture;
        while (from.PixelSize.Height >= 96 && levels.Count < 4)
        {
            var half = new PixelSize(Math.Max(1, (from.PixelSize.Width + 1) / 2), Math.Max(1, (from.PixelSize.Height + 1) / 2));
            var smaller = new RenderTargetBitmap(half);
            using (DrawingContext ctx = smaller.CreateDrawingContext())
            using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
                ctx.DrawImage(from, new Rect(0, 0, half.Width, half.Height));
            levels.Add(from = smaller);
        }
        var region = new RectD(x0 / aspect, y0, (x1 - x0) / aspect, y1 - y0);
        return new PrintedWordsPicture(region, levels.ToArray(), KeyFor(page, aspect));
    }

    public void Dispose()
    {
        foreach (Bitmap b in Levels) b.Dispose();
    }
}
