using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Reader.Core;

namespace Reader.Ui;

/// <summary>
/// The picture area of the reader. It owns a <see cref="ReaderEngine"/>, moves it on once a frame,
/// draws what the engine says is on screen, and passes touches, the wheel and the keyboard on to it.
///
/// Drawing order matters and follows the browser reader: pages (with their sharp tiles), then the
/// pictures on top, then weather, all in the page's own space so they stay put while the camera
/// moves. Over those come the focus effects (soft edges, spotlight, dim), then things to find, the
/// hint, the reduced-motion fade, lightning, the corner map, the party and the touch ripples.
/// </summary>
public sealed class ReaderView : Control
{
    static readonly Color Backdrop = Color.Parse("#1c1b22");
    static readonly IImmutableBrush BackdropBrush = new ImmutableSolidColorBrush(Backdrop);
    static readonly IImmutableBrush White = new ImmutableSolidColorBrush(Colors.White);

    readonly Stopwatch _watch = Stopwatch.StartNew();
    readonly PictureCache _tiles = new();
    readonly PictureCache _pictures = new(keep: 48, drop: 12);
    readonly BookFolderSound _sound;
    readonly HashSet<int> _down = new();
    readonly Dictionary<uint, IImmutableBrush> _brushes = new();
    LoadedBook? _book;
    bool _frameAsked, _attached, _wasRestless;

    public ReaderView(ReaderSettings settings, ISoundOutput? sound = null)
    {
        _sound = new BookFolderSound(sound ?? new NoSound());
        Engine = new ReaderEngine(settings, _sound);
        Gestures = new GestureInput(Engine);
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
        // A cut to another stop changes the picture without anything being in motion, so draw on every change too.
        Engine.Changed += _ => InvalidateVisual();
        _tiles.Arrived += InvalidateVisual;
        _pictures.Arrived += InvalidateVisual;
        AddHandler(PointerTouchPadGestureMagnifyEvent, OnMagnify);
    }

    /// <summary>The reader itself. Use <see cref="Do"/> to act on it, so it knows what time it is.</summary>
    public ReaderEngine Engine { get; }

    public GestureInput Gestures { get; }

    /// <summary>
    /// Where the time comes from, in milliseconds. Left alone, it is a stopwatch started with the view.
    /// Screenshots set their own so that the same command always draws the same picture.
    /// </summary>
    public Func<double>? Clock { get; set; }

    /// <summary>False stops the view from moving itself on every frame; call <see cref="Tick"/> by hand instead.</summary>
    public bool AutoFrames { get; set; } = true;

    /// <summary>Raised once a frame after the engine has moved on, with the time in milliseconds.</summary>
    public event Action<double>? FrameTick;

    /// <summary>A finger or the mouse went down on the picture.</summary>
    public event Action? PicturePressed;

    public double Now => Clock?.Invoke() ?? _watch.Elapsed.TotalMilliseconds;

    public LoadedBook? Book => _book;

    /// <summary>Tiles and layer pictures still loading. Screenshots wait for this to reach zero.</summary>
    public int PicturesLoading => _tiles.Loading + _pictures.Loading;

    /// <summary>Load tiles and layer pictures on the spot instead of in the background. For screenshots.</summary>
    public bool LoadPicturesAtOnce
    {
        get => _tiles.Synchronous;
        set => _tiles.Synchronous = _pictures.Synchronous = value;
    }

    /// <summary>Do something to the engine, first telling it the time.</summary>
    public void Do(Action<ReaderEngine> action)
    {
        Engine.SetTime(Now);
        action(Engine);
        InvalidateVisual();
    }

    /// <summary>Show a book. The caller keeps ownership of it and disposes it after <see cref="ClearBook"/>.</summary>
    public void SetBook(LoadedBook book, int startStep = 0)
    {
        _tiles.Clear();
        _pictures.Clear();
        _book = book;
        _sound.Folder = book.Folder;
        Engine.SetTime(Now);
        Engine.SetViewSize(Bounds.Width, Bounds.Height);
        Engine.SetBook(book.Book, book.Aspects, startStep);
        InvalidateVisual();
        AskFrame();
    }

    /// <summary>Stop showing the book and go quiet.</summary>
    public void ClearBook()
    {
        Engine.ClearBook();
        _book = null;
        _sound.Folder = null;
        _tiles.Clear();
        _pictures.Clear();
        InvalidateVisual();
    }

    // ---------------------------------------------------------------- the frame clock

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        AskFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Engine.Visible = false;
        Engine.Hush();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Engine.SetViewSize(e.NewSize.Width, e.NewSize.Height);
        InvalidateVisual();
    }

    void AskFrame()
    {
        if (_frameAsked || !_attached || !AutoFrames) return;
        if (TopLevel.GetTopLevel(this) is not TopLevel top) return;
        _frameAsked = true;
        top.RequestAnimationFrame(_ =>
        {
            _frameAsked = false;
            if (!_attached || !AutoFrames) return;
            Tick();
            AskFrame();
        });
    }

    /// <summary>One frame: move the engine on to now and ask to be redrawn.</summary>
    public void Tick()
    {
        // While the reader is hidden nothing moves on and nothing makes a noise.
        bool showing = IsEffectivelyVisible && _book is not null;
        if (showing != Engine.Visible)
        {
            Engine.Visible = showing;
            if (!showing) Engine.Hush();
        }
        if (!showing) return;
        double now = Now;
        Engine.SetViewSize(Bounds.Width, Bounds.Height);
        Engine.Update(now);
        FrameTick?.Invoke(now);
        // Only draw again when something is moving, plus once more after it stops so the last frame is the
        // settled one. A child looking at a still page then costs the computer almost nothing.
        bool restless = Engine.Restless;
        if (restless || _wasRestless) InvalidateVisual();
        _wasRestless = restless;
    }

    // ---------------------------------------------------------------- input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        Point p = e.GetPosition(this);
        e.Pointer.Capture(this);
        _down.Add(e.Pointer.Id);
        PicturePressed?.Invoke();
        Gestures.PointerDown(e.Pointer.Id, p.X, p.Y, Now);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_down.Contains(e.Pointer.Id)) return;
        Point p = e.GetPosition(this);
        Gestures.PointerMove(e.Pointer.Id, p.X, p.Y, Now);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_down.Remove(e.Pointer.Id)) return;
        Point p = e.GetPosition(this);
        Gestures.PointerUp(e.Pointer.Id, p.X, p.Y, Now);
        e.Pointer.Capture(null);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        // Only a touch that was taken away mid-gesture counts; an ordinary release has already been dealt with.
        if (_down.Remove(e.Pointer.Id)) Gestures.PointerCancel(e.Pointer.Id);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Point p = e.GetPosition(this);
        // One notch is 1.0 here and about 100 in a browser, and "up" is positive here but negative there.
        bool zoomKey = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        Gestures.Wheel(p.X, p.Y, -e.Delta.X * 100, -e.Delta.Y * 100, zoomKey, smooth: true, Now);
        e.Handled = true;
        InvalidateVisual();
    }

    void OnMagnify(object? sender, PointerDeltaEventArgs e)
    {
        Point p = e.GetPosition(this);
        Gestures.Magnify(p.X, p.Y, e.Delta.X, Now);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && HandleKey(e.Key, e.KeyModifiers)) e.Handled = true;
    }

    /// <summary>Keyboard shortcuts. Returns true when the key was used.</summary>
    public bool HandleKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        if (_book is null) return false;
        if ((modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return false;
        ReaderKey? action = key switch
        {
            Key.Right or Key.Space or Key.PageDown => ReaderKey.Next,
            Key.Left or Key.PageUp => ReaderKey.Previous,
            Key.W or Key.Up => ReaderKey.ToggleWholePage,
            Key.Down => ReaderKey.CloseUp,
            Key.OemPlus or Key.Add => ReaderKey.ZoomIn,
            Key.OemMinus or Key.Subtract => ReaderKey.ZoomOut,
            _ => null,
        };
        if (action is not ReaderKey a) return false;
        Do(engine => engine.HandleKey(a));
        return true;
    }

    // ---------------------------------------------------------------- drawing

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
        if (book is null || !ReferenceEquals(e.Book, book.Book) || e.Steps.Count == 0 || w <= 0 || h <= 0) return;

        ViewTransform v = e.GetView();
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

        for (int i = 0; i < e.PageRects.Count; i++)
        {
            RectD pr = e.PageRects[i];
            RectD r = v.ToScreen(pr);
            if (r.Right < 0 || r.X > w) continue;
            Rect dest = ToRect(r);
            ctx.FillRectangle(White, dest);
            Bitmap image = book.PageImage(i);
            using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = Shrinking(image, r.W, scaling) }))
                ctx.DrawImage(image, dest);
            DrawTiles(ctx, book, i, v, scaling);
            if (r.Y > h || r.Bottom < 0) continue;
            DrawLayers(ctx, book, i, r);
            DrawWeather(ctx, book, i, pr, r);
        }

        Shot shown = e.Shown;
        RectD f = v.ToScreen(shown.Focus);
        if (shown.Vig > 0.01)
        {
            // Soft oval darkening that leaves the focus box clear.
            FillOval(ctx, all, f.CenterX, f.CenterY, Math.Max(f.W / 2, 1) * 2.6, Math.Max(f.H / 2, 1) * 2.6,
                1.25 / 2.6, Color.FromArgb(0, 0, 0, 0), Rgba(0, 0, 0, shown.Vig));
        }
        if (shown.Spot > 0.01)
        {
            // Spotlight: an oval of light a quarter bigger than the focus box, so what's inside the box is lit
            // (all but its corners). Soft at the rim, dark everywhere else.
            FillOval(ctx, all, f.CenterX, f.CenterY, Math.Max(f.W * 0.625, 1), Math.Max(f.H * 0.625, 1),
                0.8, Color.FromArgb(0, 10, 9, 16), Rgba(10, 9, 16, shown.Spot));
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
    }

    /// <summary>
    /// Shrinking a big page picture a long way looks best averaged (medium quality uses smaller copies
    /// of the picture); enlarging looks best with the smooth curve of high quality.
    /// </summary>
    static BitmapInterpolationMode Shrinking(Bitmap image, double widthOnScreen, double scaling) =>
        widthOnScreen * scaling < image.PixelSize.Width * 0.75 ? BitmapInterpolationMode.MediumQuality : BitmapInterpolationMode.HighQuality;

    /// <summary>Sharp tiles over the overview picture, only for the part of the page on screen.</summary>
    void DrawTiles(DrawingContext ctx, LoadedBook book, int page, ViewTransform v, double scaling)
    {
        TileInfo? info = book.Book.Pages[page].Tiles;
        if (info is null) return;
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
    void DrawLayers(DrawingContext ctx, LoadedBook book, int page, RectD r)
    {
        if (book.Book.Pages[page].Layers.Count == 0) return;
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

    /// <summary>Rain, snow, leaves and sparkles, clipped to the page and kept out of its "dry" boxes.</summary>
    void DrawWeather(DrawingContext ctx, LoadedBook book, int page, RectD pr, RectD r)
    {
        List<PageEffect> effects = book.Book.Pages[page].Effects;
        if (effects.Count == 0) return;
        ReaderEngine e = Engine;
        double t = e.Now / 1000, aspect = pr.W / pr.H;
        Point P(double x, double y) => new(r.X + x * r.H, r.Y + y * r.H);

        for (int k = 0; k < effects.Count; k++)
        {
            PageEffect fx = effects[k];
            double level = e.EffectLevel(page, k);
            if (level < 0.01 || !fx.IsFalling) continue;
            double amount = fx.AmountOrDefault;

            var clip = new StreamGeometry();
            using (StreamGeometryContext g = clip.Open())
            {
                g.SetFillRule(FillRule.EvenOdd);
                AddRect(g, ToRect(r));
                foreach (RectD b in fx.Dry) AddRect(g, new Rect(r.X + b.X * r.W, r.Y + b.Y * r.H, b.W * r.W, b.H * r.H));
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
                                g.BeginFigure(P(s.X1, s.Y1), false);
                                g.LineTo(P(s.X2, s.Y2), true);
                                g.EndFigure(false);
                            }
                        }
                        ctx.DrawGeometry(null, Pen(Rgba(236, 244, 255, sheet.Alpha), Math.Max(Weather.MinLinePixels, r.H * sheet.Width), round: true), lines);
                    }
                    if (fx.Ground is double ground and > 0)
                    {
                        double width = Math.Max(Weather.MinLinePixels, r.H * Weather.RingWidth);
                        foreach (SplashRing ring in Weather.Splashes(t, aspect, amount, ground, level))
                            ctx.DrawEllipse(null, Pen(Rgba(240, 247, 255, ring.Alpha), width), P(ring.X, ring.Y), ring.RadiusX * r.H, ring.RadiusY * r.H);
                    }
                    break;

                case PageEffect.Snow:
                    foreach (Snowflake flake in Weather.Snow(t, aspect, amount, level))
                    {
                        double radius = Math.Max(Weather.MinLinePixels, r.H * flake.Radius);
                        ctx.DrawEllipse(Brush(Rgba(255, 255, 255, flake.Alpha)), null, P(flake.X, flake.Y), radius, radius);
                    }
                    break;

                case PageEffect.Leaves:
                    foreach (Leaf leaf in Weather.Leaves(t, aspect, amount, level))
                    {
                        double size = leaf.Size * r.H;
                        Point at = P(leaf.X, leaf.Y);
                        Matrix m = Matrix.CreateScale(size, size * leaf.Squash) * Matrix.CreateRotation(leaf.Rotation) * Matrix.CreateTranslation(at.X, at.Y);
                        using (ctx.PushTransform(m))
                            ctx.DrawGeometry(Brush(WithAlpha(Color.Parse(Weather.LeafTints[leaf.Tint]), leaf.Alpha)), null, UnitLeaf);
                    }
                    break;

                case PageEffect.Sparkles:
                    foreach (Glint star in Weather.Sparkles(t, aspect, amount, level))
                    {
                        if (star.Alpha < 0.004) continue;
                        Point at = P(star.X, star.Y);
                        using (ctx.PushTransform(Matrix.CreateScale(star.Size * r.H, star.Size * r.H) * Matrix.CreateTranslation(at.X, at.Y)))
                            ctx.DrawGeometry(Brush(Rgba(255, 251, 224, star.Alpha)), null, UnitGlint);
                    }
                    break;
            }
        }
    }

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
                var dashes = new ImmutableDashStyle(new[] { 14 / 6.0, 10 / 6.0 }, 0);
                var pen = new ImmutablePen(Brush(Color.Parse("#e9eef5")), 6, dashes);
                ctx.DrawEllipse(null, pen, new Point(cx + Math.Sin(t * 28) * 10 * (1 - t), cy), r.W / 2 + 10, r.H / 2 + 10);
                continue;
            }
            ctx.DrawEllipse(null, Pen(Color.Parse("#ffd23f"), 6 * (1 - t) + 1), new Point(cx, cy),
                r.W / 2 + reach * 0.5 * t + 6, r.H / 2 + reach * 0.5 * t + 6);
            IImmutableBrush pale = Brush(Color.Parse("#fff6c2")), gold = Brush(Color.Parse("#ffb703"));
            for (int k = 0; k < 10; k++)
            {
                double a = k / 10.0 * Math.PI * 2 + t;
                double d = reach * (0.9 + 0.9 * Easings.OutCubic(t));
                Star(ctx, cx + Math.Cos(a) * d, cy + Math.Sin(a) * d, 9 * (1 - t) + 3, k % 2 == 1 ? pale : gold);
            }
        }
    }

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
            foreach (var (extra, alpha) in new[] { (30.0, 0.07), (18.0, 0.12), (8.0, 0.22) })
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
            var head = new StreamGeometry();
            using (StreamGeometryContext g = head.Open())
            {
                g.BeginFigure(new Point(16, 0), true);
                g.LineTo(new Point(-8, -13), true);
                g.LineTo(new Point(-8, 13), true);
                g.EndFigure(true);
            }
            using (ctx.PushTransform(Matrix.CreateRotation(hint.ArrowAngle) * Matrix.CreateTranslation(at.X, at.Y)))
                ctx.DrawGeometry(Brush(Color.Parse("#3a2a00")), null, head);
        }
    }

    /// <summary>Small map of the whole page with a frame showing where the child is looking.</summary>
    void DrawMap(DrawingContext ctx, LoadedBook book)
    {
        MapDraw? map = Engine.GetMap();
        if (map is null) return;
        Rect m = ToRect(map.Rect), view = ToRect(map.ViewRect);
        ctx.DrawRectangle(White, null, m.Inflate(3), 0, 0, new BoxShadows(new BoxShadow { Blur = 10, Color = Rgba(0, 0, 0, 0.45) }));
        using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.MediumQuality }))
            ctx.DrawImage(book.PageImage(map.Page), m);
        var shade = new StreamGeometry();
        using (StreamGeometryContext g = shade.Open())
        {
            g.SetFillRule(FillRule.EvenOdd);
            AddRect(g, m);
            AddRect(g, view);
        }
        ctx.DrawGeometry(Brush(Rgba(0, 0, 0, 0.35)), null, shade);
        ctx.DrawRectangle(null, Pen(Colors.White, 2.5), view);
        if (map.HasDot) ctx.DrawEllipse(Brush(Rgba(255, 236, 120, map.DotAlpha)), null, new Point(map.DotX, map.DotY), map.DotRadius, map.DotRadius);
    }

    /// <summary>Stars raining down when the story is finished.</summary>
    void DrawParty(DrawingContext ctx)
    {
        PartyDraw? party = Engine.GetParty();
        if (party is null) return;
        using var fading = ctx.PushOpacity(Math.Clamp(party.Alpha, 0, 1));
        foreach (PartyStar s in party.Stars) Star(ctx, s.X, s.Y, s.Radius, Brush(Color.Parse(PartyDraw.Tints[s.Tint])));
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

    ImmutablePen Pen(Color c, double thickness, bool round = false) =>
        new(Brush(c), thickness, null, round ? PenLineCap.Round : PenLineCap.Flat);

    /// <summary>
    /// A brush that is clear in the middle of an oval and goes to <paramref name="outer"/> at its rim (and
    /// stays that colour beyond). <paramref name="clearTo"/> is how far out the clear part reaches, 0 to 1.
    /// </summary>
    static IBrush Oval(double cx, double cy, double radiusX, double radiusY, double clearTo, Color inner, Color outer) =>
        new RadialGradientBrush
        {
            Center = new RelativePoint(cx, cy, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(cx, cy, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(radiusX, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(radiusY, RelativeUnit.Absolute),
            SpreadMethod = GradientSpreadMethod.Pad,
            GradientStops = { new GradientStop(inner, 0), new GradientStop(inner, clearTo), new GradientStop(outer, 1) },
        }.ToImmutable();

    /// <summary>
    /// Cover the screen with a colour that is clear inside an oval and reaches <paramref name="outer"/> at
    /// its rim. It is drawn as a circle under a vertical stretch: a radial brush given two different radii
    /// came out nearly round here, which made the spotlight a circle instead of an oval that fits its box.
    /// </summary>
    static void FillOval(DrawingContext ctx, Rect all, double cx, double cy, double radiusX, double radiusY,
        double clearTo, Color inner, Color outer)
    {
        double stretch = radiusY / radiusX;
        Matrix m = Matrix.CreateTranslation(-cx, -cy) * Matrix.CreateScale(1, stretch) * Matrix.CreateTranslation(cx, cy);
        using (ctx.PushTransform(m))
        {
            // The area to fill, as it has to be before the stretch so that it still covers the screen after.
            var area = new Rect(all.X - 2, cy + (all.Y - cy) / stretch - 2, all.Width + 4, all.Height / stretch + 4);
            ctx.FillRectangle(Oval(cx, cy, radiusX, radiusX, clearTo, inner, outer), area);
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

    static void Star(DrawingContext ctx, double x, double y, double radius, IBrush brush)
    {
        using (ctx.PushTransform(Matrix.CreateScale(radius, radius) * Matrix.CreateTranslation(x, y)))
            ctx.DrawGeometry(brush, null, UnitStar);
    }
}
