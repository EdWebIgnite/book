using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Reader.Ui;

/// <summary>
/// One set of the app's colours. There are two: <see cref="Theme.Day"/>, the light nursery of the
/// bookcase look, and <see cref="Theme.Night"/>, the "Bedtime colours" a grown-up can switch on (deep
/// night blue with lamp-gold accents). Both share every layout; only the colours change.
/// </summary>
public sealed class Theme
{
    public required bool IsNight { get; init; }

    // Greens (day): bottle green for the things to press, deep green for headings. At night lamp gold and cream.
    public required Color Leaf { get; init; }          // the main action: main buttons, ticks, the Next arrow
    public required Color LeafBright { get; init; }    // highlights on the main colour
    public required Color LeafDeep { get; init; }      // headings
    public required Color LeafPale { get; init; }      // borders, hover washes
    public required Color Mint { get; init; }          // a pale wash behind group headings and icons
    public required Color Forest { get; init; }        // the surround of the open book while reading
    /// <summary>Words and icons on a main-action face.</summary>
    public required Color OnLeaf { get; init; }

    // Light (at night: the dark cards).
    public required Color Cream { get; init; }         // panels
    public required Color Paper { get; init; }         // cards and rows
    public required Color Wall { get; init; }          // the wall behind the bookcase
    public required Color WallStripe { get; init; }    // its faint stripes
    public required Color CaseBack { get; init; }      // the painted back of the bookcase, behind the covers

    // Brass, and at night lamp gold.
    public required Color Sun { get; init; }
    public required Color SunDeep { get; init; }
    public required Color SunLight { get; init; }      // the read-along highlight behind a word
    public required Color SunInk { get; init; }        // icons and words on brass

    // Words.
    public required Color Ink { get; init; }
    public required Color InkSoft { get; init; }
    public required Color InkFaint { get; init; }

    // The cherry wood of the bookcase (at night the same wood in lamplight, much darker).
    public required Color WoodLight { get; init; }
    public required Color Wood { get; init; }
    public required Color WoodDark { get; init; }
    public required Color WoodDeep { get; init; }
    public required Color Sheen { get; init; }          // the warm light on an edge of the wood
    /// <summary>How strongly the wood grain picture is laid over the boards (lower at night).</summary>
    public required double Grain { get; init; }

    public required Color Clay { get; init; }           // "remove"
    public required Color Shadow { get; init; }
    /// <summary>The bookmark ribbon on a book that is partly read.</summary>
    public required Color Ribbon { get; init; }
    /// <summary>The round tick on a finished book.</summary>
    public required Color Tick { get; init; }
    /// <summary>The pop-up words box and its words. Kept light at night too, only dimmer, so the words read easily.</summary>
    public required Color CaptionBack { get; init; }
    public required Color CaptionInk { get; init; }
    /// <summary>The focus ring for the keyboard.</summary>
    public required Color Focus { get; init; }

    public static readonly Theme Day = new()
    {
        IsNight = false,
        Leaf = Color.Parse("#2f6b50"), LeafBright = Color.Parse("#4a8a6b"), LeafDeep = Color.Parse("#234b3b"),
        LeafPale = Color.Parse("#d6dcc8"), Mint = Color.Parse("#eef1e4"), Forest = Color.Parse("#1f2b24"), OnLeaf = Colors.White,
        Cream = Color.Parse("#fbf5e6"), Paper = Color.Parse("#fffcf4"), Wall = Color.Parse("#f4eedc"), WallStripe = Color.Parse("#e8dfc6"),
        CaseBack = Color.Parse("#f6efdd"),
        Sun = Color.Parse("#d9b45a"), SunDeep = Color.Parse("#a8812f"), SunLight = Color.Parse("#f3df9e"), SunInk = Color.Parse("#3d2c08"),
        Ink = Color.Parse("#2b2118"), InkSoft = Color.Parse("#6b5e50"), InkFaint = Color.Parse("#8f8474"),
        WoodLight = Color.Parse("#b8653d"), Wood = Color.Parse("#9a4a2c"), WoodDark = Color.Parse("#7d3a22"), WoodDeep = Color.Parse("#5e2a18"),
        Sheen = Color.Parse("#e6a57c"), Grain = 1,
        Clay = Color.Parse("#a8473a"), Shadow = Color.Parse("#2c2414"),
        Ribbon = Color.Parse("#8a3f2a"), Tick = Color.Parse("#2f6b50"),
        CaptionBack = Color.Parse("#fbf5e6"), CaptionInk = Color.Parse("#2b2118"),
        Focus = Color.Parse("#2b2118"),
    };

    public static readonly Theme Night = new()
    {
        IsNight = true,
        Leaf = Color.Parse("#ffcb5c"), LeafBright = Color.Parse("#ffdc8a"), LeafDeep = Color.Parse("#fff4dc"),
        LeafPale = Color.Parse("#3c428a"), Mint = Color.Parse("#2a2f6c"), Forest = Color.Parse("#14173a"), OnLeaf = Color.Parse("#1b1f4b"),
        Cream = Color.Parse("#20245a"), Paper = Color.Parse("#2b3070"), Wall = Color.Parse("#1b1f4b"), WallStripe = Color.Parse("#222760"),
        CaseBack = Color.Parse("#232862"),
        Sun = Color.Parse("#ffcb5c"), SunDeep = Color.Parse("#c99a2e"), SunLight = Color.Parse("#ffe29a"), SunInk = Color.Parse("#1b1f4b"),
        Ink = Color.Parse("#fff4dc"), InkSoft = Color.Parse("#c9cbea"), InkFaint = Color.Parse("#9a9dcc"),
        WoodLight = Color.Parse("#5b3a3f"), Wood = Color.Parse("#4a2e36"), WoodDark = Color.Parse("#3a232e"), WoodDeep = Color.Parse("#2a1924"),
        Sheen = Color.Parse("#b48a62"), Grain = 0.45,
        Clay = Color.Parse("#ff8a6b"), Shadow = Color.Parse("#05061a"),
        Ribbon = Color.Parse("#ff8a6b"), Tick = Color.Parse("#7fd6a4"),
        CaptionBack = Color.Parse("#efe5cc"), CaptionInk = Color.Parse("#1f2340"),
        Focus = Color.Parse("#ffcb5c"),
    };
}

/// <summary>
/// The app's skin in one place: its colours, its fonts and how much it may move. The day look is a
/// fitted cherry bookcase in a light, quiet nursery: cream walls, a pale painted back so the covers
/// carry the colour, bottle green for the things to press and the headings, and polished brass for
/// small accents. Nothing is pure black. Bedtime colours swap in <see cref="Theme.Night"/>.
///
/// Everything takes its colours from here at the moment it is built, so after <see cref="Night"/>
/// changes the app builds its screens again (and the reader screen restyles itself).
/// </summary>
public static class Palette
{
    static Theme _t = Theme.Day;

    /// <summary>The colours in use now.</summary>
    public static Theme Current => _t;

    /// <summary>Bedtime colours on or off.</summary>
    public static bool Night
    {
        get => _t.IsNight;
        set => _t = value ? Theme.Night : Theme.Day;
    }

    public static Color Leaf => _t.Leaf;
    public static Color LeafBright => _t.LeafBright;
    public static Color LeafDeep => _t.LeafDeep;
    public static Color LeafPale => _t.LeafPale;
    public static Color Mint => _t.Mint;
    public static Color Forest => _t.Forest;
    public static Color OnLeaf => _t.OnLeaf;
    public static Color Cream => _t.Cream;
    public static Color Paper => _t.Paper;
    public static Color Wall => _t.Wall;
    public static Color WallStripe => _t.WallStripe;
    public static Color CaseBack => _t.CaseBack;
    public static Color Sun => _t.Sun;
    public static Color SunDeep => _t.SunDeep;
    public static Color SunLight => _t.SunLight;
    public static Color SunInk => _t.SunInk;
    public static Color Ink => _t.Ink;
    public static Color InkSoft => _t.InkSoft;
    public static Color InkFaint => _t.InkFaint;
    public static Color WoodLight => _t.WoodLight;
    public static Color Wood => _t.Wood;
    public static Color WoodDark => _t.WoodDark;
    public static Color WoodDeep => _t.WoodDeep;
    public static Color Sheen => _t.Sheen;
    public static Color Clay => _t.Clay;
    public static Color Shadow => _t.Shadow;
    public static Color Ribbon => _t.Ribbon;
    public static Color Tick => _t.Tick;
    public static Color Focus => _t.Focus;

    public static IImmutableSolidColorBrush Brush(Color c) => new ImmutableSolidColorBrush(c);
    public static IImmutableSolidColorBrush Brush(Color c, double alpha) => new ImmutableSolidColorBrush(c, alpha);

    public static IImmutableSolidColorBrush LeafBrush => Brush(Leaf);
    public static IImmutableSolidColorBrush LeafDeepBrush => Brush(LeafDeep);
    public static IImmutableSolidColorBrush LeafPaleBrush => Brush(LeafPale);
    public static IImmutableSolidColorBrush MintBrush => Brush(Mint);
    public static IImmutableSolidColorBrush CreamBrush => Brush(Cream);
    public static IImmutableSolidColorBrush PaperBrush => Brush(Paper);
    public static IImmutableSolidColorBrush SunBrush => Brush(Sun);
    public static IImmutableSolidColorBrush SunInkBrush => Brush(SunInk);
    public static IImmutableSolidColorBrush InkBrush => Brush(Ink);
    public static IImmutableSolidColorBrush InkSoftBrush => Brush(InkSoft);
    public static IImmutableSolidColorBrush InkFaintBrush => Brush(InkFaint);
    public static IImmutableSolidColorBrush ClayBrush => Brush(Clay);
    public static IImmutableSolidColorBrush ForestBrush => Brush(Forest);
    public static IImmutableSolidColorBrush OnLeafBrush => Brush(OnLeaf);

    /// <summary>A soft shadow of the given strength (0 to 1).</summary>
    public static Color ShadowOf(double alpha) => Color.FromArgb((byte)Math.Round(255 * Math.Clamp(alpha, 0, 1)), Shadow.R, Shadow.G, Shadow.B);

    /// <summary>
    /// Nunito, a rounded open-licence font (SIL OFL 1.1, Assets/Fonts/OFL.txt), in five weights from
    /// Regular to Black. Letters it doesn't have (✕, arrows) come from the system's font.
    /// </summary>
    public static readonly FontFamily Font = new("avares://Reader.Avalonia/Assets/Fonts#Nunito, $Default");

    /// <summary>
    /// Fraunces Bold, a soft old-style serif (SIL OFL 1.1, Assets/Fonts/OFL-Fraunces.txt), for the big
    /// headings and the name plate only. Everything children read is in Nunito.
    /// </summary>
    public static readonly FontFamily Heading = new("avares://Reader.Avalonia/Assets/Fonts#Fraunces, avares://Reader.Avalonia/Assets/Fonts#Nunito");
}

/// <summary>
/// The skin's buttons: a soft tactile look rather than flat colour. Each has a face that is a little
/// lighter at the top than the bottom, a light top edge and a darker lip along the bottom, and a soft
/// shadow; pressed, the face sinks a pixel or two, darkens a touch and the shadow shrinks.
/// </summary>
public static class Buttons
{
    /// <summary>The words and icons on ordinary buttons.</summary>
    public static Color Text => Palette.Night ? Color.Parse("#fff4dc") : Color.Parse("#2b2521");
    public static IImmutableSolidColorBrush TextBrush => new ImmutableSolidColorBrush(Text);

    /// <summary>One set of colours for a button: its face (top and bottom) at rest, under the pointer and pressed, its edge, and its words.</summary>
    public sealed record Look(Color Top, Color Bottom, Color HoverTop, Color HoverBottom, Color PressedTop, Color PressedBottom, Color EdgeTop, Color EdgeBottom, Color Ink);

    static readonly Look PlainDay = new(
        Color.Parse("#fffdf6"), Color.Parse("#eee5d1"), Color.Parse("#fffefa"), Color.Parse("#f5eedf"),
        Color.Parse("#e3d8c0"), Color.Parse("#ece3cf"), Color.Parse("#e9dcc0"), Color.Parse("#a99467"), Color.Parse("#2b2521"));
    static readonly Look PlainNight = new(
        Color.Parse("#363c82"), Color.Parse("#2a2f6c"), Color.Parse("#3e4590"), Color.Parse("#30367a"),
        Color.Parse("#252a62"), Color.Parse("#2c3170"), Color.Parse("#5a62b0"), Color.Parse("#15183f"), Color.Parse("#fff4dc"));
    static readonly Look GreenDay = new(
        Color.Parse("#3c7c5e"), Color.Parse("#2a6047"), Color.Parse("#46876a"), Color.Parse("#30694f"),
        Color.Parse("#265640"), Color.Parse("#2c5f47"), Color.Parse("#6aa184"), Color.Parse("#1a3d2c"), Colors.White);
    static readonly Look GoldNight = new(
        Color.Parse("#ffd77a"), Color.Parse("#f4bb45"), Color.Parse("#ffdf92"), Color.Parse("#f7c457"),
        Color.Parse("#e9ae38"), Color.Parse("#f0b744"), Color.Parse("#fff0c4"), Color.Parse("#a87a1e"), Color.Parse("#1b1f4b"));
    static readonly Look BrassDay = new(
        Color.Parse("#f1d68c"), Color.Parse("#c9a04a"), Color.Parse("#f6df9d"), Color.Parse("#d3ab55"),
        Color.Parse("#c29a43"), Color.Parse("#cfa752"), Color.Parse("#fbedc0"), Color.Parse("#8c6a24"), Color.Parse("#3d2c08"));
    static readonly Look RedDay = new(
        Color.Parse("#b45a4b"), Color.Parse("#913f33"), Color.Parse("#bd6556"), Color.Parse("#9b483b"),
        Color.Parse("#8a3a2f"), Color.Parse("#944236"), Color.Parse("#cf8576"), Color.Parse("#6c2b22"), Colors.White);
    static readonly Look RedNight = new(
        Color.Parse("#ff9a7e"), Color.Parse("#ef7a5d"), Color.Parse("#ffa58b"), Color.Parse("#f38466"),
        Color.Parse("#e06f53"), Color.Parse("#ea765a"), Color.Parse("#ffc3b2"), Color.Parse("#a8462e"), Color.Parse("#1b1f4b"));

    /// <summary>Ordinary buttons: warm cream (night: a lighter blue card).</summary>
    public static Look Plain => Palette.Night ? PlainNight : PlainDay;
    /// <summary>Main actions: bottle green (night: lamp gold).</summary>
    public static Look Green => Palette.Night ? GoldNight : GreenDay;
    /// <summary>Polished brass, for Show me (night: lamp gold).</summary>
    public static Look Sun => Palette.Night ? GoldNight : BrassDay;
    /// <summary>Oxblood, for "Yes, remove it".</summary>
    public static Look Red => Palette.Night ? RedNight : RedDay;

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

    public static BoxShadow Shadow => new() { OffsetY = 2, Blur = 5, Color = Palette.ShadowOf(Palette.Night ? 0.5 : 0.24) };
    public static BoxShadow ShadowPressed => new() { OffsetY = 0.5, Blur = 2, Color = Palette.ShadowOf(Palette.Night ? 0.45 : 0.2) };
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
