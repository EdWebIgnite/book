using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Reader.Core;

namespace Reader.Ui;

/*
 * The open book, the curling page turn and the cover.
 *
 * Everything here is worked out in "spread units": the open spread is one unit tall with its top-left
 * corner at 0, 0 (see BookLayout in Reader.Core for the sizes). A transform puts it on screen, so the book
 * moves with the camera and the shapes that make it up are made once and kept.
 *
 * The page turn and the cover are drawn as many narrow upright strips, each cut from the picture and
 * placed where that strip of paper is on the bending sheet (Curl in Reader.Core says where). Strips higher
 * off the page are drawn a little bigger, as nearer things look, and each is shaded by how it faces the
 * light. That is what makes the leaf read as bending paper rather than a flat card sliding over.
 */
public sealed partial class ReaderView
{
    static readonly IImmutableBrush Paper = new ImmutableSolidColorBrush(Color.Parse("#f3eee3"));
    static readonly Rect UnitSquare = new(0, 0, 1, 1);

    /// <summary>How far away the eye is, in spread heights: nearer makes a lifted page look bigger.</summary>
    const double LeafEye = 2.6, CoverEye = 5.5;

    /// <summary>How many strips a bending sheet is cut into.</summary>
    const int LeafStrips = 48, CoverStrips = 32;

    readonly Dictionary<(int Shape, int Left, int Right), BookFrame> _frames3 = new();
    Bitmap? _coverFace, _coverInside;

    /// <summary>Let go of everything made for the last book: its frames and its cover pictures.</summary>
    void ForgetBookPictures()
    {
        _frames3.Clear();
        _coverFace?.Dispose();
        _coverInside?.Dispose();
        _coverFace = _coverInside = null;
    }

    // ---------------------------------------------------------------- the open book

    /// <summary>
    /// The parts of the open book that lie around and under the pages (board, page edges, shadow) and the
    /// shading that lies over them (the crease, and the pages curving down into the binding). In spread
    /// units; made once for each spread shape and stack depth.
    /// </summary>
    sealed class BookFrame
    {
        readonly double _width;
        readonly RoundedRect _board;
        readonly IBrush _boardBrush;
        readonly IPen _rim;
        readonly List<(Rect Rect, IBrush Brush)> _stacks = new();
        readonly IPen _stackLine;
        readonly IBrush _crease;
        readonly Rect _creaseBand;
        readonly Geometry _boardRing;
        readonly Rect _shadowArea;
        Bitmap? _shadow;

        public BookFrame(double width, Color board, int leftLines, int rightLines)
        {
            _width = width;
            double side = BookLayout.StackRoom + BookLayout.Board;
            var outer = new Rect(-side, -BookLayout.Board, width + 2 * side, 1 + 2 * BookLayout.Board);
            _board = new RoundedRect(outer, 0.012);
            _boardBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, -BookLayout.Board, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(0, 1 + BookLayout.Board, RelativeUnit.Absolute),
                GradientStops = { new GradientStop(Shade(board, 1.12), 0), new GradientStop(Shade(board, 0.88), 1) },
            }.ToImmutable();
            _rim = new ImmutablePen(new ImmutableSolidColorBrush(Shade(board, 1.5, 30)), 0.0016);
            var ring = new StreamGeometry();
            using (StreamGeometryContext g = ring.Open())
            {
                g.SetFillRule(FillRule.EvenOdd);
                AddRoundedRect(g, outer, 0.012);
                AddRect(g, new Rect(0.004, 0.004, width - 0.008, 1 - 0.008));
            }
            _boardRing = ring;
            _shadowArea = outer.Inflate(0.12);

            // The edges of the pages underneath: each one a little further out than the one above it, so they
            // show as thin lines beside the top page. Only the strip that shows beside the page is painted, so
            // the frame can be blended over the pages without veiling them.
            Color[] paper = { Color.Parse("#e9e2d3"), Color.Parse("#d8cfbd"), Color.Parse("#efe9dd") };
            double step = BookLayout.StackStep;
            for (int k = rightLines; k >= 1; k--)
                _stacks.Add((new Rect(width, 0.0015 * k, k * step, 1 - 0.003 * k), new ImmutableSolidColorBrush(paper[k % 3])));
            for (int k = leftLines; k >= 1; k--)
                _stacks.Add((new Rect(-k * step, 0.0015 * k, k * step, 1 - 0.003 * k), new ImmutableSolidColorBrush(paper[k % 3])));
            _stackLine = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(70, 120, 100, 80)), 0.0007);

            // Near the middle: the pages darkening a little as they curve down into the binding, a faint
            // highlight where they turn, and a soft dark crease right at the spine. Only that band is painted.
            const double reach = 0.13;
            double mid = width / 2;
            _creaseBand = new Rect(mid - reach, 0, 2 * reach, 1);
            double at(double units) => Math.Clamp(0.5 + units / (2 * reach), 0, 1);
            _crease = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(mid - reach, 0, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(mid + reach, 0, RelativeUnit.Absolute),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 30, 20, 10), 0),
                    new GradientStop(Color.FromArgb(30, 30, 20, 10), at(-0.07)),
                    new GradientStop(Color.FromArgb(26, 255, 255, 255), at(-0.034)),
                    new GradientStop(Color.FromArgb(60, 30, 20, 10), at(-0.014)),
                    new GradientStop(Color.FromArgb(135, 25, 15, 5), 0.5),
                    new GradientStop(Color.FromArgb(60, 30, 20, 10), at(0.014)),
                    new GradientStop(Color.FromArgb(26, 255, 255, 255), at(0.034)),
                    new GradientStop(Color.FromArgb(30, 30, 20, 10), at(0.07)),
                    new GradientStop(Color.FromArgb(0, 30, 20, 10), 1),
                },
            }.ToImmutable();
        }

        public RoundedRect Board => _board;

        static Color Shade(Color c, double by, int lift = 0) => Color.FromRgb(
            (byte)Math.Clamp(c.R * by + lift, 0, 255), (byte)Math.Clamp(c.G * by + lift, 0, 255), (byte)Math.Clamp(c.B * by + lift, 0, 255));

        /// <summary>The shadow under the book, board, and the edges of the pages underneath.</summary>
        public void DrawUnder(DrawingContext ctx, bool shadow = true)
        {
            _shadow ??= MakeShadow(_shadowArea, _board);
            if (shadow) using (ctx.PushRenderOptions(Quick)) DrawAround(ctx, _shadow, _shadowArea, _board.Rect.Deflate(0.006));
            // Only the ring of board round the pages is painted: the pages hide the rest, and filling it
            // with a gradient cost more than everything else in the frame put together on a computer that
            // draws without a graphics card.
            ctx.DrawGeometry(_boardBrush, null, _boardRing);
            ctx.DrawRectangle(null, _rim, _board);
            foreach (var (rect, brush) in _stacks) ctx.DrawRectangle(brush, _stackLine, rect);
        }

        /// <summary>
        /// The board's edge along the spine, for one page shown on its own: a strip of board as wide as the
        /// board round the other edges, on the right of the spine (<paramref name="right"/>) or the left.
        /// </summary>
        public void DrawSpineEdge(DrawingContext ctx, bool right)
        {
            double mid = _width / 2, b = BookLayout.Board, r = 0.012;
            var strip = right ? new Rect(mid, -b, b, 1 + 2 * b) : new Rect(mid - b, -b, b, 1 + 2 * b);
            var round = new RoundedRect(strip, right ? new CornerRadius(0, r, r, 0) : new CornerRadius(r, 0, 0, r));
            ctx.DrawRectangle(_boardBrush, _rim, round);
        }

        /// <summary>The crease and the curve of the pages, as strong as <paramref name="strength"/> (0 to 1).</summary>
        public void DrawCrease(DrawingContext ctx, double strength)
        {
            if (strength < 0.01) return;
            using (ctx.PushOpacity(strength)) ctx.FillRectangle(_crease, _creaseBand);
        }
    }

    /// <summary>A soft shadow under a rounded board, blurred once into a small picture and stretched from then on.</summary>
    static Bitmap MakeShadow(Rect area, RoundedRect board)
    {
        const double pixelsPerUnit = 220;
        var size = new PixelSize((int)Math.Ceiling(area.Width * pixelsPerUnit), (int)Math.Ceiling(area.Height * pixelsPerUnit));
        var picture = new RenderTargetBitmap(size);
        double k = pixelsPerUnit;
        Rect b = board.Rect;
        var inPixels = new RoundedRect(new Rect((b.X - area.X) * k, (b.Y - area.Y) * k, b.Width * k, b.Height * k), 0.012 * k);
        var shadow = new BoxShadows(new BoxShadow { OffsetX = 0.004 * k, OffsetY = 0.016 * k, Blur = 0.07 * k, Color = Color.FromArgb(170, 0, 0, 0) });
        using (DrawingContext ctx = picture.CreateDrawingContext())
            ctx.DrawRectangle(null, null, inPixels, shadow);
        return picture;
    }

    BookFrame FrameFor(LoadedBook book, int page)
    {
        RectD spread = BookLayout.Spread(Engine.PageRects[page]);
        var (left, right) = BookLayout.StackLines(page, book.Book.Pages.Count);
        var key = ((int)Math.Round(spread.W / spread.H * 1000), left, right);
        if (!_frames3.TryGetValue(key, out BookFrame? frame))
            _frames3[key] = frame = new BookFrame(key.Item1 / 1000.0, book.Board, left, right);
        return frame;
    }

    /// <summary>The transform from spread units to the screen, for a spread that is at <paramref name="screen"/>.</summary>
    static Matrix SpreadSpace(RectD screen) => Matrix.CreateScale(screen.H, screen.H) * Matrix.CreateTranslation(screen.X, screen.Y);

    /// <summary>One half of a spread as a picture: which picture, and which of its columns. Plain paper when there is no picture.</summary>
    readonly record struct Half(Bitmap? Picture, double X0, double X1, bool RightHand);

    /// <summary>
    /// The left or right half of a page's spread. For a landscape picture it is half of that picture; for
    /// a portrait page the right half is the page and the left half the page before (if that is a portrait
    /// page too), or plain paper.
    /// </summary>
    Half HalfOf(LoadedBook book, int page, bool right, double spreadPixelsWide)
    {
        RectD pr = Engine.PageRects[page];
        if (!BookLayout.IsPortrait(pr))
        {
            Bitmap picture = book.PagePicture(page, spreadPixelsWide);
            double w = picture.Size.Width;
            return right ? new Half(picture, w / 2, w, true) : new Half(picture, 0, w / 2, false);
        }
        int which = right ? page : page - 1;
        if (which < 0) return new Half(null, 0, 0, right);
        RectD other = Engine.PageRects[which];
        if (!BookLayout.IsPortrait(other) || Math.Abs(other.W / pr.W - 1) > 0.03) return new Half(null, 0, 0, right);
        Bitmap p = book.PagePicture(which, spreadPixelsWide / 2);
        return new Half(p, 0, p.Size.Width, right);
    }

    static void DrawHalf(DrawingContext ctx, Half half, Rect dest)
    {
        if (half.Picture is not Bitmap picture || half.X1 - half.X0 < 0.5) { ctx.FillRectangle(Paper, dest); return; }
        ctx.DrawImage(picture, new Rect(half.X0, 0, half.X1 - half.X0, picture.Size.Height), dest);
    }

    /// <summary>A page's spread lying open: board and page edges underneath, the pages, and the crease over them.</summary>
    void DrawOpenBook(DrawingContext ctx, LoadedBook book, int page, ViewTransform v, double w, double h,
        double scaling, RenderOptions quality, double crease)
    {
        RectD pr = Engine.PageRects[page];
        RectD spread = v.ToScreen(BookLayout.Spread(pr));
        BookFrame frame = FrameFor(book, page);
        Matrix space = SpreadSpace(spread);
        using (ctx.PushTransform(space)) frame.DrawUnder(ctx);
        if (BookLayout.IsPortrait(pr))
        {
            using (ctx.PushRenderOptions(Quick))
                DrawHalf(ctx, HalfOf(book, page, right: false, spread.W * scaling), new Rect(spread.X, spread.Y, spread.W / 2, spread.H));
        }
        DrawPage(ctx, book, page, v.ToScreen(pr), null, w, h, scaling, quality, weather: true);
        using (ctx.PushTransform(space)) frame.DrawCrease(ctx, crease);
    }

    /// <summary>
    /// One page at a time: hide the page of the spread that isn't being read (<paramref name="shown"/> is
    /// the one that is), as strongly as <paramref name="strength"/>. The backdrop goes over it, and the
    /// board's edge closes off the page being read along the spine, so it looks like a book of single pages.
    /// </summary>
    void CoverOtherPage(DrawingContext ctx, LoadedBook book, int page, ViewTransform v, int shown, double strength, double w, double h)
    {
        if (strength < 0.005) return;
        RectD spread = v.ToScreen(BookLayout.Spread(Engine.PageRects[page]));
        double spine = spread.CenterX;
        bool hideRight = shown == Reader.Core.Step.LeftPage;
        Rect hidden = hideRight ? new Rect(spine, 0, Math.Max(0, w - spine), h) : new Rect(0, 0, Math.Max(0, spine), h);
        if (hidden.Width <= 0) return;
        using (ctx.PushOpacity(Math.Clamp(strength, 0, 1)))
        {
            ctx.FillRectangle(BackdropBrush, hidden);
            using (ctx.PushTransform(SpreadSpace(spread))) FrameFor(book, page).DrawSpineEdge(ctx, right: hideRight);
        }
    }

    /// <summary>
    /// One page at a time, a single page turning: the page being read lifts from its outer edge and rolls
    /// away over its own inner edge, uncovering the next page lying in its place (going back, the same
    /// backwards). Both pages are drawn exactly as they lie at rest, so nothing jumps at either end.
    /// </summary>
    void DrawOnePageCurl(DrawingContext ctx, LoadedBook book, FlipDraw flip, double w, double h, double scaling)
    {
        ReaderEngine e = Engine;
        int earlier = flip.TurningPage, later = flip.UnderPage;
        ViewTransform vTurn = e.ViewFor(flip.TurningCam), vUnder = e.ViewFor(flip.UnderCam);
        RectD pageRect = vUnder.ToScreen(e.HalfRect(later, flip.UnderHalf));
        RectD earlierSpread = vTurn.ToScreen(BookLayout.Spread(e.PageRects[earlier]));
        double turned = Math.Clamp(flip.Fold, 0, 1);
        double lift = Curl.Lift(turned), radius = Curl.MostRadius * lift;

        // Underneath: the page that is uncovered, as it lies at rest. Round it, what was round the page being
        // read fades into what is round the new one while the page is in the air.
        DrawOpenBook(ctx, book, later, vUnder, w, h, scaling, Quick, 1);
        CoverOtherPage(ctx, book, later, vUnder, flip.UnderHalf, 1, w, h);
        double before = 1 - SmoothStep(0.3, 0.7, turned);
        if (before > 0.005)
        {
            var outside = new StreamGeometry();
            using (StreamGeometryContext g = outside.Open())
            {
                g.SetFillRule(FillRule.EvenOdd);
                AddRect(g, new Rect(0, 0, w, h));
                AddRect(g, ToRect(pageRect));
            }
            using (ctx.PushGeometryClip(outside))
            using (ctx.PushOpacity(before))
            {
                using (ctx.PushTransform(SpreadSpace(earlierSpread))) FrameFor(book, earlier).DrawUnder(ctx, shadow: false);
                CoverOtherPage(ctx, book, earlier, vTurn, flip.TurningHalf, 1, w, h);
            }
        }

        // The turning page: the half of the earlier picture that was being read, hinged at the page's left edge.
        Half turning = HalfOf(book, earlier, right: flip.TurningHalf == Reader.Core.Step.RightPage, earlierSpread.W * scaling);
        var front = new Face(turning.Picture, turning.X0, turning.X1, 0, turning.Picture?.Size.Height ?? 0, SpineAtLeft: true);
        var back = new Face(null, 0, 0, 0, 0, SpineAtLeft: false);
        double hinge = pageRect.X, width = pageRect.W;
        Rect page = ToRect(pageRect);

        // The roll throws a soft shadow on the page it uncovers, just beyond its outermost point.
        double outermost = 0;
        for (int i = 0; i <= 24; i++)
        {
            var (x, z, _) = Curl.Sheet(turned, i / 24.0);
            outermost = Math.Max(outermost, x * LeafEye * pageRect.H / (LeafEye * pageRect.H - z * width));
        }
        using (ctx.PushClip(page))
            DrawShadowBand(ctx, hinge + outermost * width, width * (0.05 + 0.9 * radius), pageRect.Y, pageRect.H, 0.5 * lift);

        // What rolls past the page's inner edge goes out of sight there, as if over the edge of the book.
        var place = new SheetPlace(hinge, width, pageRect.Y, pageRect.H, 1, LeafEye * pageRect.H);
        using (ctx.PushClip(new Rect(hinge, -1e5, 2e5, 2e5)))
        using (ctx.PushRenderOptions(Quick))
            DrawSheet(ctx, place, front, back, LeafStrips, (t, row) => Curl.Sheet(Curl.Leaning(turned, row), t), backDarker: 0.05 * lift);

        // The pictures-on-top and weather of the page being turned stay on it while it lies flat.
        double flatAtStart = 1 - SmoothStep(0, 0.12, turned);
        RectD earlierPage = vTurn.ToScreen(e.PageRects[earlier]);
        DrawOnTop(ctx, book, earlier, earlierPage, page, flatAtStart);
    }

    // ---------------------------------------------------------------- bending sheets

    /// <summary>One face of a bending sheet: where its picture comes from, and whether the spine is at that picture's left.</summary>
    readonly record struct Face(Bitmap? Picture, double X0, double X1, double Y0, double Y1, bool SpineAtLeft)
    {
        public static Face Of(Half half) => new(half.Picture, half.X0, half.X1, 0, half.Picture?.Size.Height ?? 0, half.RightHand);
        public static Face Whole(Bitmap picture, bool spineAtLeft) => new(picture, 0, picture.Size.Width, 0, picture.Size.Height, spineAtLeft);

        /// <summary>The picture's column for a point <paramref name="t"/> of the way from the spine to the free edge.</summary>
        public double Column(double t) => SpineAtLeft ? X0 + t * (X1 - X0) : X1 - t * (X1 - X0);
    }

    /// <summary>Where a bending sheet is on screen: its spine, how wide and how tall it is lying flat, and which way it starts.</summary>
    readonly record struct SheetPlace(double Spine, double Width, double Top, double Height, int Direction, double Eye);

    /// <summary>
    /// Draw a bending sheet as upright strips. <paramref name="shape"/> gives, for a point t of the way from
    /// the spine to the free edge, where that point is (across, from the spine, in sheet widths), how high
    /// above the page, and how far it has turned. A strip running away from the spine shows the front;
    /// running back towards it, the back.
    /// </summary>
    void DrawSheet(DrawingContext ctx, SheetPlace at, Face front, Face back, int strips,
        Func<double, double, (double X, double Z, double Angle)> shape, Action? underTheLiftedPart = null, double backDarker = 0.05)
    {
        double eye = at.Eye, cy = at.Top + at.Height / 2, bottom0 = at.Top + at.Height;
        // Each point along the sheet is worked out twice, for its top edge and its bottom edge, so that the
        // fold can lean (the top corner leading). A strip is then the four-sided piece between two of them.
        var top = new Point[strips + 1];
        var bottom = new Point[strips + 1];
        var angle = new double[strips + 1];
        var lifted = new bool[strips + 1];
        for (int i = 0; i <= strips; i++)
        {
            double t = (double)i / strips;
            var (xt, zt, at0) = shape(t, 0);
            var (xb, zb, ab) = shape(t, 1);
            top[i] = Project(at, eye, cy, xt, zt, at.Top);
            bottom[i] = Project(at, eye, cy, xb, zb, bottom0);
            angle[i] = (at0 + ab) / 2;
            lifted[i] = zt > 1e-6 || zb > 1e-6;
        }
        // Where the sheet lies flat (on the page, or lying back over the top) one piece does for many strips.
        var flatKind = new int[strips + 1];
        for (int i = 0; i <= strips; i++)
        {
            var (_, zt, at0) = shape((double)i / strips, 0);
            var (_, zb, ab) = shape((double)i / strips, 1);
            flatKind[i] = at0 == 0 && ab == 0 && zt == 0 && zb == 0 ? 1 : at0 == Math.PI && ab == Math.PI && Math.Abs(zt - zb) < 1e-12 ? 2 : 0;
        }
        bool shadowDrawn = underTheLiftedPart is null;
        for (int i = 0, next; i < strips; i = next)
        {
            next = i + 1;
            if (flatKind[i] != 0)
                while (next < strips && flatKind[next] == flatKind[i] && flatKind[next + 1] == flatKind[i]) next++;
            if (!shadowDrawn && (lifted[i] || lifted[next]))
            {
                underTheLiftedPart!();
                shadowDrawn = true;
            }
            Point tl = top[i], tr = top[next], bl = bottom[i], br = bottom[next];
            double across = (tr.X - tl.X + br.X - bl.X) / 2;
            if (Math.Abs(across) < 0.02) continue;                   // seen edge on
            bool showsFront = across * at.Direction > 0;
            Face face = showsFront ? front : back;

            // The strip as an affine piece: its top edge runs from tl to tr and its left side from tl to bl.
            // A whisker wider than its place, so that no hairline shows between strips.
            double grow = 0.4 / Math.Abs(across);
            var shape2 = new Matrix(tr.X - tl.X, tr.Y - tl.Y, (bl.X - tl.X + br.X - tr.X) / 2, (bl.Y - tl.Y + br.Y - tr.Y) / 2, tl.X, tl.Y);
            var unit = new Rect(-grow, 0, 1 + 2 * grow, 1);
            double c0 = face.Column((double)i / strips), c1 = face.Column((double)next / strips);
            using (ctx.PushTransform(shape2))
            {
                if (face.Picture is Bitmap picture && Math.Abs(c1 - c0) > 1e-4)
                {
                    // Along the strip (left to right in its own square) the columns may count down; then the
                    // square is turned round first so the picture still lands the right way.
                    double width = Math.Abs(c1 - c0);
                    var source = new Rect(Math.Min(c0, c1) - grow * width, face.Y0, Math.Max(0.01, width * (1 + 2 * grow)), face.Y1 - face.Y0);
                    using (ctx.PushTransform(c1 < c0 ? new Matrix(-1, 0, 0, 1, 1, 0) : Matrix.Identity))
                        ctx.DrawImage(picture, source, unit);
                }
                else
                {
                    ctx.FillRectangle(Paper, unit);
                }

                // Light from above: paper facing up is bright, paper tipped up on edge is darker, and there is
                // a gleam where the sheet curves over.
                double theta = (angle[i] + angle[next]) / 2;
                double facing = Math.Abs(Math.Cos(theta));
                double dark = 0.6 * Math.Pow(1 - facing, 0.9) + (showsFront ? 0 : backDarker);
                double gleam = 0.22 * Math.Exp(-Math.Pow((theta - 0.8) / 0.3, 2)) + 0.2 * Math.Exp(-Math.Pow((theta - (Math.PI - 0.7)) / 0.3, 2));
                if (dark > 0.01) ctx.FillRectangle(Brush(Rgba(25, 18, 10, dark)), unit);
                if (gleam > 0.01) ctx.FillRectangle(Brush(Rgba(255, 255, 250, gleam)), unit);
            }
        }
        if (!shadowDrawn) underTheLiftedPart!();
    }

    /// <summary>Where a point of a bending sheet lands on screen: lifted points are nearer the eye, so further from the middle.</summary>
    static Point Project(SheetPlace at, double eye, double cy, double x, double z, double y)
    {
        double s = eye / Math.Max(eye * 0.25, eye - z * at.Width);
        double flatX = at.Spine + at.Direction * x * at.Width;
        return new Point(at.Spine + (flatX - at.Spine) * s, cy + (y - cy) * s);
    }

    /// <summary>A shadow that is darkest at x = 0 and has faded away by x = 1, in a unit square.</summary>
    static readonly IBrush FallOff = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Absolute),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(255, 0, 0, 0), 0), new GradientStop(Color.FromArgb(120, 0, 0, 0), 0.25),
            new GradientStop(Color.FromArgb(40, 0, 0, 0), 0.6), new GradientStop(Color.FromArgb(0, 0, 0, 0), 1),
        },
    }.ToImmutable();

    /// <summary>The same shadow the other way round: nothing at x = 0, darkest at x = 1.</summary>
    static readonly IBrush FallOffBack = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Absolute),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Color.FromArgb(40, 0, 0, 0), 0.4),
            new GradientStop(Color.FromArgb(120, 0, 0, 0), 0.75), new GradientStop(Color.FromArgb(255, 0, 0, 0), 1),
        },
    }.ToImmutable();

    /// <summary>
    /// A soft shadow starting at <paramref name="from"/> and reaching <paramref name="reach"/> along the
    /// screen (to the right for a positive reach, to the left for a negative one), darkest where it starts.
    /// </summary>
    static void DrawShadowBand(DrawingContext ctx, double from, double reach, double top, double height, double strength)
    {
        if (Math.Abs(reach) < 0.5 || strength < 0.01) return;
        using (ctx.PushOpacity(Math.Clamp(strength, 0, 1)))
        {
            if (reach > 0)
            {
                using (ctx.PushTransform(Matrix.CreateScale(reach, height) * Matrix.CreateTranslation(from, top)))
                    ctx.FillRectangle(FallOff, UnitSquare);
            }
            else
            {
                using (ctx.PushTransform(Matrix.CreateScale(-reach, height) * Matrix.CreateTranslation(from + reach, top)))
                    ctx.FillRectangle(FallOffBack, UnitSquare);
            }
        }
    }

    // ---------------------------------------------------------------- the page turn

    /// <summary>
    /// A page curling over. The turning sheet has the earlier spread's right half on its front and the
    /// later spread's left half on its back. Underneath lie the earlier spread's left half and the later
    /// spread's right half. Going on, the right-hand page lifts from its outer edge and rolls over to the
    /// left; going back, the left-hand page does the same towards the right.
    /// </summary>
    void DrawCurl(DrawingContext ctx, LoadedBook book, FlipDraw flip, double scaling)
    {
        ReaderEngine e = Engine;
        int earlier = flip.TurningPage, later = flip.UnderPage;
        RectD spread = e.ViewFor(flip.UnderCam).ToScreen(BookLayout.Spread(e.PageRects[later]));
        RectD earlierPage = e.ViewFor(flip.TurningCam).ToScreen(e.PageRects[earlier]);
        RectD laterPage = e.ViewFor(flip.UnderCam).ToScreen(e.PageRects[later]);
        double pixels = spread.W * scaling;
        double half = spread.W / 2, spine = spread.X + half;
        var leftHalf = new Rect(spread.X, spread.Y, half, spread.H);
        var rightHalf = new Rect(spine, spread.Y, half, spread.H);

        // How far over the sheet is, 0 to 1, counted from the side it starts on.
        int direction = flip.Forward ? 1 : -1;
        double turned = Math.Clamp(flip.Forward ? flip.Fold : 1 - flip.Fold, 0, 1);
        double lift = Curl.Lift(turned), radius = Curl.MostRadius * lift;

        // The book itself. The page edges move from one side to the other while the leaf is in the air:
        // the stack the turn starts from fades into the stack it ends on, so nothing jumps at either end.
        BookFrame from = FrameFor(book, flip.Forward ? earlier : later), to = FrameFor(book, flip.Forward ? later : earlier);
        Matrix space = SpreadSpace(spread);
        double across = SmoothStep(0.3, 0.7, turned);
        using (ctx.PushTransform(space))
        {
            // Each is drawn alone at its own end of the turn: drawn twice, the board's rim came out darker
            // than at rest and flickered as the leaf landed.
            if (ReferenceEquals(from, to) || across < 0.005) from.DrawUnder(ctx);
            else if (across > 0.995) to.DrawUnder(ctx);
            else
            {
                from.DrawUnder(ctx);
                using (ctx.PushOpacity(across)) to.DrawUnder(ctx, shadow: false);
            }
        }

        // The two pages lying underneath, with their pictures on top and their weather, just as they are
        // drawn once the book lies open, so that nothing about them changes when the leaf lands.
        Half earlierLeft = HalfOf(book, earlier, right: false, pixels), earlierRight = HalfOf(book, earlier, right: true, pixels);
        Half laterLeft = HalfOf(book, later, right: false, pixels), laterRight = HalfOf(book, later, right: true, pixels);
        using (ctx.PushRenderOptions(Quick))
        {
            DrawHalf(ctx, earlierLeft, leftHalf);
            DrawHalf(ctx, laterRight, rightHalf);
        }
        DrawOnTop(ctx, book, earlier, earlierPage, leftHalf, 1);
        DrawOnTop(ctx, book, later, laterPage, rightHalf, 1);
        using (ctx.PushTransform(space)) to.DrawCrease(ctx, 1);

        Face front = Face.Of(flip.Forward ? earlierRight : laterLeft), back = Face.Of(flip.Forward ? laterLeft : earlierRight);

        // The roll throws a soft shadow on the page it uncovers, just beyond its outermost point.
        double outermost = 0;
        for (int i = 0; i <= 24; i++)
        {
            var (x, z, _) = Curl.Sheet(turned, i / 24.0);
            outermost = Math.Max(outermost, x * LeafEye * spread.H / (LeafEye * spread.H - z * half));
        }
        var (edgeX, edgeZ, _) = Curl.Sheet(turned, 1);
        Rect pages = ToRect(spread);
        using (ctx.PushClip(pages))
            DrawShadowBand(ctx, spine + direction * outermost * half, direction * half * (0.05 + 0.9 * radius), spread.Y, spread.H, 0.5 * lift);

        // The sheet is not clipped to the pages: lifted off them, it is nearer the eye and looks a little bigger.
        // It is drawn right to the end, lying flat as it lands, so it never vanishes before the open book takes over.
        var place = new SheetPlace(spine, half, spread.Y, spread.H, direction, LeafEye * spread.H);
        using (ctx.PushRenderOptions(Quick))
        {
            DrawSheet(ctx, place, front, back, LeafStrips, (t, row) => Curl.Sheet(Curl.Leaning(turned, row), t), () =>
            {
                // The part lying back over the top throws a shadow beyond its free edge, on the pages below.
                if (edgeZ > 1e-4)
                    using (ctx.PushClip(pages))
                        DrawShadowBand(ctx, spine + direction * edgeX * half, -direction * half * (0.02 + 1.4 * edgeZ), spread.Y, spread.H, 0.38 * lift);
            }, backDarker: 0.05 * lift);
        }

        // Things that belong on top of the leaf when it lies flat: the pictures-on-top and weather of the page
        // it carries, and the crease. They fade out as the leaf lifts and back in as it lands, so the first
        // and last frames of the turn match the open book exactly.
        double flatAtStart = 1 - SmoothStep(0, 0.12, turned), flatAtEnd = SmoothStep(0.88, 1, turned);
        if (flip.Forward)
        {
            DrawOnTop(ctx, book, earlier, earlierPage, rightHalf, flatAtStart);
            DrawOnTop(ctx, book, later, laterPage, leftHalf, flatAtEnd);
        }
        else
        {
            DrawOnTop(ctx, book, later, laterPage, leftHalf, flatAtStart);
            DrawOnTop(ctx, book, earlier, earlierPage, rightHalf, flatAtEnd);
        }
        // The crease goes back over the leaf only on the half the leaf covers: the other half already has it,
        // from underneath, and doubling it there made the spine darker for the last frames of the turn.
        Rect departing = flip.Forward ? rightHalf : leftHalf, landing = flip.Forward ? leftHalf : rightHalf;
        if (flatAtStart > 0.005)
            using (ctx.PushClip(departing.Inflate(new Thickness(0, spread.H, 0, spread.H))))
            using (ctx.PushTransform(space)) to.DrawCrease(ctx, flatAtStart);
        if (flatAtEnd > 0.005)
            using (ctx.PushClip(landing.Inflate(new Thickness(0, spread.H, 0, spread.H))))
            using (ctx.PushTransform(space)) to.DrawCrease(ctx, flatAtEnd);
    }

    /// <summary>Goes smoothly from 0 at <paramref name="from"/> to 1 at <paramref name="to"/>.</summary>
    static double SmoothStep(double from, double to, double x)
    {
        double u = Math.Clamp((x - from) / (to - from), 0, 1);
        return u * u * (3 - 2 * u);
    }

    /// <summary>
    /// A page's pictures-on-top and weather, only where they fall inside <paramref name="area"/>, as
    /// strongly as <paramref name="opacity"/>. A portrait page has none on its left-hand side (that side
    /// shows the page before).
    /// </summary>
    void DrawOnTop(DrawingContext ctx, LoadedBook book, int page, RectD pageOnScreen, Rect area, double opacity)
    {
        if (opacity < 0.005) return;
        Page p = book.Book.Pages[page];
        if (p.Layers.Count == 0 && p.Effects.Count == 0) return;
        Rect inside = area.Intersect(ToRect(pageOnScreen));
        if (inside.Width <= 0.5) return;
        using (ctx.PushClip(inside))
        using (ctx.PushOpacity(opacity))
        {
            DrawLayers(ctx, book, page, pageOnScreen, Quick);
            DrawWeather(ctx, book, page, pageOnScreen);
        }
    }

    // ---------------------------------------------------------------- the cover

    /// <summary>
    /// The front of the cover: the board, with the cover picture fitted inside it with a margin (the
    /// picture is 2:3 and the board is not, so it is never cropped), and the groove of the hinge beside the
    /// spine. Made once per book, at a size that stays sharp when drawn large.
    /// </summary>
    Bitmap CoverFace(LoadedBook book, RectD board)
    {
        if (_coverFace is not null) return _coverFace;
        const double unit = 1000;                                        // pixels per spread height
        var size = new PixelSize((int)Math.Ceiling(board.W * unit), (int)Math.Ceiling(board.H * unit));
        var picture = new RenderTargetBitmap(size);
        using (DrawingContext ctx = picture.CreateDrawingContext())
        {
            var all = new Rect(0, 0, size.Width, size.Height);
            Color c = book.Board;
            var boardBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(size.Width, size.Height, RelativeUnit.Absolute),
                GradientStops = { new GradientStop(Lighter(c, 1.2), 0), new GradientStop(Lighter(c, 0.85), 1) },
            };
            ctx.DrawRectangle(boardBrush, null, new RoundedRect(all, new CornerRadius(4, 14, 14, 4)));
            if (book.Cover is Bitmap cover)
            {
                double margin = 0.035 * unit;
                Rect room = all.Deflate(new Thickness(margin + 0.03 * unit, margin, margin, margin));
                double scale = Math.Min(room.Width / cover.Size.Width, room.Height / cover.Size.Height);
                double w = cover.Size.Width * scale, h = cover.Size.Height * scale;
                var place = new Rect(room.X + (room.Width - w) / 2, room.Y + (room.Height - h) / 2, w, h);
                ctx.DrawRectangle(Brush(Color.FromArgb(90, 0, 0, 0)), null, place.Translate(new Vector(3, 4)).Inflate(2), 3, 3);
                using (ctx.PushRenderOptions(Fine)) ctx.DrawImage(cover, place);
                ctx.DrawRectangle(null, new ImmutablePen(Brush(Color.FromArgb(60, 0, 0, 0)), 1.5), place);
            }
            // The hinge: a groove pressed into the board a little way from the spine, and the spine side in shade.
            double groove = 0.022 * unit;
            ctx.FillRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(groove * 1.6, 0, RelativeUnit.Absolute),
                GradientStops = { new GradientStop(Color.FromArgb(90, 0, 0, 0), 0), new GradientStop(Color.FromArgb(0, 0, 0, 0), 1) },
            }, new Rect(0, 0, groove * 1.6, size.Height));
            ctx.FillRectangle(Brush(Color.FromArgb(80, 0, 0, 0)), new Rect(groove, 0, 3, size.Height));
            ctx.FillRectangle(Brush(Color.FromArgb(50, 255, 255, 255)), new Rect(groove + 3, 0, 2, size.Height));
        }
        return _coverFace = picture;
    }

    /// <summary>
    /// The inside of the cover, which lands as the left-hand page of the first spread: board round the
    /// edge, one line of page edge, and the left half of the first spread exactly where it lies once open,
    /// so the cover lands without a jump. Its spine is at its right-hand side.
    /// </summary>
    Bitmap CoverInside(LoadedBook book, RectD board, Half firstLeft)
    {
        if (_coverInside is not null) return _coverInside;
        const double unit = 1000;
        var size = new PixelSize((int)Math.Ceiling(board.W * unit), (int)Math.Ceiling(board.H * unit));
        var picture = new RenderTargetBitmap(size);
        using (DrawingContext ctx = picture.CreateDrawingContext())
        {
            var all = new Rect(0, 0, size.Width, size.Height);
            ctx.DrawRectangle(new ImmutableSolidColorBrush(book.Board), null, new RoundedRect(all, new CornerRadius(14, 4, 4, 14)));
            double side = (BookLayout.StackRoom + BookLayout.Board) * unit, pageWidth = size.Width - side;
            var page = new Rect(side, BookLayout.Board * unit, pageWidth, unit);
            ctx.FillRectangle(Brush(Color.Parse("#d8cfbd")), new Rect(page.X - BookLayout.StackStep * unit, page.Y + 0.8, pageWidth, unit + 0.6));
            using (ctx.PushRenderOptions(Fine)) DrawHalf(ctx, firstLeft, page);
        }
        return _coverInside = picture;
    }

    /// <summary>
    /// Make the pictures of the cover's two faces now, while the book is opening, so the first frame of
    /// the cover swinging open doesn't have to.
    /// </summary>
    void PrepareCover(LoadedBook book)
    {
        if (!book.HasCover || Engine.PageRects.Count == 0 || !Engine.ShownAsBook(0)) return;
        RectD boardWorld = BookLayout.CoverBoard(Engine.PageRects[0]);
        var inUnits = new RectD(0, 0, boardWorld.W, boardWorld.H);
        CoverFace(book, inUnits);
        double wide = book.PageSize(0).Width;
        CoverInside(book, inUnits, HalfOf(book, 0, right: false, wide));
    }

    static Color Lighter(Color c, double by) => Color.FromRgb(
        (byte)Math.Clamp(c.R * by, 0, 255), (byte)Math.Clamp(c.G * by, 0, 255), (byte)Math.Clamp(c.B * by, 0, 255));

    /// <summary>
    /// The book with its cover: shut, or with the cover swinging round the spine as a stiff board. Under a
    /// swinging cover lies the right-hand page of the first spread; its inside is that spread's left page.
    /// </summary>
    void DrawCover(DrawingContext ctx, LoadedBook book, CoverDraw cover, ViewTransform v, double scaling)
    {
        ReaderEngine e = Engine;
        RectD pr = e.PageRects[cover.Page];
        RectD spread = v.ToScreen(BookLayout.Spread(pr));
        RectD boardWorld = BookLayout.CoverBoard(pr);
        RectD board = v.ToScreen(boardWorld);
        double half = spread.W / 2, spine = spread.X + half;
        RectD boardInUnits = new(0, 0, boardWorld.W, boardWorld.H);
        Bitmap face = CoverFace(book, boardInUnits);

        if (cover.Open <= 0.0005)
        {
            // Shut: the back board and the block of pages show at the right and bottom as the book's
            // thickness, a shadow under it all, and the spine down the left.
            double thick = BookLayout.ClosedThickness * spread.H;
            var boardRound = new RoundedRect(ToRect(board).Translate(new Vector(thick, thick)), board.H * 0.012);
            ctx.DrawRectangle(null, null, boardRound, new BoxShadows(new BoxShadow { OffsetY = board.H * 0.012, Blur = board.H * 0.05, Color = Color.FromArgb(150, 0, 0, 0) }));
            ctx.DrawRectangle(new ImmutableSolidColorBrush(Lighter(book.Board, 0.7)), null, boardRound);
            var block = new Rect(spine, spread.Y, half + (BookLayout.StackRoom * 0.6) * spread.H, spread.H);
            for (int k = 4; k >= 0; k--)
            {
                double o = thick * (0.2 + 0.18 * k);
                ctx.DrawRectangle(k % 2 == 0 ? Paper : Brush(Color.Parse("#ddd4c2")), new ImmutablePen(Brush(Color.FromArgb(60, 120, 100, 80)), 0.6),
                    block.Translate(new Vector(o, o)));
            }
            using (ctx.PushRenderOptions(Quick)) ctx.DrawImage(face, ToRect(board));
            var spineStrip = new Rect(spine - spread.H * 0.012, board.Y, spread.H * 0.016, board.H + thick * 0.5);
            ctx.DrawRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(spineStrip.X, 0, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(spineStrip.Right, 0, RelativeUnit.Absolute),
                GradientStops = { new GradientStop(Lighter(book.Board, 0.55), 0), new GradientStop(Lighter(book.Board, 0.95), 0.6), new GradientStop(Lighter(book.Board, 0.7), 1) },
            }.ToImmutable(), null, new RoundedRect(spineStrip, spread.H * 0.006));
            return;
        }

        // Opening: the right-hand half of the open book lies underneath, and the cover swings over it.
        BookFrame frame = FrameFor(book, cover.Page);
        Matrix space = SpreadSpace(spread);
        using (ctx.PushClip(new Rect(spine, -1e5, 2e5, 2e5)))
        {
            using (ctx.PushTransform(space)) frame.DrawUnder(ctx);
            using (ctx.PushRenderOptions(Quick)) DrawHalf(ctx, HalfOf(book, cover.Page, right: true, spread.W * scaling), new Rect(spine, spread.Y, half, spread.H));
            using (ctx.PushTransform(space)) frame.DrawCrease(ctx, 1);
        }

        double angle = Math.PI * cover.Open, lift = Math.Sin(angle);
        // While the cover is still over the page, it shades the page beneath beyond its edge.
        if (cover.Open < 0.5)
            DrawShadowBand(ctx, spine + board.W * Math.Cos(angle), board.W * (0.05 + 0.25 * lift), spread.Y, spread.H, 0.5 * lift);

        Bitmap inside = CoverInside(book, boardInUnits, HalfOf(book, cover.Page, right: false, spread.W * scaling * 1.5));
        var place = new SheetPlace(spine, board.W, board.Y, board.H, 1, CoverEye * spread.H);
        using (ctx.PushRenderOptions(Quick))
            DrawSheet(ctx, place, Face.Whole(face, spineAtLeft: true), Face.Whole(inside, spineAtLeft: false), CoverStrips,
                (t, _) => Curl.Board(cover.Open, t), backDarker: 0);

        // As the cover settles, the open book it is turning into fades in over it, so that the last frame of
        // the swing is the open book exactly and nothing jumps when the swing ends.
        // Only the left half is blended (the right half underneath is already the open book's). Part strength
        // applies to each thing drawn separately, so nothing drawn here may lie under the page picture:
        // page-edge strips that reached under the page showed through it and washed it out.
        double settled = SmoothStep(0.94, 1, cover.Open);
        if (settled > 0.005)
        {
            var leftHalf = new Rect(spread.X, spread.Y, half, spread.H);
            using (ctx.PushClip(new Rect(-1e5, -1e5, spine + 1e5, 2e5)))
            using (ctx.PushOpacity(settled))
            {
                using (ctx.PushTransform(space)) frame.DrawUnder(ctx, shadow: false);
                using (ctx.PushRenderOptions(Quick)) DrawHalf(ctx, HalfOf(book, cover.Page, right: false, spread.W * scaling), leftHalf);
                DrawOnTop(ctx, book, cover.Page, v.ToScreen(pr), leftHalf, 1);
                using (ctx.PushTransform(space)) frame.DrawCrease(ctx, 1);
            }
        }
    }
}
