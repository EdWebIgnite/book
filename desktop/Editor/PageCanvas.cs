using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Editor.Core;
using Reader.Core;

namespace BookEditor;

/// <summary>What the page view shows on top of the picture.</summary>
sealed class Overlays
{
    /// <summary>The middle of a spread and the two pages either side of it.</summary>
    public bool Crease { get; set; } = true;
    /// <summary>What each chosen screen shows around the selected stop.</summary>
    public HashSet<string> Frames { get; } = new() { "s22-upright", "s22-sideways" };
}

/// <summary>
/// The page being edited, with its stops, things to find, pictures on top and rain-free areas drawn
/// over it. Drag on an empty part to draw something new; drag a box to move it, a corner to resize it.
/// Hold Shift to draw a new box over an old one. Ctrl and the mouse wheel zoom, and dragging with the
/// middle button (or with Space held) moves the view.
/// </summary>
sealed class PageCanvas : Control
{
    const double Margin_ = 28;
    const double Handle = 9;
    const double Min = 0.01;

    readonly EditorState _state;
    readonly Images _images;
    public Overlays Overlays { get; } = new();

    // The view: zoom 1 fits the page; the pan moves it.
    double _zoom = 1;
    Vector _pan;
    Rect _pageRect;

    enum Mode { None, New, Move, Resize, Pan }
    sealed class Drag
    {
        public Mode Mode;
        public Sel Kind;
        public int Index;
        public int Effect = -1;
        public string Part = "box";        // box, to, moveTo
        public string Handle = "";
        public Point Start;                // page fractions
        public Point StartScreen;
        public RectD Orig;
        public Point? Current;
        public bool Moved;
        public Vector PanStart;
    }
    Drag? _drag;
    bool _space;

    public PageCanvas(EditorState state, Images images)
    {
        _state = state;
        _images = images;
        Focusable = true;
        ClipToBounds = true;
        _images.Arrived += InvalidateVisual;
    }

    /// <summary>Back to the whole page in view.</summary>
    public void Fit()
    {
        _zoom = 1;
        _pan = default;
        InvalidateVisual();
    }

    Page? Page => _state.Page;

    // ---------------------------------------------------------------- geometry

    Rect PageRect()
    {
        Page? p = Page;
        if (p is null) return default;
        double aspect = _state.Aspect(p);
        double w = Bounds.Width - 2 * Margin_, h = Bounds.Height - 2 * Margin_ - 30;
        if (w <= 0 || h <= 0) return default;
        double s = Math.Min(w, h * aspect);
        double pw = s * _zoom, ph = s / aspect * _zoom;
        double cx = Bounds.Width / 2 + _pan.X, cy = (Bounds.Height - 30) / 2 + _pan.Y;
        return new Rect(cx - pw / 2, cy - ph / 2, pw, ph);
    }

    Rect ToScreen(RectD b) => new(_pageRect.X + b.X * _pageRect.Width, _pageRect.Y + b.Y * _pageRect.Height, b.W * _pageRect.Width, b.H * _pageRect.Height);
    Point ToPage(Point p) => new((p.X - _pageRect.X) / _pageRect.Width, (p.Y - _pageRect.Y) / _pageRect.Height);

    static RectD Norm(Point a, Point b, bool clamp = true)
    {
        double x0 = Math.Min(a.X, b.X), y0 = Math.Min(a.Y, b.Y), x1 = Math.Max(a.X, b.X), y1 = Math.Max(a.Y, b.Y);
        if (clamp) { x0 = Math.Clamp(x0, 0, 1); y0 = Math.Clamp(y0, 0, 1); x1 = Math.Clamp(x1, 0, 1); y1 = Math.Clamp(y1, 0, 1); }
        return new RectD(x0, y0, x1 - x0, y1 - y0);
    }

    // ---------------------------------------------------------------- drawing

    static readonly Typeface Bold = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
    static readonly Typeface Plain = new(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);
    static readonly Color Gold = Color.Parse("#ffd23f"), GoldDark = Color.Parse("#a37a00"), Dry = Color.Parse("#7fd3f2"), LayerColour = Color.Parse("#b08cff");

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Ui.Stage, new Rect(Bounds.Size));
        Page? p = Page;
        _pageRect = PageRect();
        if (p is null || _pageRect.Width <= 0) return;

        string? path = _state.Project!.PathOf(p.Image);
        Bitmap? picture = _images.Get(path, 2400);
        if (picture is not null) ctx.DrawImage(picture, new Rect(picture.Size), _pageRect);
        else
        {
            ctx.FillRectangle(Ui.Raised, _pageRect);
            DrawText(ctx, path is not null && File.Exists(path) ? "Loading the picture..." : "This page's picture is missing: " + p.Image, _pageRect.Center - new Vector(120, 8), 14, Brushes.White, Plain);
        }
        double aspect = _state.Aspect(p);

        // The crease and the two pages of a spread.
        if (Overlays.Crease && aspect >= 1.2 && !p.Explore)
        {
            double x = _pageRect.X + _pageRect.Width / 2;
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 1.5, new DashStyle(new double[] { 4, 4 }, 0));
            ctx.DrawLine(pen, new Point(x, _pageRect.Y), new Point(x, _pageRect.Bottom));
            DrawTag(ctx, "left page", new Point(_pageRect.X + 8, _pageRect.Bottom - 26), Color.FromArgb(170, 20, 22, 27));
            DrawTag(ctx, "right page", new Point(x + 8, _pageRect.Bottom - 26), Color.FromArgb(170, 20, 22, 27));
        }

        // Pictures on top.
        for (int i = 0; i < p.Layers.Count; i++)
        {
            Layer l = p.Layers[i];
            bool sel = _state.Selected == Sel.Layer && _state.SelIndex == i;
            Rect r = ToScreen(l.Box);
            Bitmap? img = _images.Get(_state.Project.PathOf(l.Image), 900);
            if (img is not null) ctx.DrawImage(img, new Rect(img.Size), r);
            if (sel || img is null) ctx.DrawRectangle(new Pen(new SolidColorBrush(LayerColour), sel ? 3 : 1.5), r);
            if (!sel) continue;
            DrawTag(ctx, "picture " + (i + 1), r.TopLeft + new Vector(4, 4), Color.Parse("#7a4fd6"));
            Handles(ctx, r, LayerColour);
            if (l.MoveTo is RectD mt)
            {
                Rect t = ToScreen(mt);
                if (img is not null) using (ctx.PushOpacity(0.45)) ctx.DrawImage(img, new Rect(img.Size), t);
                ctx.DrawRectangle(new Pen(new SolidColorBrush(LayerColour), 2.5, new DashStyle(new double[] { 3.5, 2.5 }, 0)), t);
                DrawTag(ctx, "moves to", t.TopLeft + new Vector(4, 4), Color.Parse("#7a4fd6"));
                Arrow(ctx, r.Center, t.Center, LayerColour);
            }
        }

        // Stops, in order, each in its own colour.
        for (int i = 0; i < p.Stops.Count; i++)
        {
            Stop s = p.Stops[i];
            Color c = Ui.StopColour(i);
            bool sel = _state.Selected == Sel.Stop && _state.SelIndex == i;
            Rect r = ToScreen(s.Box);
            if (sel) ctx.FillRectangle(new SolidColorBrush(c, 0.15), r);
            ctx.DrawRectangle(new Pen(new SolidColorBrush(c), sel ? 4 : 2.5), r);
            DrawTag(ctx, (i + 1).ToString(CultureInfo.InvariantCulture) + (s.Voice is not null ? "  ♪" : ""), r.TopLeft + new Vector(4, 4), c);
            if (s.To is RectD to)
            {
                Rect t = ToScreen(to);
                ctx.DrawRectangle(new Pen(new SolidColorBrush(c), sel ? 3 : 2, new DashStyle(new double[] { 4, 2.5 }, 0)), t);
                Arrow(ctx, r.Center, t.Center, c);
                DrawTag(ctx, $"{i + 1} end", t.TopLeft + new Vector(4, 4), c);
                if (sel) Handles(ctx, t, c);
            }
            if (sel) Handles(ctx, r, c);
        }

        // Areas the rain or snow misses.
        for (int e = 0; e < p.Effects.Count; e++)
        {
            for (int d = 0; d < p.Effects[e].Dry.Count; d++)
            {
                Rect r = ToScreen(p.Effects[e].Dry[d]);
                bool sel = _state.Selected == Sel.Dry && _state.SelEffect == e && _state.SelIndex == d;
                ctx.DrawRectangle(new Pen(new SolidColorBrush(Dry), sel ? 3 : 2, new DashStyle(new double[] { 1, 2.5 }, 0)), r);
                DrawTag(ctx, "dry", r.TopLeft + new Vector(4, 4), Color.Parse("#2a7f9e"));
                if (sel) Handles(ctx, r, Dry);
            }
        }

        // Things to find.
        if (p.Explore)
        {
            for (int i = 0; i < p.Hotspots.Count; i++)
            {
                Hotspot h = p.Hotspots[i];
                bool sel = _state.Selected == Sel.Hotspot && _state.SelIndex == i;
                Rect r = ToScreen(h.Box);
                if (sel) ctx.FillRectangle(new SolidColorBrush(Gold, 0.2), r);
                ctx.DrawRectangle(new Pen(new SolidColorBrush(Gold), sel ? 4 : 2.5, new DashStyle(new double[] { 1.5, 2 }, 0)), r);
                // Names only on the selected one: a busy scene would be covered in labels otherwise.
                string label = (h.Story > 0 ? "step " + h.Story : "★" + (i + 1)) + (sel && !string.IsNullOrEmpty(h.Name) ? "  " + h.Name : "");
                DrawTag(ctx, label, r.TopLeft + new Vector(4, 4), GoldDark);
                if (sel) Handles(ctx, r, Gold);
            }
        }

        // What each chosen screen shows around the selected stop.
        if (_state.Stop is Stop stop)
        {
            int n = 0;
            foreach (Device d in Device.All.Where(d => Overlays.Frames.Contains(d.Id)))
            {
                foreach (RectD part in stop.To is RectD to ? new[] { stop.Box, to } : new[] { stop.Box })
                {
                    RectD area = Framing.Visible(part, aspect, d, 0.025, caption: !string.IsNullOrEmpty(stop.Caption));
                    Rect v = ToScreen(area);
                    Color fc = FrameColours[n % FrameColours.Length];
                    ctx.DrawRectangle(new Pen(new SolidColorBrush(fc, 0.95), 2, new DashStyle(new double[] { 2, 2.5 }, 0)), v);
                    // The name goes inside the frame's top-left corner, kept on the picture so it can be read.
                    Rect seen = v.Intersect(_pageRect);
                    if (part.Equals(stop.Box)) FrameLabel(ctx, ShortName(d), new Point(seen.Right - 6, seen.Y + 4 + 22 * n), fc);
                }
                n++;
            }
            // An upright phone shows one page of the spread when the page is seen whole.
            if (Overlays.Frames.Any(id => Device.Find(id).Phone && Device.Find(id).Upright) && aspect >= 1.2 && !p.Explore)
            {
                int half = Framing.HalfOf(stop.Box, aspect);
                Rect other = half == 1
                    ? new Rect(_pageRect.X + _pageRect.Width / 2, _pageRect.Y, _pageRect.Width / 2, _pageRect.Height)
                    : new Rect(_pageRect.X, _pageRect.Y, _pageRect.Width / 2, _pageRect.Height);
                ctx.FillRectangle(new SolidColorBrush(Colors.Black, 0.18), other);
            }
        }

        // The box being drawn.
        if (_drag is { Mode: Mode.New, Current: Point cur })
        {
            Rect r = ToScreen(Norm(_drag.Start, cur));
            Color c = _state.Drawing switch { DrawKind.Hotspot => Gold, DrawKind.Dry => Dry, _ => Colors.White };
            ctx.DrawRectangle(new Pen(new SolidColorBrush(c), 2, new DashStyle(new double[] { 3, 2 }, 0)), r);
        }
    }

    static readonly Color[] FrameColours = { Color.Parse("#ffffff"), Color.Parse("#8ce99a"), Color.Parse("#74c0fc"), Color.Parse("#ffd8a8") };

    static string ShortName(Device d) => d.Id switch
    {
        "window" => "computer",
        "s22-upright" => "phone upright",
        "s22-sideways" => "phone sideways",
        "cheap-upright" => "small phone upright",
        "cheap-sideways" => "small phone sideways",
        "tablet-sideways" => "tablet sideways",
        "tablet-upright" => "tablet upright",
        _ => d.Name,
    };

    static void DrawText(DrawingContext ctx, string text, Point at, double size, IBrush brush, Typeface face)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush);
        ctx.DrawText(ft, at);
    }

    /// <summary>A screen frame's name: its colour on a dark pill, right-aligned to <paramref name="topRight"/>.</summary>
    static void FrameLabel(DrawingContext ctx, string text, Point topRight, Color colour)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Bold, 11.5, new SolidColorBrush(colour));
        double w = ft.Width + 12;
        ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 14, 15, 19)), null, new Rect(topRight.X - w, topRight.Y, w, 19), 9.5, 9.5);
        ctx.DrawText(ft, new Point(topRight.X - w + 6, topRight.Y + (19 - ft.Height) / 2));
    }

    static void DrawTag(DrawingContext ctx, string text, Point at, Color colour)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Bold, 12.5, Brushes.White);
        double w = Math.Max(22, ft.Width + 12);
        ctx.DrawRectangle(new SolidColorBrush(colour), null, new Rect(at.X, at.Y, w, 22), 11, 11);
        ctx.DrawText(ft, new Point(at.X + (w - ft.Width) / 2, at.Y + (22 - ft.Height) / 2));
    }

    static void Handles(DrawingContext ctx, Rect r, Color colour)
    {
        var pen = new Pen(new SolidColorBrush(colour), 2);
        foreach (Point p in new[] { r.TopLeft, r.TopRight, r.BottomLeft, r.BottomRight })
            ctx.DrawRectangle(Brushes.White, pen, new Rect(p.X - 6, p.Y - 6, 12, 12));
    }

    static void Arrow(DrawingContext ctx, Point a, Point b, Color colour)
    {
        var brush = new SolidColorBrush(colour);
        ctx.DrawLine(new Pen(brush, 3), a, b);
        double ang = Math.Atan2(b.Y - a.Y, b.X - a.X);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(b, true);
            g.LineTo(new Point(b.X - 14 * Math.Cos(ang - 0.4), b.Y - 14 * Math.Sin(ang - 0.4)));
            g.LineTo(new Point(b.X - 14 * Math.Cos(ang + 0.4), b.Y - 14 * Math.Sin(ang + 0.4)));
            g.EndFigure(true);
        }
        ctx.DrawGeometry(brush, null, geo);
    }

    // ---------------------------------------------------------------- finding what's under the mouse

    sealed record Hit(Sel Kind, int Index, string Part, string Handle, int Effect = -1);

    Hit? HitTest(Point pt)
    {
        Page? p = Page;
        if (p is null) return null;
        string Corner(Rect r)
        {
            foreach (var (h, c) in new[] { ("nw", r.TopLeft), ("ne", r.TopRight), ("sw", r.BottomLeft), ("se", r.BottomRight) })
                if (Math.Abs(pt.X - c.X) <= Handle && Math.Abs(pt.Y - c.Y) <= Handle) return h;
            return "";
        }
        // The selected thing's corners come first, so they can always be grabbed.
        if (_state.Layer is Layer sl)
        {
            string h = Corner(ToScreen(sl.Box));
            if (h.Length > 0) return new Hit(Sel.Layer, _state.SelIndex, "box", h);
            foreach (string part in sl.MoveTo is not null ? new[] { "moveTo", "box" } : new[] { "box" })
                if (ToScreen(part == "box" ? sl.Box : sl.MoveTo!.Value).Contains(pt)) return new Hit(Sel.Layer, _state.SelIndex, part, "");
        }
        if (_state.Stop is Stop ss)
        {
            foreach (string part in ss.To is not null ? new[] { "to", "box" } : new[] { "box" })
            {
                string h = Corner(ToScreen(part == "box" ? ss.Box : ss.To!.Value));
                if (h.Length > 0) return new Hit(Sel.Stop, _state.SelIndex, part, h);
            }
        }
        if (_state.Hotspot is Hotspot sh)
        {
            string h = Corner(ToScreen(sh.Box));
            if (h.Length > 0) return new Hit(Sel.Hotspot, _state.SelIndex, "box", h);
        }
        if (_state.Selected == Sel.Dry && _state.SelEffect >= 0 && _state.SelEffect < p.Effects.Count && _state.SelIndex < p.Effects[_state.SelEffect].Dry.Count)
        {
            string h = Corner(ToScreen(p.Effects[_state.SelEffect].Dry[_state.SelIndex]));
            if (h.Length > 0) return new Hit(Sel.Dry, _state.SelIndex, "box", h, _state.SelEffect);
        }

        // Otherwise the smallest box under the mouse.
        Hit? best = null;
        double bestArea = double.MaxValue;
        void Consider(RectD b, Hit hit, bool skipWhole = false)
        {
            if (skipWhole && b.W > 0.95 && b.H > 0.95) return;     // whole-page stops are picked from the strip instead
            Rect r = ToScreen(b);
            if (!r.Contains(pt)) return;
            double area = r.Width * r.Height;
            if (area < bestArea) { bestArea = area; best = hit; }
        }
        if (p.Explore) for (int i = 0; i < p.Hotspots.Count; i++) Consider(p.Hotspots[i].Box, new Hit(Sel.Hotspot, i, "box", ""));
        for (int i = 0; i < p.Stops.Count; i++)
        {
            Consider(p.Stops[i].Box, new Hit(Sel.Stop, i, "box", ""), true);
            if (p.Stops[i].To is RectD to) Consider(to, new Hit(Sel.Stop, i, "to", ""), true);
        }
        for (int e = 0; e < p.Effects.Count; e++)
            for (int d = 0; d < p.Effects[e].Dry.Count; d++) Consider(p.Effects[e].Dry[d], new Hit(Sel.Dry, d, "box", "", e));
        return best;
    }

    RectD Get(Sel kind, int index, string part, int effect)
    {
        Page p = Page!;
        return kind switch
        {
            Sel.Stop => part == "to" ? p.Stops[index].To!.Value : p.Stops[index].Box,
            Sel.Hotspot => p.Hotspots[index].Box,
            Sel.Layer => part == "moveTo" ? p.Layers[index].MoveTo!.Value : p.Layers[index].Box,
            Sel.Dry => p.Effects[effect].Dry[index],
            _ => default,
        };
    }

    void Set(Sel kind, int index, string part, int effect, RectD r)
    {
        Page p = Page!;
        switch (kind)
        {
            case Sel.Stop when part == "to": p.Stops[index].To = r; break;
            case Sel.Stop: p.Stops[index].Box = r; break;
            case Sel.Hotspot: p.Hotspots[index].Box = r; break;
            case Sel.Layer when part == "moveTo": p.Layers[index].MoveTo = r; break;
            case Sel.Layer: p.Layers[index].Box = r; break;
            case Sel.Dry: p.Effects[effect].Dry[index] = r; break;
        }
    }

    // ---------------------------------------------------------------- the mouse

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (Page is null) return;
        PointerPoint pp = e.GetCurrentPoint(this);
        Point at = pp.Position;
        if (pp.Properties.IsMiddleButtonPressed || (_space && pp.Properties.IsLeftButtonPressed))
        {
            _drag = new Drag { Mode = Mode.Pan, StartScreen = at, PanStart = _pan };
            e.Pointer.Capture(this);
            return;
        }
        if (!pp.Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);
        Point pt = ToPage(at);
        Hit? hit = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? null : HitTest(at);
        if (hit is not null)
        {
            if (_state.Selected != hit.Kind || _state.SelIndex != hit.Index || _state.SelEffect != hit.Effect) _state.Select(hit.Kind, hit.Index, hit.Effect);
            _drag = new Drag
            {
                Mode = hit.Handle.Length > 0 ? Mode.Resize : Mode.Move, Kind = hit.Kind, Index = hit.Index, Part = hit.Part, Handle = hit.Handle,
                Effect = hit.Effect, Start = pt, StartScreen = at, Orig = Get(hit.Kind, hit.Index, hit.Part, hit.Effect),
            };
        }
        else
        {
            _drag = new Drag { Mode = Mode.New, Start = pt, StartScreen = at };
        }
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point at = e.GetPosition(this);
        if (_drag is null)
        {
            Hit? hit = Page is null ? null : HitTest(at);
            Cursor = _space ? new Cursor(StandardCursorType.Hand)
                : hit is null ? new Cursor(StandardCursorType.Cross)
                : hit.Handle is "nw" or "se" ? new Cursor(StandardCursorType.TopLeftCorner)
                : hit.Handle is "ne" or "sw" ? new Cursor(StandardCursorType.TopRightCorner)
                : new Cursor(StandardCursorType.SizeAll);
            return;
        }
        if (_drag.Mode == Mode.Pan)
        {
            _pan = _drag.PanStart + (at - _drag.StartScreen);
            InvalidateVisual();
            return;
        }
        Point pt = ToPage(at);
        if (_drag.Mode == Mode.New) { _drag.Current = pt; InvalidateVisual(); return; }
        double dx = pt.X - _drag.Start.X, dy = pt.Y - _drag.Start.Y;
        if (!_drag.Moved)
        {
            if (Math.Abs(at.X - _drag.StartScreen.X) + Math.Abs(at.Y - _drag.StartScreen.Y) < 3) return;
            _drag.Moved = true;
            _state.BeginDrag(_drag.Mode == Mode.Move ? "Move" : "Resize");
        }
        RectD o = _drag.Orig;
        RectD next;
        if (_drag.Mode == Mode.Move)
        {
            // A picture on top may sit partly, or wholly, off the page: that's how something comes in from the side.
            bool off = _drag.Kind == Sel.Layer;
            next = new RectD(Math.Clamp(o.X + dx, off ? -o.W : 0, off ? 1 : 1 - o.W), Math.Clamp(o.Y + dy, off ? -o.H : 0, off ? 1 : 1 - o.H), o.W, o.H);
        }
        else if (_drag.Kind == Sel.Layer)
        {
            // Pictures keep their shape: the width follows the mouse and the height follows the width.
            double shape = o.H / Math.Max(1e-6, o.W);
            double w = Math.Max(Min, _drag.Handle.Contains('w') ? o.W - dx : o.W + dx), h = w * shape;
            next = new RectD(_drag.Handle.Contains('w') ? o.X + o.W - w : o.X, _drag.Handle.Contains('n') ? o.Y + o.H - h : o.Y, w, h);
            Layer l = Page!.Layers[_drag.Index];
            if (l.MoveTo is RectD mt && _drag.Part == "box") l.MoveTo = mt with { W = w, H = h };
        }
        else
        {
            double x0 = o.X, y0 = o.Y, x1 = o.Right, y1 = o.Bottom;
            if (_drag.Handle.Contains('w')) x0 += dx; else x1 += dx;
            if (_drag.Handle.Contains('n')) y0 += dy; else y1 += dy;
            next = Norm(new Point(x0, y0), new Point(x1, y1));
            if (next.W < Min || next.H < Min) return;
        }
        Set(_drag.Kind, _drag.Index, _drag.Part, _drag.Effect, next);
        _state.Touched(this);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Drag? d = _drag;
        _drag = null;
        e.Pointer.Capture(null);
        if (d is null || Page is not Page p) return;
        if (d.Mode == Mode.New)
        {
            RectD r = d.Current is Point cur ? Norm(d.Start, cur) : default;
            if (r.W > Min && r.H > Min) AddBox(p, r);
            else if (_state.Selected != Sel.None) _state.Select(Sel.None, -1);
            InvalidateVisual();
            return;
        }
        if (d.Moved) _state.Touched(null);    // let the lists catch up now the drag is over
        InvalidateVisual();
    }

    /// <summary>A box was drawn on an empty part of the page: make whatever the page is drawing now.</summary>
    void AddBox(Page p, RectD r)
    {
        if (_state.Drawing == DrawKind.Dry && _state.DryTarget >= 0 && _state.DryTarget < p.Effects.Count)
        {
            int fx = _state.DryTarget;
            _state.Change("Add an area it misses", () => p.Effects[fx].Dry.Add(r), structure: true);
            _state.Drawing = DrawKind.Stop;
            _state.DryTarget = -1;
            _state.Select(Sel.Dry, p.Effects[fx].Dry.Count - 1, fx);
        }
        else if (_state.Drawing == DrawKind.Hotspot && p.Explore)
        {
            _state.Change("Add a thing to find", () => p.Hotspots.Add(new Hotspot { Box = r }), structure: true);
            _state.Select(Sel.Hotspot, p.Hotspots.Count - 1);
        }
        else
        {
            int at = _state.Selected == Sel.Stop ? _state.SelIndex + 1 : p.Stops.Count;
            _state.Change("Add a stop", () => p.Stops.Insert(at, new Stop { Box = r }), structure: true);
            _state.Select(Sel.Stop, at);
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || Page is null) return;
        Point at = e.GetPosition(this);
        double old = _zoom;
        _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.2 : 1 / 1.2), 1, 8);
        // Keep the point under the mouse where it is.
        Point centre = new(Bounds.Width / 2 + _pan.X, (Bounds.Height - 30) / 2 + _pan.Y);
        Vector fromCentre = at - centre;
        _pan += fromCentre - fromCentre * (_zoom / old);
        if (_zoom <= 1.001) _pan = default;
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Space && !_space) { _space = true; Cursor = new Cursor(StandardCursorType.Hand); e.Handled = true; return; }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space) { _space = false; Cursor = Cursor.Default; e.Handled = true; return; }
        base.OnKeyUp(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateVisual();
    }
}
