using System.Globalization;

namespace Reader.Core;

/// <summary>
/// The words printed on a page, as in a printed picture book (see <see cref="PageText"/>). This holds
/// what to print and the sizes everything is worked out from; drawing the letters is up to the screen
/// (Reader.Avalonia's PrintedWordsPicture), which lays them out once at <see cref="ReferenceHeight"/>
/// and scales the result, so the lines break in the same places at every size.
/// </summary>
public static class PrintedWords
{
    /// <summary>The words are laid out as if the picture were this many units tall, then scaled.</summary>
    public const double ReferenceHeight = 1000;

    /// <summary>The distance from one line to the next, in letter sizes.</summary>
    public const double LineHeight = 1.3;

    /// <summary>How far the soft light glow round the letters reaches, in letter sizes.</summary>
    public const double HaloReach = 0.3;

    /// <summary>
    /// What a page prints: its own words if it has them, otherwise the words of its stops in order, joined
    /// into one paragraph. Empty when there is nothing to print.
    /// </summary>
    public static string Content(Page page)
    {
        if (page.Text?.Content is { } own && !string.IsNullOrWhiteSpace(own)) return own.Trim();
        return Captions(page);
    }

    /// <summary>The page's stop captions, in order, joined into one paragraph.</summary>
    public static string Captions(Page page) =>
        string.Join(" ", page.Stops.Select(s => s.Caption?.Trim()).Where(c => !string.IsNullOrEmpty(c)));

    /// <summary>True when this page prints words: it has a words box and something to put in it.</summary>
    public static bool Prints(Page page) => page.Text is not null && page.Text.Box.W > 0 && Content(page).Length > 0;

    /// <summary>The letter size in pixels for a picture drawn <paramref name="pagePixelsHigh"/> pixels tall.</summary>
    public static double FontPixels(PageText text, double pagePixelsHigh) => text.SizeOrDefault * pagePixelsHigh;

    /// <summary>The words box on a page drawn at <paramref name="page"/> (in any units: pixels, world units).</summary>
    public static RectD BoxOn(PageText text, RectD page) =>
        new(page.X + text.Box.X * page.W, page.Y + text.Box.Y * page.H, text.Box.W * page.W, text.Box.H * page.H);

    /// <summary>
    /// A rough idea of how tall the words come out, as a fraction of the picture's height, for when the real
    /// letters can't be measured: an average letter is taken as a little over half the letter size wide.
    /// </summary>
    public static double EstimateHeight(PageText text, string content, double aspect)
    {
        double size = text.SizeOrDefault;
        double across = text.Box.W * aspect;                      // the box's width in picture heights
        if (across <= 0 || size <= 0) return 0;
        double perLine = Math.Max(1, Math.Floor(across / (size * 0.52)));
        int lines = 0;
        foreach (string paragraph in content.Split('\n'))
            lines += Math.Max(1, (int)Math.Ceiling(paragraph.Trim().Length / perLine));
        return lines * size * LineHeight;
    }

    /// <summary>How much of <paramref name="inner"/> lies inside <paramref name="outer"/>, 0 to 1.</summary>
    public static double Coverage(RectD inner, RectD outer)
    {
        double area = inner.W * inner.H;
        if (area <= 0) return 0;
        double w = Math.Min(inner.Right, outer.Right) - Math.Max(inner.X, outer.X);
        double h = Math.Min(inner.Bottom, outer.Bottom) - Math.Max(inner.Y, outer.Y);
        return w <= 0 || h <= 0 ? 0 : w * h / area;
    }

    /// <summary>A colour written #rgb or #rrggbb, as red, green and blue; null when it isn't one.</summary>
    public static (byte R, byte G, byte B)? ParseColor(string? text)
    {
        if (string.IsNullOrEmpty(text) || text[0] != '#') return null;
        string hex = text[1..];
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return null;
        return ((byte)(v >> 16), (byte)(v >> 8 & 0xff), (byte)(v & 0xff));
    }

    /// <summary>
    /// A words box for a page that has none yet: across the top of the picture, or of the page holding the
    /// first stop on a double-page spread, where pictures usually have sky.
    /// </summary>
    public static PageText Suggest(Page page, double aspect)
    {
        bool spread = aspect >= 1.2 && !page.Explore;
        double x = 0.05, w = 0.9;
        if (spread)
        {
            bool firstOnRight = page.Stops.Count > 0 && page.Stops[0].Box.X + page.Stops[0].Box.W / 2 > 0.5;
            // The other page from the first stop, so the words don't sit on what the camera looks at first.
            x = firstOnRight ? 0.04 : 0.54;
            w = 0.42;
        }
        return new PageText { Box = new RectD(x, 0.05, w, 0.22), Size = DefaultSizeFor(aspect) };
    }

    static double DefaultSizeFor(double aspect) => aspect > 2.2 ? 0.035 : PageText.DefaultSize;
}
