using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Reader.Core;

namespace Reader.Ui;

/*
 * How the reader's picture is drawn.
 *
 * Drawing order matters and follows the browser reader: pages (with their sharp tiles), then the
 * pictures on top, then weather, all in the page's own space so they stay put while the camera
 * moves. Over those come the focus effects (soft edges, spotlight, dim), then things to find, the
 * hint, the fade, lightning, the corner map, the party and the touch ripples.
 *
 * Keeping it smooth:
 *   - A page picture is never shrunk a long way on the fly. LoadedBook keeps half-size and
 *     quarter-size copies, and the smallest one that is big enough is drawn with the cheapest
 *     filter. (Shrinking a big picture with a "high quality" filter took over a tenth of a second a
 *     frame without a graphics card, and on one it kept throwing away and rebuilding big textures.)
 *   - The finer filter is only used for enlarging, and only when everything has come to rest.
 *   - Brushes, pens, the book frame and the shapes that don't change are made once and kept. What
 *     is rebuilt every frame is only what really is different every frame (the rain).
 *   - Only pages that are on screen are drawn.
 */
public sealed partial class ReaderView
{
    static readonly Color Backdrop = Color.Parse("#1c1b22");
    static readonly IImmutableBrush BackdropBrush = new ImmutableSolidColorBrush(Backdrop);
    static readonly IImmutableBrush White = new ImmutableSolidColorBrush(Colors.White);
    static readonly RenderOptions Quick = new() { BitmapInterpolationMode = BitmapInterpolationMode.LowQuality };
    static readonly RenderOptions Fine = new() { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };

    readonly Dictionary<uint, IImmutableBrush> _brushes = new();
    readonly Dictionary<(uint Colour, int Width, bool Round), ImmutablePen> _pens = new();
    readonly Dictionary<(int Page, int Effect), Geometry> _weatherClips = new();
    readonly Dictionary<int, BookFrame> _bookFrames = new();        // by page shape (width over height, in thousandths)
    LoadedBook? _shapesFor;

    /// <summary>How many times the picture has been drawn. The bench uses it to tell drawn frames from skipped ones.</summary>
    public int RenderCount { get; private set; }

    public override void Render(DrawingContext ctx)
    {
        RenderCount++;
        double w = Bounds.Width, h = Bounds.Height;
        var all = new Rect(0, 0, w, h);
        ctx.FillRectangle(BackdropBrush, all);
        LoadedBook? book = _book;
        ReaderEngine e = Engine;
        if (book is null || !ReferenceEquals(e.Book, book.Book) || e.Steps.Count == 0 || w <= 0 || h <= 0)
        {
            DrawFrameRate(ctx);
            return;
        }
        if (!ReferenceEquals(_shapesFor, book))
        {
            // Shapes kept from the last book don't fit this one.
            _shapesFor = book;
            _weatherClips.Clear();
            _bookFrames.Clear();
        }

        ViewTransform v = e.GetView();
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        // The finer filter waits until everything is at rest; while anything moves, quick is what keeps it smooth.
        RenderOptions quality = e.Restless || e.Animating || Gestures.Active ? Quick : Fine;

        if (e.BookMode)
        {
            if (e.GetFlip() is FlipDraw flip) DrawFlip(ctx, book, flip, w, h, scaling);
            else
            {
                foreach (int i in e.BookPages())
                {
                    RectD r = v.ToScreen(e.PageRects[i]);
                    if (OnScreen(r, w, h, r.H * 0.12)) DrawBookPage(ctx, book, i, r, w, h, scaling, quality, weather: true);
                }
            }
        }
        else
        {
            for (int i = 0; i < e.PageRects.Count; i++)
            {
                RectD r = v.ToScreen(e.PageRects[i]);
                if (OnScreen(r, w, h, 0)) DrawPage(ctx, book, i, r, v, w, h, scaling, quality, weather: true);
            }
        }

        Shot shown = e.Shown;
        RectD f = v.ToScreen(shown.Focus);
        if (shown.Vig > 0.01)
        {
            // Soft oval darkening that leaves the focus box clear.
            FillOval(ctx, all, scaling, f.CenterX, f.CenterY, Math.Max(f.W / 2, 1) * 2.6, Math.Max(f.H / 2, 1) * 2.6, SoftEdge, shown.Vig);
        }
        if (shown.Spot > 0.01)
        {
            // Spotlight: an oval of light a quarter bigger than the focus box, so what's inside the box is lit
            // (all but its corners). Soft at the rim, dark everywhere else.
            FillOval(ctx, all, scaling, f.CenterX, f.CenterY, Math.Max(f.W * 0.625, 1), Math.Max(f.H * 0.625, 1), Spot, shown.Spot);
        }
        if (shown.Dim > 0.01)
        {
            // Everything but the focus box goes dark. The box has slightly rounded corners.
            var hole = new StreamGeometry();
            using (StreamGeometryContext g = hole.Open())
            {
                g.SetFillRule(FillRule.EvenOdd);
                AddRect(g, all);
                AddRoundedRect(g, ToRect(f), 6);
            }
            ctx.DrawGeometry(Brush(Rgba(0, 0, 0, shown.Dim)), null, hole);
        }

        DrawBursts(ctx, v);
        DrawHint(ctx, v);
        if (shown.Fade > 0.01) ctx.FillRectangle(Brush(Rgba(28, 27, 34, shown.Fade)), all);
        double flash = e.FlashAlpha;
        if (flash > 0.003) ctx.FillRectangle(Brush(Rgba(255, 255, 238, flash)), all);
        DrawMap(ctx, book);
        DrawParty(ctx);
        DrawRipples(ctx);
        DrawFrameRate(ctx);
    }

    static bool OnScreen(RectD r, double w, double h, double margin) =>
        r.Right + margin >= 0 && r.X - margin <= w && r.Bottom + margin >= 0 && r.Y - margin <= h;

    // ---------------------------------------------------------------- pages

    /// <summary>One page: its picture, sharp tiles if it has them, the pictures on top and the weather.</summary>
    void DrawPage(DrawingContext ctx, LoadedBook book, int page, RectD r, ViewTransform? tilesView, double w, double h,
        double scaling, RenderOptions quality, bool weather)
    {
        Rect dest = ToRect(r);
        if (!book.PageIsOpaque(page)) ctx.FillRectangle(White, dest);

        // The smallest copy of the picture that is big enough. Shrinking from it is at most by half, which
        // the quick filter does cleanly; only enlarging (the camera in closer than the picture is sharp)
        // gains from the finer one.
        double pixelsWide = r.W * scaling;
        Bitmap picture = book.PagePicture(page, pixelsWide);
        bool enlarging = pixelsWide > picture.PixelSize.Width * 1.02;
        using (ctx.PushRenderOptions(enlarging ? quality : Quick))
            ctx.DrawImage(picture, dest);

        if (tilesView is ViewTransform v) DrawTiles(ctx, book, page, v, scaling, quality);
        DrawLayers(ctx, book, page, r, quality);
        if (weather) DrawWeather(ctx, book, page, r);
    }

    /// <summary>Sharp tiles over the overview picture, only for the part of the page on screen.</summary>
    void DrawTiles(DrawingContext ctx, LoadedBook book, int page, ViewTransform v, double scaling, RenderOptions quality)
    {
        TileInfo? info = book.Book.Pages[page].Tiles;
        if (info is null) return;
        using var options = ctx.PushRenderOptions(quality);
        foreach (TileDraw t in Engine.TilesNeeded(page, v, scaling))
        {
            // A whisker bigger than its place, so no hairline shows between neighbours.
            Rect dest = ToRect(v.ToScreen(t.World)).Inflate(0.4);
            Bitmap? tile = _tiles.Get(BookLibrary.SafePath(book.Folder, info.PathFor(t.Level, t.X, t.Y)));
            if (tile is not null) { ctx.DrawImage(tile, dest); continue; }
            if (!t.HasParent) continue;
            // Not here yet: use the matching quarter of the next level down if we have it.
            Bitmap? parent = _tiles.Get(BookLibrary.SafePath(book.Folder, info.PathFor(t.Level + 1, t.ParentX, t.ParentY)));
            if (parent is not null) ctx.DrawImage(parent, ToRect(t.ParentSource), dest);
        }
    }

    /// <summary>Pictures placed on top of the page: shown between stops, moved when their stop arrives.</summary>
    void DrawLayers(DrawingContext ctx, LoadedBook book, int page, RectD r, RenderOptions quality)
    {
        if (book.Book.Pages[page].Layers.Count == 0) return;
        using var options = ctx.PushRenderOptions(quality);
        foreach (LayerDraw l in Engine.LayersFor(page))
        {
            Bitmap? image = _pictures.Get(BookLibrary.SafePath(book.Folder, l.Image));
            if (image is null) continue;
            var dest = new Rect(r.X + l.Box.X * r.W, r.Y + l.Box.Y * r.H, l.Box.W * r.W, l.Box.H * r.H);
            using (ctx.PushOpacity(l.Opacity))
            {
                if (l.Rotation == 0) { ctx.DrawImage(image, dest); continue; }
                // A sway turns about the middle of the bottom edge, like a flower on its stem.
                var foot = new Point(dest.X + dest.Width / 2, dest.Bottom);
                using (ctx.PushTransform(Matrix.CreateTranslation(-foot.X, -foot.Y) * Matrix.CreateRotation(l.Rotation) * Matrix.CreateTranslation(foot.X, foot.Y)))
                    ctx.DrawImage(image, dest);
            }
        }
    }

    static readonly Geometry UnitLeaf = BuildLeaf();
    static readonly Geometry UnitGlint = BuildStar(0.28);
    static readonly Geometry UnitStar = BuildStar(0.4);
    static readonly Color[] LeafTints = Weather.LeafTints.Select(Color.Parse).ToArray();
    static readonly Color[] PartyTints = PartyDraw.Tints.Select(Color.Parse).ToArray();

    /// <summary>
    /// Rain, snow, leaves and sparkles, clipped to the page and kept out of its "dry" boxes. It is all
    /// drawn in page units (the page one unit tall) under one transform, so the shape that clips it never
    /// has to be rebuilt as the camera moves.
    /// </summary>
    void DrawWeather(DrawingContext ctx, LoadedBook book, int page, RectD r)
    {
        List<PageEffect> effects = book.Book.Pages[page].Effects;
        if (effects.Count == 0 || r.H <= 0) return;
        ReaderEngine e = Engine;
        double t = e.Now / 1000, aspect = r.W / r.H;
        double pixel = 1 / r.H;                                    // one screen unit, in page units
        using var toPage = ctx.PushTransform(Matrix.CreateScale(r.H, r.H) * Matrix.CreateTranslation(r.X, r.Y));

        for (int k = 0; k < effects.Count; k++)
        {
            PageEffect fx = effects[k];
            double level = e.EffectLevel(page, k);
            if (level < 0.01 || !fx.IsFalling) continue;
            double amount = fx.AmountOrDefault;

            if (!_weatherClips.TryGetValue((page, k), out Geometry? clip))
            {
                var shape = new StreamGeometry();
                using (StreamGeometryContext g = shape.Open())
                {
                    g.SetFillRule(FillRule.EvenOdd);
                    AddRect(g, new Rect(0, 0, aspect, 1));
                    foreach (RectD b in fx.Dry) AddRect(g, new Rect(b.X * aspect, b.Y, b.W * aspect, b.H));
                }
                _weatherClips[(page, k)] = clip = shape;
            }
            using var clipped = ctx.PushGeometryClip(clip);

            switch (fx.Type)
            {
                case PageEffect.Rain:
                    foreach (RainSheet sheet in Weather.Rain(t, aspect, amount, level))
                    {
                        var lines = new StreamGeometry();
                        using (StreamGeometryContext g = lines.Open())
                        {
                            foreach (Streak s in sheet.Streaks)
                            {
                                g.BeginFigure(new Point(s.X1, s.Y1), false);
                                g.LineTo(new Point(s.X2, s.Y2), true);
                                g.EndFigure(false);
                            }
                        }
                        ctx.DrawGeometry(null, Pen(Rgba(236, 244, 255, sheet.Alpha), Math.Max(Weather.MinLinePixels * pixel, sheet.Width), round: true), lines);
                    }
                    if (fx.Ground is double ground and > 0)
                    {
                        double width = Math.Max(Weather.MinLinePixels * pixel, Weather.RingWidth);
                        foreach (SplashRing ring in Weather.Splashes(t, aspect, amount, ground, level))
                            ctx.DrawEllipse(null, Pen(Rgba(240, 247, 255, ring.Alpha), width), new Point(ring.X, ring.Y), ring.RadiusX, ring.RadiusY);
                    }
                    break;

                case PageEffect.Snow:
                    foreach (Snowflake flake in Weather.Snow(t, aspect, amount, level))
                    {
                        double radius = Math.Max(Weather.MinLinePixels * pixel, flake.Radius);
                        ctx.DrawEllipse(Brush(Rgba(255, 255, 255, flake.Alpha)), null, new Point(flake.X, flake.Y), radius, radius);
                    }
                    break;

                case PageEffect.Leaves:
                    foreach (Leaf leaf in Weather.Leaves(t, aspect, amount, level))
                    {
                        Matrix m = Matrix.CreateScale(leaf.Size, leaf.Size * leaf.Squash) * Matrix.CreateRotation(leaf.Rotation) * Matrix.CreateTranslation(leaf.X, leaf.Y);
                        using (ctx.PushTransform(m))
                            ctx.DrawGeometry(Brush(WithAlpha(LeafTints[leaf.Tint], leaf.Alpha)), null, UnitLeaf);
                    }
                    break;

                case PageEffect.Sparkles:
                    foreach (Glint star in Weather.Sparkles(t, aspect, amount, level))
                    {
                        if (star.Alpha < 0.004) continue;
                        using (ctx.PushTransform(Matrix.CreateScale(star.Size, star.Size) * Matrix.CreateTranslation(star.X, star.Y)))
                            ctx.DrawGeometry(Brush(Rgba(255, 251, 224, star.Alpha)), null, UnitGlint);
                    }
                    break;
            }
        }
    }

    // ---------------------------------------------------------------- the book look

    /// <summary>
    /// The parts of a book drawn around one page: a cover board showing as a border, a spine on the left,
    /// the edges of the pages underneath, a shadow, and the shading where the page dips into the gutter.
    /// Everything is in page units (the page one unit tall, its top-left corner at 0, 0) and is made once
    /// for a page shape; drawing it is a matter of setting the transform.
    /// </summary>
    sealed class BookFrame
    {
        /// <summary>How far the cover board shows beyond the page at the top, right and bottom.</summary>
        public const double Board = 0.017;
        /// <summary>The width of the spine, outside the page on the left.</summary>
        public const double Spine = 0.024;
        /// <summary>How far each page edge underneath sticks out beyond the one above it.</summary>
        const double Edge = 0.0038;
        /// <summary>How far the gutter shading reaches in from the page's left edge.</summary>
        const double Gutter = 0.055;

        readonly RoundedRect _board;
        readonly IBrush _boardBrush, _spineBrush, _gutterBrush;
        readonly Geometry _spine;
        readonly (Rect Rect, IBrush Brush)[] _edges;
        readonly IPen _crease, _rim;
        readonly Rect _gutter, _shadowArea;
        Bitmap? _shadowPicture;

        public BookFrame(double aspect, Color board)
        {
            var outer = new Rect(-Spine, -Board, aspect + Spine + Board, 1 + 2 * Board);
            _board = new RoundedRect(outer, new CornerRadius(0.007, 0.016, 0.016, 0.007));
            _boardBrush = new ImmutableSolidColorBrush(board);
            _shadowArea = outer.Inflate(0.14);
            _rim = new ImmutablePen(new ImmutableSolidColorBrush(Shade(board, 1.45, 40)), 0.0016);

            // The spine: darker than the board, a little lighter along its middle as if rounded.
            var spine = new StreamGeometry();
            using (StreamGeometryContext g = spine.Open())
            {
                const double k = 0.007;
                g.BeginFigure(new Point(0, -Board), true);
                g.LineTo(new Point(0, 1 + Board), true);
                g.LineTo(new Point(-Spine + k, 1 + Board), true);
                g.ArcTo(new Point(-Spine, 1 + Board - k), new Size(k, k), 0, false, SweepDirection.Clockwise, true);
                g.LineTo(new Point(-Spine, -Board + k), true);
                g.ArcTo(new Point(-Spine + k, -Board), new Size(k, k), 0, false, SweepDirection.Clockwise, true);
                g.EndFigure(true);
            }
            _spine = spine;
            _spineBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(-Spine, 0, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
                GradientStops =
                {
                    new GradientStop(Shade(board, 0.5), 0), new GradientStop(Shade(board, 0.82), 0.4),
                    new GradientStop(Shade(board, 0.62), 0.8), new GradientStop(Shade(board, 0.36), 1),
                },
            }.ToImmutable();

            // The pages underneath: three paper edges showing past the right and the bottom of the top page.
            Color[] paper = { Color.Parse("#cbc3b2"), Color.Parse("#efe9dc"), Color.Parse("#d5cdbd") };
            _edges = new (Rect, IBrush)[3];
            for (int i = 0; i < 3; i++)
            {
                double out_ = (3 - i) * Edge;
                _edges[i] = (new Rect(0, 0, aspect + out_, 1 + out_), new ImmutableSolidColorBrush(paper[i]));
            }

            // Where the page dips into the gutter: dark right at the spine, gone a little way in.
            _gutter = new Rect(0, 0, Gutter, 1);
            _gutterBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(Gutter, 0, RelativeUnit.Absolute),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(92, 0, 0, 0), 0), new GradientStop(Color.FromArgb(40, 0, 0, 0), 0.3),
                    new GradientStop(Color.FromArgb(12, 0, 0, 0), 0.65), new GradientStop(Color.FromArgb(0, 0, 0, 0), 1),
                },
            }.ToImmutable();
            _crease = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(110, 0, 0, 0)), 0.0018);
        }

        /// <summary>A colour made darker or lighter, with an optional lift so that near-black still shows a rim.</summary>
        static Color Shade(Color c, double by, int lift = 0) => Color.FromRgb(
            (byte)Math.Clamp(c.R * by + lift, 0, 255), (byte)Math.Clamp(c.G * by + lift, 0, 255), (byte)Math.Clamp(c.B * by + lift, 0, 255));

        /// <summary>The soft shadow under the whole book, as a picture a couple of hundred pixels to the unit.</summary>
        Bitmap MakeShadow()
        {
            const double pixelsPerUnit = 220;
            var size = new PixelSize((int)Math.Ceiling(_shadowArea.Width * pixelsPerUnit), (int)Math.Ceiling(_shadowArea.Height * pixelsPerUnit));
            var picture = new RenderTargetBitmap(size);
            // Worked out in the picture's own pixels, with no transform: a blur's width does not follow a
            // transform the way a shape does.
            double k = pixelsPerUnit;
            Rect board = _board.Rect;
            var inPixels = new RoundedRect(new Rect((board.X - _shadowArea.X) * k, (board.Y - _shadowArea.Y) * k, board.Width * k, board.Height * k), 0.012 * k);
            var shadow = new BoxShadows(new BoxShadow { OffsetX = 0.004 * k, OffsetY = 0.016 * k, Blur = 0.07 * k, Color = Color.FromArgb(170, 0, 0, 0) });
            using (DrawingContext ctx = picture.CreateDrawingContext())
                ctx.DrawRectangle(null, null, inPixels, shadow);
            return picture;
        }

        /// <summary>What lies under the page: shadow, cover board, spine, and the edges of the pages beneath.</summary>
        public void DrawUnder(DrawingContext ctx)
        {
            // The shadow is blurred once into a small picture and stretched from then on. Blurring it afresh
            // every frame cost more than drawing the page itself on a computer with no graphics card.
            _shadowPicture ??= MakeShadow();
            // Only the part that shows around the board is drawn; the board hides the rest.
            using (ctx.PushRenderOptions(Quick)) DrawAround(ctx, _shadowPicture, _shadowArea, _board.Rect.Deflate(0.006));
            ctx.DrawRectangle(_boardBrush, _rim, _board);
            ctx.DrawGeometry(_spineBrush, null, _spine);
            foreach (var (rect, brush) in _edges) ctx.FillRectangle(brush, rect);
        }

        /// <summary>What lies over the page: the shading at the gutter, and nothing anywhere near the middle.</summary>
        public void DrawOver(DrawingContext ctx)
        {
            ctx.FillRectangle(_gutterBrush, _gutter);
            ctx.DrawLine(_crease, new Point(0, 0), new Point(0, 1));
        }
    }

    BookFrame FrameFor(LoadedBook book, int page)
    {
        // Pages of the same shape share one frame.
        int shape = (int)Math.Round(book.Aspects[page] * 1000);
        if (!_bookFrames.TryGetValue(shape, out BookFrame? frame))
            _bookFrames[shape] = frame = new BookFrame(shape / 1000.0, book.Board);
        return frame;
    }

    static Matrix PageSpace(RectD r) => Matrix.CreateScale(r.H, r.H) * Matrix.CreateTranslation(r.X, r.Y);

    /// <summary>A page drawn as the top page of a book lying open on the table.</summary>
    void DrawBookPage(DrawingContext ctx, LoadedBook book, int page, RectD r, double w, double h, double scaling, RenderOptions quality, bool weather)
    {
        BookFrame frame = FrameFor(book, page);
        Matrix space = PageSpace(r);
        using (ctx.PushTransform(space)) frame.DrawUnder(ctx);
        DrawPage(ctx, book, page, r, null, w, h, scaling, quality, weather);
        using (ctx.PushTransform(space)) frame.DrawOver(ctx);
    }

    // ---------------------------------------------------------------- the page flip

    static readonly IImmutableBrush Paper = new ImmutableSolidColorBrush(Color.Parse("#f3eee3"));
    static readonly Rect UnitSquare = new(0, 0, 1, 1);

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
    /// The shading on the back of the turning leaf, in a unit square: x = 0 is the leaf's free edge, x = 1
    /// the fold. A little shade out at the free edge, plain paper, then a band of light where the paper
    /// curves up towards us, and dark right in the fold.
    /// </summary>
    static readonly IBrush LeafShade = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Absolute),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(50, 40, 30, 20), 0), new GradientStop(Color.FromArgb(0, 40, 30, 20), 0.4),
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.5), new GradientStop(Color.FromArgb(70, 255, 255, 255), 0.8),
            new GradientStop(Color.FromArgb(40, 40, 30, 20), 0.93), new GradientStop(Color.FromArgb(125, 40, 30, 20), 1),
        },
    }.ToImmutable();

    static readonly IPen LeafEdge = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(70, 60, 45, 30)), 1);

    /// <summary>
    /// A page turning over. The page underneath lies whole. The turning page is cut at the fold line: what
    /// is left of the fold has not lifted yet and is drawn as it was; what was right of it has folded over
    /// and shows its back, mirrored about the fold, as far as the spine. Shadows do the rest: one from
    /// the leaf on to the page it is passing over, and one in the crease on the page just uncovered.
    /// </summary>
    void DrawFlip(DrawingContext ctx, LoadedBook book, FlipDraw flip, double w, double h, double scaling)
    {
        ReaderEngine e = Engine;
        RectD under = e.ViewFor(flip.UnderCam).ToScreen(e.PageRects[flip.UnderPage]);
        RectD page = e.ViewFor(flip.TurningCam).ToScreen(e.PageRects[flip.TurningPage]);
        DrawBookPage(ctx, book, flip.UnderPage, under, w, h, scaling, Quick, weather: true);

        double fold = Math.Clamp(flip.Fold, 0, 1);
        if (fold >= 0.9995) return;                               // the leaf has gone over the spine
        double foldX = page.Right - page.W * fold;                // the fold line travels from the right edge to the spine
        double lift = Math.Sin(Math.PI * fold);                   // 0 lying flat at either end, 1 standing up half way

        // The crease's shadow on the page just uncovered, right of the fold.
        if (fold > 0.001)
        {
            double reach = Math.Min(page.W * (0.05 + 0.06 * lift), page.Right - foldX);
            if (reach > 0.5)
            {
                using (ctx.PushOpacity(0.2 + 0.25 * lift))
                using (ctx.PushTransform(Matrix.CreateScale(reach, page.H) * Matrix.CreateTranslation(foldX, page.Y)))
                    ctx.FillRectangle(FallOff, UnitSquare);
            }
        }

        // The part of the turning page that has not lifted yet.
        if (foldX > page.X + 0.5)
        {
            using (ctx.PushClip(new Rect(page.X, page.Y, foldX - page.X, page.H)))
            {
                DrawPage(ctx, book, flip.TurningPage, page, null, w, h, scaling, Quick, weather: false);
                using (ctx.PushTransform(PageSpace(page))) FrameFor(book, flip.TurningPage).DrawOver(ctx);
            }
        }

        // The leaf: the mirror image, about the fold, of the part that was to its right.
        double leafWidth = page.Right - foldX;                    // how wide the folded-over part is
        double freeEdge = foldX - leafWidth;                      // where its far edge lies now
        double left = Math.Max(page.X, freeEdge);                 // the spine cuts it off
        if (foldX - left < 0.5) return;

        // The shadow it throws on the page it is passing over, to the left of its free edge.
        if (freeEdge > page.X + 1)
        {
            double reach = Math.Min(page.W * 0.12 * (0.3 + 0.7 * lift), freeEdge - page.X);
            using (ctx.PushOpacity(0.5 * (0.35 + 0.65 * lift)))
            using (ctx.PushTransform(Matrix.CreateScale(reach, page.H) * Matrix.CreateTranslation(freeEdge - reach, page.Y)))
                ctx.FillRectangle(FallOffBack, UnitSquare);
        }

        // A lifted page bows: its top and bottom edges curve out a little, most in the middle of the leaf and
        // most half way through the turn. That bow is what makes it read as paper rather than a sliding panel.
        double bow = page.H * 0.026 * lift;
        var leaf = new StreamGeometry();
        using (StreamGeometryContext g = leaf.Open())
        {
            const int pieces = 10;
            // u runs from 0 at the leaf's free edge to 1 at the fold. The free edge is the part lifted
            // nearest to us, so it looks tallest; the paper also bellies a little in between.
            double Bow(double x) { double u = (x - freeEdge) / Math.Max(1e-6, leafWidth); return bow * (0.6 * (1 - u * u) + 0.4 * 4 * u * (1 - u)); }
            g.BeginFigure(new Point(foldX, page.Y), true);
            for (int i = 1; i <= pieces; i++)
            {
                double x = foldX + (left - foldX) * i / pieces;
                g.LineTo(new Point(x, page.Y - Bow(x)), true);
            }
            for (int i = pieces; i >= 0; i--)
            {
                double x = foldX + (left - foldX) * i / pieces;
                g.LineTo(new Point(x, page.Bottom + Bow(x)), true);
            }
            g.EndFigure(true);
        }
        ctx.DrawGeometry(Paper, LeafEdge, leaf);
        using (ctx.PushGeometryClip(leaf))
        using (ctx.PushTransform(Matrix.CreateScale(leafWidth, page.H + 2 * bow) * Matrix.CreateTranslation(freeEdge, page.Y - bow)))
            ctx.FillRectangle(LeafShade, UnitSquare);
    }

    // ---------------------------------------------------------------- over the pages

    /// <summary>Sparkle bursts on found things, and the grey "not yet" ring that shakes its head.</summary>
    void DrawBursts(DrawingContext ctx, ViewTransform v)
    {
        foreach (BurstDraw b in Engine.Bursts())
        {
            RectD r = v.ToScreen(b.Box);
            double cx = r.CenterX, cy = r.CenterY, t = b.T;
            double reach = Math.Max(r.W, r.H) / 2;
            using var fading = ctx.PushOpacity(1 - t);
            if (b.Locked)
            {
                ctx.DrawEllipse(null, LockedPen, new Point(cx + Math.Sin(t * 28) * 10 * (1 - t), cy), r.W / 2 + 10, r.H / 2 + 10);
                continue;
            }
            ctx.DrawEllipse(null, Pen(Gold, 6 * (1 - t) + 1), new Point(cx, cy),
                r.W / 2 + reach * 0.5 * t + 6, r.H / 2 + reach * 0.5 * t + 6);
            for (int k = 0; k < 10; k++)
            {
                double a = k / 10.0 * Math.PI * 2 + t;
                double d = reach * (0.9 + 0.9 * Easings.OutCubic(t));
                Star(ctx, cx + Math.Cos(a) * d, cy + Math.Sin(a) * d, 9 * (1 - t) + 3, k % 2 == 1 ? PaleGold : DeepGold);
            }
        }
    }

    static readonly Color Gold = Color.Parse("#ffd23f");
    static readonly IImmutableBrush PaleGold = new ImmutableSolidColorBrush(Color.Parse("#fff6c2"));
    static readonly IImmutableBrush DeepGold = new ImmutableSolidColorBrush(Color.Parse("#ffb703"));
    static readonly IImmutableBrush ArrowInk = new ImmutableSolidColorBrush(Color.Parse("#3a2a00"));
    static readonly ImmutablePen LockedPen = new(new ImmutableSolidColorBrush(Color.Parse("#e9eef5")), 6, new ImmutableDashStyle(new[] { 14 / 6.0, 10 / 6.0 }, 0));
    static readonly Geometry ArrowHead = BuildArrowHead();
    static readonly (double Extra, double Alpha)[] Halo = { (30.0, 0.07), (18.0, 0.12), (8.0, 0.22) };

    /// <summary>The story beacon, and after an idle spell a glow or an edge arrow pointing at something not found yet.</summary>
    void DrawHint(DrawingContext ctx, ViewTransform v)
    {
        HintDraw? hint = Engine.GetHint(v);
        if (hint is null) return;
        double pulse = hint.Pulse;
        if (hint.Beacon)
        {
            RectD b = hint.BeaconBox;
            var centre = new Point(b.CenterX, b.CenterY);
            double grow = hint.BeaconGrow;
            ctx.DrawEllipse(null, Pen(Rgba(255, 255, 255, 0.75 * (1 - grow)), 5), centre, b.W / 2 + 6 + grow * 40, b.H / 2 + 6 + grow * 40);
            ctx.DrawEllipse(null, Pen(Rgba(255, 236, 120, 0.55 + 0.35 * pulse), 4), centre, b.W / 2 + 6, b.H / 2 + 6);
        }
        if (hint.Kind == HintKind.Glow)
        {
            RectD r = hint.GlowBox;
            var centre = new Point(r.CenterX, r.CenterY);
            double rx = r.W / 2 + 10 + 6 * pulse, ry = r.H / 2 + 10 + 6 * pulse, width = 4 + 4 * pulse;
            // A soft halo: a few wide, faint rings under the bright one.
            foreach (var (extra, alpha) in Halo)
                ctx.DrawEllipse(null, Pen(Rgba(255, 210, 63, alpha * (0.5 + 0.5 * pulse)), width + extra), centre, rx, ry);
            ctx.DrawEllipse(null, Pen(Rgba(255, 210, 63, 0.5 + 0.5 * pulse), width), centre, rx, ry);
        }
        else if (hint.Kind == HintKind.Arrow)
        {
            var at = new Point(hint.ArrowX, hint.ArrowY);
            double radius = 28 + 3 * pulse;
            ctx.DrawEllipse(Brush(Rgba(0, 0, 0, 0.12)), null, at, radius + 6, radius + 6);
            ctx.DrawEllipse(Brush(Rgba(0, 0, 0, 0.16)), null, at, radius + 3, radius + 3);
            ctx.DrawEllipse(Brush(Rgba(255, 210, 63, 0.75 + 0.25 * pulse)), null, at, radius, radius);
            using (ctx.PushTransform(Matrix.CreateRotation(hint.ArrowAngle) * Matrix.CreateTranslation(at.X, at.Y)))
                ctx.DrawGeometry(ArrowInk, null, ArrowHead);
        }
    }

    static readonly BoxShadows MapShadow = new(new BoxShadow { Blur = 10, Color = Color.FromArgb(115, 0, 0, 0) });
    static readonly IImmutableBrush MapShade = new ImmutableSolidColorBrush(Color.FromArgb(89, 0, 0, 0));
    static readonly ImmutablePen MapFrame = new(White, 2.5);

    /// <summary>Small map of the whole page with a frame showing where the child is looking.</summary>
    void DrawMap(DrawingContext ctx, LoadedBook book)
    {
        MapDraw? map = Engine.GetMap();
        if (map is null) return;
        Rect m = ToRect(map.Rect), view = ToRect(map.ViewRect);
        ctx.DrawRectangle(White, null, m.Inflate(3), 0, 0, MapShadow);
        using (ctx.PushRenderOptions(Quick))
            ctx.DrawImage(book.SmallPicture(map.Page), m);
        // Everything outside the frame is shaded: four plain bands around it.
        ctx.FillRectangle(MapShade, new Rect(m.X, m.Y, m.Width, Math.Max(0, view.Y - m.Y)));
        ctx.FillRectangle(MapShade, new Rect(m.X, view.Bottom, m.Width, Math.Max(0, m.Bottom - view.Bottom)));
        ctx.FillRectangle(MapShade, new Rect(m.X, view.Y, Math.Max(0, view.X - m.X), view.Height));
        ctx.FillRectangle(MapShade, new Rect(view.Right, view.Y, Math.Max(0, m.Right - view.Right), view.Height));
        ctx.DrawRectangle(null, MapFrame, view);
        if (map.HasDot) ctx.DrawEllipse(Brush(Rgba(255, 236, 120, map.DotAlpha)), null, new Point(map.DotX, map.DotY), map.DotRadius, map.DotRadius);
    }

    /// <summary>Stars raining down when the story is finished.</summary>
    void DrawParty(DrawingContext ctx)
    {
        PartyDraw? party = Engine.GetParty();
        if (party is null) return;
        using var fading = ctx.PushOpacity(Math.Clamp(party.Alpha, 0, 1));
        foreach (PartyStar s in party.Stars) Star(ctx, s.X, s.Y, s.Radius, Brush(PartyTints[s.Tint]));
    }

    /// <summary>The ring that answers every touch.</summary>
    void DrawRipples(DrawingContext ctx)
    {
        foreach (RippleDraw r in Engine.Ripples())
        {
            var at = new Point(r.X, r.Y);
            double radius = 10 + Easings.OutCubic(r.T) * 34, width = 5 * (1 - r.T) + 1.5, alpha = (1 - r.T) * 0.85;
            // A faint dark edge so the white ring shows on a pale picture too.
            ctx.DrawEllipse(null, Pen(Rgba(0, 0, 0, 0.16 * alpha), width + 5), at, radius, radius);
            ctx.DrawEllipse(null, Pen(Rgba(255, 255, 255, alpha), width), at, radius, radius);
        }
    }

    static readonly IImmutableBrush ReadoutBack = new ImmutableSolidColorBrush(Color.FromArgb(190, 0, 0, 0));
    FormattedText? _readoutText;
    string _readoutShown = "";

    /// <summary>The frame-rate readout, top left under the back button. The words are only laid out when they change.</summary>
    void DrawFrameRate(DrawingContext ctx)
    {
        if (!_showFrameRate || _readout.Length == 0) return;
        if (_readoutText is null || _readoutShown != _readout)
        {
            _readoutShown = _readout;
            _readoutText = new FormattedText(_readout, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 15, White);
        }
        var box = new Rect(12, 72, _readoutText.Width + 20, _readoutText.Height + 10);
        ctx.DrawRectangle(ReadoutBack, null, box, 8, 8);
        ctx.DrawText(_readoutText, new Point(box.X + 10, box.Y + 5));
    }

    // ---------------------------------------------------------------- small drawing helpers

    static Rect ToRect(RectD r) => new(r.X, r.Y, Math.Max(0, r.W), Math.Max(0, r.H));

    static Color Rgba(byte r, byte g, byte b, double alpha) => Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), r, g, b);

    static Color WithAlpha(Color c, double alpha) => Rgba(c.R, c.G, c.B, alpha * c.A / 255.0);

    /// <summary>Brushes are asked for thousands of times a second with only a few hundred different colours, so they are kept.</summary>
    IImmutableBrush Brush(Color c)
    {
        uint key = c.ToUInt32();
        if (!_brushes.TryGetValue(key, out IImmutableBrush? brush))
        {
            if (_brushes.Count > 2000) _brushes.Clear();
            _brushes[key] = brush = new ImmutableSolidColorBrush(c);
        }
        return brush;
    }

    /// <summary>
    /// Pens are kept too. Widths are rounded to a five-hundredth of a unit, far finer than the eye can
    /// tell, so that a width that changes smoothly still lands on pens made before. Very thin pens (those
    /// measured in page units) are not rounded and not kept.
    /// </summary>
    ImmutablePen Pen(Color c, double thickness, bool round = false)
    {
        if (thickness < 0.2) return new ImmutablePen(Brush(c), thickness, null, round ? PenLineCap.Round : PenLineCap.Flat);
        var key = (c.ToUInt32(), (int)Math.Round(thickness * 500), round);
        if (!_pens.TryGetValue(key, out ImmutablePen? pen))
        {
            if (_pens.Count > 4000) _pens.Clear();
            _pens[key] = pen = new ImmutablePen(Brush(c), key.Item2 / 500.0, null, round ? PenLineCap.Round : PenLineCap.Flat);
        }
        return pen;
    }

    // The soft oval: a small picture of a round hole with a soft rim, made once and stretched to fit.

    sealed class SoftOval
    {
        public required Bitmap Picture;
        public required Color Colour;
        /// <summary>How far out from the middle the picture is completely clear, as a share of its radius.</summary>
        public required double ClearTo;
    }

    /// <summary>
    /// Draw a picture stretched over <paramref name="dest"/>, leaving out the part over <paramref name="hole"/>:
    /// four bands around it. For pictures that are see-through or hidden in the middle, where painting
    /// the middle would be work for nothing.
    /// </summary>
    static void DrawAround(DrawingContext ctx, Bitmap picture, Rect dest, Rect hole)
    {
        hole = hole.Intersect(dest);
        if (hole.Width <= 0 || hole.Height <= 0 || dest.Width <= 0 || dest.Height <= 0) { ctx.DrawImage(picture, dest); return; }
        double sx = picture.PixelSize.Width / dest.Width, sy = picture.PixelSize.Height / dest.Height;
        void Band(double x, double y, double width, double height)
        {
            if (width <= 0 || height <= 0) return;
            ctx.DrawImage(picture, new Rect((x - dest.X) * sx, (y - dest.Y) * sy, width * sx, height * sy), new Rect(x, y, width, height));
        }
        Band(dest.X, dest.Y, dest.Width, hole.Y - dest.Y);
        Band(dest.X, hole.Bottom, dest.Width, dest.Bottom - hole.Bottom);
        Band(dest.X, hole.Y, hole.X - dest.X, hole.Height);
        Band(hole.Right, hole.Y, dest.Right - hole.Right, hole.Height);
    }

    static SoftOval? _softEdge, _spot;
    static SoftOval SoftEdge => _softEdge ??= MakeOval(1.25 / 2.6, Color.FromRgb(0, 0, 0));
    static SoftOval Spot => _spot ??= MakeOval(0.8, Color.FromRgb(10, 9, 16));

    /// <summary>
    /// A square picture that is clear inside a circle and solid outside it. <paramref name="clearTo"/> is
    /// how far out from the middle the clear part reaches before the rim starts (0 to 1).
    /// </summary>
    static SoftOval MakeOval(double clearTo, Color colour)
    {
        const int size = 256;
        var picture = new RenderTargetBitmap(new PixelSize(size, size));
        using (DrawingContext ctx = picture.CreateDrawingContext())
        {
            // The rim ends one pixel inside the edge, so the outermost pixels are fully solid and the
            // picture joins the plain colour painted around it without a seam.
            double radius = size / 2.0 - 1;
            var clear = Color.FromArgb(0, colour.R, colour.G, colour.B);
            var brush = new RadialGradientBrush
            {
                Center = new RelativePoint(size / 2.0, size / 2.0, RelativeUnit.Absolute),
                GradientOrigin = new RelativePoint(size / 2.0, size / 2.0, RelativeUnit.Absolute),
                RadiusX = new RelativeScalar(radius, RelativeUnit.Absolute),
                RadiusY = new RelativeScalar(radius, RelativeUnit.Absolute),
                GradientStops = { new GradientStop(clear, 0), new GradientStop(clear, clearTo), new GradientStop(colour, 1) },
            };
            ctx.FillRectangle(brush, new Rect(0, 0, size, size));
        }
        return new SoftOval { Picture = picture, Colour = colour, ClearTo = clearTo };
    }

    /// <summary>
    /// Cover the screen with a colour that is clear inside an oval and solid from its rim outwards.
    ///
    /// The oval is a picture of a circle stretched to the oval's shape, not a radial brush given two
    /// different radii: that came out nearly round here, which made the spotlight a circle instead of an
    /// oval that fits its box. Stretching a small ready-made picture is also far quicker than working the
    /// gradient out for every pixel of every frame.
    /// </summary>
    void FillOval(DrawingContext ctx, Rect all, double scaling, double cx, double cy, double radiusX, double radiusY, SoftOval oval, double strength)
    {
        // On whole pixels, so the picture and the plain colour around it meet exactly.
        double Snap(double v) => Math.Round(v * scaling) / scaling;
        double x0 = Snap(cx - radiusX), x1 = Snap(cx + radiusX), y0 = Snap(cy - radiusY), y1 = Snap(cy + radiusY);
        IImmutableBrush solid = Brush(oval.Colour);
        using (ctx.PushOpacity(Math.Clamp(strength, 0, 1)))
        {
            if (x1 > all.X && x0 < all.Right && y1 > all.Y && y0 < all.Bottom)
            {
                // The middle of the oval is clear, so only what lies around a box inside the clear part is drawn.
                double clearX = (x1 - x0) / 2 * oval.ClearTo * 0.68, clearY = (y1 - y0) / 2 * oval.ClearTo * 0.68;
                var clear = new Rect(Snap((x0 + x1) / 2 - clearX), Snap((y0 + y1) / 2 - clearY), 0, 0);
                clear = new Rect(clear.X, clear.Y, Snap((x0 + x1) / 2 + clearX) - clear.X, Snap((y0 + y1) / 2 + clearY) - clear.Y);
                using (ctx.PushRenderOptions(Quick))
                    DrawAround(ctx, oval.Picture, new Rect(x0, y0, x1 - x0, y1 - y0), clear);
            }
            double top = Math.Clamp(y0, all.Y, all.Bottom), bottom = Math.Clamp(y1, all.Y, all.Bottom);
            if (top > all.Y) ctx.FillRectangle(solid, new Rect(all.X, all.Y, all.Width, top - all.Y));
            if (bottom < all.Bottom) ctx.FillRectangle(solid, new Rect(all.X, bottom, all.Width, all.Bottom - bottom));
            if (bottom > top)
            {
                double left = Math.Clamp(x0, all.X, all.Right), right = Math.Clamp(x1, all.X, all.Right);
                if (left > all.X) ctx.FillRectangle(solid, new Rect(all.X, top, left - all.X, bottom - top));
                if (right < all.Right) ctx.FillRectangle(solid, new Rect(right, top, all.Right - right, bottom - top));
            }
        }
    }

    static void AddRect(StreamGeometryContext g, Rect r)
    {
        g.BeginFigure(r.TopLeft, true);
        g.LineTo(r.TopRight, true);
        g.LineTo(r.BottomRight, true);
        g.LineTo(r.BottomLeft, true);
        g.EndFigure(true);
    }

    static void AddRoundedRect(StreamGeometryContext g, Rect r, double radius)
    {
        double k = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
        if (k <= 0) { AddRect(g, r); return; }
        var corner = new Size(k, k);
        g.BeginFigure(new Point(r.X + k, r.Y), true);
        g.LineTo(new Point(r.Right - k, r.Y), true);
        g.ArcTo(new Point(r.Right, r.Y + k), corner, 0, false, SweepDirection.Clockwise, true);
        g.LineTo(new Point(r.Right, r.Bottom - k), true);
        g.ArcTo(new Point(r.Right - k, r.Bottom), corner, 0, false, SweepDirection.Clockwise, true);
        g.LineTo(new Point(r.X + k, r.Bottom), true);
        g.ArcTo(new Point(r.X, r.Bottom - k), corner, 0, false, SweepDirection.Clockwise, true);
        g.LineTo(new Point(r.X, r.Y + k), true);
        g.ArcTo(new Point(r.X + k, r.Y), corner, 0, false, SweepDirection.Clockwise, true);
        g.EndFigure(true);
    }

    /// <summary>A pointed oval one unit long each way from the middle.</summary>
    static Geometry BuildLeaf()
    {
        var leaf = new StreamGeometry();
        using (StreamGeometryContext g = leaf.Open())
        {
            g.BeginFigure(new Point(-1, 0), true);
            g.QuadraticBezierTo(new Point(0, -0.75), new Point(1, 0), true);
            g.QuadraticBezierTo(new Point(0, 0.75), new Point(-1, 0), true);
            g.EndFigure(true);
        }
        return leaf;
    }

    /// <summary>A four-pointed star of radius one. <paramref name="waist"/> is how far in the dips between the points come.</summary>
    static Geometry BuildStar(double waist)
    {
        var star = new StreamGeometry();
        using (StreamGeometryContext g = star.Open())
        {
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4, d = i % 2 == 1 ? waist : 1;
                var p = new Point(Math.Cos(angle) * d, Math.Sin(angle) * d);
                if (i == 0) g.BeginFigure(p, true); else g.LineTo(p, true);
            }
            g.EndFigure(true);
        }
        return star;
    }

    static Geometry BuildArrowHead()
    {
        var head = new StreamGeometry();
        using (StreamGeometryContext g = head.Open())
        {
            g.BeginFigure(new Point(16, 0), true);
            g.LineTo(new Point(-8, -13), true);
            g.LineTo(new Point(-8, 13), true);
            g.EndFigure(true);
        }
        return head;
    }

    static void Star(DrawingContext ctx, double x, double y, double radius, IBrush brush)
    {
        using (ctx.PushTransform(Matrix.CreateScale(radius, radius) * Matrix.CreateTranslation(x, y)))
            ctx.DrawGeometry(brush, null, UnitStar);
    }
}
