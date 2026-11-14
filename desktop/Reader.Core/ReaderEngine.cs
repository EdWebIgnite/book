using static Reader.Core.Geo;

namespace Reader.Core;

/// <summary>
/// The reader with no screen attached: where the camera is, what is in focus, what the child has
/// found, what should be heard. It is a port of class Reader in web/reader.js, minus the drawing.
///
/// How to drive it:
///   - Tell it the size of the picture area with <see cref="SetViewSize"/>.
///   - Give it a book with <see cref="SetBook"/>.
///   - Call <see cref="Update"/> every frame with the time in milliseconds. Any steady clock will do,
///     and the engine never looks at a clock itself, so tests can use a made-up one.
///   - Before passing on a touch or a key between frames, call <see cref="SetTime"/> so the engine
///     knows when it happened.
///   - Draw from the plain data it exposes: <see cref="GetView"/>, <see cref="Shown"/>,
///     <see cref="LayersFor"/>, <see cref="EffectLevel"/>, <see cref="GetHint"/> and so on.
///
/// World units: pages sit side by side, each 1.0 tall and as wide as its aspect ratio, with a gap
/// between them (see <see cref="Geo.LayoutPages"/>). Screen units are whatever the host uses for
/// layout (CSS pixels in the browser, device-independent pixels on the desktop).
/// </summary>
public sealed class ReaderEngine
{
    /// <summary>Screen units per centimetre, for the smallest touch target. 37.8 matches CSS pixels.</summary>
    public const double DefaultUnitsPerCm = 37.8;

    /// <summary>The closest zoom on an explore page shows this much of the page's height.</summary>
    public const double ClosestZoom = 0.12;

    enum AnimKind { Move, Fly, Pan, Flip, Cover }

    sealed class Anim
    {
        public AnimKind Kind;
        public Shot From, To;
        public double Start, Ms;
        public string Easing = Easings.EaseInOutCubic;
        public bool Smooth;
        public double ArcK;
        public bool Fade;
        public Action? Then;
        /// <summary>The page the camera leaves and the page it ends on (the same page for most moves).</summary>
        public int FromPage, ToPage;
        /// <summary>Flip only: true when going on through the book.</summary>
        public bool Forward = true;
        /// <summary>What plays straight after this, as part of the same journey (pull back, flip, go in).</summary>
        public Anim? After;
        /// <summary>A dip to dark that leaves the closed book, or arrives at it: the cover shows on that side of the dip.</summary>
        public bool FromCover, ToCover;
        /// <summary>Flip only, with one page at a time: which page of each spread it turns from and to.</summary>
        public int FromHalf, ToHalf;
    }

    /// <summary>How long a page takes to flip at the standard move speed, in milliseconds.</summary>
    public const double FlipMs = 900;

    /// <summary>How long the cover takes to swing open at the standard move speed, in milliseconds.</summary>
    public const double CoverMs = 1100;
    const double StandardMoveMs = 1400;

    sealed record Note(string Text, string Ask, double Until);
    sealed class LayerState { public double Vis = -1, T = -1; }
    readonly record struct Burst(RectD Box, double Start, bool Locked);
    readonly record struct Touch(double X, double Y, double Start);

    // ---------------------------------------------------------------- state

    double _now;
    double? _lastUpdate;
    Anim? _anim;
    Shot _shown;
    bool _hasShown;
    double _restSince, _lastTouch;
    double _lastGo = double.NegativeInfinity;      // for ignoring accidental double taps
    bool _moved;                                   // has the reader gone anywhere yet (for the first-tap hint)
    bool _arrivedForward;
    (double Vx, double Vy)? _fling;                // explore: glide velocity after a flick, world units per millisecond
    readonly HashSet<(int Page, int Index)> _found = new();
    readonly List<Burst> _bursts = new();          // explore: sparkles playing
    readonly List<Touch> _ripples = new();         // the little ring that answers every touch
    Note? _note;                                   // explore: caption from a tapped hotspot
    double? _party;                                // explore: when the story was finished, for the celebration
    readonly Dictionary<(int Page, int Index), double> _fx = new();
    readonly Dictionary<(int Page, int Index), LayerState> _layers = new();
    (double Start, double Strength)? _shake;
    double? _flash;
    double _rainLoud;
    // Narration: the voice of the stop being read, when it started (reader time) and when it ended.
    Voice? _voice;
    int _voiceWordCount;
    double _voiceAt;
    double? _voiceEndedAt;
    double _insetSpeedTop, _insetSpeedBottom;      // how fast each inset is changing, in units per millisecond
    List<RectD> _pageRects = new();
    List<Step> _steps = new();

    public ReaderEngine(ReaderSettings? settings = null, ISoundOutput? sound = null)
    {
        Settings = settings ?? new ReaderSettings();
        Sound = sound ?? new NoSound();
    }

    public ReaderSettings Settings { get; }
    public ISoundOutput Sound { get; set; }

    /// <summary>Raised whenever something the reader screen shows may have changed.</summary>
    public event Action<ReaderState>? Changed;

    public Book? Book { get; private set; }
    public IReadOnlyList<RectD> PageRects => _pageRects;
    public IReadOnlyList<Step> Steps => _steps;
    public int StepIndex { get; private set; }
    public bool Overview { get; private set; }
    public bool Finished { get; private set; }

    /// <summary>Room kept clear at the top and bottom right now.</summary>
    public Insets Insets { get; } = new(64, 88);

    /// <summary>The screen can ask for more room (for a caption); the picture eases to it.</summary>
    public Insets InsetGoal { get; } = new(64, 88);

    public double ViewWidth { get; private set; }
    public double ViewHeight { get; private set; }

    /// <summary>False while the reader is hidden: sounds stop and nothing moves on by itself.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>What the device itself asks for. Used when the reduce-motion setting is "auto".</summary>
    public bool DeviceReducedMotion { get; set; }

    /// <summary>Screen units per centimetre on this screen.</summary>
    public double UnitsPerCm { get; set; } = DefaultUnitsPerCm;

    /// <summary>The time the engine believes it is, in milliseconds.</summary>
    public double Now => _now;

    /// <summary>What is on screen: camera (before any resting drift), focus box and focus strengths.</summary>
    public Shot Shown => _shown;

    /// <summary>True while the camera is travelling, gliding or panning.</summary>
    public bool Animating => _anim is not null || _fling is not null;

    /// <summary>
    /// True when the picture would look different a moment from now: the camera is moving, weather is
    /// falling, something is pulsing or fading. A renderer can skip redrawing while this is false, which
    /// is most of the time a child spends looking at a page. Worked out by <see cref="Update"/>.
    /// </summary>
    public bool Restless { get; private set; }

    public void SetViewSize(double width, double height)
    {
        ViewWidth = Math.Max(0, width);
        ViewHeight = Math.Max(0, height);
    }

    /// <summary>Set both the current insets and their goal, with no easing.</summary>
    public void SetInsets(double top, double bottom)
    {
        Insets.Top = InsetGoal.Top = top;
        Insets.Bottom = InsetGoal.Bottom = bottom;
        _insetSpeedTop = _insetSpeedBottom = 0;
    }

    /// <summary>Tell the engine what time it is without moving anything on. Call before passing on input.</summary>
    public void SetTime(double nowMs)
    {
        if (nowMs > _now) _now = nowMs;
    }

    // ---------------------------------------------------------------- books and steps

    /// <summary>
    /// Start a book. <paramref name="pageAspects"/> is each page image's width divided by its height,
    /// which is all the engine needs to know about the pictures.
    /// </summary>
    /// <param name="hasCover">
    /// True when the book's cover picture can be shown. The book then starts closed on its cover (with the
    /// book look on), and the first tap opens it.
    /// </param>
    public void SetBook(Book book, IReadOnlyList<double> pageAspects, int startStep = 0, bool hasCover = false)
    {
        if (pageAspects.Count != book.Pages.Count) throw new ArgumentException("One aspect ratio is needed for every page.", nameof(pageAspects));
        Book = book;
        HasCover = hasCover;
        StopClip();
        _fx.Clear();
        _layers.Clear();
        _shake = null;
        _flash = null;
        _arrivedForward = false;
        _party = null;
        _moved = false;
        _lastGo = double.NegativeInfinity;
        _pageRects = LayoutPages(pageAspects);
        _steps = BuildSteps();
        StepIndex = Math.Max(0, Math.Min(startStep, _steps.Count - 1));
        Overview = false;
        Finished = false;
        _anim = null;
        _fling = null;
        _found.Clear();
        _bursts.Clear();
        _ripples.Clear();
        _note = null;
        if (_steps.Count == 0) { _hasShown = false; RaiseChanged(); return; }
        _shown = Target();
        _hasShown = true;
        _restSince = _lastTouch = _now;
        AfterArrive();
        RaiseChanged();
    }

    /// <summary>Put the book away: go quiet and forget it, so nothing more moves or sounds until another is set.</summary>
    public void ClearBook()
    {
        Hush();
        Book = null;
        _steps = new List<Step>();
        _pageRects = new List<RectD>();
        StepIndex = 0;
        Overview = false;
        Finished = false;
        _anim = null;
        _fling = null;
        _note = null;
        _party = null;
        _shake = null;
        _flash = null;
        _hasShown = false;
        _fx.Clear();
        _layers.Clear();
        _bursts.Clear();
        _ripples.Clear();
        _found.Clear();
    }

    /// <summary>The book has a cover picture to start on (see <see cref="SetBook"/>).</summary>
    public bool HasCover { get; private set; }

    /// <summary>
    /// One page at a time, as on a phone held upright: a picture that fills a double-page spread is read
    /// a page (half the picture) at a time when seen whole. Close-ups still move over the whole picture.
    /// Call <see cref="Refresh"/> after changing it, to re-plan the steps and keep the place.
    /// </summary>
    public bool OnePage { get; set; }

    /// <summary>True when this page is read one page at a time right now.</summary>
    bool OnePageFor(int page) => OnePage && ShownAsBook(page);

    /// <summary>Which page of the open book a box mostly lies on: the left-hand page or the right-hand one.</summary>
    int HalfOfBox(int page, RectD box) =>
        BookLayout.IsPortrait(_pageRects[page]) ? Step.RightPage : box.CenterX < 0.5 ? Step.LeftPage : Step.RightPage;

    /// <summary>
    /// With one page at a time, which page a step is seen on when the whole page shows: a whole-page step's
    /// own half, or for a stop the page holding it. 0 when both pages show.
    /// </summary>
    int HalfFor(Step step)
    {
        if (!OnePageFor(step.Page) || step.Explore || step.Stop == Step.Cover) return Step.Both;
        if (step.Half != Step.Both) return step.Half;
        if (step.Stop >= 0) return HalfOfBox(step.Page, Book!.Pages[step.Page].Stops[step.Stop].Box);
        return BookLayout.IsPortrait(_pageRects[step.Page]) ? Step.RightPage : Step.LeftPage;
    }

    /// <summary>
    /// With one page at a time, the page the camera is on right now (by where its middle is), for drawing
    /// the page on its own. 0 when both pages show.
    /// </summary>
    public int VisibleHalf()
    {
        if (!BookMode || !OnePageFor(CurrentStep.Page) || CurrentStep.Stop == Step.Cover) return Step.Both;
        RectD pr = _pageRects[CurrentStep.Page];
        if (BookLayout.IsPortrait(pr)) return Step.RightPage;
        return DisplayedCam().CenterX < BookLayout.SpineX(pr) ? Step.LeftPage : Step.RightPage;
    }

    /// <summary>
    /// Index of the first step showing the given page and stop (-1 whole page, -2 explore, -3 the cover).
    /// When there is no such step, the first step of the page, not counting the cover.
    /// </summary>
    public int FindStep(int page, int stop) => FindStep(page, stop, closing: false);

    /// <summary>The same, telling the whole page as it opens from the whole page again at the end.</summary>
    public int FindStep(int page, int stop, bool closing)
    {
        int i = _steps.FindIndex(s => s.Page == page && s.Stop == stop && s.Closing == closing);
        if (i < 0) i = _steps.FindIndex(s => s.Page == page && s.Stop == stop);
        if (i < 0) i = _steps.FindIndex(s => s.Page == page && s.Stop != Step.Cover);
        return Math.Max(0, i);
    }

    /// <summary>Show a page and stop straight away, then play its pan if it has one.</summary>
    public void JumpTo(int page, int stop)
    {
        if (!HasBook) return;
        _arrivedForward = false;
        StepIndex = FindStep(page, stop);
        Overview = false;
        Finished = false;
        _anim = null;
        _fling = null;
        _shown = Target();
        _restSince = _lastTouch = _now;
        AfterArrive();
        RaiseChanged();
    }

    /// <summary>The book as a list of steps, following the settings and each page's own wishes.</summary>
    public List<Step> BuildSteps()
    {
        var s = Settings;
        var steps = new List<Step>();
        if (Book is null) return steps;
        // With the book look the book starts closed on its cover, if it has one and its first page is shown as a book.
        if (HasCover && s.BookLook && Book.Pages.Count > 0 && !Book.Pages[0].Explore && Book.Pages[0].Tiles is null)
            steps.Add(new Step(0, Step.Cover));
        for (int p = 0; p < Book.Pages.Count; p++)
        {
            Page page = Book.Pages[p];
            int n = page.Stops.Count;
            bool one = OnePageFor(p);
            // With one page at a time, the whole page as it opens is the page holding the first stop, and the
            // whole page at the end is the page holding the last stop (where its pan ends, if it pans).
            int openingHalf = !one ? Step.Both : n > 0 ? HalfOfBox(p, page.Stops[0].Box) : HalfFor(new Step(p, Step.WholePage));
            int closingHalf = !one || n == 0 ? Step.Both : HalfOfBox(p, page.Stops[n - 1].To ?? page.Stops[n - 1].Box);
            if (page.Explore)
            {
                // Guided intro (if any), then the page is handed over for exploring.
                if (n > 0 && s.WholePageFirst && page.OpenWhole) steps.Add(new Step(p, Step.WholePage, Half: openingHalf));
                for (int i = 0; i < n; i++) steps.Add(new Step(p, i));
                steps.Add(new Step(p, Step.Exploring, true));
                continue;
            }
            if (n == 0)
            {
                // A page with nothing to stop at is shown whole; one page at a time, both of its pages in turn.
                if (one && !BookLayout.IsPortrait(_pageRects[p]))
                {
                    steps.Add(new Step(p, Step.WholePage, Half: Step.LeftPage));
                    steps.Add(new Step(p, Step.WholePage, Half: Step.RightPage));
                }
                else steps.Add(new Step(p, Step.WholePage, Half: one ? Step.RightPage : Step.Both));
                continue;
            }
            if (s.WholePageFirst && page.OpenWhole) steps.Add(new Step(p, Step.WholePage, Half: openingHalf));
            for (int i = 0; i < n; i++) steps.Add(new Step(p, i));
            // The whole page again at the end, unless the last stop already shows all of it.
            Stop last = page.Stops[n - 1];
            bool full = last.To is null && last.Box.W > 0.95 && last.Box.H > 0.95;
            if (s.WholePageLast && !full) steps.Add(new Step(p, Step.WholePage, Closing: true, Half: closingHalf));
        }
        return steps;
    }

    /// <summary>Call after settings change. Keeps the reader on the same page and stop.</summary>
    public void Refresh()
    {
        if (!HasBook) return;
        Step cur = CurrentStep;
        _steps = BuildSteps();
        StepIndex = FindStep(cur.Page, cur.Stop, cur.Closing);
        if (!Exploring) MoveTo(cur.Page);
        else RaiseChanged();
    }

    /// <summary>True when moves should be swapped for fades: the parent's setting, or the device's own.</summary>
    public bool Reduced => Settings.ReduceMotion switch
    {
        "on" => true,
        "off" => false,
        _ => DeviceReducedMotion,
    };

    bool HasBook => Book is not null && _steps.Count > 0;

    public Step CurrentStep => _steps[StepIndex];

    /// <summary>The stop the camera is on, or null on a whole page or while exploring.</summary>
    public Stop? CurrentStop
    {
        get
        {
            if (!HasBook) return null;
            Step st = CurrentStep;
            return st.Stop >= 0 ? Book!.Pages[st.Page].Stops[st.Stop] : null;
        }
    }

    public bool Exploring => HasBook && CurrentStep.Explore && !Finished;

    public Page CurrentPage => Book!.Pages[CurrentStep.Page];

    /// <summary>A box given in fractions of a page, in world units.</summary>
    public RectD BoxRect(int page, RectD b)
    {
        RectD pr = _pageRects[page];
        return new RectD(pr.X + b.X * pr.W, pr.Y + b.Y * pr.H, b.W * pr.W, b.H * pr.H);
    }

    // ---------------------------------------------------------------- sounds

    bool SoundOn => Settings.Sound;

    void PlayClip(string? name)
    {
        StopClip();
        if (!SoundOn || string.IsNullOrEmpty(name)) return;
        Sound.PlayClip(name);
    }

    void StopClip()
    {
        Sound.StopClip();
        StopVoice();
    }

    void StopVoice()
    {
        bool had = _voice is not null;
        _voice = null;
        _voiceEndedAt = null;
        if (had) Sound.StopVoice();
    }

    /// <summary>Start a stop's recorded voice, when "Read to me" is on.</summary>
    void PlayVoice(Stop stop)
    {
        if (!Settings.ReadToMe || stop.Voice is not { File.Length: > 0 } v) return;
        _voice = v;
        _voiceWordCount = ReadAlong.WordCount(stop.Caption);
        _voiceAt = _now;
        _voiceEndedAt = null;
        Sound.PlayVoice(v.File, v.StartOrZero, v.End ?? 0);
    }

    /// <summary>How far into its sound file the voice has got, in seconds, or null with no voice playing.</summary>
    public double? VoiceSeconds
    {
        get
        {
            if (_voice is not Voice v || _voiceEndedAt is not null) return null;
            double reported = Sound.VoicePosition;
            return reported >= 0 ? reported : v.StartOrZero + (_now - _voiceAt) / 1000;
        }
    }

    /// <summary>True while a stop's voice is being read aloud.</summary>
    public bool Reading => _voice is not null && _voiceEndedAt is null;

    /// <summary>True when the stop on screen has a voice that has finished reading.</summary>
    public bool ReadingDone => _voice is not null && _voiceEndedAt is not null;

    /// <summary>The word of the caption being read right now (0 = first), or -1.</summary>
    public int ReadAlongWord()
    {
        if (VoiceSeconds is not double at || _voice is null) return -1;
        return ReadAlong.WordAt(_voice.Words, _voiceWordCount, at);
    }

    /// <summary>Notice when the voice has come to its end. Judged by the trimmed end, or by the sound output when there is none.</summary>
    void UpdateVoice()
    {
        if (_voice is not Voice v || _voiceEndedAt is not null) return;
        double elapsed = _now - _voiceAt;
        double? end = ReadAlong.EndOf(v);
        double reported = Sound.VoicePosition;
        double at = reported >= 0 ? reported : v.StartOrZero + elapsed / 1000;
        bool done = end is double e
            ? at >= e || (elapsed > 600 && !Sound.VoicePlaying && reported >= 0)
            : elapsed > 600 && !Sound.VoicePlaying;
        // Never wait for ever: a voice that can't be heard still ends when its time is up.
        if (end is double limit && elapsed > (limit - v.StartOrZero) * 1000 + 1500) done = true;
        if (done)
        {
            _voiceEndedAt = _now;
            RaiseChanged();
        }
    }

    /// <summary>Stop everything that makes a noise by itself. Call when the reader is put away.</summary>
    public void Hush()
    {
        StopClip();
        _rainLoud = 0;
        Sound.Rain(0);
    }

    /// <summary>Answer a touch: a small ring where the finger landed.</summary>
    public void Ripple(double sx, double sy) => _ripples.Add(new Touch(sx, sy, _now));

    // ---------------------------------------------------------------- moving between steps

    /// <summary>
    /// How long a camera move should take. Longer and bigger moves take longer so the speed feels
    /// even, and zooming is never faster than about double per quarter second.
    /// </summary>
    public (double Ms, bool Far) MoveTime(RectD from, RectD to, double baseMs)
    {
        double gw = Math.Sqrt(from.W * to.W);
        double dc = Math.Sqrt(Math.Pow(to.CenterX - from.CenterX, 2) + Math.Pow(to.CenterY - from.CenterY, 2)) / gw;
        double dz = Math.Abs(Math.Log(to.W / from.W));
        double ms = Settings.ScaleTime ? baseMs * Clamp(0.55 + 0.45 * (dc + dz), 0.5, 2.4) : baseMs;
        return (Math.Max(ms, dz * 330), dc > 3);
    }

    /// <summary>Where the camera starts when a page is handed over for exploring.</summary>
    public RectD ExploreStart()
    {
        Step st = CurrentStep;
        if (StepIndex > 0)
        {
            Step prev = _steps[StepIndex - 1];
            if (prev.Page == st.Page && prev.Stop >= 0)
            {
                // Carry on from where the guided intro ended.
                Stop s = Book!.Pages[prev.Page].Stops[prev.Stop];
                return Inflate(BoxRect(st.Page, s.To ?? s.Box), Settings.Padding);
            }
        }
        RectD pr = _pageRects[st.Page];
        double aspect = pr.W / pr.H;
        if (aspect > 2.5) return new RectD(pr.X, pr.Y, pr.H * 1.4, pr.H);              // wide scene: start at the left
        if (aspect < 0.4) return new RectD(pr.X, pr.Y, pr.W, pr.W * 1.4);              // tall scene: start at the top
        return Inflate(pr, 0.03);
    }

    Shot Styled(RectD cam, RectD focus, string? style, double k)
    {
        var s = Settings;
        return new Shot(cam, focus,
            style == FocusStyles.Dim ? s.Dim * k : 0,
            style == FocusStyles.Vignette ? s.Vignette * k : 0,
            style == FocusStyles.Spotlight ? s.Spotlight * k : 0);
    }

    /// <summary>True when this page is shown as an open book: the book look is on, and it is a guided page and not a tiled scene.</summary>
    public bool ShownAsBook(int page) => HasBook && Settings.BookLook && !IsTiled(page);

    /// <summary>
    /// The camera that shows a page whole, with a little air around it. With the book look it frames the
    /// whole open book, board and page edges included.
    /// </summary>
    public RectD WholeCam(int page) => WholeCam(page, Step.Both);

    /// <summary>
    /// The same for one page of the open book (<paramref name="half"/> 1 left, 2 right): that page with its
    /// board and page edges round it.
    /// </summary>
    public RectD WholeCam(int page, int half)
    {
        if (!ShownAsBook(page)) return Inflate(_pageRects[page], 0.03);
        if (half == Step.Both) return Inflate(BookLayout.Book(_pageRects[page]), BookLayout.Air);
        RectD one = HalfRect(page, half);
        double side = BookLayout.StackRoom + BookLayout.Board + BookLayout.Air, edge = BookLayout.Board + BookLayout.Air;
        return new RectD(one.X - side, one.Y - edge, one.W + 2 * side, one.H + 2 * edge);
    }

    /// <summary>One page of a picture's open spread, in world units.</summary>
    public RectD HalfRect(int page, int half)
    {
        RectD spread = BookLayout.Spread(_pageRects[page]);
        return half == Step.LeftPage ? spread with { W = spread.W / 2 } : half == Step.RightPage ? new RectD(spread.CenterX, spread.Y, spread.W / 2, spread.H) : spread;
    }

    /// <summary>The camera on the closed book, centred on it.</summary>
    public RectD ClosedCam(int page) => Inflate(BookLayout.Closed(_pageRects[page]), BookLayout.Air);

    /// <summary>A page seen whole, with the book's focus style at a gentler strength.</summary>
    Shot WholeShot(int page, int half = Step.Both)
    {
        // The whole page never gets a spotlight: a book set to spotlight shows soft edges here instead.
        string wholeStyle = Book!.FocusStyle == FocusStyles.Spotlight ? FocusStyles.Vignette : Book.FocusStyle;
        // In the open book the focus is both pages (or the one page showing), so a portrait book's
        // left-hand page isn't darkened.
        RectD focus = ShownAsBook(page) ? HalfRect(page, half) : _pageRects[page];
        return Styled(WholeCam(page, half), focus, wholeStyle, 0.6);
    }

    /// <summary>The closed book: nothing in focus and no effects, just the cover in the middle of the screen.</summary>
    Shot CoverShot(int page) => Styled(ClosedCam(page), BookLayout.Closed(_pageRects[page]), FocusStyles.None, 0);

    /// <summary>
    /// How strongly the crease and the curve of the pages show right now, 0 to 1: fully at the whole-book
    /// view, fading out as the camera goes in closer (see <see cref="BookLayout.CreaseStrength"/>).
    /// </summary>
    public double CreaseStrength()
    {
        if (!BookMode || ViewWidth <= 0) return 0;
        double whole = ViewFor(WholeCam(CurrentStep.Page, VisibleHalf())).Scale, now = GetView(withShake: false).Scale;
        return whole <= 0 ? 0 : BookLayout.CreaseStrength(now / whole);
    }

    /// <summary>Camera, focus box and effect strengths for the current step (start box, or end box of a pan).</summary>
    public Shot Target(bool end = false)
    {
        Step step = CurrentStep;
        RectD pr = _pageRects[step.Page];
        if (step.Explore) return Styled(ExploreStart(), pr, FocusStyles.None, 0);
        if (step.Stop == Step.Cover) return CoverShot(step.Page);
        Stop? stop = CurrentStop;
        if (Overview || stop is null) return WholeShot(step.Page, HalfFor(step));
        RectD box = BoxRect(step.Page, end && stop.To is RectD to ? to : stop.Box);
        return Styled(Inflate(box, Settings.Padding), box, string.IsNullOrEmpty(stop.Style) ? Book!.FocusStyle : stop.Style, 1);
    }

    /// <summary>What the reader was showing when it was asked to move on: needed to decide how a page should change.</summary>
    readonly record struct Leaving(Step Step, bool Whole, int Direction);

    bool IsTiled(int page) => Book!.Pages[page].Tiles is not null;

    /// <summary>
    /// True when pages are shown one at a time as pages of a book, rather than side by side. That is when
    /// the book look is switched on, except while a page is being explored and on tiled big scenes.
    /// </summary>
    public bool BookMode => HasBook && !CurrentStep.Explore && ShownAsBook(CurrentStep.Page);

    /// <summary>Move from wherever the camera is now (drift included) to the current step.</summary>
    void MoveTo(int? previousPage, bool instant = false, Leaving? leaving = null)
    {
        var s = Settings;
        double now = _now;
        _party = null;
        Step step = CurrentStep;
        int fromPage = previousPage ?? step.Page;
        bool pageChanged = fromPage != step.Page;
        Shot from = _shown with { Cam = DisplayedCam() };
        Shot to = Target();
        bool reduced = Reduced;
        var timing = MoveTime(from.Cam, to.Cam, s.MoveMs);
        double ms = reduced ? 340 : timing.Ms;
        // A stop can set its own travel time. It only applies when arriving from the stop before it.
        Stop? stop = CurrentStop;
        double? own = stop is not null && !Overview && _arrivedForward ? stop.MoveSeconds : null;
        if (own is double seconds && !reduced) ms = Math.Max(0, seconds * 1000);
        if (instant || (pageChanged && s.PageTurn == "cut" && own is null)) ms = 0;
        // Pull back mid-flight on page changes (if chosen) and on any long journey, so the child sees where they are going.
        double arcK = reduced ? 0 : pageChanged && s.PageTurn == "zoomOut" ? s.ArcAmount : timing.Far ? 0.45 : 0;
        bool fade = reduced;
        _fling = null;
        _note = null;
        StopClip();
        _lastTouch = now;

        // Opening the book from its cover, or closing it again: the cover swings over like a stiff board.
        bool fromCover = leaving is Leaving l0 && l0.Step.Stop == Step.Cover, toCover = step.Stop == Step.Cover;
        if ((fromCover || toCover) && ms > 0 && leaving is Leaving l1)
        {
            if (!reduced && CanSwingCover(l1, step))
            {
                StartCover(l1, from, to, own);
                RaiseChanged();
                return;
            }
            fade = true;
            ms = 340;
            arcK = 0;
        }
        // With the book look, a page change is a page turning over where it can be, and a dip to dark where
        // it can't. Explore pages and tiled scenes are not shown as books, so they keep the flight.
        // One page at a time, going from one whole page of a picture to its other page is a page turn too.
        else if ((pageChanged || PageOfSpreadChanges(leaving, step)) && ms > 0 && !reduced && leaving is Leaving left && s.BookLook && s.PageTurn == "flip"
            && !left.Step.Explore && !step.Explore && !IsTiled(fromPage) && !IsTiled(step.Page))
        {
            if (CanFlip(fromPage, left, step))
            {
                StartFlip(fromPage, left, from, to, own);
                RaiseChanged();
                return;
            }
            fade = true;
            ms = 340;
            arcK = 0;
        }

        _anim = new Anim
        {
            Kind = AnimKind.Move, From = from, To = to,
            Start = now, Ms = ms + (arcK != 0 ? 220 : 0),
            Easing = s.Easing,
            Smooth = s.ZoomPath == "smooth",
            ArcK = arcK, Fade = fade,
            FromPage = fromPage, ToPage = step.Page,
            FromCover = fromCover, ToCover = toCover,
        };
        if (ms == 0) Finish();
        else if (SoundOn) Sound.Whoosh(_anim.Ms);
        RaiseChanged();
    }

    /// <summary>
    /// Can this page change be shown as a page turning? A flip shows both pages whole, so it must never
    /// show the whole of a page that opens on a close-up to keep something hidden, unless the reader is
    /// already looking at that page whole or has seen it through to its last stop.
    /// </summary>
    /// <summary>One page at a time: the whole of one page of a picture gives way to the whole of its other page.</summary>
    bool PageOfSpreadChanges(Leaving? leaving, Step to) =>
        leaving is Leaving left && left.Step.Page == to.Page && left.Whole && to.Stop == Step.WholePage
        && HalfFor(left.Step) != Step.Both && HalfFor(left.Step) != HalfFor(to);

    /// <summary>The shape of what is seen whole: the spread, or one page of it.</summary>
    double ShapeOf(int page, int half)
    {
        RectD r = ShownAsBook(page) ? HalfRect(page, half) : _pageRects[page];
        return r.W / r.H;
    }

    bool CanFlip(int fromPage, Leaving left, Step to)
    {
        Page leave = Book!.Pages[fromPage], arrive = Book.Pages[to.Page];
        bool lastStopGoingOn = left.Direction > 0 && left.Step.Stop >= 0 && left.Step.Stop == leave.Stops.Count - 1;
        bool canLeave = left.Whole || leave.OpenWhole || lastStopGoingOn;
        bool canArrive = to.Stop == Step.WholePage || arrive.OpenWhole;
        // The two pages lie on top of each other during the turn, so they have to be the same shape.
        double a = ShapeOf(fromPage, HalfFor(left.Step)), b = ShapeOf(to.Page, HalfFor(to));
        return canLeave && canArrive && Math.Abs(a / b - 1) < 0.03;
    }

    /// <summary>
    /// Can the cover swing open (or shut) here? Opening shows the first spread whole, so the first page
    /// must be one that may be seen whole. Shutting starts from the first page seen whole, pulling back to
    /// it first if that is allowed. From anywhere else the book is simply faded shut.
    /// </summary>
    bool CanSwingCover(Leaving left, Step to)
    {
        Page first = Book!.Pages[0];
        if (left.Step.Stop == Step.Cover) return to.Page == 0 && (to.Stop == Step.WholePage || first.OpenWhole);
        return left.Step.Page == 0 && (left.Whole || first.OpenWhole);
    }

    /// <summary>
    /// The cover swinging open, with the camera easing from the closed book to the open one, then going in
    /// to the first stop if the first page doesn't open whole. Shutting is the same backwards, pulling back
    /// to the whole spread first when the camera is in close.
    /// </summary>
    void StartCover(Leaving left, Shot from, Shot to, double? ownSeconds)
    {
        var s = Settings;
        bool smooth = s.ZoomPath == "smooth";
        double ms = Math.Max(1, CoverMs * s.MoveMs / StandardMoveMs);
        Shot whole = WholeShot(0, HalfFor(left.Step.Stop == Step.Cover ? CurrentStep : left.Step)), closed = CoverShot(0);
        if (left.Step.Stop == Step.Cover)
        {
            var open = new Anim
            {
                Kind = AnimKind.Cover, From = closed, To = whole, Start = _now, Ms = ms,
                Easing = Easings.EaseInOutSine, Smooth = smooth, FromPage = 0, ToPage = 0, Forward = true,
            };
            if (CurrentStep.Stop != Step.WholePage)
            {
                double goIn = ownSeconds is double seconds ? Math.Max(1, seconds * 1000) : MoveTime(whole.Cam, to.Cam, s.MoveMs).Ms;
                open.After = new Anim { Kind = AnimKind.Move, From = whole, To = to, Ms = goIn, Easing = s.Easing, Smooth = smooth, FromPage = 0, ToPage = 0 };
            }
            _anim = open;
            if (SoundOn) Sound.PageTurn();
            return;
        }
        var shut = new Anim
        {
            Kind = AnimKind.Cover, From = whole, To = closed, Start = _now, Ms = ms,
            Easing = Easings.EaseInOutSine, Smooth = smooth, FromPage = 0, ToPage = 0, Forward = false,
        };
        if (left.Whole)
        {
            shut.From = from;
            _anim = shut;
            if (SoundOn) Sound.PageTurn();
            return;
        }
        _anim = new Anim
        {
            Kind = AnimKind.Move, From = from, To = whole, Start = _now,
            Ms = MoveTime(from.Cam, whole.Cam, s.MoveMs).Ms, Easing = s.Easing, Smooth = smooth,
            FromPage = 0, ToPage = 0, After = shut,
        };
        if (SoundOn) Sound.Whoosh(_anim.Ms);
    }

    /// <summary>
    /// A page turn in up to three parts: pull back to the whole page if the camera is in close, turn
    /// the page, then go in to the stop if the new page does not open whole.
    /// </summary>
    void StartFlip(int fromPage, Leaving left, Shot from, Shot to, double? ownSeconds)
    {
        var s = Settings;
        Step step = CurrentStep;
        bool smooth = s.ZoomPath == "smooth";
        int fromHalf = HalfFor(left.Step), toHalf = HalfFor(step);
        Shot wholeFrom = WholeShot(fromPage, fromHalf), wholeTo = WholeShot(step.Page, toHalf);
        var flip = new Anim
        {
            Kind = AnimKind.Flip, From = wholeTo, To = wholeTo, Start = _now,
            Ms = Math.Max(1, FlipMs * s.MoveMs / StandardMoveMs),
            FromPage = fromPage, ToPage = step.Page, Forward = left.Direction >= 0,
            FromHalf = fromHalf, ToHalf = toHalf,
        };
        if (step.Stop != Step.WholePage)
        {
            double goIn = ownSeconds is double seconds ? Math.Max(1, seconds * 1000) : MoveTime(wholeTo.Cam, to.Cam, s.MoveMs).Ms;
            flip.After = new Anim
            {
                Kind = AnimKind.Move, From = wholeTo, To = to, Ms = goIn, Easing = s.Easing, Smooth = smooth,
                FromPage = step.Page, ToPage = step.Page,
            };
        }
        // Already on the whole page, or so nearly (a last stop that covers the page) that pulling back
        // would only be a pause: turn straight away.
        RectD a = from.Cam, b = wholeFrom.Cam;
        bool alreadyWhole = Math.Abs(Math.Log(a.W / b.W)) < 0.05 && Math.Abs(Math.Log(a.H / b.H)) < 0.05
                         && Math.Abs(a.CenterX - b.CenterX) < b.W * 0.03 && Math.Abs(a.CenterY - b.CenterY) < b.H * 0.03;
        if (alreadyWhole)
        {
            flip.From = wholeTo with { Dim = from.Dim, Vig = from.Vig, Spot = from.Spot };
            _anim = flip;
            if (SoundOn) Sound.PageTurn();
            return;
        }
        _anim = new Anim
        {
            Kind = AnimKind.Move, From = from, To = wholeFrom, Start = _now,
            Ms = MoveTime(from.Cam, wholeFrom.Cam, s.MoveMs).Ms, Easing = s.Easing, Smooth = smooth,
            FromPage = fromPage, ToPage = fromPage, After = flip,
        };
        if (SoundOn) Sound.Whoosh(_anim.Ms);
    }

    /// <summary>Explore: animate the camera to a box, then optionally run something.</summary>
    void FlyTo(RectD cam, Action? then = null, bool quick = false)
    {
        double now = _now;
        Shot from = _shown with { Cam = VisibleCam() };
        Shot to = _shown with { Cam = ClampCam(MatchAspect(cam)) };
        bool reduced = Reduced;
        var timing = MoveTime(from.Cam, to.Cam, Math.Max(450, Settings.MoveMs));
        double arcK = !reduced && !quick && timing.Far ? 0.45 : 0;
        _fling = null;
        _anim = new Anim
        {
            Kind = AnimKind.Fly, From = from, To = to,
            Start = now, Ms = reduced ? 340 : quick ? 260 : timing.Ms + (arcK != 0 ? 220 : 0),
            Easing = Easings.EaseInOutCubic, Smooth = true, ArcK = arcK, Fade = reduced && !quick, Then = then,
        };
        if (!quick && SoundOn) Sound.Whoosh(_anim.Ms);
    }

    void Finish()
    {
        Anim done = _anim!;
        _shown = done.To with { Fade = 0 };
        if (done.After is Anim next)
        {
            // The next part of the same journey starts exactly where this one ended, not a frame late.
            next.Start = Math.Min(_now, done.Start + done.Ms);
            _anim = next;
            if (next.Kind is AnimKind.Flip or AnimKind.Cover && SoundOn) Sound.PageTurn();
            return;
        }
        _anim = null;
        _restSince = _now;
        if (done.Kind is AnimKind.Move or AnimKind.Flip or AnimKind.Cover) { Arrive(); AfterArrive(); }
        done.Then?.Invoke();
    }

    /// <summary>Skip to the end of a journey, however many parts of it are still to come.</summary>
    void FinishJourney()
    {
        while (_anim?.After is Anim next) _anim = next;
        Finish();
    }

    /// <summary>True while the camera is on its way to a step: flying, turning a page, or fading across.</summary>
    bool Travelling => _anim is { Kind: AnimKind.Move or AnimKind.Flip or AnimKind.Cover };

    /// <summary>A stop with an end box glides there once the camera has arrived.</summary>
    void AfterArrive()
    {
        Stop? stop = CurrentStop;
        if (Overview || stop?.To is null) return;
        // With reduced motion the glide becomes a short pause and a fade to where it would have ended.
        bool reduced = Reduced;
        _anim = new Anim
        {
            Kind = AnimKind.Pan,
            From = _shown,
            To = Target(true),
            Start = _now + (reduced ? 1400 : 0),
            Ms = reduced ? 340 : (stop.PanSeconds is > 0 ? stop.PanSeconds.Value : 4) * 1000,
            Easing = Easings.EaseInOutSine,
            Smooth = true,
            ArcK = 0, Fade = reduced,
            FromPage = CurrentStep.Page, ToPage = CurrentStep.Page,
        };
    }

    /// <summary>Arriving at a stop: its sound clip, and any lightning or shake set for it.</summary>
    void Arrive()
    {
        Stop? stop = CurrentStop;
        if (Overview || stop is null) return;
        Step step = CurrentStep;
        if (!string.IsNullOrEmpty(stop.Sound)) PlayClip(stop.Sound);
        PlayVoice(stop);
        foreach (PageEffect e in Book!.Pages[step.Page].Effects)
        {
            if (e.StopOrFirst - 1 != step.Stop) continue;
            if (e.Type == PageEffect.Lightning)
            {
                // The flash comes a beat after arriving, and the rumble a little after the flash.
                if (!Reduced) _flash = _now + 700;
                if (SoundOn) Sound.Thunder(0.85);
            }
            else if (e.Type == PageEffect.Shake)
            {
                // Silent on purpose: a stop that wants a sound with its shake sets its own sound file.
                if (!Reduced) _shake = (_now + 120, e.StrengthOrDefault);
            }
        }
    }

    /// <summary>Go on (+1) or back (-1) a step.</summary>
    public void Go(int delta)
    {
        if (Finished || !HasBook) return;
        double now = _now;
        // Young children often tap twice without meaning to: ignore a second one that comes straight away.
        if (now - _lastGo < 300) return;
        _lastGo = now;
        // A tap while the camera is still travelling (or a page is still turning) just finishes that move.
        // The next tap goes on.
        if (delta > 0 && Travelling)
        {
            FinishJourney();
            RaiseChanged();
            return;
        }
        _moved = true;
        Step fromStep = CurrentStep;
        int before = fromStep.Page;
        bool wasOverview = Overview;
        var leaving = new Leaving(fromStep, wasOverview || fromStep.Stop == Step.WholePage, delta);
        Overview = false;
        int next = StepIndex + delta;
        _arrivedForward = delta > 0;
        if (next >= _steps.Count) { Finished = true; RaiseChanged(); return; }
        if (next < 0) { if (wasOverview) MoveTo(before); return; }
        StepIndex = next;
        MoveTo(before, false, leaving);
    }

    public void Next() => Go(1);
    public void Prev() => Go(-1);

    /// <summary>Whole page and back. While exploring: zoom right out, or back in to where exploring started.</summary>
    public void ToggleOverview()
    {
        if (!HasBook) return;
        if (Exploring)
        {
            RectD whole = MaxCam();
            bool zoomedOut = VisibleCam().W >= whole.W * 0.98;
            FlyTo(zoomedOut ? ExploreStart() : whole);
            return;
        }
        if (!Overview && CurrentStop is null) { Next(); return; }
        Overview = !Overview;
        MoveTo(CurrentStep.Page);
    }

    public void SetOverview(bool on)
    {
        if (!HasBook) return;
        if (Exploring) { if (on) FlyTo(MaxCam()); return; }
        if (Overview != on) ToggleOverview();
    }

    /// <summary>Back to the very start, with nothing found.</summary>
    public void Restart()
    {
        if (!HasBook) return;
        Finished = false;
        Overview = false;
        _found.Clear();
        Step fromStep = CurrentStep;
        var leaving = new Leaving(fromStep, fromStep.Stop == Step.WholePage, -1);
        StepIndex = 0;
        MoveTo(fromStep.Page, false, leaving);
    }

    /// <summary>Replays the move into the current step from the one before it.</summary>
    public void Replay()
    {
        if (!HasBook || StepIndex == 0) return;
        StepIndex--;
        Overview = false;
        _anim = null;
        _shown = Target();
        _lastGo = double.NegativeInfinity;
        Go(1);
    }

    /// <summary>Keyboard shortcuts, already translated from whatever the keyboard calls the keys.</summary>
    public void HandleKey(ReaderKey key)
    {
        if (!HasBook) return;
        switch (key)
        {
            case ReaderKey.Next: Next(); break;
            case ReaderKey.Previous: Prev(); break;
            case ReaderKey.ToggleWholePage: ToggleOverview(); break;
            case ReaderKey.CloseUp: SetOverview(false); break;
            case ReaderKey.ZoomIn: ZoomStep(1.7); break;
            case ReaderKey.ZoomOut: ZoomStep(1 / 1.7); break;
        }
    }

    // ---------------------------------------------------------------- things to find

    public bool IsFound(int index) => _found.Contains((CurrentStep.Page, index));

    /// <summary>Hotspot indexes that make up the page's story, in story order.</summary>
    public List<int> StoryOrder()
    {
        var hs = CurrentPage.Hotspots;
        return Enumerable.Range(0, hs.Count).Where(i => hs[i].Story != 0).OrderBy(i => hs[i].Story).ToList();
    }

    /// <summary>The story step the child is looking for now, or -1.</summary>
    public int NextStory()
    {
        if (!Exploring) return -1;
        foreach (int i in StoryOrder()) if (!IsFound(i)) return i;
        return -1;
    }

    /// <summary>What hints and Show me lead to: the next story step first, then anything else not found yet.</summary>
    public List<int> UnfoundHotspots()
    {
        var list = new List<int>();
        if (!Exploring) return list;
        var hs = CurrentPage.Hotspots;
        int next = NextStory();
        if (next >= 0) list.Add(next);
        if (ExtrasOpen())
        {
            for (int i = 0; i < hs.Count; i++) if (hs[i].Story == 0 && !IsFound(i)) list.Add(i);
        }
        return list;
    }

    /// <summary>Are the just-for-fun things to find switched on right now? (A parent setting.)</summary>
    public bool ExtrasOpen() => Settings.Extras switch
    {
        "off" => false,
        "afterStory" => NextStory() < 0,   // no story on the page counts as finished
        _ => true,
    };

    /// <summary>Explore: fly to the next thing not found yet and make it react.</summary>
    public void ShowMe()
    {
        var unfound = UnfoundHotspots();
        if (unfound.Count == 0) return;
        int next = unfound[0];
        RectD box = BoxRect(CurrentStep.Page, CurrentPage.Hotspots[next].Box);
        _lastTouch = _now;
        FlyTo(Inflate(box, Math.Max(box.H, box.W) * 0.6), () => React(next));
    }

    /// <summary>Show a caption for a while (longer for longer text), then clear it.</summary>
    void Say(string text, double extraMs = 0, string? ask = null)
    {
        ask ??= "";
        double ms = Math.Max(4500, 2500 + (text.Length + ask.Length) * 65) + extraMs;
        _note = new Note(text, ask, _now + ms);
        RaiseChanged();
    }

    /// <summary>A hotspot was tapped: sparkle and show its caption. Story steps only open in order.</summary>
    public void React(int index)
    {
        if (!Exploring) return;
        int p = CurrentStep.Page;
        Hotspot h = CurrentPage.Hotspots[index];
        double now = _now;
        if (h.Story != 0 && !IsFound(index) && index != NextStory())
        {
            Hotspot first = CurrentPage.Hotspots[NextStory()];
            _bursts.Add(new Burst(BoxRect(p, h.Box), now, true));
            if (SoundOn) Sound.Locked();
            Say("Not yet! First find " + (string.IsNullOrEmpty(first.Name) ? "the next part of the story" : first.Name) + ".");
            return;
        }
        bool wasNew = !IsFound(index);
        _found.Add((p, index));
        if (h.Effect != "none") _bursts.Add(new Burst(BoxRect(p, h.Box), now, false));
        bool storyDone = wasNew && h.Story != 0 && NextStory() < 0;
        if (storyDone) _party = now;
        if (SoundOn) { if (storyDone) Sound.Party(); else Sound.Find(); }
        if (!string.IsNullOrEmpty(h.Sound)) PlayClip(h.Sound);
        if (!string.IsNullOrEmpty(h.Caption)) Say(h.Caption, storyDone ? 3000 : 0, h.Ask);
        else { _note = null; RaiseChanged(); }
    }

    /// <summary>Which thing to find is under a touch, or -1. Small things get a touch area of at least minTargetCm.</summary>
    public int HotspotAt(double sx, double sy)
    {
        if (!Exploring || ViewWidth <= 0) return -1;
        ViewTransform v = GetView(withShake: false);
        int p = CurrentStep.Page;
        double min = Settings.MinTargetCm * UnitsPerCm;
        bool extras = ExtrasOpen();
        var hs = CurrentPage.Hotspots;
        int inside = -1, near = -1;
        double insideArea = double.PositiveInfinity, nearDist = double.PositiveInfinity;
        for (int i = 0; i < hs.Count; i++)
        {
            if (hs[i].Story == 0 && !extras) continue;
            RectD r = v.ToScreen(BoxRect(p, hs[i].Box));
            if (r.Contains(sx, sy))
            {
                if (r.W * r.H < insideArea) { inside = i; insideArea = r.W * r.H; }   // the smallest box under the finger
                continue;
            }
            double w = Math.Max(r.W, min), h = Math.Max(r.H, min);
            if (Math.Abs(sx - r.CenterX) <= w / 2 && Math.Abs(sy - r.CenterY) <= h / 2)
            {
                double d = Math.Sqrt(Math.Pow(sx - r.CenterX, 2) + Math.Pow(sy - r.CenterY, 2));
                if (d < nearDist) { near = i; nearDist = d; }                         // otherwise the nearest small one
            }
        }
        if (inside >= 0) return inside;
        // The story step the child is being guided to wins over a nearby extra when the touch is within its area.
        int next = NextStory();
        if (next >= 0 && near >= 0 && near != next)
        {
            RectD nb = v.ToScreen(BoxRect(p, hs[next].Box));
            if (Math.Abs(sx - nb.CenterX) <= Math.Max(nb.W, min) / 2 && Math.Abs(sy - nb.CenterY) <= Math.Max(nb.H, min) / 2) return next;
        }
        return near;
    }

    public void TapAt(double sx, double sy)
    {
        if (!Exploring) return;
        int hit = HotspotAt(sx, sy);
        _lastTouch = _now;
        if (hit >= 0) React(hit);
        else if (SoundOn) Sound.Tap();
    }

    public void DoubleTapAt(double sx, double sy)
    {
        if (!Exploring) return;
        RectD cam = TakeCamera();
        bool zoomedIn = cam.H < 0.45;
        FlyTo(zoomedIn ? MaxCam() : ZoomAt(cam, 2.2, sx, sy));
    }

    // ---------------------------------------------------------------- explore camera

    double ViewAspect()
    {
        if (ViewWidth <= 0 || ViewHeight <= 0) return 1.6;   // not laid out yet: any sensible shape will do
        return ViewWidth / Math.Max(1, ViewHeight - Insets.Top - Insets.Bottom);
    }

    RectD MatchAspect(RectD cam) => FitToAspect(cam, ViewAspect());

    /// <summary>The area actually on screen right now, in world units, with the screen's shape.</summary>
    public RectD VisibleCam() => MatchAspect(DisplayedCam());

    /// <summary>Fully zoomed out: the whole page.</summary>
    public RectD MaxCam() => MatchAspect(Inflate(_pageRects[CurrentStep.Page], 0.03));

    /// <summary>Keep the explore camera on the page and within sensible zoom limits.</summary>
    public RectD ClampCam(RectD cam)
    {
        RectD pr = _pageRects[CurrentStep.Page];
        RectD max = MaxCam();
        RectD c = cam;
        if (c.W > max.W) c = ScaleAround(c, max.W / c.W);
        if (c.H < ClosestZoom) c = ScaleAround(c, ClosestZoom / c.H);
        const double m = 0.03;
        static double Fit(double pos, double size, double lo, double len) =>
            size >= len + 2 * m ? lo + len / 2 - size / 2 : Clamp(pos, lo - m, lo + len + m - size);
        return c with { X = Fit(c.X, c.W, pr.X, pr.W), Y = Fit(c.Y, c.H, pr.Y, pr.H) };
    }

    /// <summary>Explore: the user takes the camera (stops any animation or glide). Returns where it is.</summary>
    public RectD TakeCamera()
    {
        RectD cam = VisibleCam();
        if (Travelling) FinishJourney();
        _anim = null;
        _fling = null;
        _shown = _shown with { Cam = cam, Dim = 0, Vig = 0, Spot = 0, Fade = 0 };
        _lastTouch = _now;
        return cam;
    }

    /// <summary>soft: let a drag pull a little way past the edge, so the child feels where the scene ends.</summary>
    public void SetCam(RectD cam, bool soft = false)
    {
        RectD c = ClampCam(cam);
        if (soft && !Reduced && Math.Abs(c.W - cam.W) < 1e-6)
        {
            static double Give(double want, double got, double size) => got + Clamp((want - got) * 0.35, -size * 0.12, size * 0.12);
            c = c with { X = Give(cam.X, c.X, c.W), Y = Give(cam.Y, c.Y, c.H) };
        }
        _shown = _shown with { Cam = c };
        _lastTouch = _now;
    }

    /// <summary>After a drag past the edge, ease back inside. Returns true if it had to.</summary>
    public bool Settle()
    {
        RectD cam = _shown.Cam, c = ClampCam(cam);
        if (Math.Abs(c.X - cam.X) < 1e-5 && Math.Abs(c.Y - cam.Y) < 1e-5) return false;
        FlyTo(c, null, true);
        return true;
    }

    /// <summary>Explore: keep gliding after a flick. The speed is in world units per millisecond.</summary>
    public void StartFling(double vx, double vy)
    {
        if (Exploring && !Reduced) _fling = (vx, vy);
    }

    /// <summary>Screen units per world unit for a camera that matches the screen's shape.</summary>
    public double ScreenScale(RectD cam) => ViewWidth / cam.W;

    /// <summary>Screen point to world point, for a camera that matches the screen shape.</summary>
    public PointD ToWorld(RectD cam, double sx, double sy)
    {
        double sc = ScreenScale(cam);
        return new PointD(cam.X + sx / sc, cam.Y + (sy - Insets.Top) / sc);
    }

    /// <summary>Zoom by factor f keeping the world point under (sx, sy) fixed.</summary>
    public RectD ZoomAt(RectD cam, double f, double sx, double sy)
    {
        PointD a = ToWorld(cam, sx, sy);
        double w = cam.W / f, h = cam.H / f;
        double sc = ViewWidth / w;
        return new RectD(a.X - sx / sc, a.Y - (sy - Insets.Top) / sc, w, h);
    }

    /// <summary>Explore: zoom in or out a step, for children who can't pinch yet.</summary>
    public void ZoomStep(double f)
    {
        if (!Exploring) return;
        RectD cam = TakeCamera();
        if (SoundOn) Sound.Zoom(f > 1);
        double midY = Insets.Top + (ViewHeight - Insets.Top - Insets.Bottom) / 2;
        FlyTo(ZoomAt(cam, f, ViewWidth / 2, midY), RaiseChanged, true);
    }

    /// <summary>Centre the view on a point of the corner map (a screen point inside the map).</summary>
    public void MapGoTo(double sx, double sy)
    {
        MapDraw? map = GetMap();
        if (map is null) return;
        RectD m = map.Rect, pr = _pageRects[CurrentStep.Page];
        RectD cam = TakeCamera();
        double wx = pr.X + (sx - m.X) / m.W * pr.W, wy = pr.Y + (sy - m.Y) / m.H * pr.H;
        SetCam(new RectD(wx - cam.W / 2, wy - cam.H / 2, cam.W, cam.H));
    }

    // ---------------------------------------------------------------- the frame

    /// <summary>The camera as drawn: where it rests, plus any slow drift while resting on a stop.</summary>
    public RectD DisplayedCam()
    {
        if (!_hasShown) return new RectD(0, 0, 1, 1);
        if (_anim is not null) return _shown.Cam;
        var s = Settings;
        Stop? stop = CurrentStop;
        if (s.Drift == "off" || Overview || stop is null || stop.To is not null || Reduced) return _shown.Cam;
        double p = Easings.InOutSine(Math.Min(1, (_now - _restSince) / (s.DriftSeconds * 1000)));
        if (s.Drift == "zoomIn") return ScaleAround(_shown.Cam, 1 - s.DriftAmount * p);
        return ScaleAround(_shown.Cam, 1, _shown.Cam.W * s.DriftAmount * p);
    }

    /// <summary>Drift starts again from here. Call when a drift setting changes.</summary>
    public void RestartRest() => _restSince = _now;

    /// <summary>
    /// Move everything on to <paramref name="nowMs"/>. Call once a frame. Everything is worked out from
    /// the time that has passed, so the result is the same at 30, 60 or 144 frames a second.
    /// </summary>
    public void Update(double nowMs)
    {
        double dt = _lastUpdate is double last ? Math.Max(0, nowMs - last) : 0;
        _lastUpdate = nowMs;
        SetTime(nowMs);
        double now = _now;
        UpdateVoice();

        // The picture eases towards the room the screen has asked for.
        Insets.Top = EaseInset(Insets.Top, InsetGoal.Top, ref _insetSpeedTop, dt);
        Insets.Bottom = EaseInset(Insets.Bottom, InsetGoal.Bottom, ref _insetSpeedBottom, dt);

        if (_anim is Anim a)
        {
            double raw = a.Ms == 0 ? 1 : Clamp((now - a.Start) / a.Ms, 0, 1);
            if (a.Kind == AnimKind.Flip)
            {
                // The camera holds still on the whole page while the leaf turns (see GetFlip). Only the
                // strength of the focus effect eases, from what it was to what the whole page has.
                _shown = a.To with
                {
                    Dim = Lerp(a.From.Dim, a.To.Dim, raw),
                    Vig = Lerp(a.From.Vig, a.To.Vig, raw),
                    Spot = Lerp(a.From.Spot, a.To.Spot, raw),
                };
            }
            else if (a.Fade)
            {
                // No travelling (reduced motion, or a page that must not be seen whole on the way in):
                // dip to dark, switch, and come back.
                Shot side = raw < 0.5 ? a.From : a.To;
                _shown = side with { Fade = 1 - Math.Abs(1 - raw * 2) };
            }
            else
            {
                double e = Easings.Apply(a.Easing, raw);
                RectD cam = MixRect(a.From.Cam, a.To.Cam, e, a.Smooth);
                if (a.ArcK != 0)
                {
                    // Swell the camera mid-move so both ends fit, then settle in.
                    RectD u = Inflate(Union(a.From.Cam, a.To.Cam), 0.05);
                    RectD mid = MixRect(a.From.Cam, a.To.Cam, 0.5, a.Smooth);
                    double need = Math.Max(u.W / mid.W, u.H / mid.H) - 1;
                    cam = ScaleAround(cam, 1 + Math.Max(0, need) * a.ArcK * Math.Sin(Math.PI * Clamp(e, 0, 1)));
                }
                _shown = new Shot(cam,
                    MixRect(a.From.Focus, a.To.Focus, Math.Min(1, e), a.Smooth),
                    Lerp(a.From.Dim, a.To.Dim, raw),
                    Lerp(a.From.Vig, a.To.Vig, raw),
                    Lerp(a.From.Spot, a.To.Spot, raw));
            }
            if (raw >= 1) { Finish(); RaiseChanged(); }
        }
        else if (_fling is (double vx, double vy))
        {
            // Glide after a flick, slowing down. The distance is worked out exactly for the time passed,
            // so a slow frame glides just as far as several quick ones.
            RectD cam = _shown.Cam;
            double perMs = Math.Log(0.9 + 0.08 * Settings.Glide) / 16;      // speed falls by this (natural log) every millisecond
            double decay = Math.Exp(perMs * dt);
            double travel = Math.Abs(perMs) < 1e-12 ? dt : (decay - 1) / perMs;
            double wantX = cam.X + vx * travel, wantY = cam.Y + vy * travel;
            RectD moved = ClampCam(new RectD(wantX, wantY, cam.W, cam.H));
            if (Math.Abs(moved.X - wantX) > 1e-6) vx = 0;
            if (Math.Abs(moved.Y - wantY) > 1e-6) vy = 0;
            _shown = _shown with { Cam = moved };
            vx *= decay;
            vy *= decay;
            _fling = Math.Sqrt(vx * vx + vy * vy) < 1e-5 ? null : (vx, vy);
        }
        else if (HasBook && !Finished && !Exploring && Visible && ViewWidth > 0)
        {
            // Moving on without a tap: a stop marked to go on by itself (only when we came to it going forwards,
            // so going back never gets pushed forward again), or watch mode.
            Stop? stop = CurrentStop;
            if (Sound.ClipPlaying || Reading) _restSince = now;   // let narration finish first
            double rested = now - _restSince;
            bool own = stop is not null && stop.Auto && _arrivedForward && !Overview;
            double hold = stop?.HoldSeconds is > 0 ? stop.HoldSeconds.Value : 0;
            // After a recorded voice has read the words, watch mode goes on after a short breath rather
            // than the full time a reader would need.
            double watchHold = hold > 0 ? hold : ReadingDone ? Math.Min(1.5, Settings.AutoSeconds) : Settings.AutoSeconds;
            bool goOn = own ? rested > (hold > 0 ? hold : 1.5) * 1000
                            : Settings.AutoPlay && rested > watchHold * 1000;
            if (goOn)
            {
                _lastGo = double.NegativeInfinity;
                Next();
            }
        }

        // A hotspot's caption clears itself after a while.
        if (_note is not null && now >= _note.Until + 50) { _note = null; RaiseChanged(); }

        UpdateWeather(dt);

        // Forget what has finished playing.
        _bursts.RemoveAll(b => now - b.Start >= 1100);
        _ripples.RemoveAll(r => now - r.Start >= 450);
        if (_shake is (double shakeStart, _) && now - shakeStart > 620) _shake = null;
        if (_flash is double flashStart && now - flashStart > 800) _flash = null;
        if (_party is double partyStart && now - partyStart > 4200) _party = null;

        Restless = IsRestless();
    }

    bool IsRestless()
    {
        if (!HasBook) return false;
        if (_anim is not null || _fling is not null) return true;
        if (Insets.Top != InsetGoal.Top || Insets.Bottom != InsetGoal.Bottom) return true;
        if (_bursts.Count > 0 || _ripples.Count > 0 || _party is not null || _flash is not null || _shake is not null) return true;
        // The words light up one by one while the voice reads them.
        if (Reading && _voice!.Words.Count > 0) return true;
        if (_shown.Fade > 0) return true;

        // Weather that is showing or fading, and pictures on top that are moving, fading or bobbing.
        foreach (double level in _fx.Values) if (level > 0) return true;
        bool reduced = Reduced;
        int page = CurrentStep.Page;
        foreach (var (key, st) in _layers)
        {
            if (st.Vis is > 0 and < 1 || st.T is > 0 and < 1) return true;
            if (key.Page != page || st.Vis <= 0 || reduced) continue;
            Layer l = Book!.Pages[key.Page].Layers[key.Index];
            if (l.Idle is "bob" or "sway") return true;
            var (show, moved) = LayerWanted(key.Page, l);
            if ((show ? 1 : 0) != st.Vis || moved != st.T) return true;
        }

        // A slow drift while resting on a stop.
        Stop? stop = CurrentStop;
        if (Settings.Drift != "off" && !Overview && stop is not null && stop.To is null && !reduced
            && _now - _restSince < Settings.DriftSeconds * 1000 + 50) return true;

        // The story beacon and the idle hint both pulse.
        if (Exploring && ViewWidth > 0 && (NextStory() >= 0 || GetHint(GetView(withShake: false)) is not null)) return true;
        return false;
    }

    /// <summary>
    /// Move an inset towards its goal the way a well-damped spring would: it starts gently, covers most of
    /// the way in about a quarter of a second and settles without overshooting. (The browser reader takes
    /// a fixed share of the gap every frame, which starts with a jolt: the whole picture jumps a centimetre
    /// in the first frame whenever a caption appears.) Worked out exactly for the time passed, so it is the
    /// same at any frame rate.
    /// </summary>
    static double EaseInset(double have, double goal, ref double speed, double dt)
    {
        const double stiffness = 0.016;                       // per millisecond: higher is quicker
        double gap = have - goal;
        if (Math.Abs(gap) < 0.5 && Math.Abs(speed) < 0.01) { speed = 0; return goal; }
        if (dt <= 0) return have;
        double b = speed + stiffness * gap, fade = Math.Exp(-stiffness * dt);
        double next = (gap + b * dt) * fade;
        speed = (b - stiffness * (gap + b * dt)) * fade;
        return goal + next;
    }

    void RaiseChanged() => Changed?.Invoke(State);

    public ReaderState State
    {
        get
        {
            if (!HasBook) return new ReaderState { PageCount = Book?.Pages.Count ?? 0 };
            Step step = CurrentStep;
            Stop? stop = CurrentStop;
            bool exploring = Exploring;
            var hs = exploring ? CurrentPage.Hotspots : new List<Hotspot>();
            double now = _now;
            bool noteShowing = _note is not null && _note.Until > now;
            bool found(int i) => _found.Contains((step.Page, i));
            return new ReaderState
            {
                StepIndex = StepIndex,
                StepCount = _steps.Count,
                Page = step.Page,
                PageCount = Book!.Pages.Count,
                Overview = Overview,
                WholePage = Overview || (stop is null && !exploring),
                Finished = Finished,
                Exploring = exploring,
                Caption = exploring ? (noteShowing ? _note!.Text : "")
                                    : (!Overview && stop?.Caption is string c ? c : ""),
                Ask = !Settings.Prompts ? ""
                    : exploring ? (noteShowing ? _note!.Ask : "")
                    : (!Overview && stop?.Ask is string q ? q : ""),
                FirstStep = !_moved && !exploring && StepIndex == 0,
                Reading = Reading && !Overview,
                ReadingDone = ReadingDone && !Overview,
                CanZoomIn = exploring && VisibleCam().H > ClosestZoom * 1.03,
                CanZoomOut = exploring && VisibleCam().W < MaxCam().W * 0.98,
                CanGoBack = StepIndex > 0 || Overview,
                CanShowMe = exploring && CurrentPage.Guidance.ShowMe && UnfoundHotspots().Count > 0,
                Found = Enumerable.Range(0, hs.Count).Count(found),
                HotspotCount = hs.Count,
                StoryTotal = hs.Count(h => h.Story != 0),
                StoryFound = Enumerable.Range(0, hs.Count).Count(i => hs[i].Story != 0 && found(i)),
                ExtraTotal = Settings.Extras == "off" ? 0 : hs.Count(h => h.Story == 0),
                ExtraFound = Enumerable.Range(0, hs.Count).Count(i => hs[i].Story == 0 && found(i)),
            };
        }
    }

    // ---------------------------------------------------------------- moving effects and layers

    /// <summary>Is this effect running at the step we're on? 1 or 0; the level eases towards it.</summary>
    public int EffectWanted(int page, PageEffect e)
    {
        if (!HasBook || Reduced) return 0;
        Step step = CurrentStep;
        if (step.Page != page) return 0;
        // -1 is the whole page as it opens, -2 is exploring. The whole page shown again after the stops
        // counts as being past the last stop, so what started during the page is still there.
        // The closed book shows nothing of its first page.
        if (step.Stop == Step.Cover) return 0;
        int at = step.Closing ? Book!.Pages[page].Stops.Count : step.Stop;
        if (e.From is > 1 && at >= -1 && at < e.From - 1) return 0;
        if (e.To is >= 1 && (at == Step.Exploring || at > e.To - 1)) return 0;
        return 1;
    }

    /// <summary>How strongly a page's effect is showing right now, 0 to 1. It fades in and out over about half a second.</summary>
    public double EffectLevel(int page, int effectIndex) => _fx.TryGetValue((page, effectIndex), out double level) ? level : 0;

    /// <summary>Where a layer should be for the step we're on: showing or not, and moved (1) or not (0).</summary>
    public (bool Show, int Moved) LayerWanted(int page, Layer l)
    {
        // Before its page is reached a layer waits at its start; once the page is behind us it stays at its end.
        if (!HasBook || CurrentStep.Page < page) return (!(l.From is > 1), 0);
        Step step = CurrentStep;
        if (step.Page > page) return (!(l.To is >= 1), 1);
        int at = step.Closing ? Book!.Pages[page].Stops.Count : step.Stop;
        bool show = !(l.From is > 1 && at >= -1 && at < l.From - 1) && !(l.To is >= 1 && (at == Step.Exploring || at > l.To - 1));
        int moved = l.MoveTo is not null && (at == Step.Exploring || at >= l.MoveAtOrFirst - 1) ? 1 : 0;
        return (show, moved);
    }

    /// <summary>Effects fade in and out; layers ease towards where they should be.</summary>
    void UpdateWeather(double dt)
    {
        if (Book is null) return;
        double loud = 0;
        bool showing = Visible && ViewWidth > 0;
        if (!showing) StopClip();
        double ease = 1 - Math.Exp(-dt / 250);
        for (int i = 0; i < Book.Pages.Count; i++)
        {
            Page page = Book.Pages[i];
            for (int k = 0; k < page.Effects.Count; k++)
            {
                PageEffect e = page.Effects[k];
                if (!e.IsFalling) continue;
                double want = showing ? EffectWanted(i, e) : 0, have = EffectLevel(i, k);
                double level = Math.Abs(want - have) < 0.01 ? want : have + (want - have) * ease;
                _fx[(i, k)] = level;
                if (e.Type == PageEffect.Rain) loud = Math.Max(loud, level * (0.4 + 0.6 * e.AmountOrDefault));
            }
            for (int k = 0; k < page.Layers.Count; k++)
            {
                Layer l = page.Layers[k];
                if (!_layers.TryGetValue((i, k), out LayerState? st)) _layers[(i, k)] = st = new LayerState();
                var (show, moved) = LayerWanted(i, l);
                bool here = HasBook && CurrentStep.Page == i;
                bool first = st.Vis < 0, snap = first || Reduced || !here;
                st.Vis = snap ? (show ? 1 : 0) : Clamp(st.Vis + (show ? 1 : -1) * dt / 240, 0, 1);
                st.T = snap ? moved : Clamp(st.T + (moved == 1 ? 1 : -1) * dt / (l.MoveSecondsOrDefault * 1000), 0, 1);
            }
        }
        if (!SoundOn) loud = 0;
        if (Math.Abs(loud - _rainLoud) > 0.01 || (loud == 0 && _rainLoud != 0))
        {
            _rainLoud = loud;
            Sound.Rain(loud);
        }
    }

    /// <summary>How far a layer has moved from its box to its moveTo box, 0 to 1 (before easing).</summary>
    public double LayerProgress(int page, int layerIndex) => _layers.TryGetValue((page, layerIndex), out LayerState? st) ? Math.Max(0, st.T) : 0;

    /// <summary>How visible a layer is, 0 to 1.</summary>
    public double LayerOpacity(int page, int layerIndex) => _layers.TryGetValue((page, layerIndex), out LayerState? st) ? Math.Max(0, st.Vis) : 0;

    /// <summary>
    /// A page's pictures-on-top, where they are right now. Boxes are fractions of the page; Rotation
    /// (radians) is about the middle of the box's bottom edge. Layers that can't be seen are left out.
    /// </summary>
    public List<LayerDraw> LayersFor(int page)
    {
        var list = new List<LayerDraw>();
        if (Book is null) return list;
        var layers = Book.Pages[page].Layers;
        double aspect = _pageRects[page].W / _pageRects[page].H;
        bool reduced = Reduced;
        for (int k = 0; k < layers.Count; k++)
        {
            Layer l = layers[k];
            if (!_layers.TryGetValue((page, k), out LayerState? st) || st.Vis < 0.01) continue;
            double e = Easings.InOutSine(Clamp(st.T, 0, 1));
            RectD b = l.MoveTo is RectD to ? MixRect(l.Box, to, e, false) : l.Box;
            double y = b.Y, rotation = 0;
            // A hop lifts the picture in an arc: higher for a longer jump.
            if (l.MoveTo is RectD dest && l.Path == "hop")
                y -= Math.Sin(Math.PI * e) * Math.Max(b.H * 0.7, Math.Abs(dest.X - l.Box.X) * aspect * 0.3);
            bool still = reduced || (st.T > 0.01 && st.T < 0.99);
            if (!still && l.Idle == "bob") y += Math.Sin(_now / 520 + k * 2) * b.H * 0.035;
            if (!still && l.Idle == "sway") rotation = Math.Sin(_now / 700 + k * 2) * 0.05;
            list.Add(new LayerDraw(l.Image, b with { Y = y }, st.Vis, rotation));
        }
        return list;
    }

    // ---------------------------------------------------------------- what a renderer needs

    /// <summary>
    /// How the world maps to the screen right now: the camera fitted into the picture area between the
    /// insets, plus the camera shake if one is playing.
    /// </summary>
    public ViewTransform GetView(bool withShake = true)
    {
        ViewTransform v = ViewFor(DisplayedCam());
        if (!withShake) return v;
        PointD shake = ShakeOffset();
        return v with { OffsetX = v.OffsetX + shake.X, OffsetY = v.OffsetY + shake.Y };
    }

    /// <summary>How the world would map to the screen if the camera were at <paramref name="cam"/>.</summary>
    public ViewTransform ViewFor(RectD cam)
    {
        double w = ViewWidth, h = ViewHeight;
        double viewH = Math.Max(1, h - Insets.Top - Insets.Bottom);
        double sc = Math.Min(w / cam.W, viewH / cam.H);
        double ox = w / 2 - cam.CenterX * sc;
        double oy = Insets.Top + viewH / 2 - cam.CenterY * sc;
        return new ViewTransform(sc, ox, oy, w, h);
    }

    /// <summary>The page turn in progress, or null when no page is turning.</summary>
    public FlipDraw? GetFlip()
    {
        if (_anim is not { Kind: AnimKind.Flip } a) return null;
        double raw = a.Ms <= 0 ? 1 : Clamp((_now - a.Start) / a.Ms, 0, 1);
        double eased = Easings.InOutSine(raw);          // slow to lift, slow to settle
        return a.Forward
            ? new FlipDraw(a.FromPage, a.ToPage, eased, true, WholeCam(a.FromPage, a.FromHalf), WholeCam(a.ToPage, a.ToHalf), a.FromHalf, a.ToHalf)
            : new FlipDraw(a.ToPage, a.FromPage, 1 - eased, false, WholeCam(a.ToPage, a.ToHalf), WholeCam(a.FromPage, a.FromHalf), a.ToHalf, a.FromHalf);
    }

    /// <summary>
    /// The book's cover, when it is to be drawn: shut while the reader rests on the cover (or during the
    /// half of a dip to dark that is on the cover's side), turning while it swings open or shut. Null
    /// when the book is open.
    /// </summary>
    public CoverDraw? GetCover()
    {
        if (!HasBook) return null;
        if (_anim is Anim a)
        {
            double raw = a.Ms <= 0 ? 1 : Clamp((_now - a.Start) / a.Ms, 0, 1);
            if (a.Kind == AnimKind.Cover)
            {
                double eased = Easings.InOutSine(raw);
                return new CoverDraw(0, a.Forward ? eased : 1 - eased, a.Forward);
            }
            if (a.Fade && (a.FromCover || a.ToCover)) return (raw < 0.5 ? a.FromCover : a.ToCover) ? new CoverDraw(0, 0, false) : null;
            if (a.Kind == AnimKind.Pan && CurrentStep.Stop == Step.Cover) return new CoverDraw(0, 0, false);
            return null;
        }
        return CurrentStep.Stop == Step.Cover ? new CoverDraw(0, 0, false) : null;
    }

    /// <summary>
    /// With the book look, the pages to draw right now. Usually just the one being read. While the
    /// camera flies from one page to another it is both, and during a dip to dark it is the old page
    /// until the darkest moment and the new one after.
    /// </summary>
    public List<int> BookPages()
    {
        var pages = new List<int>(2);
        if (!HasBook) return pages;
        if (_anim is not Anim a || a.Kind == AnimKind.Fly) { pages.Add(CurrentStep.Page); return pages; }
        if (a.Fade)
        {
            double raw = a.Ms == 0 ? 1 : Clamp((_now - a.Start) / a.Ms, 0, 1);
            pages.Add(raw < 0.5 ? a.FromPage : a.ToPage);
            return pages;
        }
        pages.Add(a.FromPage);
        if (a.ToPage != a.FromPage) pages.Add(a.ToPage);
        return pages;
    }

    /// <summary>A short, fading shake of the whole view, in screen units. Zero when nothing is shaking.</summary>
    public PointD ShakeOffset()
    {
        if (_shake is not (double start, double strength)) return default;
        double t = _now - start;
        if (t < 0 || t > 620) return default;
        double viewH = Math.Max(1, ViewHeight - Insets.Top - Insets.Bottom);
        double d = Math.Pow(1 - t / 620, 2) * strength * viewH * 0.022;
        return new PointD(Math.Sin(t * 0.085) * d, Math.Cos(t * 0.107) * d * 0.8);
    }

    /// <summary>Lightning: one soft flash and a fainter echo, 0 to 0.6. Kept gentle and short on purpose.</summary>
    public double FlashAlpha
    {
        get
        {
            if (_flash is not double start) return 0;
            double t = _now - start;
            if (t < 0 || t > 800) return 0;
            double a = 0.5 * Math.Exp(-t / 90) + (t > 170 ? 0.28 * Math.Exp(-(t - 170) / 150) : 0);
            return Math.Min(0.6, a);
        }
    }

    /// <summary>The rings answering touches, in screen units.</summary>
    public List<RippleDraw> Ripples()
    {
        var list = new List<RippleDraw>(_ripples.Count);
        foreach (Touch r in _ripples)
        {
            double t = (_now - r.Start) / 450;
            if (t is >= 0 and < 1) list.Add(new RippleDraw(r.X, r.Y, t));
        }
        return list;
    }

    /// <summary>Sparkle bursts on found hotspots and "not yet" rings on locked ones. Boxes are in world units.</summary>
    public List<BurstDraw> Bursts()
    {
        var list = new List<BurstDraw>(_bursts.Count);
        foreach (Burst b in _bursts)
        {
            double t = (_now - b.Start) / 1100;
            if (t is >= 0 and < 1) list.Add(new BurstDraw(b.Box, t, b.Locked));
        }
        return list;
    }

    /// <summary>The story beacon and the idle hint for an explore page, or null when there is nothing to show.</summary>
    public HintDraw? GetHint(ViewTransform v)
    {
        if (!Exploring) return null;
        double now = _now, w = v.Width, h = v.Height, top = Insets.Top, bottom = Insets.Bottom;
        double pulse = 0.5 + 0.5 * Math.Sin(now / 260);
        var hint = new HintDraw { Pulse = pulse };
        var hs = CurrentPage.Hotspots;
        int page = CurrentStep.Page;

        int story = NextStory();
        if (story >= 0)
        {
            RectD b = v.ToScreen(BoxRect(page, hs[story].Box));
            if (b.CenterX > -b.W && b.CenterX < w + b.W && b.CenterY > -b.H && b.CenterY < h + b.H)
                hint = hint with { Beacon = true, BeaconBox = b, BeaconGrow = Reduced ? 0.25 : now / 1400 % 1 };
        }

        // When the child has been idle, point at something not found yet.
        bool idle = _anim is null && _fling is null && CurrentPage.Guidance.Hints && now - _lastTouch >= Settings.HintSeconds * 1000;
        var unfound = idle ? UnfoundHotspots() : null;
        if (unfound is { Count: > 0 })
        {
            RectD r = v.ToScreen(BoxRect(page, hs[unfound[0]].Box));
            double cx = r.CenterX, cy = r.CenterY;
            if (cx > 0 && cx < w && cy > top && cy < h - bottom)
            {
                hint = hint with { Kind = HintKind.Glow, GlowBox = r };
            }
            else
            {
                // Arrow at the screen edge, pointing the way.
                double mx = w / 2, my = top + (h - top - bottom) / 2;
                double ang = Math.Atan2(cy - my, cx - mx);
                const double m = 46;
                hint = hint with
                {
                    Kind = HintKind.Arrow,
                    ArrowX = Clamp(mx + Math.Cos(ang) * w, m, w - m),
                    ArrowY = Clamp(my + Math.Sin(ang) * h, top + m, h - bottom - m),
                    ArrowAngle = ang,
                };
            }
        }
        return hint.Beacon || hint.Kind != HintKind.None ? hint : null;
    }

    /// <summary>Small map of the whole page with a frame showing where the child is looking. Null when the page has none.</summary>
    public MapDraw? GetMap()
    {
        if (!Exploring || !CurrentPage.Guidance.Map || ViewWidth <= 0) return null;
        int page = CurrentStep.Page;
        RectD pr = _pageRects[page];
        double mw = Math.Min(210, ViewWidth * 0.3), mh = mw * pr.H / pr.W;
        double mx = ViewWidth - mw - 12, my = Insets.Top + 6;
        RectD cam = VisibleCam();
        double k = mw / pr.W;
        double vx = Clamp(mx + (cam.X - pr.X) * k, mx, mx + mw), vy = Clamp(my + (cam.Y - pr.Y) * k, my, my + mh);
        double vx1 = Clamp(mx + (cam.Right - pr.X) * k, mx, mx + mw), vy1 = Clamp(my + (cam.Bottom - pr.Y) * k, my, my + mh);
        var map = new MapDraw { Page = page, Rect = new RectD(mx, my, mw, mh), ViewRect = new RectD(vx, vy, vx1 - vx, vy1 - vy) };
        int next = NextStory();
        if (next < 0) return map;
        RectD b = BoxRect(page, CurrentPage.Hotspots[next].Box);
        double pulse = 0.5 + 0.5 * Math.Sin(_now / 260);
        return map with
        {
            HasDot = true,
            DotX = mx + (b.CenterX - pr.X) * k,
            DotY = my + (b.CenterY - pr.Y) * k,
            DotRadius = 4 + 2 * pulse,
            DotAlpha = 0.6 + 0.4 * pulse,
        };
    }

    /// <summary>Stars raining down when the story is finished, in screen units. Null when there is no party.</summary>
    public PartyDraw? GetParty()
    {
        if (_party is not double start || Reduced) return null;
        double t = (_now - start) / 4200;
        if (t < 0 || t > 1) return null;
        double w = ViewWidth, h = ViewHeight;
        var stars = new PartyStar[70];
        for (int k = 0; k < stars.Length; k++)
        {
            double a = Math.Sin(k * 91.7) * 43758.5, b = Math.Sin(k * 17.3) * 12543.1;
            double fx = a - Math.Floor(a), fy = b - Math.Floor(b);
            double x = fx * w + Math.Sin(t * 6 + k) * 24;
            double y = -40 + t * (1.2 + fy * 0.9) * (h + 80) - fy * h * 0.5;
            stars[k] = new PartyStar(x, y, 8 + fy * 12, k % PartyDraw.Tints.Length);
        }
        return new PartyDraw(t > 0.8 ? (1 - t) / 0.2 : 1, stars);
    }

    /// <summary>
    /// Big scenes: the sharp tiles that cover the part of a page on screen, at the level that suits the
    /// zoom. Empty when the page has no tiles or is zoomed out so far that its overview picture is enough.
    /// </summary>
    /// <param name="pixelsPerUnit">Real pixels per screen unit (the display's scaling).</param>
    public List<TileDraw> TilesNeeded(int page, ViewTransform v, double pixelsPerUnit = 1)
    {
        var list = new List<TileDraw>();
        TileInfo? info = Book?.Pages[page].Tiles;
        if (info is null || info.Size <= 0 || info.Width <= 0 || info.Height <= 0 || v.Scale <= 0) return list;
        RectD pr = _pageRects[page];
        double perSource = v.Scale * pixelsPerUnit * pr.H / info.Height;          // screen pixels per full-size pixel
        int z = perSource >= 1 ? 0 : (int)Math.Floor(Math.Log2(1 / perSource));
        if (z >= info.Levels) return list;                                         // zoomed out: the overview is enough
        double f = Math.Pow(2, z);
        int lw = (int)Math.Ceiling(info.Width / f), lh = (int)Math.Ceiling(info.Height / f);
        double tw = info.Size * f / info.Width * pr.W, th = info.Size * f / info.Height * pr.H;  // tile size in world units
        int x0 = Math.Max(0, (int)Math.Floor(((0 - v.OffsetX) / v.Scale - pr.X) / tw));
        int x1 = Math.Min((int)Math.Ceiling(lw / (double)info.Size) - 1, (int)Math.Floor(((v.Width - v.OffsetX) / v.Scale - pr.X) / tw));
        int y0 = Math.Max(0, (int)Math.Floor(((0 - v.OffsetY) / v.Scale - pr.Y) / th));
        int y1 = Math.Min((int)Math.Ceiling(lh / (double)info.Size) - 1, (int)Math.Floor(((v.Height - v.OffsetY) / v.Scale - pr.Y) / th));
        double half = info.Size / 2.0;
        for (int ty = y0; ty <= y1; ty++)
        {
            for (int tx = x0; tx <= x1; tx++)
            {
                int pw = Math.Min(info.Size, lw - tx * info.Size), ph = Math.Min(info.Size, lh - ty * info.Size);
                var world = new RectD(pr.X + tx * tw, pr.Y + ty * th, tw * pw / info.Size, th * ph / info.Size);
                bool hasParent = z + 1 < info.Levels;
                list.Add(new TileDraw(z, tx, ty, world, hasParent, tx >> 1, ty >> 1,
                    new RectD((tx & 1) * half, (ty & 1) * half, pw / 2.0, ph / 2.0)));
            }
        }
        return list;
    }
}
