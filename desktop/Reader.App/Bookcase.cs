using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Reader.Ui;

namespace Reader.App;

/// <summary>
/// The bookcase on the shelf screen, a fitted Victorian nursery bookcase in cherry: a moulded cornice
/// with a small brass plate, fluted sides, a solid back of raised panels, a plinth on turned feet, and
/// shelves with the books standing face out on them. Spare room holds a fern in a pot (the shelf's one
/// touch of green) or a little pile of clothbound books lying down.
///
/// The case itself is drawn once (<see cref="CaseArt"/>) and only drawn again when its size or its books
/// change. The books are child controls, so they can lift on their own.
/// </summary>
sealed class Bookcase : Panel
{
    /// <summary>The sizes of the case, for a computer or for a phone.</summary>
    sealed record Sizes(double Side, double Crown, double Base, double PadX, double Gap, double Headroom,
        double PlankTop, double PlankFront, double MaxCover, double MinCover, double Reserved, double Overhang, double Cap);

    static readonly Sizes Desktop = new(Side: 32, Crown: 56, Base: 48, PadX: 36, Gap: 44, Headroom: 40,
        PlankTop: 16, PlankFront: 26, MaxCover: 330, MinCover: 140, Reserved: 200, Overhang: 10, Cap: 12);
    static readonly Sizes Phone = new(Side: 16, Crown: 30, Base: 26, PadX: 16, Gap: 22, Headroom: 26,
        PlankTop: 10, PlankFront: 16, MaxCover: 190, MinCover: 96, Reserved: 130, Overhang: 5, Cap: 7);

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

    static readonly Color FrameLight = Color.Parse("#a4532f"), FrameMid = Color.Parse("#8c4227"), FrameDark = Color.Parse("#6e321c"), FrameDeep = Color.Parse("#4e2314");
    /// <summary>The warm light on an edge of cherry: a highlight that stays in the wood's own colour rather than going white.</summary>
    static readonly Color Sheen = Color.Parse("#e6a57c");

    /// <summary>The wood grain laid over a board of the frame, along it or (for the sides) up and down it.</summary>
    static void WoodBoard(DrawingContext ctx, Rect r, bool upright, double opacity)
    {
        var wood = ShelfArt.Wood;
        // The grain picture is laid in strips no wider than it is tall, each cut from a different part of it,
        // so a wide board keeps a fine grain instead of one stretched swirl.
        double across = upright ? r.Width : r.Height, along = upright ? r.Height : r.Width;
        double strip = Math.Min(across, 60);
        int strips = Math.Max(1, (int)Math.Ceiling(across / strip));
        using (ctx.PushClip(r))
        using (ctx.PushOpacity(opacity))
        {
            for (int i = 0; i < strips; i++)
            {
                double offset = (i * 389) % Math.Max(1, wood.PixelSize.Width - 600);
                var src = new Rect(offset, 0, Math.Min(wood.PixelSize.Width - offset, Math.Max(200, along * 0.8)), wood.PixelSize.Height);
                if (!upright) { ctx.DrawImage(wood, src, new Rect(r.X, r.Y + i * strip, r.Width, strip)); continue; }
                // Turned a quarter: the picture's length runs down the board.
                using (ctx.PushTransform(Matrix.CreateRotation(Math.PI / 2) * Matrix.CreateTranslation(r.X + (i + 1) * strip, r.Y)))
                    ctx.DrawImage(wood, src, new Rect(0, 0, r.Height, strip));
            }
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
        double side = _s.Side, ov = _s.Overhang, cap = _s.Cap;
        double feet = Math.Round(cap * 0.9), plinthH = Math.Round(cap * 1.4);
        double plinthTop = h - feet - plinthH;
        var body = new Rect(ov, 0, w - 2 * ov, plinthTop + 2);
        var inner = new Rect(side, Top, w - 2 * side, h - Top - _s.Base);

        // A soft shadow on the wall round the whole case, deepest at the floor.
        ctx.DrawRectangle(null, null, new Rect(ov, cap, w - 2 * ov, h - cap - feet), 2, 2,
            new BoxShadows(new BoxShadow { OffsetY = 10, Blur = 30, Color = Palette.ShadowOf(0.26) }));

        // The carcass: cherry, the grain running up the sides.
        ctx.FillRectangle(Vertical((FrameLight, 0), (FrameMid, 0.55), (FrameDark, 1)), body);
        foreach (double x in new[] { body.X, w - side })
            WoodBoard(ctx, new Rect(x, 0, side - ov, body.Height), upright: true, 0.75);

        // The back: solid cherry in raised panels, one row to each shelf.
        ctx.FillRectangle(Vertical((FrameDark, 0), (FrameMid, 1)), inner);
        for (int shelf = 0; shelf < _shelves; shelf++)
        {
            double top = shelf == 0 ? inner.Y : StandY(shelf - 1) + Plank;
            double bottom = StandY(shelf) - _s.PlankTop / 2;
            RaisedPanels(ctx, new Rect(inner.X, top, inner.Width, bottom - top));
        }
        using (ctx.PushClip(inner))
        {
            // Warm light from the upper left, and the case's own shade in the corners and under the cornice.
            ctx.FillRectangle(new RadialGradientBrush
            {
                Center = new RelativePoint(0.15, 0, RelativeUnit.Relative), GradientOrigin = new RelativePoint(0.15, 0, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.8, RelativeUnit.Relative), RadiusY = new RelativeScalar(1.2, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb(56, 255, 214, 170), 0), new GradientStop(Color.FromArgb(0, 255, 214, 170), 1) },
            }, inner);
            ctx.FillRectangle(Vertical((Palette.ShadowOf(0.45), 0), (Palette.ShadowOf(0), 1)), new Rect(inner.X, inner.Y, inner.Width, 34));
            ctx.FillRectangle(Horizontal((Palette.ShadowOf(0.35), 0), (Palette.ShadowOf(0), 1)), new Rect(inner.X, inner.Y, 26, inner.Height));
            ctx.FillRectangle(Horizontal((Palette.ShadowOf(0), 0), (Palette.ShadowOf(0.38), 1)), new Rect(inner.Right - 26, inner.Y, 26, inner.Height));
        }

        // The sides: light from the left, shade on the right, and two fine flutes down each, as on a pilaster.
        double sideW = side - ov;
        ctx.FillRectangle(Horizontal((Color.FromArgb(60, Sheen.R, Sheen.G, Sheen.B), 0), (Color.FromArgb(0, Sheen.R, Sheen.G, Sheen.B), 1)), new Rect(body.X, Top, sideW, inner.Height));
        ctx.FillRectangle(Horizontal((Palette.ShadowOf(0), 0), (Palette.ShadowOf(0.2), 1)), new Rect(w - side, Top, sideW, inner.Height));
        if (!_touch)
        {
            var groove = new ImmutablePen(new ImmutableSolidColorBrush(Palette.ShadowOf(0.45)), 1.6, lineCap: PenLineCap.Round);
            var lip = new ImmutablePen(new ImmutableSolidColorBrush(Sheen, 0.45), 1, lineCap: PenLineCap.Round);
            foreach (double x0 in new[] { body.X, w - side })
                foreach (double f in new[] { 0.36, 0.64 })
                {
                    double x = x0 + sideW * f, y0 = Top + 18, y1 = inner.Bottom - 10;
                    ctx.DrawLine(groove, new Point(x, y0), new Point(x, y1));
                    ctx.DrawLine(lip, new Point(x + 1.4, y0), new Point(x + 1.4, y1));
                }
        }
        // A bevel round the opening.
        ctx.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(Sheen, 0.5), 1.2), inner.Inflate(1.6));
        ctx.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(Palette.ShadowOf(0.5)), 1.4), inner.Inflate(0.4));

        DrawCornice(ctx, w, ov, cap);
        DrawPlinth(ctx, w, ov, plinthTop, plinthH, feet);

        for (int shelf = 0; shelf < _shelves; shelf++) DrawShelf(ctx, shelf, inner, shelf == _shelves - 1 ? h - Math.Round(_s.Cap * 0.9) - Math.Round(_s.Cap * 1.4) : null);
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

    /// <summary>
    /// A row of raised panels on the back, as on a fitted Victorian bookcase: each a slightly lighter field
    /// standing out from its frame, with bevels lit from the upper left.
    /// </summary>
    void RaisedPanels(DrawingContext ctx, Rect bay)
    {
        double stile = _touch ? 9 : 16, rail = _touch ? 9 : 16;
        int count = Math.Max(1, (int)Math.Round(bay.Width / (_touch ? 170 : 300)));
        double pw = (bay.Width - stile * (count + 1)) / count;
        double ph = bay.Height - 2 * rail;
        if (pw <= 10 || ph <= 10) return;
        double bevel = _touch ? 4 : 7;
        var light = new ImmutableSolidColorBrush(Sheen, 0.32);
        var dark = new ImmutableSolidColorBrush(Palette.ShadowOf(0.38));
        for (int i = 0; i < count; i++)
        {
            var r = new Rect(bay.X + stile + i * (pw + stile), bay.Y + rail, pw, ph);
            // The groove round the panel.
            ctx.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(Palette.ShadowOf(0.5)), 1.5), r.Inflate(1));
            ctx.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(Sheen, 0.25), 1), r.Inflate(2.4));
            // The bevels: four sloping strips, the top and left catching the light.
            Point a = r.TopLeft, b = r.TopRight, c = r.BottomRight, d = r.BottomLeft;
            Point ai = new(a.X + bevel, a.Y + bevel), bi = new(b.X - bevel, b.Y + bevel), ci = new(c.X - bevel, c.Y - bevel), di = new(d.X + bevel, d.Y - bevel);
            ctx.FillRectangle(Vertical((FrameMid, 0), (FrameDark, 1)), r);
            Quad(ctx, light, a, b, bi, ai);
            Quad(ctx, new ImmutableSolidColorBrush(Sheen, 0.18), a, ai, di, d);
            Quad(ctx, dark, d, di, ci, c);
            Quad(ctx, new ImmutableSolidColorBrush(Palette.ShadowOf(0.26)), b, c, ci, bi);
            // The field, a little lighter, with its own grain.
            var field = new Rect(ai, ci);
            ctx.FillRectangle(Vertical((FrameLight, 0), (FrameMid, 1)), field);
            WoodBoard(ctx, field, upright: true, 0.45);
        }
    }

    static void Quad(DrawingContext ctx, IBrush brush, Point a, Point b, Point c, Point d)
    {
        var g = new StreamGeometry();
        using (StreamGeometryContext k = g.Open())
        {
            k.BeginFigure(a, true);
            k.LineTo(b); k.LineTo(c); k.LineTo(d);
            k.EndFigure(true);
        }
        ctx.DrawGeometry(brush, null, g);
    }

    /// <summary>The cornice along the top: a cap standing proud, a cove, a bead, and a frieze with a small brass plate.</summary>
    void DrawCornice(DrawingContext ctx, double w, double ov, double cap)
    {
        double cove = Math.Round(cap * (_touch ? 0.8 : 1.0)), bead = _touch ? 2 : 3;
        var capRect = new Rect(0, 0, w, cap);
        ctx.DrawRectangle(null, null, capRect, 2, 2, new BoxShadows(new BoxShadow { OffsetY = 3, Blur = 6, Color = Palette.ShadowOf(0.35) }));
        ctx.DrawRectangle(Vertical((FrameLight, 0), (FrameMid, 1)), null, capRect, 2, 2);
        using (ctx.PushClip(new RoundedRect(capRect, 2))) WoodBoard(ctx, capRect, upright: false, 0.6);
        ctx.FillRectangle(new ImmutableSolidColorBrush(Sheen, 0.75), new Rect(2, 0.5, w - 4, 1.4));
        ctx.FillRectangle(new ImmutableSolidColorBrush(Palette.ShadowOf(0.35)), new Rect(0, cap - 1.2, w, 1.2));
        // The cove curves in under the cap: dark at the top, catching the light at the bottom.
        var coveRect = new Rect(ov * 0.5, cap, w - ov, cove);
        ctx.FillRectangle(Vertical((FrameDeep, 0), (FrameDark, 0.45), (FrameLight, 1)), coveRect);
        var beadRect = new Rect(ov * 0.7, cap + cove, w - ov * 1.4, bead);
        ctx.FillRectangle(Vertical((Sheen, 0), (FrameMid, 1)), beadRect);
        // The frieze, with the grain running along it.
        var frieze = new Rect(ov, cap + cove + bead, w - 2 * ov, Top - cap - cove - bead);
        ctx.FillRectangle(Vertical((FrameMid, 0), (FrameDark, 1)), frieze);
        WoodBoard(ctx, frieze, upright: false, 0.7);
        ctx.FillRectangle(new ImmutableSolidColorBrush(Palette.ShadowOf(0.3)), new Rect(frieze.X, frieze.Y, frieze.Width, 1.2));
        if (frieze.Height > 12) BrassPlate(ctx, new Point(w / 2, frieze.Y + frieze.Height / 2), Math.Min(frieze.Height - 8, _touch ? 12 : 18));
    }

    /// <summary>A small brass plate with a fine engraved border and two pins, like a library shelf label.</summary>
    static void BrassPlate(DrawingContext ctx, Point centre, double height)
    {
        double width = height * 4.2;
        var r = new Rect(centre.X - width / 2, centre.Y - height / 2, width, height);
        ctx.DrawRectangle(Vertical((Palette.SunLight, 0), (Palette.Sun, 0.5), (Palette.SunDeep, 1)), new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#7a5a1c")), 1),
            r, height * 0.22, height * 0.22, new BoxShadows(new BoxShadow { OffsetY = 1.5, Blur = 3, Color = Palette.ShadowOf(0.45) }));
        ctx.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#8c6a24"), 0.8), 0.9), r.Deflate(height * 0.2), height * 0.12, height * 0.12);
        ctx.FillRectangle(new ImmutableSolidColorBrush(Colors.White, 0.45), new Rect(r.X + height * 0.3, r.Y + 1.2, width - height * 0.6, 1));
        var pin = new ImmutableSolidColorBrush(Color.Parse("#8c6a24"));
        ctx.DrawEllipse(pin, null, new Point(r.X + height * 0.42, centre.Y), height * 0.09, height * 0.09);
        ctx.DrawEllipse(pin, null, new Point(r.Right - height * 0.42, centre.Y), height * 0.09, height * 0.09);
    }

    /// <summary>The plinth: a moulded top, a deep board, and two turned bun feet.</summary>
    void DrawPlinth(DrawingContext ctx, double w, double ov, double top, double height, double feet)
    {
        // The feet first, so the plinth sits on them.
        double footW = Math.Max(16, _s.Side * 1.3);
        foreach (double cx in new[] { ov + footW * 0.7, w - ov - footW * 0.7 })
        {
            var foot = new Rect(cx - footW / 2, top + height - 2, footW, feet + 2);
            ctx.DrawRectangle(Vertical((FrameMid, 0), (FrameDeep, 1)), null, foot, footW * 0.45, feet * 0.6,
                new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 4, Color = Palette.ShadowOf(0.4) }));
            ctx.FillRectangle(new ImmutableSolidColorBrush(Sheen, 0.4), new Rect(foot.X + footW * 0.2, foot.Y + 3, footW * 0.25, 1.4));
        }
        var moulding = new Rect(ov * 0.4, top, w - ov * 0.8, Math.Round(height * 0.38));
        var board = new Rect(0, moulding.Bottom, w, height - moulding.Height);
        ctx.DrawRectangle(Vertical((FrameMid, 0), (FrameDark, 1)), null, board, 2, 2);
        using (ctx.PushClip(new RoundedRect(board, 2))) WoodBoard(ctx, board, upright: false, 0.55);
        ctx.FillRectangle(new ImmutableSolidColorBrush(Sheen, 0.55), new Rect(2, board.Y, w - 4, 1.2));
        ctx.FillRectangle(Vertical((Sheen, 0), (FrameLight, 0.4), (FrameDark, 1)), moulding);
    }

    /// <param name="downTo">The bottom shelf: its front runs down to the plinth as one deep board, rather than a shelf above a base.</param>
    void DrawShelf(DrawingContext ctx, int shelf, Rect inner, double? downTo = null)
    {
        double y = StandY(shelf);
        double topY = y - _s.PlankTop / 2, frontY = y + _s.PlankTop / 2, frontH = downTo is double d ? d - frontY : _s.PlankFront;
        double x0 = inner.X, x1 = inner.Right;

        // The shelf's top, seen a little from above: lighter at the back edge.
        var topFace = new Rect(x0, topY, x1 - x0, _s.PlankTop);
        ctx.FillRectangle(Vertical((Palette.WoodDark, 0), (Palette.Wood, 0.45), (Palette.WoodLight, 1)), topFace);
        ctx.FillRectangle(Vertical((Palette.ShadowOf(0.16), 0), (Palette.ShadowOf(0), 1)), new Rect(x0, topY, x1 - x0, _s.PlankTop * 0.45));

        // The front edge, with the grain, a highlight along its top and a darker bottom.
        var front = new Rect(x0 - 3, frontY, x1 - x0 + 6, frontH);
        using (ctx.PushClip(new RoundedRect(front, 3)))
            ctx.DrawImage(ShelfArt.Wood, new Rect(0, 0, ShelfArt.Wood.PixelSize.Width, ShelfArt.Wood.PixelSize.Height), front);
        ctx.DrawRectangle(Vertical((Color.FromArgb(60, Sheen.R, Sheen.G, Sheen.B), 0), (Color.FromArgb(0, Sheen.R, Sheen.G, Sheen.B), 0.3), (Palette.ShadowOf(0), 0.6), (Palette.ShadowOf(0.25), 1)), null, front, 3, 3);
        ctx.DrawLine(new ImmutablePen(new ImmutableSolidColorBrush(Sheen, 0.8), 1.2), new Point(front.X + 2, frontY + 0.6), new Point(front.Right - 2, frontY + 0.6));

        // The shadow the shelf throws on the back boards below it.
        if (downTo is null) ctx.FillRectangle(Vertical((Palette.ShadowOf(0.22), 0), (Palette.ShadowOf(0.06), 0.4), (Palette.ShadowOf(0), 1)), new Rect(x0, frontY + frontH, x1 - x0, _touch ? 12 : 20));
    }
}
