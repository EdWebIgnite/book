namespace Reader.Core;

/// <summary>
/// One thing the reader shows: a page whole, one of its stops, or the page handed over for exploring.
/// A book is played as a list of these, in order.
/// </summary>
/// <param name="Closing">
/// The whole page shown again after its stops, before moving on. It counts as being past the last
/// stop, so whatever started during the page is still there.
/// </param>
public readonly record struct Step(int Page, int Stop, bool Explore = false, bool Closing = false)
{
    /// <summary>Stop number meaning "the whole page".</summary>
    public const int WholePage = -1;
    /// <summary>Stop number meaning "the child is exploring".</summary>
    public const int Exploring = -2;
    /// <summary>Stop number meaning "the book closed on its cover", which comes before the first page.</summary>
    public const int Cover = -3;
}

/// <summary>
/// What is on screen at one moment: where the camera is, which box is in focus, and how strong each
/// focus effect is (0 = off). Fade is the dip to dark used instead of travelling with reduced motion.
/// </summary>
public readonly record struct Shot(RectD Cam, RectD Focus, double Dim, double Vig, double Spot, double Fade = 0);

/// <summary>Room kept clear of the picture at the top and bottom of the screen, for the buttons and the words.</summary>
public sealed class Insets
{
    public double Top { get; set; }
    public double Bottom { get; set; }

    public Insets(double top, double bottom) { Top = top; Bottom = bottom; }
}

/// <summary>How world units map to the screen: screen = world * Scale + Offset.</summary>
public readonly record struct ViewTransform(double Scale, double OffsetX, double OffsetY, double Width, double Height)
{
    public RectD ToScreen(RectD r) => new(r.X * Scale + OffsetX, r.Y * Scale + OffsetY, r.W * Scale, r.H * Scale);
    public PointD ToScreen(double x, double y) => new(x * Scale + OffsetX, y * Scale + OffsetY);
    public PointD ToWorld(double sx, double sy) => new((sx - OffsetX) / Scale, (sy - OffsetY) / Scale);
}

/// <summary>A summary for the reader screen: which buttons to show, which words, how far along.</summary>
public sealed record ReaderState
{
    public int StepIndex { get; init; }
    public int StepCount { get; init; }
    public int Page { get; init; }
    public int PageCount { get; init; }
    public bool Overview { get; init; }
    /// <summary>The whole page is showing (because the page opened that way, or the child asked for it).</summary>
    public bool WholePage { get; init; }
    public bool Finished { get; init; }
    public bool Exploring { get; init; }
    public string Caption { get; init; } = "";
    public string Ask { get; init; } = "";
    /// <summary>Nothing has been tapped yet: time for the tapping-hand hint.</summary>
    public bool FirstStep { get; init; }
    public bool CanZoomIn { get; init; }
    public bool CanZoomOut { get; init; }
    public bool CanGoBack { get; init; }
    public bool CanShowMe { get; init; }
    public int Found { get; init; }
    public int HotspotCount { get; init; }
    public int StoryTotal { get; init; }
    public int StoryFound { get; init; }
    public int ExtraTotal { get; init; }
    public int ExtraFound { get; init; }
}

/// <summary>A picture on top of the page, ready to draw. The box is in fractions of the page, like book.json.</summary>
public readonly record struct LayerDraw(string Image, RectD Box, double Opacity, double Rotation);

/// <summary>The ring that answers a touch. T runs from 0 (just touched) to 1 (gone).</summary>
public readonly record struct RippleDraw(double X, double Y, double T);

/// <summary>A found hotspot's sparkle, or the "not yet" ring of a locked one. The box is in world units.</summary>
public readonly record struct BurstDraw(RectD Box, double T, bool Locked);

public enum HintKind { None, Glow, Arrow }

/// <summary>Guidance on an explore page, in screen units.</summary>
public sealed record HintDraw
{
    /// <summary>The next story step always carries a soft beacon, so the thread of the story is never lost.</summary>
    public bool Beacon { get; init; }
    public RectD BeaconBox { get; init; }
    /// <summary>0 to 1: how far the beacon's outer ring has spread.</summary>
    public double BeaconGrow { get; init; }

    /// <summary>After an idle spell: a glow around the next thing to find, or an arrow at the screen edge.</summary>
    public HintKind Kind { get; init; }
    public RectD GlowBox { get; init; }
    public double ArrowX { get; init; }
    public double ArrowY { get; init; }
    public double ArrowAngle { get; init; }

    /// <summary>0 to 1, a slow heartbeat shared by everything that pulses.</summary>
    public double Pulse { get; init; }
}

/// <summary>The corner map of an explore page, in screen units.</summary>
public sealed record MapDraw
{
    public int Page { get; init; }
    public RectD Rect { get; init; }
    /// <summary>The frame showing where the child is looking.</summary>
    public RectD ViewRect { get; init; }
    /// <summary>The next story step blinks on the map.</summary>
    public bool HasDot { get; init; }
    public double DotX { get; init; }
    public double DotY { get; init; }
    public double DotRadius { get; init; }
    public double DotAlpha { get; init; }
}

/// <summary>One star of the celebration, in screen units.</summary>
public readonly record struct PartyStar(double X, double Y, double Radius, int Tint);

public sealed record PartyDraw(double Alpha, PartyStar[] Stars)
{
    public static readonly string[] Tints = { "#ffd23f", "#ff6b6b", "#74c0fc", "#8ce99a", "#fff6c2", "#b197fc" };
}

/// <summary>
/// A page being turned like the page of a book. Both pages are seen whole. The turning page folds
/// over towards the spine on its left, showing the page underneath.
/// </summary>
/// <param name="TurningPage">The page whose leaf is in the air.</param>
/// <param name="UnderPage">The page lying underneath it.</param>
/// <param name="Fold">
/// How far the turn has got: 0 is the leaf lying flat and covering the page underneath (the fold is at
/// its right edge), 1 is the leaf gone over the spine (the fold has reached the left edge). Going on
/// through the book it runs from 0 to 1; going back, from 1 to 0.
/// </param>
/// <param name="Forward">True when going on through the book.</param>
/// <param name="TurningCam">The camera that shows the turning page whole, in world units.</param>
/// <param name="UnderCam">The camera that shows the page underneath whole.</param>
public readonly record struct FlipDraw(int TurningPage, int UnderPage, double Fold, bool Forward, RectD TurningCam, RectD UnderCam);

/// <summary>
/// The book's cover. <paramref name="Open"/> is 0 with the book shut and 1 once the cover has swung
/// right over to the left; while it is between, the cover is turning.
/// </summary>
public readonly record struct CoverDraw(int Page, double Open, bool Opening);

/// <summary>One sharp tile to draw over a big scene's overview picture.</summary>
/// <param name="Level">0 is full size, each level after is half the one before.</param>
/// <param name="World">Where it goes, in world units.</param>
/// <param name="HasParent">While this tile loads, the matching quarter of the next level down can stand in.</param>
/// <param name="ParentSource">That quarter, in the parent tile's own pixels.</param>
public readonly record struct TileDraw(int Level, int X, int Y, RectD World,
    bool HasParent, int ParentX, int ParentY, RectD ParentSource);

/// <summary>Keys the reader understands, whatever the keyboard calls them.</summary>
public enum ReaderKey { Next, Previous, ToggleWholePage, CloseUp, ZoomIn, ZoomOut }
