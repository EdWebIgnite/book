namespace Reader.Core;

/*
 * The shape of the open book, in world units, shared by the engine (which frames it) and the screen
 * (which draws it).
 *
 * A landscape picture is one picture across both pages of a double-page spread: the crease runs down
 * its middle. A portrait picture is the right-hand page of a spread, and the left-hand page shows the
 * picture before it (or plain paper). Either way the spread is one unit tall.
 *
 *     board    a cover board slightly larger than the spread, showing as a border all round
 *     stacks   the edges of the pages underneath, on the left and right, inside the board's margin
 *     closed   the book shut: the right-hand half of the spread, with its cover on top
 */
public static class BookLayout
{
    /// <summary>How far the cover board shows beyond the pages at the top and bottom.</summary>
    public const double Board = 0.02;
    /// <summary>The room for the stack of page edges at each side, inside the board.</summary>
    public const double StackRoom = 0.016;
    /// <summary>The most lines of page edges drawn on one side.</summary>
    public const int MostStackLines = 5;
    /// <summary>The gap between two lines of page edges.</summary>
    public const double StackStep = StackRoom / (MostStackLines + 0.5);
    /// <summary>A little air around the book when it is seen whole.</summary>
    public const double Air = 0.018;
    /// <summary>How thick the closed book shows at its right and bottom edges.</summary>
    public const double ClosedThickness = 0.016;

    /// <summary>True for a page taller than it is wide: it is one page of a spread, not the whole spread.</summary>
    public static bool IsPortrait(RectD page) => page.W < page.H * 0.999;

    /// <summary>Both pages of the open book: the picture itself for a landscape page, two pages wide for a portrait one.</summary>
    public static RectD Spread(RectD page) => IsPortrait(page) ? new RectD(page.X - page.W, page.Y, page.W * 2, page.H) : page;

    /// <summary>Where the crease runs, down the middle of the spread.</summary>
    public static double SpineX(RectD page) => Spread(page).CenterX;

    /// <summary>The whole open book: spread, page stacks and board.</summary>
    public static RectD Book(RectD page)
    {
        RectD s = Spread(page);
        double side = StackRoom + Board;
        return new RectD(s.X - side, s.Y - Board, s.W + 2 * side, s.H + 2 * Board);
    }

    /// <summary>The closed book: the right-hand half of the spread under its cover, with its thickness showing.</summary>
    public static RectD Closed(RectD page)
    {
        RectD s = Spread(page);
        double side = StackRoom + Board;
        return new RectD(s.CenterX, s.Y - Board, s.W / 2 + side + ClosedThickness, s.H + 2 * Board + ClosedThickness);
    }

    /// <summary>The cover board of the closed book: from the spine to just past the right-hand page.</summary>
    public static RectD CoverBoard(RectD page)
    {
        RectD s = Spread(page);
        return new RectD(s.CenterX, s.Y - Board, s.W / 2 + StackRoom + Board, s.H + 2 * Board);
    }

    /// <summary>
    /// How many lines of page edges show on each side: few on the left at the start of the book and many
    /// on the right, the other way round at the end.
    /// </summary>
    public static (int Left, int Right) StackLines(int page, int pageCount)
    {
        double read = pageCount <= 1 ? 0 : (double)page / (pageCount - 1);
        int left = 1 + (int)Math.Round(read * (MostStackLines - 1));
        int right = 1 + (int)Math.Round((1 - read) * (MostStackLines - 1));
        return (left, right);
    }

    /// <summary>
    /// How strongly the crease and the curve of the pages into the binding show, 0 to 1, for a camera
    /// that is <paramref name="zoom"/> times closer than the whole-book view. Fully there when the book is
    /// seen whole, gone by 1.3 times closer, so a close-up never has a dark line through a character.
    /// </summary>
    public static double CreaseStrength(double zoom) => Geo.Clamp((1.3 - zoom) / 0.3, 0, 1);
}

/// <summary>
/// The bent shape of a turning page and of the opening cover, as plain numbers.
///
/// Positions are across the book, measured from the spine in half-spread widths: 1 is the outer edge of
/// the side the leaf starts on, -1 the outer edge of the other side. Heights are above the page, in the
/// same units. <c>t</c> runs along the leaf from the spine (0) to its free edge (1).
/// </summary>
public static class Curl
{
    /// <summary>The largest radius of the roll, as a share of a page's width.</summary>
    public const double MostRadius = 0.14;

    /// <summary>
    /// A point on a turning page. The page rolls over a cylinder that travels from the outer edge to the
    /// spine: flat on the page up to the cylinder, round it, then lying back over the top with its back
    /// showing. The free edge travels evenly from one outer edge to the other, half way being at the
    /// spine; the roll is widest half way and shrinks to nothing at both ends, so the page starts flat on
    /// one side and ends flat on the other. <paramref name="turned"/> is 0 at the start and 1 at the end.
    /// Angle is how far the paper there has turned from lying face up: 0 flat, pi lying face down.
    /// </summary>
    public static (double X, double Z, double Angle) Sheet(double turned, double t)
    {
        turned = Geo.Clamp(turned, 0, 1);
        double r = MostRadius * Math.Sin(Math.PI * turned);
        double edge = 1 - 2 * turned;                            // where the free edge lies
        double a = Geo.Clamp((edge + 1 - Math.PI * r) / 2, 0, 1); // where the paper leaves the page
        if (t <= a) return (t, 0, 0);
        if (r <= 1e-9) return (a - (t - a), 0, Math.PI);
        double theta = (t - a) / r;
        if (theta <= Math.PI) return (a + r * Math.Sin(theta), r * (1 - Math.Cos(theta)), theta);
        return (a - (t - a - Math.PI * r), 2 * r, Math.PI);
    }

    /// <summary>How much the fold leans: the top of the page is this much further through the turn than the middle.</summary>
    public const double Lean = 0.06;

    /// <summary>
    /// How far through the turn one edge of the page is: the top edge (<paramref name="row"/> 0) a little
    /// ahead and the bottom edge (row 1) a little behind, so the lifted corner leads and the fold runs on
    /// a slant. They agree at the start and the end.
    /// </summary>
    public static double Leaning(double turned, double row)
    {
        turned = Geo.Clamp(turned, 0, 1);
        return Geo.Clamp(turned + Lean * (1 - 2 * Geo.Clamp(row, 0, 1)) * Math.Sin(Math.PI * turned), 0, 1);
    }

    /// <summary>How high the turning page lifts at its highest, 0 lying flat to 1 at the middle of the turn.</summary>
    public static double Lift(double turned) => Math.Sin(Math.PI * Geo.Clamp(turned, 0, 1));

    /// <summary>A point on the cover as it swings open: a stiff board turning about the spine, 0 shut to 1 open.</summary>
    public static (double X, double Z, double Angle) Board(double open, double t)
    {
        double angle = Math.PI * Geo.Clamp(open, 0, 1);
        return (t * Math.Cos(angle), t * Math.Sin(angle), angle);
    }
}
