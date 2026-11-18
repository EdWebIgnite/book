using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Reader.Ui;

namespace Reader.App;

/// <summary>
/// The bookcase on the shelf screen: a fitted nursery bookcase in cherry with a moulded cornice
/// carrying a brass name plate ("Lia's books"), plain sides, a pale painted back so the covers are the
/// most colourful thing on screen, a plinth on turned feet, and the books standing face out on its
/// shelves. On a wide screen the "Carry on reading" card stands on the top shelf beside the books; on
/// a low shelf below sit a little pile of clothbound books and the dassie. On a phone held upright
/// there is no low shelf, and the dassie lies on top of the cornice.
///
/// The case itself is drawn once (<see cref="CaseArt"/>) and only drawn again when its size or its books
/// change. The books, the card and the dassie are child controls, so they can lift and blink on their own.
/// </summary>
sealed class Bookcase : Panel
{
    /// <summary>The sizes of the case, for a computer or tablet, or for a phone.</summary>
    sealed record Sizes(double Side, double Crown, double Base, double PadX, double Gap, double Headroom,
        double PlankTop, double PlankFront, double MaxCover, double MinCover, double Overhang, double Cap, double Plate, double OneRow);

    static readonly Sizes Large = new(Side: 26, Crown: 64, Base: 44, PadX: 34, Gap: 32, Headroom: 30,
        PlankTop: 14, PlankFront: 22, MaxCover: 300, MinCover: 120, Overhang: 10, Cap: 12, Plate: 30, OneRow: 136);
    static readonly Sizes Small = new(Side: 12, Crown: 42, Base: 22, PadX: 14, Gap: 22, Headroom: 18,
        PlankTop: 9, PlankFront: 13, MaxCover: 200, MinCover: 96, Overhang: 5, Cap: 7, Plate: 22, OneRow: 96);

    readonly Sizes _s;
    readonly bool _small;
    readonly List<ShelfView.BookCover> _books = new();
    readonly CaseArt _art;
    readonly Dassie _dassie = new() { Look = Dassie.Pose.Sit };
    Control? _card;

    // The last layout.
    double _cover, _offsetX, _gridX, _width, _cardH;
    int _columns;
    bool _lowShelf, _dassieOnTop;
    readonly List<double> _heights = new();

    /// <param name="small">Phone sizes: a slimmer frame and smaller gaps.</param>
    public Bookcase(bool small)
    {
        _small = small;
        _s = small ? Small : Large;
        _art = new CaseArt(this);
        Children.Add(_art);
        EmptySign = new ShelfView.Sign(small) { IsVisible = false };
        Children.Add(EmptySign);
        Children.Add(_dassie);
    }

    /// <summary>The height the case may take, so a row of books never grows taller than fits on the screen.</summary>
    public double ViewportHeight
    {
        get => _viewport;
        set { if (Math.Abs(_viewport - value) > 0.5) { _viewport = value; InvalidateMeasure(); } }
    }
    double _viewport = 800;

    /// <summary>The words on the brass plate.</summary>
    public string Plate
    {
        get => _plate;
        set { _plate = value; _art.InvalidateVisual(); }
    }
    string _plate = "";

    /// <summary>A low shelf below a single shelf of books, with the dassie and a pile of books (wide screens with room).</summary>
    public bool AllowLowShelf { get; set; } = true;

    /// <summary>The biggest a cover may be across (smaller on a tablet held upright, so it shows three to a shelf).</summary>
    public double MaxCover { get; set; } = double.NaN;

    /// <summary>The sign that stands on an empty shelf.</summary>
    public ShelfView.Sign EmptySign { get; }

    public bool Empty => _books.Count == 0;

    /// <summary>The "Carry on reading" card standing on the top shelf, two books wide, or null.</summary>
    public void SetCard(Control? card)
    {
        if (_card is not null) Children.Remove(_card);
        _card = card;
        if (card is not null) Children.Add(card);
        InvalidateMeasure();
    }

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
        // The empty shelf's sign has the dassie on it already.
        _dassie.IsVisible = _books.Count > 0;
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
    const double LowShelf = 0.42;

    /// <summary>Room above the cornice for the dassie lying on it.</summary>
    double DassieTop => _dassieOnTop ? (_small ? 26 : 34) : 0;
    double Top => DassieTop + _s.Crown;
    double Plank => _s.PlankTop / 2 + _s.PlankFront;
    double CaseHeight => Top + _heights.Sum() + _s.Base;
    int CardSlots => _card is null ? 0 : 2;
    double CardWidth => 2 * _cover + _s.Gap;

    /// <summary>How tall the card wants to be for a given width: its picture, its words and its button.</summary>
    double CardWanted(double cover) => _small ? cover * 1.5 : Math.Max(cover * 1.5, Math.Min(cover * 2.1, 300));

    void Plan(double width)
    {
        _width = width;
        double inner = Math.Max(100, width - 2 * (_s.Side + _s.PadX));
        int units = Math.Max(1, _books.Count) + CardSlots;
        double max = double.IsNaN(MaxCover) ? _s.MaxCover : Math.Min(MaxCover, _s.MaxCover);
        // As many to a shelf as fit at no more than the biggest cover size, all the same size...
        int k = Math.Max(1, (int)Math.Ceiling((inner + _s.Gap) / (max + _s.Gap)));
        k = Math.Min(k, units);
        // ...unless they all fit on one shelf at a good size, which keeps everything in one look.
        if ((inner - _s.Gap * (units - 1)) / units >= _s.OneRow) k = units;
        double cover = (inner - _s.Gap * (k - 1)) / k;
        while (cover < _s.MinCover && k > Math.Max(1, CardSlots + 1)) { k--; cover = (inner - _s.Gap * (k - 1)) / k; }
        cover = Math.Min(cover, max);
        int rows = (int)Math.Ceiling(units / (double)k);
        // ...but not so tall that the first shelf doesn't fit on the screen (with a low shelf below it
        // when there is room for one, or a peek of the next shelf when there are more books).
        _dassieOnTop = !AllowLowShelf || rows > 1;
        bool low = AllowLowShelf && rows == 1;
        double fixedH = Top + _s.Base + _s.Headroom + Plank + (low ? _s.Headroom + Plank : 0) + (rows > 1 ? _s.Headroom * 2 : 0);
        double room = _viewport - fixedH;
        double Need(double c) => (_card is null ? 1.5 * c : CardWanted(c)) + (low ? 1.5 * LowShelf * c : 0);
        double tallest = room / (1.5 + (low ? 1.5 * LowShelf : 0));
        while (tallest > _s.MinCover && Need(tallest) > room) tallest -= 2;
        tallest = Math.Max(_s.MinCover, tallest);
        if (cover > tallest)
        {
            cover = tallest;
            k = Math.Min(units, Math.Max(CardSlots + 1, (int)Math.Floor((inner + _s.Gap) / (cover + _s.Gap))));
            rows = (int)Math.Ceiling(units / (double)k);
            _dassieOnTop = !AllowLowShelf || rows > 1;
            low = AllowLowShelf && rows == 1;
        }
        _cover = Math.Floor(cover);
        _columns = k;
        _lowShelf = low;
        _cardH = _card is null ? 0 : Math.Floor(CardWanted(_cover));
        double used = Math.Min(units, k);
        _gridX = _s.Side + _s.PadX + (inner - (k * _cover + (k - 1) * _s.Gap)) / 2;
        // One shelf with room to spare: the books sit in the middle of it.
        _offsetX = rows == 1 ? _gridX + (k - used) * (_cover + _s.Gap) / 2 : _gridX;

        _heights.Clear();
        for (int r = 0; r < rows; r++)
        {
            double tallestThing = r == 0 ? Math.Max(_cover * 1.5, _cardH) : _cover * 1.5;
            _heights.Add(_s.Headroom + tallestThing + Plank);
        }
        if (low) _heights.Add(_s.Headroom + _cover * 1.5 * LowShelf + Plank);
    }

    double SlotX(int slot) => _offsetX + slot * (_cover + _s.Gap);
    double ShelfTop(int shelf) => Top + _heights.Take(shelf).Sum();
    /// <summary>Where the books on a shelf stand: the middle of the plank's top.</summary>
    double StandY(int shelf) => ShelfTop(shelf) + _heights[shelf] - Plank;

    (int Shelf, int Slot) Place(int index)
    {
        int i = index + CardSlots;
        return (i / _columns, i % _columns);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 1100 : availableSize.Width;
        Plan(width);
        var size = new Size(_cover, _cover * 1.5);
        foreach (var b in _books) b.Measure(size);
        _card?.Measure(new Size(CardWidth, _cardH));
        _art.Measure(new Size(width, CaseHeight));
        _dassie.Measure(new Size(200, 200));
        EmptySign.Measure(new Size(Math.Min(620, width - 2 * (_s.Side + _s.PadX)), Math.Max(_cover * 1.5, 200)));
        return new Size(width, CaseHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(finalSize.Width - _width) > 0.5) Plan(finalSize.Width);
        for (int i = 0; i < _books.Count; i++)
        {
            var (shelf, slot) = Place(i);
            _books[i].Arrange(new Rect(SlotX(slot), StandY(shelf) - _cover * 1.5, _cover, _cover * 1.5));
        }
        _card?.Arrange(new Rect(SlotX(0), StandY(0) - _cardH, CardWidth, _cardH));
        _art.Arrange(new Rect(finalSize));
        if (EmptySign.IsVisible)
        {
            double w = Math.Min(620, finalSize.Width - 2 * (_s.Side + _s.PadX));
            double h = EmptySign.DesiredSize.Height;
            EmptySign.Arrange(new Rect((finalSize.Width - w) / 2, StandY(0) - h, w, h));
        }
        if (_dassieOnTop)
        {
            _dassie.Look = Dassie.Pose.Lie;
            double dw = _small ? 62 : 84, dh = dw / Dassie.Aspect(Dassie.Pose.Lie);
            _dassie.Arrange(new Rect(finalSize.Width - _s.Overhang - dw - (_small ? 18 : 40), DassieTop - dh + 2, dw, dh));
        }
        else
        {
            _dassie.Look = Dassie.Pose.Sit;
            int shelf = _heights.Count - 1;
            double clear = _heights[shelf] - Plank - _s.Headroom * 0.4;
            double d = Math.Min(_cover * 0.75, clear);
            double right = finalSize.Width - _s.Side - _s.PadX - _cover * 0.2;
            _dassie.Arrange(new Rect(right - d, StandY(shelf) - d + 1, d, d));
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

    // The frame's cherry, from an edge catching the light to the deepest shade (the bedtime version is the same wood by lamplight).
    static Color FrameLight => Palette.Night ? Color.Parse("#563842") : Color.Parse("#a4532f");
    static Color FrameMid => Palette.Night ? Color.Parse("#472d38") : Color.Parse("#8c4227");
    static Color FrameDark => Palette.Night ? Color.Parse("#38212c") : Color.Parse("#6e321c");
    static Color FrameDeep => Palette.Night ? Color.Parse("#28161f") : Color.Parse("#4e2314");
    static Color Sheen => Palette.Sheen;

    /// <summary>The wood grain laid over a board of the frame, along it or (for the sides) up and down it.</summary>
    static void WoodBoard(DrawingContext ctx, Rect r, bool upright, double opacity)
    {
        var wood = ShelfArt.Wood;
        double across = upright ? r.Width : r.Height, along = upright ? r.Height : r.Width;
        double strip = Math.Min(across, 60);
        int strips = Math.Max(1, (int)Math.Ceiling(across / strip));
        using (ctx.PushClip(r))
        using (ctx.PushOpacity(opacity * Palette.Current.Grain))
        {
            for (int i = 0; i < strips; i++)
            {
                double offset = (i * 389) % Math.Max(1, wood.PixelSize.Width - 600);
                var src = new Rect(offset, 0, Math.Min(wood.PixelSize.Width - offset, Math.Max(200, along * 0.8)), wood.PixelSize.Height);
                if (!upright) { ctx.DrawImage(wood, src, new Rect(r.X, r.Y + i * strip, r.Width, strip)); continue; }
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
        double side = _s.Side, ov = _s.Overhang, cap = _s.Cap, top0 = DassieTop;
        double feet = Math.Round(cap * 0.9), plinthH = Math.Round(cap * 1.4);
        double plinthTop = h - feet - plinthH;
        var body = new Rect(ov, top0, w - 2 * ov, plinthTop + 2 - top0);
        var inner = new Rect(side, Top, w - 2 * side, h - Top - _s.Base);

        // A soft shadow on the wall round the whole case.
        ctx.DrawRectangle(null, null, new Rect(ov, top0 + cap, w - 2 * ov, h - top0 - cap - feet), 2, 2,
            new BoxShadows(new BoxShadow { OffsetY = 8, Blur = 26, Color = Palette.ShadowOf(Palette.Night ? 0.5 : 0.22) }));

        // The carcass, the grain running up the sides.
        ctx.FillRectangle(Vertical((FrameLight, 0), (FrameMid, 0.55), (FrameDark, 1)), body);
        foreach (double x in new[] { body.X, w - side })
            WoodBoard(ctx, new Rect(x, top0, side - ov, body.Height), upright: true, 0.7);

        // The back: plain and pale, painted, so nothing behind the covers competes with them.
        ctx.FillRectangle(Palette.Brush(Palette.CaseBack), inner);
        using (ctx.PushClip(inner))
        {
            // The case's own shade: under the cornice, under each shelf, and in the corners.
            ctx.FillRectangle(Vertical((Palette.ShadowOf(0.22), 0), (Palette.ShadowOf(0), 1)), new Rect(inner.X, inner.Y, inner.Width, _small ? 14 : 22));
            ctx.FillRectangle(Horizontal((Palette.ShadowOf(0.14), 0), (Palette.ShadowOf(0), 1)), new Rect(inner.X, inner.Y, _small ? 10 : 18, inner.Height));
            ctx.FillRectangle(Horizontal((Palette.ShadowOf(0), 0), (Palette.ShadowOf(0.16), 1)), new Rect(inner.Right - (_small ? 10 : 18), inner.Y, _small ? 10 : 18, inner.Height));
        }

        // The sides: light from the left, shade on the right.
        double sideW = side - ov;
        ctx.FillRectangle(Horizontal((Color.FromArgb(60, Sheen.R, Sheen.G, Sheen.B), 0), (Color.FromArgb(0, Sheen.R, Sheen.G, Sheen.B), 1)), new Rect(body.X, Top, sideW, inner.Height));
        ctx.FillRectangle(Horizontal((Palette.ShadowOf(0), 0), (Palette.ShadowOf(0.2), 1)), new Rect(w - side, Top, sideW, inner.Height));
        // A bevel round the opening.
        ctx.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(Sheen, 0.5), 1.2), inner.Inflate(1.6));
        ctx.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(Palette.ShadowOf(0.45)), 1.2), inner.Inflate(0.4));

        DrawCornice(ctx, w, ov, cap, top0);
        DrawPlinth(ctx, w, ov, plinthTop, plinthH, feet);

        for (int shelf = 0; shelf < _heights.Count; shelf++)
            DrawShelf(ctx, shelf, inner, shelf == _heights.Count - 1 ? h - feet - plinthH : null);
        if (_lowShelf)
        {
            int shelf = _heights.Count - 1;
            double clear = _heights[shelf] - Plank - _s.Headroom * 0.3;
            ShelfArt.BookPile(ctx, new Point(_gridX + _cover * 0.6, StandY(shelf) + 1), Math.Min(_cover * 0.95, clear * 1.6));
        }
        // Little soft shadows where each book touches its shelf.
        var contact = new RadialGradientBrush { GradientStops = { new GradientStop(Palette.ShadowOf(0.28), 0), new GradientStop(Palette.ShadowOf(0), 1) } };
        for (int i = 0; i < _books.Count; i++)
        {
            var (shelf, slot) = Place(i);
            ctx.DrawEllipse(contact, null, new Point(SlotX(slot) + _cover / 2 + 3, StandY(shelf) + 1), _cover * 0.58, _s.PlankTop * 0.55);
        }
        if (_card is not null) ctx.DrawEllipse(contact, null, new Point(SlotX(0) + CardWidth / 2 + 3, StandY(0) + 1), CardWidth * 0.55, _s.PlankTop * 0.55);
    }

    /// <summary>The cornice along the top: a cap standing proud, a cove, a bead, and a frieze with the brass name plate.</summary>
    void DrawCornice(DrawingContext ctx, double w, double ov, double cap, double y0)
    {
        double cove = Math.Round(cap * (_small ? 0.8 : 1.0)), bead = _small ? 2 : 3;
        var capRect = new Rect(0, y0, w, cap);
        ctx.DrawRectangle(null, null, capRect, 2, 2, new BoxShadows(new BoxShadow { OffsetY = 3, Blur = 6, Color = Palette.ShadowOf(0.35) }));
        ctx.DrawRectangle(Vertical((FrameLight, 0), (FrameMid, 1)), null, capRect, 2, 2);
        using (ctx.PushClip(new RoundedRect(capRect, 2))) WoodBoard(ctx, capRect, upright: false, 0.6);
        ctx.FillRectangle(new ImmutableSolidColorBrush(Sheen, 0.75), new Rect(2, y0 + 0.5, w - 4, 1.4));
        ctx.FillRectangle(new ImmutableSolidColorBrush(Palette.ShadowOf(0.35)), new Rect(0, y0 + cap - 1.2, w, 1.2));
        var coveRect = new Rect(ov * 0.5, y0 + cap, w - ov, cove);
        ctx.FillRectangle(Vertical((FrameDeep, 0), (FrameDark, 0.45), (FrameLight, 1)), coveRect);
        var beadRect = new Rect(ov * 0.7, y0 + cap + cove, w - ov * 1.4, bead);
        ctx.FillRectangle(Vertical((Sheen, 0), (FrameMid, 1)), beadRect);
        var frieze = new Rect(ov, y0 + cap + cove + bead, w - 2 * ov, Top - y0 - cap - cove - bead);
        ctx.FillRectangle(Vertical((FrameMid, 0), (FrameDark, 1)), frieze);
        WoodBoard(ctx, frieze, upright: false, 0.7);
        ctx.FillRectangle(new ImmutableSolidColorBrush(Palette.ShadowOf(0.3)), new Rect(frieze.X, frieze.Y, frieze.Width, 1.2));
        if (frieze.Height > 12) BrassPlate(ctx, new Point(w / 2, frieze.Y + frieze.Height / 2), Math.Min(frieze.Height - 6, _s.Plate), Plate, w * 0.7);
    }

    /// <summary>A brass plate with a fine engraved border and two pins, like a library shelf label, with words engraved on it.</summary>
    static void BrassPlate(DrawingContext ctx, Point centre, double height, string text, double widest)
    {
        double size = Math.Round(height * 0.56);
        FormattedText? t = null;
        double width = height * 4.2;
        if (!string.IsNullOrEmpty(text))
        {
            t = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(Palette.Heading, FontStyle.Normal, FontWeight.Bold), size, new ImmutableSolidColorBrush(Color.Parse("#3d2c08")));
            t.MaxTextWidth = widest - height * 1.6;
            t.MaxLineCount = 1;
            t.Trimming = TextTrimming.CharacterEllipsis;
            width = Math.Max(width, t.WidthIncludingTrailingWhitespace + height * 1.6);
        }
        var r = new Rect(centre.X - width / 2, centre.Y - height / 2, width, height);
        Color light = Color.Parse("#f3df9e"), mid = Color.Parse("#d9b45a"), deep = Color.Parse("#a8812f");
        ctx.DrawRectangle(Vertical((light, 0), (mid, 0.5), (deep, 1)), new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#7a5a1c")), 1),
            r, height * 0.18, height * 0.18, new BoxShadows(new BoxShadow { OffsetY = 1.5, Blur = 3, Color = Palette.ShadowOf(0.45) }));
        ctx.DrawRectangle(null, new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#8c6a24"), 0.8), 0.9), r.Deflate(height * 0.14), height * 0.1, height * 0.1);
        ctx.FillRectangle(new ImmutableSolidColorBrush(Colors.White, 0.45), new Rect(r.X + height * 0.3, r.Y + 1.2, width - height * 0.6, 1));
        var pin = new ImmutableSolidColorBrush(Color.Parse("#8c6a24"));
        ctx.DrawEllipse(pin, null, new Point(r.X + height * 0.36, centre.Y), height * 0.08, height * 0.08);
        ctx.DrawEllipse(pin, null, new Point(r.Right - height * 0.36, centre.Y), height * 0.08, height * 0.08);
        if (t is not null) ctx.DrawText(t, new Point(centre.X - t.WidthIncludingTrailingWhitespace / 2, centre.Y - t.Height / 2));
    }

    /// <summary>The plinth: a moulded top, a deep board, and two turned bun feet.</summary>
    void DrawPlinth(DrawingContext ctx, double w, double ov, double top, double height, double feet)
    {
        double footW = Math.Max(16, _s.Side * 1.3);
        foreach (double cx in new[] { ov + footW * 0.7, w - ov - footW * 0.7 })
        {
            var foot = new Rect(cx - footW / 2, top + height - 2, footW, feet + 2);
            ctx.DrawRectangle(Vertical((FrameMid, 0), (FrameDeep, 1)), null, foot, footW * 0.45, feet * 0.6,
                new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 4, Color = Palette.ShadowOf(0.4) }));
        }
        var moulding = new Rect(ov * 0.4, top, w - ov * 0.8, Math.Round(height * 0.38));
        var board = new Rect(0, moulding.Bottom, w, height - moulding.Height);
        ctx.DrawRectangle(Vertical((FrameMid, 0), (FrameDark, 1)), null, board, 2, 2);
        using (ctx.PushClip(new RoundedRect(board, 2))) WoodBoard(ctx, board, upright: false, 0.55);
        ctx.FillRectangle(new ImmutableSolidColorBrush(Sheen, 0.55), new Rect(2, board.Y, w - 4, 1.2));
        ctx.FillRectangle(Vertical((Sheen, 0), (FrameLight, 0.4), (FrameDark, 1)), moulding);
    }

    /// <param name="downTo">The bottom shelf: its front runs down to the plinth as one deep board.</param>
    void DrawShelf(DrawingContext ctx, int shelf, Rect inner, double? downTo = null)
    {
        double y = StandY(shelf);
        double topY = y - _s.PlankTop / 2, frontY = y + _s.PlankTop / 2, frontH = downTo is double d ? d - frontY : _s.PlankFront;
        double x0 = inner.X, x1 = inner.Right;

        var topFace = new Rect(x0, topY, x1 - x0, _s.PlankTop);
        ctx.FillRectangle(Vertical((Palette.WoodDark, 0), (Palette.Wood, 0.45), (Palette.WoodLight, 1)), topFace);
        ctx.FillRectangle(Vertical((Palette.ShadowOf(0.16), 0), (Palette.ShadowOf(0), 1)), new Rect(x0, topY, x1 - x0, _s.PlankTop * 0.45));

        var front = new Rect(x0 - 3, frontY, x1 - x0 + 6, frontH);
        using (ctx.PushClip(new RoundedRect(front, 3)))
        {
            ctx.FillRectangle(Palette.Brush(Palette.Wood), front);
            using (ctx.PushOpacity(Palette.Current.Grain))
                ctx.DrawImage(ShelfArt.Wood, new Rect(0, 0, ShelfArt.Wood.PixelSize.Width, ShelfArt.Wood.PixelSize.Height), front);
        }
        ctx.DrawRectangle(Vertical((Color.FromArgb(60, Sheen.R, Sheen.G, Sheen.B), 0), (Color.FromArgb(0, Sheen.R, Sheen.G, Sheen.B), 0.3), (Palette.ShadowOf(0), 0.6), (Palette.ShadowOf(0.25), 1)), null, front, 3, 3);
        ctx.DrawLine(new ImmutablePen(new ImmutableSolidColorBrush(Sheen, 0.8), 1.2), new Point(front.X + 2, frontY + 0.6), new Point(front.Right - 2, frontY + 0.6));

        // The shadow the shelf throws on the painted back below it.
        if (downTo is null) ctx.FillRectangle(Vertical((Palette.ShadowOf(0.16), 0), (Palette.ShadowOf(0.04), 0.4), (Palette.ShadowOf(0), 1)), new Rect(x0, frontY + frontH, x1 - x0, _small ? 10 : 16));
    }
}
