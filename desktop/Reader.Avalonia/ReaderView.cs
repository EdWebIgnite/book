using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Reader.Core;

namespace Reader.Ui;

/// <summary>
/// The picture area of the reader. It owns a <see cref="ReaderEngine"/>, moves it on once a frame,
/// draws what the engine says is on screen (see ReaderPaint.cs), and passes touches, the wheel and
/// the keyboard on to it.
///
/// How frames are driven: while anything on screen is changing, the view asks the window for the
/// next frame (the same call a browser's requestAnimationFrame makes), moves the engine on to that
/// frame's time and asks to be redrawn, then asks for the frame after. Motion is tied to the display
/// that way, with no timer of its own in between. When the picture is still it stops asking, and a
/// slow check ten times a second looks for anything that should start moving again by itself (a stop
/// that goes on without a tap, a hint after an idle spell).
/// </summary>
public sealed partial class ReaderView : Control
{
    readonly Stopwatch _watch = Stopwatch.StartNew();
    readonly FrameClock _pacer = new();
    readonly DispatcherTimer _idleCheck;
    readonly PictureCache _tiles = new();
    readonly PictureCache _pictures = new(keep: 48, drop: 12);
    readonly BookFolderSound _sound;
    readonly HashSet<int> _down = new();
    LoadedBook? _book;
    bool _frameAsked, _attached, _wasRestless, _loadAtOnce;

    // The time of the latest frame, and what the stopwatch said then, so that a touch between two frames
    // can be given a time on the same clock.
    double _frameTime, _watchAtFrame, _clockOffset;
    bool _haveOffset;

    public ReaderView(ReaderSettings settings, ISoundOutput? sound = null)
    {
        _sound = new BookFolderSound(sound ?? new NoSound());
        Engine = new ReaderEngine(settings, _sound);
        Gestures = new GestureInput(Engine);
        Focusable = true;
        ClipToBounds = true;
        // A cut to another stop changes the picture without anything being in motion, so draw on every change too.
        Engine.Changed += _ => Wake();
        _tiles.Arrived += Wake;
        _pictures.Arrived += Wake;
        AddHandler(PointerTouchPadGestureMagnifyEvent, OnMagnify);
        _idleCheck = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => IdleCheck());
    }

    /// <summary>The reader itself. Use <see cref="Do"/> to act on it, so it knows what time it is.</summary>
    public ReaderEngine Engine { get; }

    public GestureInput Gestures { get; }

    /// <summary>
    /// Where the time comes from, in milliseconds. Left alone, it is the time of the display's frames.
    /// Screenshots and the bench set their own so that the same command always draws the same picture.
    /// </summary>
    public Func<double>? Clock { get; set; }

    /// <summary>False stops the view from moving itself on every frame; call <see cref="Tick"/> by hand instead.</summary>
    public bool AutoFrames { get; set; } = true;

    /// <summary>Raised once a frame after the engine has moved on, with the time in milliseconds.</summary>
    public event Action<double>? FrameTick;

    /// <summary>A finger or the mouse went down on the picture.</summary>
    public event Action? PicturePressed;

    /// <summary>
    /// The reader screen sets this while something of its own is animating (a pulsing button), so that
    /// frames keep coming even though the picture itself is still.
    /// </summary>
    public bool KeepAwake { get; set; }

    /// <summary>The time now, on the same clock the frames use.</summary>
    public double Now => Clock?.Invoke() ?? _frameTime + (_watch.Elapsed.TotalMilliseconds - _watchAtFrame);

    public LoadedBook? Book => _book;

    /// <summary>Pictures still loading. Screenshots wait for this to reach zero.</summary>
    public int PicturesLoading => _tiles.Loading + _pictures.Loading + (_book?.Loading ?? 0);

    /// <summary>Load tiles, layer pictures and full-size pages on the spot instead of in the background. For screenshots.</summary>
    public bool LoadPicturesAtOnce
    {
        get => _loadAtOnce;
        set
        {
            _loadAtOnce = value;
            _tiles.Synchronous = _pictures.Synchronous = value;
            if (_book is not null) _book.LoadAtOnce = value;
        }
    }

    /// <summary>Do something to the engine, first telling it the time.</summary>
    public void Do(Action<ReaderEngine> action)
    {
        Engine.SetTime(Now);
        action(Engine);
        Wake();
    }

    /// <summary>Show a book. The caller keeps ownership of it and disposes it after <see cref="ClearBook"/>.</summary>
    public void SetBook(LoadedBook book, int startStep = 0)
    {
        if (_book is not null) _book.PictureArrived -= Wake;
        _tiles.Clear();
        _pictures.Clear();
        _frames.Clear();
        _book = book;
        book.LoadAtOnce = _loadAtOnce;
        book.PictureArrived += Wake;
        _sound.Folder = book.Folder;
        Engine.SetTime(Now);
        Engine.SetViewSize(Bounds.Width, Bounds.Height);
        Engine.SetBook(book.Book, book.Aspects, startStep);
        Wake();
    }

    /// <summary>Stop showing the book and go quiet.</summary>
    public void ClearBook()
    {
        Engine.ClearBook();
        if (_book is not null) _book.PictureArrived -= Wake;
        _book = null;
        _sound.Folder = null;
        _tiles.Clear();
        _pictures.Clear();
        _frames.Clear();
        InvalidateVisual();
    }

    // ---------------------------------------------------------------- frames

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _idleCheck.Start();
        Wake();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _idleCheck.Stop();
        Engine.Visible = false;
        Engine.Hush();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Engine.SetViewSize(e.NewSize.Width, e.NewSize.Height);
        Wake();
    }

    /// <summary>Something has changed: draw again, and start asking for frames if that had stopped.</summary>
    public void Wake()
    {
        InvalidateVisual();
        AskFrame();
    }

    void AskFrame()
    {
        if (_frameAsked || !_attached || !AutoFrames) return;
        if (TopLevel.GetTopLevel(this) is not TopLevel top) return;
        _frameAsked = true;
        top.RequestAnimationFrame(OnFrame);
    }

    /// <summary>The window is about to draw a frame.</summary>
    void OnFrame(TimeSpan frameTime)
    {
        _frameAsked = false;
        if (!_attached || !AutoFrames) return;

        // The frame's own time, evened out (see FrameClock), then shifted on to the stopwatch's clock so
        // that touches, which are timed by the stopwatch, and frames agree about what time it is.
        double raw = frameTime.TotalMilliseconds, watch = _watch.Elapsed.TotalMilliseconds;
        if (!_haveOffset || Math.Abs(raw + _clockOffset - watch) > 100)
        {
            _clockOffset = watch - raw;
            _haveOffset = true;
            _pacer.Reset();
        }
        double now = Clock?.Invoke() ?? _pacer.Next(raw) + _clockOffset;
        _frameTime = now;
        _watchAtFrame = watch;

        NoteFrame(watch);
        Step(now);
        if (Showing && (Engine.Restless || _wasRestless || KeepAwake || Gestures.Active || ShowFrameRate)) AskFrame();
    }

    /// <summary>
    /// While no frames are being asked for, look ten times a second for anything that has started to move
    /// by itself, and for a hidden reader coming back into view.
    /// </summary>
    void IdleCheck()
    {
        if (_frameAsked || !_attached || !AutoFrames) return;
        Step(Now);
        if (Showing && (Engine.Restless || KeepAwake || ShowFrameRate)) AskFrame();
    }

    bool Showing => IsEffectivelyVisible && _book is not null;

    /// <summary>One frame, by hand: move the engine on to now and redraw if anything changed. For screenshots and the bench.</summary>
    public void Tick()
    {
        double now = Now;
        NoteFrame(now);
        Step(now);
    }

    void Step(double now)
    {
        // While the reader is hidden nothing moves on and nothing makes a noise.
        bool showing = Showing;
        if (showing != Engine.Visible)
        {
            Engine.Visible = showing;
            if (!showing) Engine.Hush();
        }
        if (!showing) return;
        Engine.SetViewSize(Bounds.Width, Bounds.Height);
        Engine.Update(now);
        FrameTick?.Invoke(now);

        // Have the pictures the camera may need next on their way before it needs them.
        if (!Engine.Animating && _book is LoadedBook book && Engine.Steps.Count > 0)
        {
            int page = Engine.CurrentStep.Page;
            book.Warm(page);
            book.Warm(page + 1);
        }

        // Only draw again when something is moving, plus once more after it stops: that last frame is the
        // settled one, drawn at full quality. A child looking at a still page then costs the computer
        // almost nothing.
        bool restless = Engine.Restless;
        if (restless || _wasRestless || ShowFrameRate) InvalidateVisual();
        _wasRestless = restless;
    }

    // ---------------------------------------------------------------- the frame-rate readout

    readonly Queue<(double At, double Gap)> _frames = new();
    double _lastFrameAt = double.NaN, _readoutAt = double.NegativeInfinity;
    string _readout = "";
    bool _showFrameRate;

    /// <summary>
    /// Show how many frames a second are being drawn, and the longest wait between two of them in the
    /// last second, in the corner. While it is on, every frame is drawn, moving or not.
    /// </summary>
    public bool ShowFrameRate
    {
        get => _showFrameRate;
        set
        {
            _showFrameRate = value;
            _frames.Clear();
            _lastFrameAt = double.NaN;
            _readout = "";
            Wake();
        }
    }

    /// <summary>Frames per second over the last second, and the longest gap between two frames in it (milliseconds).</summary>
    public (double PerSecond, double WorstMs) FrameRate
    {
        get
        {
            if (_frames.Count == 0) return (0, 0);
            double worst = 0;
            foreach (var f in _frames) worst = Math.Max(worst, f.Gap);
            return (_frames.Count, worst);
        }
    }

    void NoteFrame(double at)
    {
        if (!_showFrameRate) return;
        if (!double.IsNaN(_lastFrameAt) && at > _lastFrameAt) _frames.Enqueue((at, at - _lastFrameAt));
        _lastFrameAt = at;
        while (_frames.Count > 0 && at - _frames.Peek().At > 1000) _frames.Dequeue();
        if (at - _readoutAt < 250) return;      // change the words four times a second, so they can be read
        _readoutAt = at;
        var (fps, worst) = FrameRate;
        _readout = _frames.Count == 0 ? "measuring…"
            : string.Create(CultureInfo.InvariantCulture, $"{fps:0} frames a second   longest wait {worst:0.0} ms");
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
        Wake();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_down.Contains(e.Pointer.Id)) return;
        Point p = e.GetPosition(this);
        Gestures.PointerMove(e.Pointer.Id, p.X, p.Y, Now);
        Wake();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_down.Remove(e.Pointer.Id)) return;
        Point p = e.GetPosition(this);
        Gestures.PointerUp(e.Pointer.Id, p.X, p.Y, Now);
        e.Pointer.Capture(null);
        e.Handled = true;
        Wake();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        // Only a touch that was taken away mid-gesture counts; an ordinary release has already been dealt with.
        if (_down.Remove(e.Pointer.Id))
        {
            Gestures.PointerCancel(e.Pointer.Id);
            Wake();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Point p = e.GetPosition(this);
        // One notch is 1.0 here and about 100 in a browser, and "up" is positive here but negative there.
        bool zoomKey = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        Gestures.Wheel(p.X, p.Y, -e.Delta.X * 100, -e.Delta.Y * 100, zoomKey, smooth: true, Now);
        e.Handled = true;
        Wake();
    }

    void OnMagnify(object? sender, PointerDeltaEventArgs e)
    {
        Point p = e.GetPosition(this);
        Gestures.Magnify(p.X, p.Y, e.Delta.X, Now);
        e.Handled = true;
        Wake();
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
}
