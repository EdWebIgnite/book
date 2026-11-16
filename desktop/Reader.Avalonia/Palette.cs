using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Reader.Ui;

/// <summary>
/// The app's skin in one place: its colours, its fonts and how much it may move. The feel is a London
/// nursery of the late 1800s, kept light and happy: a fitted cherry wood bookcase, faded sage and cream
/// wallpaper, bottle and sage greens for the things to press and the headings, warm cream, and polished
/// brass for small accents. The colours are soft enough to sit beside watercolour pictures, and nothing
/// is pure black.
///
/// (The names are from the first, leafier version of the skin: "Leaf" is now the period bottle-sage green
/// and "Sun" the brass.)
///
/// To change the look, change the colours here: the shelf, the settings, the reader's buttons, the
/// pop-up words and the boxes all take theirs from this class (Reader.App's AppSkin passes them on to
/// the standard controls, such as the settings' tick boxes and lists).
/// </summary>
public static class Palette
{
    // Greens: Victorian bottle and sage.
    public static readonly Color Leaf = Color.Parse("#3f6b4c");          // the lead colour: main buttons, ticks, the Next arrow
    public static readonly Color LeafBright = Color.Parse("#5d8a68");    // highlights on green
    public static readonly Color LeafDeep = Color.Parse("#2b4a37");      // headings, words on green-tinted light
    public static readonly Color LeafPale = Color.Parse("#cdd8c3");      // borders, hover washes: pale sage
    public static readonly Color Mint = Color.Parse("#eef1e5");          // light sage wash behind group headings
    public static readonly Color Forest = Color.Parse("#1f2b24");        // the deep bottle green round the open book

    // Light.
    public static readonly Color Cream = Color.Parse("#fcf7ea");
    public static readonly Color Paper = Color.Parse("#fffdf7");
    public static readonly Color Wall = Color.Parse("#efe9d6");          // the wallpaper's ground: faded cream
    public static readonly Color WallSage = Color.Parse("#a9b796");      // the wallpaper's stripes and sprigs, used very faintly
    public static readonly Color Panelling = Color.Parse("#dfe2cf");     // the painted panelling below the dado rail

    // Brass (the names are from the first version, when this was a sunny yellow).
    public static readonly Color Sun = Color.Parse("#d8b45c");           // polished brass: Show me, the Ask badge, small trims
    public static readonly Color SunDeep = Color.Parse("#a8812f");
    public static readonly Color SunLight = Color.Parse("#f3df9e");      // brass catching the light; the read-along highlight
    public static readonly Color SunInk = Color.Parse("#3d2c08");        // icons and words on brass

    // Words.
    public static readonly Color Ink = Color.Parse("#2a2b22");           // main words: a warm very dark olive, never black
    public static readonly Color InkSoft = Color.Parse("#5b5d4e");       // help and notes
    public static readonly Color InkFaint = Color.Parse("#85877a");

    // Cherry wood for the bookcase, from the light catching an edge to the deepest grain, and the warm red kept for "remove".
    public static readonly Color WoodLight = Color.Parse("#b8653d");
    public static readonly Color Wood = Color.Parse("#9a4a2c");
    public static readonly Color WoodDark = Color.Parse("#7d3a22");
    public static readonly Color WoodDeep = Color.Parse("#5e2a18");
    public static readonly Color Clay = Color.Parse("#a8473a");          // a period oxblood red for "remove"
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

    /// <summary>
    /// Fraunces Bold, a soft old-style serif (SIL OFL 1.1, Assets/Fonts/OFL-Fraunces.txt), for the few big
    /// headings only: "My Books", "Settings", "The End". It gives the period feel; everything children
    /// read is in Nunito.
    /// </summary>
    public static readonly FontFamily Heading = new("avares://Reader.Avalonia/Assets/Fonts#Fraunces, avares://Reader.Avalonia/Assets/Fonts#Nunito");
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

    /// <summary>Ordinary buttons: warm cream, a brass-tinted edge.</summary>
    public static readonly Look Plain = new(
        Color.Parse("#fffdf6"), Color.Parse("#eee5d1"), Color.Parse("#fffefa"), Color.Parse("#f5eedf"),
        Color.Parse("#e3d8c0"), Color.Parse("#ece3cf"), Color.Parse("#e9dcc0"), Color.Parse("#a99467"));
    /// <summary>Main actions: bottle green.</summary>
    public static readonly Look Green = new(
        Color.Parse("#4f7d5c"), Color.Parse("#355c43"), Color.Parse("#5a8967"), Color.Parse("#3c6549"),
        Color.Parse("#30553d"), Color.Parse("#3a6247"), Color.Parse("#7aa184"), Color.Parse("#22402e"));
    /// <summary>Polished brass, for Show me.</summary>
    public static readonly Look Sun = new(
        Color.Parse("#f1d68c"), Color.Parse("#c9a04a"), Color.Parse("#f6df9d"), Color.Parse("#d3ab55"),
        Color.Parse("#c29a43"), Color.Parse("#cfa752"), Color.Parse("#fbedc0"), Color.Parse("#8c6a24"));
    /// <summary>Oxblood, for "Yes, remove it".</summary>
    public static readonly Look Red = new(
        Color.Parse("#b45a4b"), Color.Parse("#913f33"), Color.Parse("#bd6556"), Color.Parse("#9b483b"),
        Color.Parse("#8a3a2f"), Color.Parse("#944236"), Color.Parse("#cf8576"), Color.Parse("#6c2b22"));

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
