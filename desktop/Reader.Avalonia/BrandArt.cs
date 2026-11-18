using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Reader.Core;

namespace Reader.Ui;

/// <summary>
/// The app's mascot: a small dassie (a rock hyrax), round and friendly, drawn in code in the app's
/// colours. It only appears on the welcome, in a corner of the shelf and at The End. It never speaks
/// and never asks for anything. When it first appears it may blink once; after that it is still.
/// </summary>
public sealed class Dassie : Control
{
    public enum Pose { Sit, Lie, Peek }

    public static readonly StyledProperty<Pose> PoseProperty = AvaloniaProperty.Register<Dassie, Pose>(nameof(Look), Pose.Sit);

    static Dassie() => AffectsRender<Dassie>(PoseProperty);

    public Dassie() => IsHitTestVisible = false;

    public Pose Look
    {
        get => GetValue(PoseProperty);
        set => SetValue(PoseProperty, value);
    }

    /// <summary>The shape of each pose, width to height, for sizing the control.</summary>
    public static double Aspect(Pose p) => p switch { Pose.Lie => 100 / 46.0, Pose.Peek => 100 / 72.0, _ => 1 };

    bool _blinking, _blinked;

    /// <summary>The one blink when it first appears (none with reduced movement, or in screenshots).</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_blinked || !Motion.Allowed) return;
        _blinked = true;
        DispatcherTimer.RunOnce(() =>
        {
            _blinking = true;
            InvalidateVisual();
            DispatcherTimer.RunOnce(() => { _blinking = false; InvalidateVisual(); }, TimeSpan.FromMilliseconds(170));
        }, TimeSpan.FromMilliseconds(900));
    }

    static readonly Color Fur = Color.Parse("#a9805a"), FurDark = Color.Parse("#8c6545"), Belly = Color.Parse("#e2c9a3"),
        EarInner = Color.Parse("#c99f78"), EyeInk = Color.Parse("#2b2118"), Nose = Color.Parse("#3d2c22"), Outline = Color.Parse("#6f5036");
    static readonly IImmutableBrush FurBrush = new ImmutableSolidColorBrush(Fur), DarkBrush = new ImmutableSolidColorBrush(FurDark),
        BellyBrush = new ImmutableSolidColorBrush(Belly), EarBrush = new ImmutableSolidColorBrush(EarInner), EyeBrush = new ImmutableSolidColorBrush(EyeInk),
        NoseBrush = new ImmutableSolidColorBrush(Nose), Glint = new ImmutableSolidColorBrush(Colors.White),
        Blush = new ImmutableSolidColorBrush(Color.Parse("#e79a86"), 0.55);
    static readonly ImmutablePen Line = new(new ImmutableSolidColorBrush(Outline), 1.3, lineJoin: PenLineJoin.Round);
    static readonly ImmutablePen Lid = new(new ImmutableSolidColorBrush(EyeInk), 1.6, lineCap: PenLineCap.Round);

    public override void Render(DrawingContext ctx) => Draw(ctx, Look, Bounds.Width, Bounds.Height, _blinking);

    /// <summary>Draw the dassie in a pose, as big as fits in w by h, standing on the bottom edge.</summary>
    public static void Draw(DrawingContext ctx, Pose pose, double w, double h, bool blinking = false)
    {
        if (w <= 0 || h <= 0) return;
        double bw = 100, bh = 100 / Aspect(pose);
        double k = Math.Min(w / bw, h / bh);
        using var _ = ctx.PushTransform(Matrix.CreateScale(k, k) * Matrix.CreateTranslation((w - bw * k) / 2, h - bh * k));
        switch (pose)
        {
            case Pose.Lie: DrawLying(ctx, blinking); break;
            case Pose.Peek: DrawPeek(ctx, blinking); break;
            default: DrawSitting(ctx, blinking); break;
        }
    }

    static void Eye(DrawingContext ctx, Point c, double r, bool blinking)
    {
        if (blinking)
        {
            ctx.DrawLine(Lid, new Point(c.X - r, c.Y), new Point(c.X + r, c.Y));
            return;
        }
        ctx.DrawEllipse(EyeBrush, null, c, r, r);
        ctx.DrawEllipse(Glint, null, new Point(c.X + r * 0.35, c.Y - r * 0.35), r * 0.32, r * 0.32);
    }

    static void DrawSitting(DrawingContext ctx, bool blinking)
    {
        // A round loaf sitting up, facing right: body, head, one ear, small paws.
        ctx.DrawEllipse(new ImmutableSolidColorBrush(Palette.ShadowOf(0.18)), null, new Point(52, 97), 34, 3.2);
        ctx.DrawEllipse(FurBrush, Line, new Point(46, 66), 36, 30);
        ctx.DrawEllipse(DarkBrush, null, new Point(34, 58), 20, 16);
        ctx.DrawEllipse(FurBrush, null, new Point(42, 64), 30, 26);
        ctx.DrawEllipse(BellyBrush, null, new Point(60, 72), 17, 19);
        // The ear behind the head.
        ctx.DrawEllipse(FurBrush, Line, new Point(50, 19), 5.4, 5);
        ctx.DrawEllipse(EarBrush, null, new Point(50.4, 19.4), 2.6, 2.4);
        ctx.DrawEllipse(FurBrush, Line, new Point(64, 36), 23, 21);
        ctx.DrawEllipse(BellyBrush, null, new Point(77, 43), 10, 8);
        ctx.DrawEllipse(BellyBrush, null, new Point(69, 25.5), 4.2, 2);
        Eye(ctx, new Point(70, 31), 3.4, blinking);
        ctx.DrawEllipse(Blush, null, new Point(68, 43), 4, 2.8);
        ctx.DrawEllipse(NoseBrush, null, new Point(86, 39), 3.2, 2.5);
        ctx.DrawGeometry(null, Lid, Geometry.Parse("M83 45 q-2.5 2 -5 0.6"));
        // Paws.
        ctx.DrawEllipse(FurBrush, Line, new Point(62, 94), 7.5, 4.6);
        ctx.DrawEllipse(FurBrush, Line, new Point(77, 93), 7.5, 4.6);
    }

    static void DrawLying(DrawingContext ctx, bool blinking)
    {
        // Lying stretched out in the sun, facing right, as on the wordmark.
        ctx.DrawEllipse(new ImmutableSolidColorBrush(Palette.ShadowOf(0.16)), null, new Point(50, 44.5), 42, 2.2);
        ctx.DrawEllipse(FurBrush, Line, new Point(44, 31), 40, 13.5);
        ctx.DrawEllipse(DarkBrush, null, new Point(38, 25), 28, 7);
        ctx.DrawEllipse(FurBrush, null, new Point(44, 32), 37, 11);
        ctx.DrawEllipse(BellyBrush, null, new Point(50, 39), 26, 4.5);
        ctx.DrawEllipse(FurBrush, Line, new Point(72.5, 12.5), 3.8, 3.5);
        ctx.DrawEllipse(EarBrush, null, new Point(72.8, 12.8), 1.8, 1.6);
        ctx.DrawEllipse(FurBrush, Line, new Point(81, 24), 15, 13.5);
        ctx.DrawEllipse(BellyBrush, null, new Point(89, 29), 7, 5.5);
        Eye(ctx, new Point(86, 20), 2.5, blinking);
        ctx.DrawEllipse(Blush, null, new Point(83, 29), 3, 2);
        ctx.DrawEllipse(NoseBrush, null, new Point(95.5, 25.5), 2.4, 1.9);
        ctx.DrawEllipse(FurBrush, Line, new Point(86, 40), 6, 3.4);
    }

    static void DrawPeek(DrawingContext ctx, bool blinking)
    {
        // Looking over an edge: the round face, two ears and two paws.
        // Small round ears low on the sides of a wide head, as a rock hyrax has.
        foreach (double x in new[] { 20.0, 80.0 })
        {
            ctx.DrawEllipse(FurBrush, Line, new Point(x, 30), 5.2, 4.8);
            ctx.DrawEllipse(EarBrush, null, new Point(x, 30.4), 2.5, 2.3);
        }
        ctx.DrawEllipse(FurBrush, Line, new Point(50, 44), 33, 26);
        ctx.DrawEllipse(DarkBrush, null, new Point(50, 24), 16, 5);
        ctx.DrawEllipse(FurBrush, null, new Point(50, 46), 31, 23);
        ctx.DrawEllipse(BellyBrush, null, new Point(50, 55), 14, 9.5);
        // The pale marks above the eyes.
        ctx.DrawEllipse(BellyBrush, null, new Point(36.5, 33.8), 3, 1.6);
        ctx.DrawEllipse(BellyBrush, null, new Point(63.5, 33.8), 3, 1.6);
        Eye(ctx, new Point(38, 40), 3.8, blinking);
        Eye(ctx, new Point(62, 40), 3.8, blinking);
        ctx.DrawEllipse(Blush, null, new Point(29, 52), 4.5, 3);
        ctx.DrawEllipse(Blush, null, new Point(71, 52), 4.5, 3);
        ctx.DrawEllipse(NoseBrush, null, new Point(50, 50.5), 4.8, 3.4);
        ctx.DrawGeometry(null, Lid, Geometry.Parse("M45.5 56 q2.2 2 4.5 0 q2.3 2 4.5 0"));
        ctx.DrawEllipse(FurBrush, Line, new Point(29, 67), 9, 5.5);
        ctx.DrawEllipse(FurBrush, Line, new Point(71, 67), 9, 5.5);
    }
}

/// <summary>
/// The app's name as a wordmark: lowercase, round and heavy, with the dassie lying along the top of
/// the double s and the dot of the i in cherry, after the sketch in docs/design/mockups/wordmarks.png.
/// It reads <see cref="Brand.Name"/>, so a new name is drawn the same way (the dassie then lies over
/// the middle of the word).
/// </summary>
public sealed class Wordmark : Control
{
    public static readonly StyledProperty<double> SizeProperty = AvaloniaProperty.Register<Wordmark, double>(nameof(Size), 40);
    public static readonly StyledProperty<IBrush?> InkProperty = AvaloniaProperty.Register<Wordmark, IBrush?>(nameof(Ink));

    static Wordmark() => AffectsMeasure<Wordmark>(SizeProperty);

    public Wordmark()
    {
        IsHitTestVisible = false;
        Avalonia.Automation.AutomationProperties.SetName(this, Brand.Name);
    }

    /// <summary>The letter size.</summary>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>The letters' colour (the ink of the theme when left out).</summary>
    public IBrush? Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }

    static readonly Typeface Face = new(Palette.Font, FontStyle.Normal, FontWeight.Black);
    string Word => Brand.Name.ToLowerInvariant();

    FormattedText Text(string s, IBrush ink) => new(s, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, Size, ink);

    /// <summary>Room above the letters for the dassie.</summary>
    double Above => Size * 0.42;

    protected override Size MeasureOverride(Size availableSize)
    {
        FormattedText t = Text(Word, Brushes.Black);
        return new Size(Math.Ceiling(t.WidthIncludingTrailingWhitespace + Size * 0.08), Math.Ceiling(t.Height + Above - Size * 0.18));
    }

    public override void Render(DrawingContext ctx)
    {
        IBrush ink = Ink ?? Palette.InkBrush;
        string word = Word;
        FormattedText t = Text(word, ink);
        double top = Above - Size * 0.2;
        ctx.DrawText(t, new Point(0, top));
        double baseline = top + t.Baseline;
        double xHeight = Size * 0.49;

        // The dassie lies on the double s, or over the middle of the word.
        int ss = word.IndexOf("ss", StringComparison.Ordinal);
        double from, across;
        if (ss >= 0)
        {
            from = Text(word[..ss], ink).WidthIncludingTrailingWhitespace;
            across = Text(word[..(ss + 2)], ink).WidthIncludingTrailingWhitespace - from;
        }
        else { across = t.Width * 0.3; from = (t.Width - across) / 2; }
        double dw = across * 1.18, dh = dw / Dassie.Aspect(Dassie.Pose.Lie);
        double dx = from + (across - dw) / 2 - across * 0.22, dy = baseline - xHeight - dh * 0.84;
        using (ctx.PushTransform(Matrix.CreateTranslation(dx, dy))) Dassie.Draw(ctx, Dassie.Pose.Lie, dw, dh);

        // The dot of the i in cherry.
        int i = word.IndexOf('i', Math.Max(0, ss));
        if (i >= 0)
        {
            double ix = Text(word[..i], ink).WidthIncludingTrailingWhitespace, iw = Text("i", ink).WidthIncludingTrailingWhitespace;
            ctx.DrawEllipse(new ImmutableSolidColorBrush(Palette.Ribbon), null, new Point(ix + iw / 2, baseline - Size * 0.632), Size * 0.112, Size * 0.112);
        }
    }
}

/// <summary>
/// The app icons, drawn in code like the mascot: the dassie peeking over the top of an open book, in
/// front of a brass sun. The reader's icon is on bottle green; the Book Editor's is on cream paper with
/// a pencil across the corner, so the two belong together but can't be mistaken for each other on a
/// taskbar. Very small sizes (24 pixels and under) leave out the book and the sun so the face still
/// reads. Rendered to .ico and Android launcher pictures by "BookEditor --make-icons" (see the README).
/// </summary>
public static class AppIcon
{
    /// <summary>What goes behind the picture.</summary>
    public enum Back
    {
        /// <summary>A rounded square: Windows and the Android launcher's square icon.</summary>
        Square,
        /// <summary>A circle: Android's round icon.</summary>
        Circle,
        /// <summary>Nothing: the foreground of an Android adaptive icon (the colour is a layer of its own).</summary>
        None,
    }

    /// <summary>The background colour of each icon, for Android's adaptive icon background layer.</summary>
    public static Color BackColour(bool editor) => editor ? Color.Parse("#f8f0dc") : Color.Parse("#2f6b50");

    static IBrush Background(bool editor) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = editor
            ? new GradientStops { new GradientStop(Color.Parse("#fffaee"), 0), new GradientStop(Color.Parse("#efe2c2"), 1) }
            : new GradientStops { new GradientStop(Color.Parse("#4a8a6b"), 0), new GradientStop(Color.Parse("#245640"), 1) },
    }.ToImmutable();

    static readonly ImmutablePen BookLine = new(new ImmutableSolidColorBrush(Color.Parse("#6f5036")), 1.6, lineJoin: PenLineJoin.Round, lineCap: PenLineCap.Round);

    /// <summary>
    /// Draw the icon filling a square of <paramref name="size"/> units. <paramref name="art"/> shrinks the
    /// picture inside the square (Android's adaptive foreground keeps its picture in the middle two thirds).
    /// </summary>
    public static void Draw(DrawingContext ctx, double size, bool editor, Back back = Back.Square, double art = 1)
    {
        double k = size / 100;
        using var _ = ctx.PushTransform(Matrix.CreateScale(k, k));
        bool tiny = size <= 24.5;
        switch (back)
        {
            case Back.Square:
                ctx.DrawRectangle(Background(editor), editor ? new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#c9b483")), tiny ? 4 : 2) : null,
                    new Rect(tiny ? 2 : 1, tiny ? 2 : 1, tiny ? 96 : 98, tiny ? 96 : 98), tiny ? 18 : 22, tiny ? 18 : 22);
                break;
            case Back.Circle:
                ctx.DrawEllipse(Background(editor), editor ? new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#c9b483")), 2) : null, new Point(50, 50), 49, 49);
                break;
        }
        using var __ = ctx.PushTransform(Matrix.CreateTranslation(-50, -50) * Matrix.CreateScale(art, art) * Matrix.CreateTranslation(50, 50));
        if (tiny)
        {
            // Just the face, as big as it goes.
            using (ctx.PushTransform(Matrix.CreateTranslation(6, 14))) Dassie.Draw(ctx, Dassie.Pose.Peek, 88, 88 / Dassie.Aspect(Dassie.Pose.Peek));
            if (editor) Pencil(ctx, new Point(93, 62), 32, 13);
            return;
        }
        // The sun behind.
        ctx.DrawEllipse(new ImmutableSolidColorBrush(Color.Parse(editor ? "#f1d68c" : "#f3df9e"), editor ? 0.9 : 0.95), null, new Point(50, 42), 31, 31);
        // The dassie, its paws on the top of the book.
        using (ctx.PushTransform(Matrix.CreateTranslation(15, 21))) Dassie.Draw(ctx, Dassie.Pose.Peek, 70, 70 / Dassie.Aspect(Dassie.Pose.Peek));
        OpenBook(ctx);
        if (editor) Pencil(ctx, new Point(90.5, 61), 36, 9);
    }

    /// <summary>An open book seen from the front: two cream pages bowed at the top, with a spine.</summary>
    static void OpenBook(DrawingContext ctx)
    {
        var cover = new ImmutableSolidColorBrush(Color.Parse("#9a4a2c"));
        ctx.DrawGeometry(cover, null, Geometry.Parse("M12 70 Q31 63 50 70 Q69 63 88 70 L88 88 Q69 82 50 88 Q31 82 12 88 Z"));
        var page = new ImmutableSolidColorBrush(Color.Parse("#fffcf4"));
        var shade = new ImmutableSolidColorBrush(Color.Parse("#ead9b5"));
        ctx.DrawGeometry(page, BookLine, Geometry.Parse("M15 66 Q32 60 50 67 L50 85 Q32 79 15 85 Z"));
        ctx.DrawGeometry(page, BookLine, Geometry.Parse("M85 66 Q68 60 50 67 L50 85 Q68 79 85 85 Z"));
        ctx.DrawGeometry(shade, null, Geometry.Parse("M50 67 L50 85 Q47 83 44 82.4 L44 65.6 Q47 66 50 67 Z"));
        ctx.DrawGeometry(shade, null, Geometry.Parse("M50 67 L50 85 Q53 83 56 82.4 L56 65.6 Q53 66 50 67 Z"));
        var lines = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#c9b98e")), 1.4, lineCap: PenLineCap.Round);
        foreach (double y in new[] { 71.5, 75.5, 79.5 })
        {
            ctx.DrawLine(lines, new Point(20, y - 0.6), new Point(41, y + 0.6));
            ctx.DrawLine(lines, new Point(59, y + 0.6), new Point(80, y - 0.6));
        }
        ctx.DrawLine(BookLine, new Point(50, 67), new Point(50, 85));
    }

    /// <summary>A yellow pencil from its end at <paramref name="tail"/> down to the left, writing on the book's right-hand page.</summary>
    static void Pencil(DrawingContext ctx, Point tail, double length, double width)
    {
        using var _ = ctx.PushTransform(Matrix.CreateRotation(Math.PI * 0.75) * Matrix.CreateTranslation(tail.X, tail.Y));
        // Drawn along +x from the tail (eraser) to the point.
        double w = width, h = w / 2, body = length - w * 1.6, ferrule = w * 0.55, eraser = w * 0.7;
        var outline = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#5a3d1c")), Math.Max(1.2, w * 0.14), lineJoin: PenLineJoin.Round);
        ctx.DrawRectangle(new ImmutableSolidColorBrush(Color.Parse("#e79a86")), outline, new Rect(-eraser, -h, eraser, w), h * 0.6, h * 0.6);
        ctx.DrawRectangle(new ImmutableSolidColorBrush(Color.Parse("#b9b4a8")), outline, new Rect(0, -h, ferrule, w));
        ctx.DrawRectangle(new ImmutableSolidColorBrush(Color.Parse("#e8b23a")), outline, new Rect(ferrule, -h, body - ferrule, w));
        ctx.DrawLine(new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#c98e1e")), w * 0.18), new Point(ferrule, 0), new Point(body, 0));
        var cone = new StreamGeometry();
        using (var g = cone.Open())
        {
            g.BeginFigure(new Point(body, -h), true);
            g.LineTo(new Point(length, 0));
            g.LineTo(new Point(body, h));
            g.EndFigure(true);
        }
        ctx.DrawGeometry(new ImmutableSolidColorBrush(Color.Parse("#f3d9a6")), outline, cone);
        var lead = new StreamGeometry();
        using (var g = lead.Open())
        {
            double l0 = body + (length - body) * 0.62;
            g.BeginFigure(new Point(l0, -h * 0.38), true);
            g.LineTo(new Point(length, 0));
            g.LineTo(new Point(l0, h * 0.38));
            g.EndFigure(true);
        }
        ctx.DrawGeometry(new ImmutableSolidColorBrush(Color.Parse("#3d2c22")), null, lead);
    }

    /// <summary>The icon as a picture of <paramref name="pixels"/> square.</summary>
    public static Avalonia.Media.Imaging.RenderTargetBitmap Render(int pixels, bool editor, Back back = Back.Square, double art = 1)
    {
        var bmp = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(pixels, pixels), new Vector(96, 96));
        using (DrawingContext ctx = bmp.CreateDrawingContext(true)) Draw(ctx, pixels, editor, back, art);
        return bmp;
    }

    /// <summary>The window's icon, at a few sizes so the system can pick.</summary>
    public static WindowIcon ForWindow(bool editor)
    {
        using var bmp = Render(256, editor);
        var ms = new MemoryStream();
        bmp.Save(ms);
        ms.Position = 0;
        return new WindowIcon(ms);
    }
}
