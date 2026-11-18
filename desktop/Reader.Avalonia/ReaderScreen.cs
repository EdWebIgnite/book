using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Reader.Core;

namespace Reader.Ui;

/// <summary>
/// The reader screen: the picture, its buttons, captions and hints. A port of web/reader-ui.js and
/// web/reader.css, with the same sizes and places.
///
/// Layout follows the research in docs/movement-ideas.md: picture icons instead of words (most of
/// these children can't read yet), controls on the side and in the corners rather than along the
/// bottom edge where wrists rest, and zoom buttons so nobody has to pinch.
/// </summary>
public sealed class ReaderScreen : UserControl
{
    /// <summary>Room kept under the picture for the arrows and the page number.</summary>
    const double BaseBottom = 104;
    const double TopInset = 64;


    readonly ReaderSettings _settings;
    readonly Panel _root = new();
    readonly RoundButton _back, _gear, _zoomIn, _zoomOut, _whole, _showMe, _prev, _next;
    readonly TextBlock _title, _pageNo, _caption, _askText, _askBadge;
    readonly Border _captionBox, _askBox, _askBadgeBox, _end;
    readonly StackPanel _side, _words;
    PillButton _again = null!, _endBack = null!, _endNext = null!;

    bool _showingCloseUp, _showGear = true, _firstStep, _yourTurn;
    // Read-along: the caption shown as separate words, and which one is lit up.
    string _captionText = "";
    int _litWord = -2;
    Thickness _safe;
    string _againText = Strings.T("end.again"), _backText = Strings.T("common.myBooks");
    (string Title, Bitmap? Cover)? _nextBook;
    bool _autoOnePage;

    /// <summary>Room at the top for the back button, title and gear, below any notch or status bar.</summary>
    double TopRoom => (Compact ? CompactTop : TopInset) + _safe.Top;
    /// <summary>Room at the bottom for the arrows, above any gesture bar.</summary>
    double BottomRoom => (Compact ? CompactBottom : BaseBottom) + _safe.Bottom;

    /// <summary>
    /// A phone on its side: the screen is so short that the picture takes nearly the whole height, and the
    /// buttons shrink, turn see-through and sit over the corners of the picture instead of above and below it.
    /// </summary>
    bool Compact => IsCompact(Bounds.Size);

    bool IsCompact(Size size) => _autoOnePage && size.Height > 0 && size.Height <= 519;

    /// <summary>
    /// No room is kept above and below the picture on a phone on its side, beyond what the phone itself
    /// keeps: the book is framed by its pictures there (<see cref="ReaderEngine.PictureFillsHeight"/>) and
    /// fills the height, its board running off the top and bottom.
    /// </summary>
    const double CompactTop = 0, CompactBottom = 0;

    /// <summary>The see-through white of the buttons when they sit over the picture.</summary>
    // The buttons' faces are in Buttons (Palette.cs). The Next arrow, the button children press most, is
    // leaf green; Show me is sunny yellow; over the picture on a phone on its side they are see-through.
    static BoxShadow GlassShadow => new() { OffsetY = 1, Blur = 4, Color = Palette.ShadowOf(0.16) };
    static BoxShadow RoundShadow => new() { OffsetY = 2, Blur = 9, Color = Palette.ShadowOf(0.32) };
    static BoxShadow ArrowShadow => new() { OffsetY = 3, Blur = 12, Color = Palette.ShadowOf(0.38) };

    public ReaderScreen(ReaderSettings settings, ISoundOutput? sound = null)
    {
        _settings = settings;
        Background = Palette.ForestBrush;
        Focusable = false;
        FontFamily = Palette.Font;

        View = new ReaderView(settings, sound);
        View.Engine.SetInsets(TopInset, BaseBottom);
        View.Engine.Changed += Update;
        View.FrameTick += OnFrame;

        // Top bar: back to my books, the book's title, and the grown-ups' lock.
        _back = Button(Strings.T("common.myBooks"), Icons.Books, 28, () => CloseRequested?.Invoke());
        _back.HorizontalAlignment = HorizontalAlignment.Left;
        _back.VerticalAlignment = VerticalAlignment.Top;
        _back.Margin = new Thickness(12, 8, 0, 0);

        // The grown-ups' lock: smaller and quieter than the child's buttons. It leads to the gate.
        _gear = new RoundButton(Strings.T("shelf.grownups"), 48) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 12, 0) };
        _gear.SetIcon(Icons.Lock, 22);
        _gear.Click += () => GearRequested?.Invoke();

        _title = new TextBlock
        {
            Foreground = Brushes.White, FontWeight = FontWeight.ExtraBold, FontSize = 21,
            TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
            Effect = new DropShadowEffect { OffsetX = 0, OffsetY = 1, BlurRadius = 6, Color = Palette.Forest, Opacity = 0.9 },
        };

        // Zoom, whole page and Show me: a column on the right, half way up.
        _zoomIn = Button(Strings.T("reader.zoomIn"), Icons.Plus, 28, () => View.Do(e => e.ZoomStep(1.7)));
        _zoomOut = Button(Strings.T("reader.zoomOut"), Icons.Minus, 28, () => View.Do(e => e.ZoomStep(1 / 1.7)));
        _whole = Button(Strings.T("reader.whole"), Icons.Whole, 28, () => View.Do(e => e.ToggleOverview()));
        _showMe = new RoundButton(Strings.T("reader.showMe")) { Background = Palette.SunBrush };
        _showMe.SetIcon(Icons.Star, 28, Palette.SunInkBrush, filled: true);
        _showMe.Click += () => View.Do(e => e.ShowMe());
        _side = new StackPanel
        {
            Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0), Children = { _zoomIn, _zoomOut, _whole, _showMe },
        };

        // Words: the caption, and above it an optional question for the adult.
        _askBadge = new TextBlock { Text = Strings.T("reader.ask"), FontWeight = FontWeight.Black, FontSize = 12, LetterSpacing = 0.6, Foreground = Palette.SunInkBrush };
        _askText = new TextBlock { Foreground = Brushes.White, FontSize = 15, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var askRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
        _askBadgeBox = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 1), VerticalAlignment = VerticalAlignment.Top, Child = _askBadge };
        Grid.SetColumn(_askText, 1);
        askRow.Children.Add(_askBadgeBox);
        askRow.Children.Add(_askText);
        _askBox = new Border
        {
            CornerRadius = new CornerRadius(14), IsHitTestVisible = false,
            Padding = new Thickness(14, 7), HorizontalAlignment = HorizontalAlignment.Center, IsVisible = false, Child = askRow,
        };
        _caption = new TextBlock { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        _captionBox = new Border
        {
            CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1.5, 1.5, 1.5, 4),
            Padding = new Thickness(22, 12), HorizontalAlignment = HorizontalAlignment.Center, IsVisible = false, Child = _caption,
            IsHitTestVisible = false, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        // Tap a word to hear it: only where the stop's recording has a time for every word.
        _captionBox.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            int word = WordAt(e.GetPosition(_caption));
            if (word >= 0) View.Do(engine => engine.SayWord(word));
        };
        _words = new StackPanel
        {
            Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Children = { _askBox, _captionBox },
        };

        // The next and back arrows sit in the bottom corners, lifted clear of the edge where wrists rest.
        _prev = Button(Strings.T("common.back"), Icons.Back, 38, () => View.Do(e => e.Prev()), 76);
        _next = Button(Strings.T("common.next"), Icons.Forward, 38, () => View.Do(e => e.Next()), 76);
        foreach (RoundButton arrow in new[] { _prev, _next })
        {
            arrow.DisabledOpacity = 0.3;
            arrow.Shadow = ArrowShadow;
            arrow.VerticalAlignment = VerticalAlignment.Bottom;
        }
        _prev.HorizontalAlignment = HorizontalAlignment.Left;
        _next.HorizontalAlignment = HorizontalAlignment.Right;
        _pageNo = new TextBlock
        {
            Foreground = new ImmutableSolidColorBrush(Color.FromArgb(230, 255, 255, 255)), FontSize = 13,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false,
            FontWeight = FontWeight.Bold,
            Effect = new DropShadowEffect { OffsetX = 0, OffsetY = 1, BlurRadius = 3, Color = Palette.Forest, Opacity = 0.9 },
        };

        // The End: built by BuildEnd, again when the colours or the language change.
        _end = new Border { IsVisible = false };
        // A touch on the dark surround shouldn't fall through to the picture underneath.
        _end.PointerPressed += (_, e) => e.Handled = true;

        _root.Children.AddRange(new Control[] { View, _title, _back, _gear, _side, _words, _prev, _pageNo, _next, _end });
        Content = _root;
        Colour();
        BuildEnd(new Size(1000, 700));
        ApplyLayout(new Size(1000, 700));
    }

    /// <summary>The picture area, which owns the engine.</summary>
    public ReaderView View { get; }

    public ReaderEngine Engine => View.Engine;

    /// <summary>The back button or "Back to books" was pressed.</summary>
    public event Action? CloseRequested;

    /// <summary>The grown-ups' lock was pressed: the app shows its gate.</summary>
    public event Action? GearRequested;

    /// <summary>"Read next" at The End was pressed.</summary>
    public event Action? NextBookRequested;

    /// <summary>The child's name, for "You read the whole book, Lia!". Empty: no name.</summary>
    public string ReaderName { get; set; } = "";

    /// <summary>The book offered by "Read next" at The End (title and cover), or null for none.</summary>
    public void SetNextBook(string? title, Bitmap? cover)
    {
        _nextBook = title is null ? null : (title, cover);
        _endBuiltFor = default;
        if (_end.IsVisible) BuildEnd(Bounds.Size);
    }

    /// <summary>Passed on from the engine after the screen has brought itself up to date.</summary>
    public event Action<ReaderState>? StateChanged;

    /// <summary>The gear is for the playground and the app's parents. The editor's preview hides it.</summary>
    public bool ShowGear
    {
        get => _showGear;
        set { _showGear = value; _gear.IsVisible = value; ApplyLayout(Bounds.Size); }
    }

    public string Title
    {
        get => _title.Text ?? "";
        set => _title.Text = value;
    }

    /// <summary>The words on The End box's buttons, which the editor's preview changes.</summary>
    /// <summary>
    /// The parts of the screen a phone keeps for itself (a notch or camera hole, rounded corners, the
    /// status bar and the gesture bar). Buttons and words stay clear of them; the picture fills the
    /// screen behind the top and bottom bars and stays clear of the sides.
    /// </summary>
    public Thickness SafeArea
    {
        get => _safe;
        set
        {
            if (_safe == value) return;
            _safe = value;
            View.Margin = new Thickness(value.Left, 0, value.Right, 0);
            View.Engine.SetInsets(TopRoom, BottomRoom);
            ApplyLayout(Bounds.Size);
            if (View.Engine.Book is not null) MakeRoom(View.Engine.State);
        }
    }

    /// <summary>
    /// Read one page at a time whenever the screen is taller than it is wide (a phone or tablet held
    /// upright), and the open spread when it is on its side. Off on a computer, which always shows the spread.
    /// </summary>
    public bool AutoOnePage
    {
        get => _autoOnePage;
        set { _autoOnePage = value; UpdateOnePage(Bounds.Size); }
    }

    void UpdateOnePage(Size size)
    {
        bool one = _autoOnePage && size.Height > size.Width;
        bool fill = IsCompact(size);
        if (one == View.Engine.OnePage && fill == View.Engine.PictureFillsHeight) return;
        View.Engine.OnePage = one;
        View.Engine.PictureFillsHeight = fill;
        if (View.Engine.Book is not null) View.Do(e => e.Refresh());
    }

    public string EndAgainText { get => _againText; set { _againText = value; _endBuiltFor = default; } }
    public string EndBackText { get => _backText; set { _backText = value; _endBuiltFor = default; } }

    public void SetBook(LoadedBook book, int startStep = 0)
    {
        Title = book.Book.Title;
        View.Engine.OnePage = _autoOnePage && Bounds.Height > Bounds.Width;
        View.Engine.PictureFillsHeight = Compact;
        View.SetBook(book, startStep);
        View.Focus();
    }

    /// <summary>Bring the buttons and words up to date. Call after a setting changes.</summary>
    public void Refresh()
    {
        if (View.Engine.Book is not null) Update(View.Engine.State);
    }

    RoundButton Button(string label, string icon, double iconSize, Action click, double diameter = 56)
    {
        var b = new RoundButton(label, diameter);
        b.SetIcon(icon, iconSize);
        b.Click += click;
        return b;
    }

    // ---------------------------------------------------------------- keeping up with the engine

    void Update(ReaderState st)
    {
        string progress = "";
        if (st.Exploring && st.StoryTotal > 0) progress += " · " + Strings.T("reader.story", st.StoryFound, st.StoryTotal);
        if (st.Exploring && st.ExtraTotal > 0) progress += " · " + Strings.T("reader.found", st.ExtraFound, st.ExtraTotal);
        _pageNo.Text = Strings.T("reader.page", st.Page + 1, st.PageCount) + progress;

        // Side buttons: zoom only makes sense while exploring.
        _zoomIn.IsVisible = _zoomOut.IsVisible = st.Exploring;
        _zoomIn.Enabled = st.CanZoomIn;
        _zoomOut.Enabled = st.CanZoomOut;
        _showMe.IsVisible = st.CanShowMe;
        bool closeUp = st.WholePage && !st.Exploring;
        if (closeUp != _showingCloseUp || _whole.Child is null)
        {
            _showingCloseUp = closeUp;
            _whole.SetIcon(closeUp ? Icons.CloseUp : Icons.Whole, 28);
            string label = closeUp ? Strings.T("reader.closeUp") : Strings.T("reader.whole");
            Avalonia.Automation.AutomationProperties.SetName(_whole, label);
            ToolTip.SetTip(_whole, label);
        }
        _prev.Enabled = st.CanGoBack;

        if (st.Caption.Length > 0 && st.Caption != _captionText)
        {
            _captionText = st.Caption;
            _litWord = -2;
            _caption.Inlines = null;
            _caption.Text = st.Caption;
        }
        if (!st.Reading && _litWord != -2) ShowWords(-1);
        _captionBox.IsVisible = PopsUp(st);
        _captionBox.IsHitTestVisible = View.Engine.WordsCanBeHeard;
        if (st.Ask.Length > 0) _askText.Text = st.Ask;
        _askBox.IsVisible = st.Ask.Length > 0;

        // At the very start the Next arrow pulses, to show where to tap. (On explore pages the idle hints
        // already show a child what to do, so nothing more is needed there.)
        _firstStep = st.FirstStep && !st.Finished;
        // When a recorded voice has finished reading, the Next arrow glows once to say "your turn".
        bool turn = st.ReadingDone && !st.Finished && !_settings.AutoPlay;
        if (turn && !_yourTurn) _yourTurnSince = View.Now;
        _yourTurn = turn;
        if (!_firstStep && !_yourTurn) _next.Ring = null;

        if (st.Finished && !_end.IsVisible) BuildEnd(Bounds.Size);
        _end.IsVisible = st.Finished;
        ApplyLayout(Bounds.Size);
        MakeRoom(st);
        StateChanged?.Invoke(st);
    }

    /// <summary>
    /// On guided pages the picture moves up to leave room for the words, so a caption never sits on top
    /// of what the camera is showing. Skipped on short windows, where the caption tucks in between the
    /// arrows instead.
    /// </summary>
    void MakeRoom(ReaderState st)
    {
        double h = Bounds.Height;
        double words = 0;
        if ((PopsUp(st) || st.Ask.Length > 0) && !st.Exploring && h > 0)
        {
            _words.Measure(new Size(_words.Width, double.PositiveInfinity));
            words = _words.DesiredSize.Height - _words.Margin.Top - _words.Margin.Bottom + 18;
        }
        bool roomy = h >= 520 && h - TopRoom - BottomRoom - words >= h * 0.45;
        View.Engine.InsetGoal.Top = TopRoom;
        View.Engine.InsetGoal.Bottom = BottomRoom + (roomy ? words : 0);
    }

    double _yourTurnSince;

    /// <summary>
    /// Whether the caption pops up: always with pop-ups on; with them off, only where the page's printed
    /// words can't be read (zoomed in away from them, or a page that prints none), and always for the
    /// words of things found on explore pages, which are never printed.
    /// </summary>
    bool PopsUp(ReaderState st) => st.Caption.Length > 0 && (_settings.PopUpWords || st.Exploring || !st.PrintedWordsInView);

    static IBrush LitWord => Palette.Brush(Palette.Night ? Color.Parse("#ffd877") : Color.Parse("#ecc96a"));

    /// <summary>The word of the caption under a point of the caption's text, or -1 (between words, or no timings).</summary>
    int WordAt(Point p)
    {
        if (!View.Engine.WordsCanBeHeard || _captionText.Length == 0) return -1;
        TextHitTestResult hit = _caption.TextLayout.HitTestPoint(p);
        if (!hit.IsInside) return -1;
        int at = hit.TextPosition;
        var words = ReadAlong.Words(_captionText);
        for (int i = 0; i < words.Count; i++)
            if (at >= words[i].Start && at < words[i].Start + words[i].Length) return i;
        return -1;
    }

    /// <summary>
    /// Light up one word of the caption (-1 for none). The words are separate pieces of text only while a
    /// voice is reading; otherwise the caption is plain text, exactly as before narration existed.
    /// </summary>
    void ShowWords(int lit)
    {
        if (lit == _litWord) return;
        _litWord = lit;
        if (lit < 0)
        {
            _caption.Inlines = null;
            _caption.Text = _captionText;
            return;
        }
        var words = ReadAlong.Words(_captionText);
        var inlines = new Avalonia.Controls.Documents.InlineCollection();
        int at = 0;
        for (int i = 0; i < words.Count; i++)
        {
            ReadAlong.Word w = words[i];
            if (w.Start > at) inlines.Add(new Avalonia.Controls.Documents.Run(_captionText[at..w.Start]));
            var run = new Avalonia.Controls.Documents.Run(_captionText.Substring(w.Start, w.Length));
            if (i == lit) run.Background = LitWord;
            inlines.Add(run);
            at = w.Start + w.Length;
        }
        if (at < _captionText.Length) inlines.Add(new Avalonia.Controls.Documents.Run(_captionText[at..]));
        _caption.Inlines = inlines;
    }

    void OnFrame(double now)
    {
        bool still = View.Engine.Reduced;

        // Read-along: follow the voice word by word, or light the word that was tapped while it is said.
        int said = View.Engine.SaidWord;
        if (View.Engine.Reading && _captionBox.IsVisible) ShowWords(View.Engine.ReadAlongWord());
        else if (said >= 0 && _captionBox.IsVisible) ShowWords(said);
        else if (_litWord != -2 && _litWord != -1) ShowWords(-1);

        // "Your turn": two soft rings from the Next arrow once the voice has finished, then it rests.
        if (_yourTurn && !_firstStep)
        {
            double t = (now - _yourTurnSince) / 1800;
            if (t >= 2 || still) _next.Ring = still && t < 2 ? new BoxShadow { Spread = 6, Blur = 12, Color = Color.FromArgb(160, 255, 255, 255) } : null;
            else
            {
                double u = t % 1, grow = Easings.OutCubic(u);
                _next.Ring = new BoxShadow { Spread = 2 + 14 * grow, Blur = 12, Color = Color.FromArgb((byte)(210 * (1 - u)), 255, 255, 255) };
            }
        }

        // The Next arrow's pulse: a soft ring that grows out of the button and fades, every 1.8 seconds.
        // With reduced motion it is a steady ring instead.
        if (_firstStep)
        {
            double u = still ? 0.35 : now / 1800 % 1;
            double grow = Easings.OutCubic(u);
            _next.Ring = new BoxShadow { Spread = 2 + 14 * grow, Blur = 12, Color = Color.FromArgb((byte)(210 * (1 - u) * (still ? 0.8 : 1)), 255, 255, 255) };
        }

        // Show me breathes gently so it catches the eye: a soft ring that swells and fades every 2.4 seconds.
        if (_showMe.IsVisible)
        {
            double k = still ? 0 : Math.Pow(Math.Sin(Math.PI * (now / 2400 % 1)), 2);
            _showMe.Ring = k < 0.01 ? null : new BoxShadow { Spread = 8 * k, Color = Color.FromArgb((byte)(255 * 0.35 * k), 255, 210, 63) };
        }

        // Ask the picture to keep the frames coming while one of these little animations is running.
        View.KeepAwake = (_firstStep && !still) || (_showMe.IsVisible && !still)
            || View.Engine.Reading || said >= 0 || (_yourTurn && now - _yourTurnSince < 3600);
    }

    // ---------------------------------------------------------------- colours and words

    /// <summary>Put the colours of the theme in use on the words, the boxes and the surround.</summary>
    void Colour()
    {
        Background = Palette.ForestBrush;
        _title.Effect = new DropShadowEffect { OffsetX = 0, OffsetY = 1, BlurRadius = 6, Color = Palette.Forest, Opacity = 0.9 };
        _pageNo.Effect = new DropShadowEffect { OffsetX = 0, OffsetY = 1, BlurRadius = 3, Color = Palette.Forest, Opacity = 0.9 };
        _askBox.Background = Palette.Brush(Palette.Night ? Palette.Paper : Palette.LeafDeep, 0.92);
        _askText.Foreground = Palette.Night ? Palette.InkBrush : Brushes.White;
        _askBadgeBox.Background = Palette.SunBrush;
        _askBadge.Foreground = Palette.SunInkBrush;
        Theme t = Palette.Current;
        _captionBox.Background = Palette.Brush(t.CaptionBack, 0.97);
        _captionBox.BorderBrush = Palette.Brush(t.IsNight ? Color.Parse("#c9b98e") : Color.Parse("#d9c48a"), 0.9);
        _captionBox.BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 5, Blur = 18, Color = Palette.ShadowOf(0.32) });
        _caption.Foreground = Palette.Brush(t.CaptionInk);
        _showMe.Background = Palette.SunBrush;
        _showMe.SetIcon(Icons.Star, 28, Palette.SunInkBrush, filled: true);
    }

    /// <summary>
    /// The colours or the language changed: put the new ones on everything. The book, the place in it
    /// and everything moving carry on as they were.
    /// </summary>
    public void Restyle()
    {
        _againText = Strings.T("end.again");
        _backText = Strings.T("common.myBooks");
        Colour();
        _endBuiltFor = default;
        if (_end.IsVisible) BuildEnd(Bounds.Size);
        ApplyLayout(Bounds.Size);
        _litWord = -2;
        Refresh();
        View.InvalidateVisual();
    }

    (Size Size, bool Night, string Lang, string Name, LoadedBook? Book) _endBuiltFor;

    /// <summary>
    /// The End: the book's cover with its new tick, "The End", a line of plain praise, then "Read it
    /// again" as the main button, and "My books" and "Read next" (the next unfinished book) below it.
    /// Nothing plays on by itself. The dassie looks over the top of the card.
    /// </summary>
    void BuildEnd(Size size)
    {
        double w = size.Width > 0 ? size.Width : 1000, h = size.Height > 0 ? size.Height : 700;
        var key = (new Size(Math.Round(w), Math.Round(h)), Palette.Night, Strings.Language, ReaderName, View.Book);
        if (key == _endBuiltFor && _end.Child is not null) return;
        _endBuiltFor = key;
        bool narrow = w < 600 || w < h * 0.9, low = h < 520;
        LoadedBook? book = View.Book;
        Bitmap? coverPicture = book?.Cover ?? (book is null ? null : book.SmallPicture(0));
        double coverW = narrow ? (h < 760 ? 92 : 120) : low ? 128 : 188;

        Control Cover()
        {
            double d = Math.Max(36, coverW * 0.26);
            return new Panel
            {
                Width = coverW, Height = coverW * 1.5, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new Border
                    {
                        CornerRadius = new CornerRadius(3, 10, 10, 3), ClipToBounds = true, Background = Palette.LeafBrush,
                        Child = coverPicture is null ? null : new Image { Source = coverPicture, Stretch = Stretch.UniformToFill },
                    },
                    new Border
                    {
                        Width = d, Height = d, CornerRadius = new CornerRadius(d / 2), Background = Palette.Brush(Palette.Tick), BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(3), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, -d * 0.3, -d * 0.15),
                        BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 5, Color = Palette.ShadowOf(0.35) }),
                        Child = Centred(Icons.Make(Icons.Tick, d * 0.6, Palette.Night ? Palette.Brush(Color.Parse("#1b1f4b")) : Brushes.White)),
                    },
                },
                Effect = new DropShadowEffect { OffsetX = 2, OffsetY = 5, BlurRadius = 12, Color = Palette.ShadowOf(1), Opacity = 0.28 },
            };
        }

        var title = new TextBlock
        {
            Text = Strings.T("end.title"), FontSize = narrow ? 40 : low ? 40 : 52, FontWeight = FontWeight.Bold, FontFamily = Palette.Heading,
            Foreground = Palette.LeafDeepBrush, TextWrapping = TextWrapping.Wrap,
        };
        var praise = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(ReaderName) ? Strings.T("end.praise") : Strings.T("end.praise.name", ReaderName),
            FontSize = narrow ? 18 : 20, FontWeight = FontWeight.SemiBold, Foreground = Palette.InkSoftBrush, TextWrapping = TextWrapping.Wrap,
        };
        _again = new PillButton(_againText, primary: true, icon: Icons.Again, height: low ? 62 : 74, fontSize: low ? 21 : 23);
        _again.Click += () => View.Do(e => e.Restart());
        _endBack = new PillButton(_backText, primary: false, icon: Icons.Books, height: 58, fontSize: 18);
        _endBack.Click += () => CloseRequested?.Invoke();
        _endNext = new PillButton(Strings.T("end.next"), primary: false, thumb: _nextBook?.Cover, trailing: Icons.Chevron, height: 58, fontSize: 18)
        {
            IsVisible = _nextBook is not null,
        };
        if (_nextBook is (string nextTitle, _)) { ToolTip.SetTip(_endNext, nextTitle); Avalonia.Automation.AutomationProperties.SetName(_endNext, Strings.T("end.next") + ": " + nextTitle); }
        _endNext.Click += () => NextBookRequested?.Invoke();

        Control inner;
        if (narrow)
        {
            title.TextAlignment = praise.TextAlignment = TextAlignment.Center;
            title.HorizontalAlignment = praise.HorizontalAlignment = HorizontalAlignment.Center;
            var stack = new StackPanel { Spacing = 12, Children = { Cover(), title, praise, _again, _endBack, _endNext } };
            title.Margin = new Thickness(0, 6, 0, -6);
            inner = stack;
        }
        else
        {
            var lower = new Grid { ColumnDefinitions = new ColumnDefinitions(_nextBook is null ? "*" : "*,*"), ColumnSpacing = 14 };
            lower.Children.Add(_endBack);
            Grid.SetColumn(_endNext, 1);
            lower.Children.Add(_endNext);
            var right = new StackPanel { Spacing = low ? 10 : 14, VerticalAlignment = VerticalAlignment.Center, Children = { title, praise, _again, lower } };
            title.Margin = new Thickness(0, 0, 0, -8);
            praise.Margin = new Thickness(0, 0, 0, 6);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = low ? 30 : 40 };
            grid.Children.Add(Cover());
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            inner = grid;
        }
        var card = new Border
        {
            Background = Palette.CreamBrush, CornerRadius = new CornerRadius(30),
            Padding = narrow ? new Thickness(22, 22, 22, 24) : low ? new Thickness(30, 22, 34, 24) : new Thickness(40, 34, 44, 38),
            BorderBrush = Palette.LeafPaleBrush, BorderThickness = new Thickness(1.5),
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 10, Blur = 34, Color = Palette.ShadowOf(0.4) }),
            Child = inner,
        };
        double peek = narrow ? 70 : 92;
        var dassie = new Dassie
        {
            Look = Dassie.Pose.Peek, Width = peek, Height = peek / Dassie.Aspect(Dassie.Pose.Peek),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, narrow ? 28 : 56, 0),
        };
        double peekH = peek / Dassie.Aspect(Dassie.Pose.Peek);
        card.Margin = new Thickness(0, peekH * 0.72, 0, 0);
        var holder = new Panel
        {
            Width = narrow ? Math.Min(400, w - 28 - _safe.Left - _safe.Right) : Math.Min(720, w - 80),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Children = { card, dassie },
        };
        _end.Background = Palette.Brush(Palette.Forest, 0.58);
        _end.Padding = new Thickness(_safe.Left, _safe.Top, _safe.Right, _safe.Bottom);
        _end.Child = narrow && h < 760 ? new ScrollViewer { Content = holder, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto } : holder;
    }

    static Control Centred(Control c)
    {
        c.HorizontalAlignment = HorizontalAlignment.Center;
        c.VerticalAlignment = VerticalAlignment.Center;
        return c;
    }

    /// <summary>The End's buttons, for screenshots and tests: 0 Read it again, 1 My books, 2 Read next.</summary>
    public void PressEnd(int which) => (which switch { 0 => _again, 1 => _endBack, _ => _endNext }).Press();

    // ---------------------------------------------------------------- sizes and places

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyLayout(e.NewSize);
        if (_end.IsVisible) BuildEnd(e.NewSize);
        if (View.Engine.Book is not null) MakeRoom(View.Engine.State);
        else View.Engine.SetInsets(TopRoom, BottomRoom);
        UpdateOnePage(e.NewSize);
    }

    /// <summary>
    /// Everything that depends on the window's size, following reader.css: smaller buttons on a narrow
    /// window (a phone held upright), and on a short one (a phone on its side) the words drop down
    /// between the arrows and the page number goes.
    /// </summary>
    void ApplyLayout(Size size)
    {
        double w = size.Width > 0 ? size.Width : 1000, h = size.Height > 0 ? size.Height : 700;
        bool narrow = w <= 520, low = h <= 519;
        bool glass = _autoOnePage && low;

        double round = glass ? 44 : narrow ? 52 : 56, arrow = glass ? 54 : narrow ? 66 : 76;
        foreach (RoundButton b in new[] { _back, _gear, _zoomIn, _zoomOut, _whole, _showMe })
        {
            b.Diameter = b == _gear ? Math.Min(48, round) : round;
            b.SetLook(b == _showMe ? Buttons.Sun : Buttons.Plain, glass, b == _showMe ? 0.7 : 0.55);
            b.Shadow = glass ? GlassShadow : RoundShadow;
        }
        _prev.Diameter = _next.Diameter = arrow;
        foreach (RoundButton b in new[] { _prev, _next })
        {
            b.SetLook(b == _next ? Buttons.Green : Buttons.Plain, glass, b == _next ? 0.62 : 0.55);
            b.Shadow = glass ? GlassShadow : ArrowShadow;
        }
        _next.SetIcon(Icons.Forward, 38, new ImmutableSolidColorBrush(Buttons.Green.Ink));
        foreach (RoundButton b in new[] { _back, _gear, _zoomIn, _zoomOut, _whole, _prev })
            if (b.Child is Viewbox { Child: Canvas { Children: [Avalonia.Controls.Shapes.Path p] } }) p.Stroke = Buttons.TextBrush;
        _title.IsVisible = !glass;
        Thickness safe = _safe;
        double edge = (glass ? 10 : low ? 12 : 22) + safe.Bottom;
        _prev.Margin = new Thickness(14 + safe.Left, 0, 0, edge);
        _next.Margin = new Thickness(0, 0, 14 + safe.Right, edge);
        _pageNo.IsVisible = !low;
        _pageNo.Margin = new Thickness(arrow + 20 + safe.Left, 0, arrow + 20 + safe.Right, edge + 4);
        _back.Margin = new Thickness(12 + safe.Left, 8 + safe.Top, 0, 0);
        _gear.Margin = new Thickness(0, 8 + safe.Top + (round - _gear.Diameter) / 2, 12 + safe.Right + (round - _gear.Diameter) / 2, 0);
        // The title sits between the back button and the gear, level with their middles.
        double gearRoom = _showGear ? round + 12 + 12 : 12;
        double sideRoom = 0;
        if (glass)
        {
            // Over the picture: a short column on the right, half way up, between the gear and the Next arrow.
            _side.Orientation = Orientation.Vertical;
            _side.VerticalAlignment = VerticalAlignment.Center;
            _side.Spacing = 8;
            _side.Margin = new Thickness(0, safe.Top, 12 + safe.Right, safe.Bottom);
        }
        else if (low)
        {
            _side.Spacing = 12;
            // Too short for a column (a phone on its side): the zoom buttons go in a row along the top, beside the gear.
            _side.Orientation = Orientation.Horizontal;
            _side.VerticalAlignment = VerticalAlignment.Top;
            _side.Margin = new Thickness(0, 8 + safe.Top, gearRoom + safe.Right, 0);
            int shown = _side.Children.Count(c => c.IsVisible);
            sideRoom = shown * (round + _side.Spacing);
        }
        else
        {
            _side.Spacing = 12;
            _side.Orientation = Orientation.Vertical;
            _side.VerticalAlignment = VerticalAlignment.Center;
            _side.Margin = new Thickness(0, safe.Top, 12 + safe.Right, safe.Bottom);
        }
        _title.Margin = new Thickness(12 + round + 12 + safe.Left, 8 + safe.Top + (round - 27) / 2, gearRoom + sideRoom + safe.Right, 0);

        string size_ = _settings.CaptionSize;
        double cap = size_ == "small" ? 18 : size_ == "large" ? (narrow ? 23 : 28) : (narrow ? 18 : 22);
        if (low) cap = Math.Min(cap, 17);
        _caption.FontSize = cap;
        _caption.LineHeight = cap * 1.35;
        _captionBox.Padding = low ? new Thickness(14, 8) : new Thickness(20, 12);
        _askText.FontSize = low ? 12 : 15;
        _askText.LineHeight = (low ? 12 : 15) * 1.35;
        _askBox.Padding = low ? new Thickness(10, 4) : new Thickness(14, 7);
        _words.Spacing = low ? 4 : 8;
        double across = w - safe.Left - safe.Right;
        _words.Width = Math.Max(120, low ? across - 210 : narrow ? across - 24 : Math.Min(720, across - 150));
        _words.Margin = new Thickness(safe.Left, 0, safe.Right, (low ? 12 : narrow ? 104 : 112) + safe.Bottom);
    }
}
