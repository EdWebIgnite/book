using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Reader.Ui;

namespace Reader.App;

/// <summary>
/// The bookcase on the shelf screen: a frame painted leaf green, a cream back of tongue-and-groove boards,
/// and light wooden shelves with the books standing face out on them. Shelves with room to spare get a
/// potted plant or a little pile of books lying down, and a garland of leaves runs along the top.
///
/// The case itself is drawn once in <see cref="Render"/> and only drawn again when its size changes. The
/// books are child controls (so they can lift on their own), and so is the garland, so that its leaves can
/// sway for a moment without anything else being drawn again.
/// </summary>
sealed class Bookcase : Panel
{
    /// <summary>The sizes of the case, for a computer or for a phone.</summary>
    sealed record Sizes(double Side, double Crown, double Base, double PadX, double Gap, double Headroom,
        double PlankTop, double PlankFront, double MaxCover, double MinCover, double Reserved, double GarlandRise);

    static readonly Sizes Desktop = new(Side: 22, Crown: 30, Base: 30, PadX: 36, Gap: 44, Headroom: 40,
        PlankTop: 16, PlankFront: 26, MaxCover: 330, MinCover: 140, Reserved: 200, GarlandRise: 22);
    static readonly Sizes Phone = new(Side: 12, Crown: 18, Base: 18, PadX: 16, Gap: 22, Headroom: 26,
        PlankTop: 10, PlankFront: 16, MaxCover: 190, MinCover: 96, Reserved: 130, GarlandRise: 14);

    readonly Sizes _s;
    readonly bool _touch;
    readonly List<ShelfView.BookCover> _books = new();
    readonly Garland _garland;
    readonly CaseArt _art;

    // The last layout: where each shelf and each book is, and what stands in the spare room.
    double _cover, _offsetX, _gridX, _width;
    int _columns, _shelves;
    readonly List<(double Slot, int Shelf, Decor Kind)> _decor = new();

    public Bookcase(bool touch)
    {
        _touch = touch;
        _s = touch ? Phone : Desktop;
        _art = new CaseArt(this);
        _garland = new Garland(touch ? 0.72 : 1) { IsHitTestVisible = false };
        Children.Add(_art);
        EmptySign = new ShelfView.Sign(touch) { IsVisible = false };
        Children.Add(_garland);
        Children.Add(EmptySign);
    }

    /// <summary>The height of the screen, so a row of books never grows taller than fits on it.</summary>
    public double ViewportHeight
    {
        get => _viewport;
        set { if (Math.Abs(_viewport - value) > 0.5) { _viewport = value; InvalidateMeasure(); } }
    }
    double _viewport = 800;

    /// <summary>The sign that stands on an empty shelf.</summary>
    public ShelfView.Sign EmptySign { get; }

    public bool Empty => _books.Count == 0;

    /// <summary>The pointer came onto one of the books.</summary>
    public event Action? CoverEntered;

    public void SetBooks(IReadOnlyList<ShelfView.BookCover> books)
    {
        foreach (var b in _books) Children.Remove(b);
        _books.Clear();
        foreach (var b in books)
        {
            b.Entered += () => CoverEntered?.Invoke();
            _books.Add(b);
            Children.Add(b);
        }
        EmptySign.IsVisible = _books.Count == 0;
        InvalidateMeasure();
        _art.InvalidateVisual();
    }

    /// <summary>The books drop gently into place one after another, and the leaves sway.</summary>
    public void SettleIn()
    {
        for (int i = 0; i < _books.Count; i++) _books[i].Settle(i);
        Rustle(5200);
    }

    /// <summary>The leaves along the top sway for this long, dying away to still.</summary>
    public void Rustle(double ms) => _garland.Sway(ms);

    /// <summary>A book on the shelf, for screenshots.</summary>
    public ShelfView.BookCover? Book(int index) => index >= 0 && index < _books.Count ? _books[index] : null;

    // ---------------------------------------------------------------- layout

    /// <summary>The low shelf under a single shelf of books is this much as tall as a book.</summary>
    const double LowShelf = 0.5;

    double Top => _s.GarlandRise + _s.Crown;
    double Plank => _s.PlankTop / 2 + _s.PlankFront;
    double CaseHeight => Top + _heights.Sum() + _s.Base;

    void Plan(double width)
    {
        _width = width;
        double inner = Math.Max(100, width - 2 * (_s.Side + _s.PadX));
        int n = Math.Max(1, _books.Count);
        // As many books to a shelf as fit at no more than the biggest cover size, all the same size...
        int k = Math.Max(1, (int)Math.Ceiling((inner + _s.Gap) / (_s.MaxCover + _s.Gap)));
        double cover = (inner - _s.Gap * (k - 1)) / k;
        while (cover < _s.MinCover && k > 1) { k--; cover = (inner - _s.Gap * (k - 1)) / k; }
        int rows = (int)Math.Ceiling(n / (double)k);
        // ...but not so tall that the case doesn't fit on the screen: all of it when the books fill one
        // shelf (with a low shelf below), or the first shelf with a peek of the next when there are more.
        bool low = rows == 1;
        double room = _viewport - _s.Reserved - Top - _s.Base - (low ? 2 : 1) * (_s.Headroom + Plank) - (low ? 0 : _s.Headroom * 2);
        double tallest = Math.Max(_s.MinCover, room / (low ? 1 + LowShelf : 1) / 1.5);
        if (cover > tallest)
        {
            cover = tallest;
            k = Math.Max(1, (int)Math.Floor((inner + _s.Gap) / (cover + _s.Gap)));
            rows = (int)Math.Ceiling(n / (double)k);
        }
        _cover = Math.Floor(cover);
        _columns = k;
        _gridX = _s.Side + _s.PadX + (inner - (k * _cover + (k - 1) * _s.Gap)) / 2;
        _offsetX = _gridX;

        _heights.Clear();
        for (int r = 0; r < rows; r++) _heights.Add(_s.Headroom + _cover * 1.5 + Plank);
        _decor.Clear();
        // Spare room at the end of the last shelf of books holds a plant.
        bool plant = _books.Count > 0 && rows * k - n > 0;
        if (plant)
        {
            if (rows == 1)
            {
                // One shelf with room to spare: the books and the plant beside them sit in the middle of it.
                int used = n + 1;
                _offsetX = _gridX + (k - used) * (_cover + _s.Gap) / 2;
                _decor.Add((n, 0, Decor.Plant));
            }
            else _decor.Add((k - 1, rows - 1, Decor.Plant));
        }
        // A case is never just one shelf: a low one below holds a pile of books and some toy blocks.
        if (rows == 1)
        {
            _heights.Add(_s.Headroom + _cover * 1.5 * LowShelf + Plank);
            _decor.Add((0, rows, Decor.Pile));
            if (k > 1) _decor.Add((k - 1, rows, plant || k < 3 ? Decor.Blocks : Decor.Plant));
            if (!plant && k >= 3) _decor.Add((k - 2, rows, Decor.Blocks));
        }
        _shelves = _heights.Count;
    }

    enum Decor { Plant, Pile, Blocks }

    readonly List<double> _heights = new();

    double SlotX(double slot) => _offsetX + slot * (_cover + _s.Gap);
    /// <summary>The same, on the full grid of the case, for things on the low shelf.</summary>
    double GridX(double slot) => _gridX + slot * (_cover + _s.Gap);
    double ShelfTop(int shelf) => Top + _heights.Take(shelf).Sum();
    /// <summary>Where the books on a shelf stand: the middle of the plank's top.</summary>
    double StandY(int shelf) => ShelfTop(shelf) + _heights[shelf] - Plank;
    /// <summary>The clear height above a shelf, up to the shelf above.</summary>
    double Clear(int shelf) => _heights[shelf] - Plank;

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 1100 : availableSize.Width;
        Plan(width);
        var size = new Size(_cover, _cover * 1.5);
        foreach (var b in _books) b.Measure(size);
        _garland.Measure(new Size(width, Top + 60));
        _art.Measure(new Size(width, CaseHeight));
        EmptySign.Measure(new Size(Math.Min(620, width - 2 * (_s.Side + _s.PadX)), _cover * 1.5));
        return new Size(width, CaseHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(finalSize.Width - _width) > 0.5) Plan(finalSize.Width);
        for (int i = 0; i < _books.Count; i++)
        {
            int shelf = i / _columns, slot = i % _columns;
            _books[i].Arrange(new Rect(SlotX(slot), StandY(shelf) - _cover * 1.5, _cover, _cover * 1.5));
        }
        _art.Arrange(new Rect(finalSize));
        // The garland lies along the crown, rising a little above it and hanging a little below.
        _garland.Arrange(new Rect(0, 0, finalSize.Width, Top + _s.Headroom * 1.2));
        _garland.Inset = _s.Side;
        _garland.Line = _s.GarlandRise + _s.Crown * 0.45;
        if (EmptySign.IsVisible)
        {
            double w = Math.Min(620, finalSize.Width - 2 * (_s.Side + _s.PadX));
            double h = Math.Min(_cover * 1.5, Math.Max(EmptySign.DesiredSize.Height, _cover));
            EmptySign.Arrange(new Rect((finalSize.Width - w) / 2, StandY(0) - h, w, h));
        }
        _art.InvalidateVisual();
        return finalSize;
    }

    // ---------------------------------------------------------------- drawing

    static IBrush Vertical(params (Color C, double At)[] stops)
    {
        var b = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative) };
        foreach (var (c, at) in stops) b.GradientStops.Add(new GradientStop(c, at));
        return b;
    }

    static IBrush Horizontal(params (Color C, double At)[] stops)
    {
        var b = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative) };
        foreach (var (c, at) in stops) b.GradientStops.Add(new GradientStop(c, at));
        return b;
    }

    static readonly Color PaintLight = Color.Parse("#86c96a"), PaintMid = Color.Parse("#68b04e"), PaintDark = Color.Parse("#4f9439");
    static readonly Color Back = Color.Parse("#fbf4e2"), BackShade = Color.Parse("#efe3c6");

    /// <summary>Draws the case behind the books. Only drawn again when the case changes size or its books change.</summary>
    sealed class CaseArt : Control
    {
        readonly Bookcase _owner;
        public CaseArt(Bookcase owner) { _owner = owner; IsHitTestVisible = false; }
        public override void Render(DrawingContext ctx) => _owner.DrawCase(ctx, Bounds.Size);
    }

    void DrawCase(DrawingContext ctx, Size size)
    {
        double w = size.Width, h = size.Height;
        if (w <= 0 || _cover <= 0) return;
        double side = _s.Side, top = _s.GarlandRise, bottom = h;
        var outer = new Rect(0, top, w, bottom - top);
        var inner = new Rect(side, top + _s.Crown, w - 2 * side, bottom - top - _s.Crown - _s.Base);
        double radius = _touch ? 10 : 16;

        // A soft shadow on the wall round the whole case.
        ctx.DrawRectangle(null, null, outer, radius, radius, new BoxShadows(new BoxShadow { OffsetY = 8, Blur = 28, Color = Palette.ShadowOf(0.18) }));

        // The painted frame.
        ctx.DrawRectangle(Vertical((PaintLight, 0), (PaintMid, 0.5), (PaintDark, 1)), null, outer, radius, radius);
        using (ctx.PushClip(new RoundedRect(outer, radius)))
        {
            ShelfArt.Speckle(ctx, outer, 0.55);
            // Light catching the top edge and the left side, a little shade on the right.
            ctx.FillRectangle(new ImmutableSolidColorBrush(Colors.White, 0.28), new Rect(outer.X, outer.Y, outer.Width, 2.5));
            ctx.FillRectangle(Horizontal((Color.FromArgb(50, 255, 255, 255), 0), (Color.FromArgb(0, 255, 255, 255), 1)), new Rect(0, top, side, outer.Height));
            ctx.FillRectangle(Horizontal((Color.FromArgb(0, 0, 0, 0), 0), (Palette.ShadowOf(0.12), 1)), new Rect(w - side, top, side, outer.Height));
        }

        // The back: cream boards with soft grooves, darker towards the top and under each shelf.
        ctx.FillRectangle(Vertical((BackShade, 0), (Back, 0.25), (Back, 1)), inner);
        using (ctx.PushClip(inner))
        {
            double board = _touch ? 34 : 52;
            var groove = new ImmutablePen(new ImmutableSolidColorBrush(Palette.ShadowOf(0.07)), 1.2);
            var shine = new ImmutablePen(new ImmutableSolidColorBrush(Colors.White, 0.6), 1);
            for (double x = inner.X + board; x < inner.Right - 4; x += board)
            {
                ctx.DrawLine(groove, new Point(x, inner.Y), new Point(x, inner.Bottom));
                ctx.DrawLine(shine, new Point(x + 1.2, inner.Y), new Point(x + 1.2, inner.Bottom));
            }
            ShelfArt.Speckle(ctx, inner, 0.4);
            // Shade cast by the crown, and by the sides on the back boards.
            ctx.FillRectangle(Vertical((Palette.ShadowOf(0.20), 0), (Palette.ShadowOf(0), 1)), new Rect(inner.X, inner.Y, inner.Width, 26));
            ctx.FillRectangle(Horizontal((Palette.ShadowOf(0.12), 0), (Palette.ShadowOf(0), 1)), new Rect(inner.X, inner.Y, 18, inner.Height));
            ctx.FillRectangle(Horizontal((Palette.ShadowOf(0), 0), (Palette.ShadowOf(0.12), 1)), new Rect(inner.Right - 18, inner.Y, 18, inner.Height));
        }

        // The crown and the base: the same paint, with a moulded line along them.
        var line = new ImmutablePen(new ImmutableSolidColorBrush(PaintDark, 0.6), 1.5);
        var light = new ImmutablePen(new ImmutableSolidColorBrush(Colors.White, 0.35), 1.2);
        ctx.DrawLine(line, new Point(side - 2, inner.Y - 0.75), new Point(w - side + 2, inner.Y - 0.75));
        ctx.DrawLine(light, new Point(side, inner.Bottom + 1.5), new Point(w - side, inner.Bottom + 1.5));

        for (int shelf = 0; shelf < _shelves; shelf++) DrawShelf(ctx, shelf, inner);
        foreach (var (slot, shelf, kind) in _decor)
        {
            double x = (kind == Decor.Plant && shelf == 0 ? SlotX(slot) : GridX(slot)) + _cover / 2, y = StandY(shelf) + 1, clear = Clear(shelf) - _s.Headroom * 0.3;
            switch (kind)
            {
                case Decor.Plant: ShelfArt.Plant(ctx, new Point(x, y), Math.Min(_cover * 1.1, clear * 0.85)); break;
                case Decor.Pile: ShelfArt.BookPile(ctx, new Point(x, y), Math.Min(_cover * 0.95, clear * 1.6)); break;
                case Decor.Blocks: ShelfArt.Blocks(ctx, new Point(x, y), Math.Min(_cover * 0.32, clear * 0.36)); break;
            }
        }
        // Little soft shadows where each book touches its shelf.
        for (int i = 0; i < _books.Count; i++)
        {
            int shelf = i / _columns, slot = i % _columns;
            double cx = SlotX(slot) + _cover / 2, cy = StandY(shelf) + 1;
            var contact = new RadialGradientBrush
            {
                GradientStops = { new GradientStop(Palette.ShadowOf(0.30), 0), new GradientStop(Palette.ShadowOf(0), 1) },
            };
            ctx.DrawEllipse(contact, null, new Point(cx + 3, cy), _cover * 0.58, _s.PlankTop * 0.55);
        }
    }

    void DrawShelf(DrawingContext ctx, int shelf, Rect inner)
    {
        double y = StandY(shelf);
        double topY = y - _s.PlankTop / 2, frontY = y + _s.PlankTop / 2, frontH = _s.PlankFront;
        double x0 = inner.X, x1 = inner.Right;

        // The shelf's top, seen a little from above: lighter at the back edge.
        var topFace = new Rect(x0, topY, x1 - x0, _s.PlankTop);
        ctx.FillRectangle(Vertical((Color.Parse("#e9cfa0"), 0), (Palette.WoodLight, 0.5), (Color.Parse("#f7e4c0"), 1)), topFace);
        ctx.FillRectangle(Vertical((Palette.ShadowOf(0.16), 0), (Palette.ShadowOf(0), 1)), new Rect(x0, topY, x1 - x0, _s.PlankTop * 0.45));

        // The front edge, with the grain, a highlight along its top and a darker bottom.
        var front = new Rect(x0 - 3, frontY, x1 - x0 + 6, frontH);
        using (ctx.PushClip(new RoundedRect(front, 3)))
            ctx.DrawImage(ShelfArt.Wood, new Rect(0, 0, ShelfArt.Wood.PixelSize.Width, ShelfArt.Wood.PixelSize.Height), front);
        ctx.DrawRectangle(Vertical((Color.FromArgb(70, 255, 255, 255), 0), (Color.FromArgb(0, 255, 255, 255), 0.25), (Palette.ShadowOf(0), 0.6), (Palette.ShadowOf(0.18), 1)), null, front, 3, 3);
        ctx.DrawLine(new ImmutablePen(new ImmutableSolidColorBrush(Colors.White, 0.7), 1.2), new Point(front.X + 2, frontY + 0.6), new Point(front.Right - 2, frontY + 0.6));

        // The shadow the shelf throws on the back boards below it.
        ctx.FillRectangle(Vertical((Palette.ShadowOf(0.22), 0), (Palette.ShadowOf(0.06), 0.4), (Palette.ShadowOf(0), 1)), new Rect(x0, frontY + frontH, x1 - x0, _touch ? 12 : 20));
    }

    // ================================================================ the garland along the top

    /// <summary>
    /// Leaves and small yellow flowers on a stem that sags in gentle swags along the top of the case. When
    /// asked to, the leaves sway for a few seconds, dying away to exactly where they rest; only this strip
    /// is drawn again while they do.
    /// </summary>
    sealed class Garland : Control
    {
        readonly double _scale;
        readonly List<(double T, double Side, double Length, double Angle, Color Colour, double Phase)> _leaves = new();
        double _swayStart, _swayLength, _now;
        bool _ticking;

        public Garland(double scale) => _scale = scale;

        /// <summary>How far in from each end the garland starts, and where its pins sit.</summary>
        public double Inset { get; set; }
        public double Line { get; set; }

        public void Sway(double ms)
        {
            if (!Motion.Allowed || TopLevel.GetTopLevel(this) is not TopLevel top) return;
            double left = _swayLength - (_now - _swayStart);
            if (_ticking && left > ms) return;
            _swayStart = _now;
            _swayLength = ms;
            if (_ticking) return;
            _ticking = true;
            double? origin = null;
            void Frame(TimeSpan t)
            {
                origin ??= t.TotalMilliseconds - _now;
                _now = t.TotalMilliseconds - origin.Value;
                InvalidateVisual();
                if (_now - _swayStart < _swayLength && Motion.Allowed) top.RequestAnimationFrame(Frame);
                else { _ticking = false; _swayLength = 0; InvalidateVisual(); }
            }
            top.RequestAnimationFrame(Frame);
        }

        /// <summary>How far each leaf is turned from rest just now, in degrees.</summary>
        double Swing(double phase)
        {
            if (!_ticking || _swayLength <= 0) return 0;
            double u = (_now - _swayStart) / _swayLength;
            if (u >= 1) return 0;
            double fade = (1 - u) * (1 - u);
            double rise = Math.Min(1, (_now - _swayStart) / 400);
            return 7 * fade * rise * Math.Sin((_now / 1900) * Math.PI * 2 + phase);
        }

        void Grow(double width)
        {
            _leaves.Clear();
            int seed = 12345;
            double Next() { seed = unchecked(seed * 1103515245 + 12345); return ((seed >> 8) & 0xffff) / 65535.0; }
            double step = 14 * _scale;
            int count = (int)((width - 2 * Inset) / step);
            for (int i = 0; i <= count; i++)
            {
                double t = i / (double)Math.Max(1, count);
                bool up = i % 3 == 1;
                _leaves.Add((t, up ? 1 : -1, (up ? 15 + 8 * Next() : 20 + 14 * Next()) * _scale, 40 + 35 * Next(), Ornaments.LeafColours[(int)(Next() * 4) % 4], Next() * 6.28));
            }
        }

        Point At(double t, double width)
        {
            // Pinned every 300 units or so, sagging between the pins.
            double span = width - 2 * Inset;
            int swags = Math.Max(2, (int)Math.Round(span / (300 * _scale)));
            double local = t * swags % 1;
            double sag = Math.Sin(local * Math.PI) * 13 * _scale;
            return new Point(Inset + t * span, Line + sag);
        }

        public override void Render(DrawingContext ctx)
        {
            double width = Bounds.Width;
            if (width <= 2 * Inset + 40) return;
            if (_leaves.Count == 0 || Math.Abs(_grownFor - width) > 0.5) { Grow(width); _grownFor = width; }

            var stem = new StreamGeometry();
            using (StreamGeometryContext g = stem.Open())
            {
                g.BeginFigure(At(0, width), false);
                for (int i = 1; i <= 160; i++) g.LineTo(At(i / 160.0, width));
                g.EndFigure(false);
            }
            ctx.DrawGeometry(null, new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#4c8a37")), 2.2 * _scale, lineCap: PenLineCap.Round), stem);

            foreach (var (t, side, length, angle, colour, phase) in _leaves)
            {
                Point p = At(t, width);
                Point q = At(Math.Min(1, t + 0.002), width);
                double along = Math.Atan2(q.Y - p.Y, q.X - p.X) * 180 / Math.PI;
                // Leaves on the near side droop down over the crown; those on the far side stand up behind it.
                double rest = side < 0 ? along + angle + 90 : along - angle + 90;
                Ornaments.Leaf(ctx, p, length, rest + Swing(phase), colour);
            }
            double span = width - 2 * Inset;
            int swags = Math.Max(2, (int)Math.Round(span / (300 * _scale)));
            for (int i = 0; i <= swags; i++) Ornaments.Flower(ctx, At(i / (double)swags, width), 7 * _scale);
            for (int i = 0; i < swags; i++) Ornaments.Flower(ctx, At((i + 0.5) / swags, width) + new Point(0, 3 * _scale), 5 * _scale);
        }

        double _grownFor;
    }
}
