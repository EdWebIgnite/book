using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Reader.Ui;

/// <summary>
/// The app's skin in one place: its colours, its font and how much it may move. Fresh leaf green leads,
/// with cream and white, a sunny yellow for the things to press and warm cherry wood for the shelf. The
/// colours are soft enough to sit beside watercolour pictures, and nothing is pure black.
///
/// To change the look, change the colours here: the shelf, the settings, the reader's buttons, the
/// pop-up words and the boxes all take theirs from this class (Reader.App's AppSkin passes them on to
/// the standard controls, such as the settings' tick boxes and lists).
/// </summary>
public static class Palette
{
    // Greens.
    public static readonly Color Leaf = Color.Parse("#4f9d3a");          // the lead colour: buttons, ticks, the Next arrow
    public static readonly Color LeafBright = Color.Parse("#6db84f");    // highlights, the bookcase paint
    public static readonly Color LeafDeep = Color.Parse("#2f6a2a");      // headings, icons, words on green-tinted light
    public static readonly Color LeafPale = Color.Parse("#cfe8bd");      // borders, hover washes
    public static readonly Color Mint = Color.Parse("#eef7e6");          // light green wash behind group headings
    public static readonly Color Forest = Color.Parse("#21302a");        // the dark round the open book (was a purple-black)

    // Light.
    public static readonly Color Cream = Color.Parse("#fffaf0");
    public static readonly Color Paper = Color.Parse("#ffffff");
    public static readonly Color Wall = Color.Parse("#f8f6ee");          // top of the shelf's wall: warm cream with a breath of sage
    public static readonly Color WallLow = Color.Parse("#ebebdd");       // bottom of the shelf's wall

    // Sun.
    public static readonly Color Sun = Color.Parse("#ffcf4a");
    public static readonly Color SunDeep = Color.Parse("#e9a917");
    public static readonly Color SunInk = Color.Parse("#5a3d00");        // icons and words on yellow

    // Words.
    public static readonly Color Ink = Color.Parse("#26331f");           // main words: a very dark green, never black
    public static readonly Color InkSoft = Color.Parse("#56634c");       // help and notes
    public static readonly Color InkFaint = Color.Parse("#7f8a76");

    // Cherry wood for the bookcase, from the light catching an edge to the deepest grain, and the warm red kept for "remove".
    public static readonly Color WoodLight = Color.Parse("#b8653d");
    public static readonly Color Wood = Color.Parse("#9a4a2c");
    public static readonly Color WoodDark = Color.Parse("#7d3a22");
    public static readonly Color WoodDeep = Color.Parse("#5e2a18");
    public static readonly Color Clay = Color.Parse("#c8583e");
    public static readonly Color Shadow = Color.Parse("#2c2414");        // the colour of shadows: warm, not grey

    public static IImmutableSolidColorBrush Brush(Color c) => new ImmutableSolidColorBrush(c);
    public static IImmutableSolidColorBrush Brush(Color c, double alpha) => new ImmutableSolidColorBrush(c, alpha);

    public static readonly IImmutableSolidColorBrush LeafBrush = Brush(Leaf);
    public static readonly IImmutableSolidColorBrush LeafDeepBrush = Brush(LeafDeep);
    public static readonly IImmutableSolidColorBrush LeafPaleBrush = Brush(LeafPale);
    public static readonly IImmutableSolidColorBrush MintBrush = Brush(Mint);
    public static readonly IImmutableSolidColorBrush CreamBrush = Brush(Cream);
    public static readonly IImmutableSolidColorBrush PaperBrush = Brush(Paper);
    public static readonly IImmutableSolidColorBrush SunBrush = Brush(Sun);
    public static readonly IImmutableSolidColorBrush SunInkBrush = Brush(SunInk);
    public static readonly IImmutableSolidColorBrush InkBrush = Brush(Ink);
    public static readonly IImmutableSolidColorBrush InkSoftBrush = Brush(InkSoft);
    public static readonly IImmutableSolidColorBrush InkFaintBrush = Brush(InkFaint);
    public static readonly IImmutableSolidColorBrush ClayBrush = Brush(Clay);
    public static readonly IImmutableSolidColorBrush ForestBrush = Brush(Forest);

    /// <summary>A soft warm shadow of the given strength (0 to 1).</summary>
    public static Color ShadowOf(double alpha) => Color.FromArgb((byte)Math.Round(255 * Math.Clamp(alpha, 0, 1)), Shadow.R, Shadow.G, Shadow.B);

    /// <summary>
    /// Nunito, a rounded open-licence font (SIL OFL 1.1, Assets/Fonts/OFL.txt), in five weights from
    /// Regular to Black. Letters it doesn't have (✕, arrows) come from the system's font.
    /// </summary>
    public static readonly FontFamily Font = new("avares://Reader.Avalonia/Assets/Fonts#Nunito, $Default");
}

/// <summary>
/// The skin's buttons: a soft tactile look rather than flat colour. Each has a face that is a little
/// lighter at the top than the bottom, a light top edge and a darker lip along the bottom, and a soft
/// shadow; pressed, the face sinks a pixel or two, darkens a touch and the shadow shrinks. The words on
/// ordinary buttons are the plain dark text colour; the main actions keep a coloured face.
/// </summary>
public static class Buttons
{
    /// <summary>The words and icons on ordinary buttons: a warm near-black.</summary>
    public static readonly Color Text = Color.Parse("#2b2521");
    public static readonly IImmutableSolidColorBrush TextBrush = new ImmutableSolidColorBrush(Text);

    /// <summary>One set of colours for a button: its face (top and bottom) at rest, under the pointer and pressed, and its edge.</summary>
    public sealed record Look(Color Top, Color Bottom, Color HoverTop, Color HoverBottom, Color PressedTop, Color PressedBottom, Color EdgeTop, Color EdgeBottom);

    public static readonly Look Plain = new(
        Color.Parse("#ffffff"), Color.Parse("#efe8dc"), Color.Parse("#ffffff"), Color.Parse("#f6f1e8"),
        Color.Parse("#e6dece"), Color.Parse("#efe9de"), Color.Parse("#e4dacb"), Color.Parse("#b3a58c"));
    public static readonly Look Green = new(
        Color.Parse("#66b44d"), Color.Parse("#47912f"), Color.Parse("#70be57"), Color.Parse("#4f9a37"),
        Color.Parse("#438a2d"), Color.Parse("#4e9836"), Color.Parse("#7fc466"), Color.Parse("#2f6a22"));
    public static readonly Look Sun = new(
        Color.Parse("#ffd966"), Color.Parse("#f2b72c"), Color.Parse("#ffe07a"), Color.Parse("#f5c03e"),
        Color.Parse("#eeb028"), Color.Parse("#f4bf3a"), Color.Parse("#fff0b0"), Color.Parse("#c98f12"));
    public static readonly Look Red = new(
        Color.Parse("#d76b4f"), Color.Parse("#b74c33"), Color.Parse("#de7659"), Color.Parse("#c0553b"),
        Color.Parse("#ad4730"), Color.Parse("#b9513a"), Color.Parse("#e48a70"), Color.Parse("#8a3420"));

    static IBrush Vertical(Color top, Color bottom, double alpha = 1) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(WithAlpha(top, alpha), 0), new GradientStop(WithAlpha(bottom, alpha), 1) },
    }.ToImmutable();

    static Color WithAlpha(Color c, double a) => Color.FromArgb((byte)Math.Round(c.A * a), c.R, c.G, c.B);

    public static IBrush Face(Look l, double alpha = 1) => Vertical(l.Top, l.Bottom, alpha);
    public static IBrush HoverFace(Look l) => Vertical(l.HoverTop, l.HoverBottom);
    public static IBrush PressedFace(Look l) => Vertical(l.PressedTop, l.PressedBottom);
    public static IBrush Edge(Look l, double alpha = 1) => Vertical(l.EdgeTop, l.EdgeBottom, alpha);

    /// <summary>The edge is thin round the top and sides and thicker along the bottom: the button's lip.</summary>
    public static readonly Thickness Lip = new(1, 1, 1, 2.5);
    public static readonly Thickness LipPressed = new(1, 1.5, 1, 1.5);

    public static readonly BoxShadow Shadow = new() { OffsetY = 2, Blur = 5, Color = Palette.ShadowOf(0.24) };
    public static readonly BoxShadow ShadowPressed = new() { OffsetY = 0.5, Blur = 2, Color = Palette.ShadowOf(0.2) };
}

/// <summary>
/// The little bits of movement round the reader (covers lifting, the books settling on the shelf).
/// Each runs for a moment and stops, so nothing is redrawn while nothing is happening.
/// </summary>
public static class Motion
{
    /// <summary>The grown-ups asked for less movement (or the device did): changes happen without animating.</summary>
    public static bool Reduced { get; set; }

    /// <summary>For screenshots: every change lands at once, so the picture shows where it ends up.</summary>
    public static bool Instant { get; set; }

    /// <summary>True when little animations may run.</summary>
    public static bool Allowed => !Reduced && !Instant;
}
