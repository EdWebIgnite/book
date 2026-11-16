using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Reader.Ui;

namespace Reader.App;

/// <summary>
/// The bookcase on the shelf screen: a light honey oak frame with a top cap and a plinth, a soft cream back
/// of tongue-and-groove boards, and wooden shelves with the books standing face out on them. Spare room
/// holds a potted plant (the shelf's one touch of green) or a little pile of books lying down.
///
/// The case itself is drawn once (<see cref="CaseArt"/>) and only drawn again when its size or its books
/// change. The books are child controls, so they can lift on their own.
/// </summary>
sealed class Bookcase : Panel
{
    /// <summary>The sizes of the case, for a computer or for a phone.</summary>
    sealed record Sizes(double Side, double Crown, double Base, double PadX, double Gap, double Headroom,
        double PlankTop, double PlankFront, double MaxCover, double MinCover, double Reserved, double Overhang, double Cap);

    static readonly Sizes Desktop = new(Side: 28, Crown: 34, Base: 34, PadX: 36, Gap: 44, Headroom: 40,
        PlankTop: 16, PlankFront: 26, MaxCover: 330, MinCover: 140, Reserved: 200, Overhang: 7, Cap: 12);
    static readonly Sizes Phone = new(Side: 15, Crown: 20, Base: 20, PadX: 16, Gap: 22, Headroom: 26,
        PlankTop: 10, PlankFront: 16, MaxCover: 190, MinCover: 96, Reserved: 130, Overhang: 4, Cap: 7);

    readonly Sizes _s;
    readonly bool _touch;
    readonly List<ShelfView.BookCover> _books = new();
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
        Children.Add(_art);
        EmptySign = new ShelfView.Sign(touch) { IsVisible = false };
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


    public void SetBooks(IReadOnlyList<ShelfView.BookCover> books)
    {
        foreach (var b in _books) Children.Remove(b);
        _books.Clear();
        foreach (var b in books)
        {
            _books.Add(b);
            Children.Add(b);
        }
        EmptySign.IsVisible = _books.Count == 0;
        InvalidateMeasure();
        _art.InvalidateVisual();
    }

    /// <summary>The books drop gently into place one after another.</summary>
    public void SettleIn()
    {
        for (int i = 0; i < _books.Count; i++) _books[i].Settle(i);
    }

    /// <summary>A book on the shelf, for screenshots.</summary>
    public ShelfView.BookCover? Book(int index) => index >= 0 && index < _books.Count ? _books[index] : null;

    // ---------------------------------------------------------------- layout

    /// <summary>The low shelf under a single shelf of books is this much as tall as a book.</summary>
    const double LowShelf = 0.5;

    double Top => _s.Crown;
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
        // A case is never just one shelf: a low one below holds a pile of books (and the plant, if it had no room above).
        if (rows == 1)
        {
            _heights.Add(_s.Headroom + _cover * 1.5 * LowShelf + Plank);
            _decor.Add((0, rows, Decor.Pile));
            if (!plant && k > 1) _decor.Add((k - 1, rows, Decor.Plant));
        }
        _shelves = _heights.Count;
    }

    enum Decor { Plant, Pile }

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

    static readonly Color FrameLight = Color.Parse("#e7c189"), FrameMid = Color.Parse("#d7a96a"), FrameDark = Color.Parse("#c28f55");
    static readonly Color Back = Color.Parse("#f5ead4"), BackShade = Color.Parse("#e3d0ac");

    /// <summary>The wood grain laid over a board of the frame, along it or (for the sides) up and down it.</summary>
    static void WoodBoard(DrawingContext ctx, Rect r, bool upright, double opacity)
    {
        var wood = ShelfArt.Wood;
        var source = new Rect(0, 0, wood.PixelSize.Width, wood.PixelSize.Height);
        using (ctx.PushOpacity(opacity))
        {
            if (!upright) { ctx.DrawImage(wood, source, r); return; }
            // Turned a quarter: the picture's length runs down the board.
            using (ctx.PushTransform(Matrix.CreateRotation(Math.PI / 2) * Matrix.CreateTranslation(r.Right, r.Y)))
                ctx.DrawImage(wood, source, new Rect(0, 0, r.Height, r.Width));
        }
    }

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
        double side = _s.Side, ov = _s.Overhang;
        var body = new Rect(ov, 0, w - 2 * ov, h);
        var inner = new Rect(side, Top, w - 2 * side, h - Top - _s.Base);
        double radius = _touch ? 3 : 4;

        // A soft shadow on the wall round the whole case.
        ctx.DrawRectangle(null, null, body, radius, radius, new BoxShadows(new BoxShadow { OffsetY = 10, Blur = 30, Color = Palette.ShadowOf(0.22) }));

        // The frame: light honey oak, the grain running along each board.
        ctx.DrawRectangle(Vertical((FrameLight, 0), (FrameMid, 0.55), (FrameDark, 1)), null, body, radius, radius);
        using (ctx.PushClip(new RoundedRect(body, radius)))
        {
            // The sides, their grain running up and down.
            foreach (double x in new[] { body.X, w - side })
                WoodBoard(ctx, new Rect(x, 0, side - ov, h), upright: true, 0.55);
            WoodBoard(ctx, new Rect(body.X, 0, body.Width, Top), upright: false, 0.6);
            WoodBoard(ctx, new Rect(body.X, h - _s.Base, body.Width, _s.Base), upright: false, 0.6);
            // Light from the upper left: the left side catches it, the right side is in a little shade.
            ctx.FillRectangle(Horizontal((Color.FromArgb(60, 255, 255, 255), 0), (Color.FromArgb(0, 255, 255, 255), 1)), new Rect(body.X, 0, side - ov, h));
            ctx.FillRectangle(Horizontal((Palette.ShadowOf(0), 0), (Palette.ShadowOf(0.16), 1)), new Rect(w - side, 0, side - ov, h));
            ctx.FillRectangle(Vertical((Palette.ShadowOf(0), 0.4), (Palette.ShadowOf(0.18), 1)), new Rect(body.X, h - _s.Base, body.Width, _s.Base));
        }

        // The back: soft cream boards with fine grooves, shaded under the top and beside the sides.
        ctx.FillRectangle(Vertical((BackShade, 0), (Back, 0.3), (Back, 1)), inner);
        using (ctx.PushClip(inner))
        {
            double board = _touch ? 34 : 52;
            var groove = new ImmutablePen(new ImmutableSolidColorBrush(Palette.ShadowOf(0.07)), 1.2);
            var shine = new ImmutablePen(new ImmutableSolidColorBrush(Colors.White, 0.55), 1);
            for (double x = inner.X + board; x < inner.Right - 4; x += board)
            {
                ctx.DrawLine(groove, new Point(x, inner.Y), new Point(x, inner.Bottom));
                ctx.DrawLine(shine, new Point(x + 1.2, inner.Y), new Point(x + 1.2, inner.Bottom));
            }
            ShelfArt.Speckle(ctx, inner, 0.4);
            ctx.FillRectangle(Vertical((Palette.ShadowOf(0.24), 0), (Palette.ShadowOf(0), 1)), new Rect(inner.X, inner.Y, inner.Width, 30));
            ctx.FillRectangle(Horizontal((Palette.ShadowOf(0.16), 0), (Palette.ShadowOf(0), 1)), new Rect(inner.X, inner.Y, 22, inner.Height));
            ctx.FillRectangle(Horizontal((Palette.ShadowOf(0), 0), (Palette.ShadowOf(0.14), 1)), new Rect(inner.Right - 22, inner.Y, 22, inner.Height));
        }
        // A bevel round the opening: a dark line where the back meets the frame, a light one on the frame's edge.
        var bevelDark = new ImmutablePen(new ImmutableSolidColorBrush(Palette.ShadowOf(0.35)), 1.4);
        var bevelLight = new ImmutablePen(new ImmutableSolidColorBrush(Colors.White, 0.45), 1.2);
        ctx.DrawRectangle(null, bevelLight, inner.Inflate(1.6));
        ctx.DrawRectangle(null, bevelDark, inner.Inflate(0.4));

        // The top cap and the plinth stand a little proud of the sides, as on a real bookcase.
        var cap = new Rect(0, 0, w, _s.Cap);
        ctx.DrawRectangle(null, null, cap, radius, radius, new BoxShadows(new BoxShadow { OffsetY = 3, Blur = 5, Color = Palette.ShadowOf(0.28) }));
        ctx.DrawRectangle(Vertical((FrameLight, 0), (FrameMid, 1)), null, cap, radius, radius);
        using (ctx.PushClip(new RoundedRect(cap, radius))) WoodBoard(ctx, cap, upright: false, 0.5);
        ctx.FillRectangle(new ImmutableSolidColorBrush(Colors.White, 0.5), new Rect(2, 0.5, w - 4, 1.4));
        var plinth = new Rect(0, h - _s.Cap * 0.9, w, _s.Cap * 0.9);
        ctx.DrawRectangle(Vertical((FrameMid, 0), (FrameDark, 1)), null, plinth, radius, radius);
        using (ctx.PushClip(new RoundedRect(plinth, radius))) WoodBoard(ctx, plinth, upright: false, 0.35);
        ctx.FillRectangle(new ImmutableSolidColorBrush(Colors.White, 0.35), new Rect(2, plinth.Y, w - 4, 1.2));

        for (int shelf = 0; shelf < _shelves; shelf++) DrawShelf(ctx, shelf, inner, shelf == _shelves - 1 ? h - _s.Cap * 0.9 : null);
        foreach (var (slot, shelf, kind) in _decor)
        {
            double x = (kind == Decor.Plant && shelf == 0 ? SlotX(slot) : GridX(slot)) + _cover / 2, y = StandY(shelf) + 1, clear = Clear(shelf) - _s.Headroom * 0.3;
            switch (kind)
            {
                case Decor.Plant: ShelfArt.Plant(ctx, new Point(x, y), Math.Min(_cover * 1.1, clear * 0.85)); break;
                case Decor.Pile: ShelfArt.BookPile(ctx, new Point(x, y), Math.Min(_cover * 0.95, clear * 1.6)); break;
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

    /// <param name="downTo">The bottom shelf: its front runs down to the plinth as one deep board, rather than a shelf above a base.</param>
    void DrawShelf(DrawingContext ctx, int shelf, Rect inner, double? downTo = null)
    {
        double y = StandY(shelf);
        double topY = y - _s.PlankTop / 2, frontY = y + _s.PlankTop / 2, frontH = downTo is double d ? d - frontY : _s.PlankFront;
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
        if (downTo is null) ctx.FillRectangle(Vertical((Palette.ShadowOf(0.22), 0), (Palette.ShadowOf(0.06), 0.4), (Palette.ShadowOf(0), 1)), new Rect(x0, frontY + frontH, x1 - x0, _touch ? 12 : 20));
    }
}
