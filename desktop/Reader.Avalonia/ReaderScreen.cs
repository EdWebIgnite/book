using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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

    static readonly IBrush BackdropBrush = new ImmutableSolidColorBrush(Color.Parse("#1c1b22"));
    static readonly IBrush Yellow = new ImmutableSolidColorBrush(Color.Parse("#ffd23f"));

    readonly ReaderSettings _settings;
    readonly Panel _root = new();
    readonly RoundButton _back, _gear, _zoomIn, _zoomOut, _whole, _showMe, _prev, _next;
    readonly TextBlock _title, _pageNo, _caption, _askText, _askBadge;
    readonly Border _captionBox, _askBox, _gateTip, _end;
    readonly StackPanel _side, _words;
    readonly PillButton _again, _endBack;

    bool _showingCloseUp, _showGear = true, _firstStep;
    double _gateTipUntil = double.NegativeInfinity;
    double? _gearHeldSince;
    bool _gearOpened;
    Thickness _safe;
    bool _autoOnePage;

    /// <summary>Room at the top for the back button, title and gear, below any notch or status bar.</summary>
    double TopRoom => TopInset + _safe.Top;
    /// <summary>Room at the bottom for the arrows, above any gesture bar.</summary>
    double BottomRoom => (Compact ? CompactBottom : BaseBottom) + _safe.Bottom;

    /// <summary>
    /// A phone on its side: the screen is so short that the picture also uses the room between the arrows,
    /// which sit in the bottom corners beside it, or the open book would be tiny.
    /// </summary>
    bool Compact => _autoOnePage && Bounds.Height > 0 && Bounds.Height <= 519;

    const double CompactBottom = 16;

    public ReaderScreen(ReaderSettings settings, ISoundOutput? sound = null)
    {
        _settings = settings;
        Background = BackdropBrush;
        Focusable = false;

        View = new ReaderView(settings, sound);
        View.Engine.SetInsets(TopInset, BaseBottom);
        View.Engine.Changed += Update;
        View.FrameTick += OnFrame;

        // Top bar: back, the book's title, and the gear for grown-ups.
        _back = Button("Back", Icons.Back, 28, () => CloseRequested?.Invoke());
        _back.HorizontalAlignment = HorizontalAlignment.Left;
        _back.VerticalAlignment = VerticalAlignment.Top;
        _back.Margin = new Thickness(12, 8, 0, 0);

        _gear = new RoundButton("Settings for grown-ups") { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 12, 0) };
        _gear.SetIcon(Icons.Gear, 28);
        _gear.Pressed += GearPressed;
        _gear.Released += () => { _gearHeldSince = null; _gear.Ring = null; };
        _gear.Click += GearClicked;

        _title = new TextBlock
        {
            Foreground = Brushes.White, FontWeight = FontWeight.Bold, FontSize = 20,
            TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
            Effect = new DropShadowEffect { OffsetX = 0, OffsetY = 1, BlurRadius = 5, Color = Colors.Black, Opacity = 0.8 },
        };

        // Zoom, whole page and Show me: a column on the right, half way up.
        _zoomIn = Button("Zoom in", Icons.Plus, 28, () => View.Do(e => e.ZoomStep(1.7)));
        _zoomOut = Button("Zoom out", Icons.Minus, 28, () => View.Do(e => e.ZoomStep(1 / 1.7)));
        _whole = Button("Whole page", Icons.Whole, 28, () => View.Do(e => e.ToggleOverview()));
        _showMe = new RoundButton("Show me") { Background = Yellow };
        _showMe.SetIcon(Icons.Star, 28, new ImmutableSolidColorBrush(Color.Parse("#5a3b00")), filled: true);
        _showMe.Click += () => View.Do(e => e.ShowMe());
        _side = new StackPanel
        {
            Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0), Children = { _zoomIn, _zoomOut, _whole, _showMe },
        };

        // Words: the caption, and above it an optional question for the adult.
        _askBadge = new TextBlock { Text = "ASK", FontWeight = FontWeight.Bold, FontSize = 12, LetterSpacing = 0.48, Foreground = new ImmutableSolidColorBrush(Color.Parse("#3a2a00")) };
        _askText = new TextBlock { Foreground = Brushes.White, FontSize = 15, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var askRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
        var badge = new Border { Background = Yellow, CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 1), VerticalAlignment = VerticalAlignment.Top, Child = _askBadge };
        Grid.SetColumn(_askText, 1);
        askRow.Children.Add(badge);
        askRow.Children.Add(_askText);
        _askBox = new Border
        {
            Background = new ImmutableSolidColorBrush(Color.FromArgb(224, 40, 32, 58)), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 7), HorizontalAlignment = HorizontalAlignment.Center, IsVisible = false, Child = askRow,
        };
        _caption = new TextBlock { Foreground = new ImmutableSolidColorBrush(Color.Parse("#222222")), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        _captionBox = new Border
        {
            Background = new ImmutableSolidColorBrush(Color.FromArgb(245, 255, 255, 255)), CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20, 12), HorizontalAlignment = HorizontalAlignment.Center, IsVisible = false, Child = _caption,
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 4, Blur = 16, Color = Color.FromArgb(77, 0, 0, 0) }),
        };
        _words = new StackPanel
        {
            Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false, Children = { _askBox, _captionBox },
        };

        // The next and back arrows sit in the bottom corners, lifted clear of the edge where wrists rest.
        _prev = Button("Back", Icons.Back, 38, () => View.Do(e => e.Prev()), 76);
        _next = Button("Next", Icons.Forward, 38, () => View.Do(e => e.Next()), 76);
        foreach (RoundButton arrow in new[] { _prev, _next })
        {
            arrow.DisabledOpacity = 0.3;
            arrow.Shadow = new BoxShadow { OffsetY = 2, Blur = 10, Color = Color.FromArgb(89, 0, 0, 0) };
            arrow.VerticalAlignment = VerticalAlignment.Bottom;
        }
        _prev.HorizontalAlignment = HorizontalAlignment.Left;
        _next.HorizontalAlignment = HorizontalAlignment.Right;
        _pageNo = new TextBlock
        {
            Foreground = new ImmutableSolidColorBrush(Color.FromArgb(230, 255, 255, 255)), FontSize = 13,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false,
            Effect = new DropShadowEffect { OffsetX = 0, OffsetY = 1, BlurRadius = 3, Color = Colors.Black, Opacity = 0.8 },
        };

        // Grown-up lock on the gear.
        _gateTip = new Border
        {
            Background = new ImmutableSolidColorBrush(Color.FromArgb(204, 0, 0, 0)), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 72, 12, 0),
            IsVisible = false, IsHitTestVisible = false,
            Child = new TextBlock { Text = "Hold for 2 seconds", Foreground = Brushes.White, FontSize = 14 },
        };

        // The End.
        _again = new PillButton("Read again", primary: true);
        _again.Click += () => View.Do(e => e.Restart());
        _endBack = new PillButton("Back to books", primary: false);
        _endBack.Click += () => CloseRequested?.Invoke();
        _end = new Border
        {
            Background = new ImmutableSolidColorBrush(Color.FromArgb(153, 0, 0, 0)), IsVisible = false,
            Child = new Border
            {
                Background = Brushes.White, CornerRadius = new CornerRadius(24), Padding = new Thickness(40, 32),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "The End", FontSize = 40, FontWeight = FontWeight.Black, Foreground = new ImmutableSolidColorBrush(Color.Parse("#222222")),
                            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12),
                        },
                        _again, _endBack,
                    },
                },
            },
        };
        // A touch on the dark surround shouldn't fall through to the picture underneath.
        _end.PointerPressed += (_, e) => e.Handled = true;

        _root.Children.AddRange(new Control[] { View, _title, _back, _gear, _side, _words, _prev, _pageNo, _next, _gateTip, _end });
        Content = _root;
        ApplyLayout(new Size(1000, 700));
    }

    /// <summary>The picture area, which owns the engine.</summary>
    public ReaderView View { get; }

    public ReaderEngine Engine => View.Engine;

    /// <summary>The back button or "Back to books" was pressed.</summary>
    public event Action? CloseRequested;

    /// <summary>The gear was pressed (and held, if the lock is on).</summary>
    public event Action? GearRequested;

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
        if (one == View.Engine.OnePage) return;
        View.Engine.OnePage = one;
        if (View.Engine.Book is not null) View.Do(e => e.Refresh());
    }

    public string EndAgainText { get => _again.Label.Text ?? ""; set => _again.Label.Text = value; }
    public string EndBackText { get => _endBack.Label.Text ?? ""; set => _endBack.Label.Text = value; }

    public void SetBook(LoadedBook book, int startStep = 0)
    {
        Title = book.Book.Title;
        View.Engine.OnePage = _autoOnePage && Bounds.Height > Bounds.Width;
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
        if (st.Exploring && st.StoryTotal > 0) progress += $" · story {st.StoryFound} of {st.StoryTotal}";
        if (st.Exploring && st.ExtraTotal > 0) progress += $" · found {st.ExtraFound} of {st.ExtraTotal}";
        _pageNo.Text = $"Page {st.Page + 1} of {st.PageCount}" + progress;

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
            string label = closeUp ? "Back to the close-up" : "Whole page";
            Avalonia.Automation.AutomationProperties.SetName(_whole, label);
            ToolTip.SetTip(_whole, label);
        }
        _prev.Enabled = st.CanGoBack;

        if (st.Caption.Length > 0) _caption.Text = st.Caption;
        _captionBox.IsVisible = st.Caption.Length > 0;
        if (st.Ask.Length > 0) _askText.Text = st.Ask;
        _askBox.IsVisible = st.Ask.Length > 0;

        // At the very start the Next arrow pulses, to show where to tap. (On explore pages the idle hints
        // already show a child what to do, so nothing more is needed there.)
        _firstStep = st.FirstStep && !st.Finished;
        if (!_firstStep) _next.Ring = null;

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
        if ((st.Caption.Length > 0 || st.Ask.Length > 0) && !st.Exploring && h > 0)
        {
            _words.Measure(new Size(_words.Width, double.PositiveInfinity));
            words = _words.DesiredSize.Height - _words.Margin.Top - _words.Margin.Bottom + 18;
        }
        bool roomy = h >= 520 && h - TopRoom - BottomRoom - words >= h * 0.45;
        View.Engine.InsetGoal.Bottom = BottomRoom + (roomy ? words : 0);
    }

    void OnFrame(double now)
    {
        bool still = View.Engine.Reduced;

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

        // The gear's lock: a ring grows around it for two seconds, then the settings open.
        if (_gearHeldSince is double since)
        {
            double held = (now - since) / 2000;
            _gear.Ring = new BoxShadow { Spread = 10 * Math.Clamp(held, 0, 1), Color = Color.FromArgb(230, 232, 115, 12) };
            if (held >= 1)
            {
                _gearHeldSince = null;
                _gearOpened = true;
                _gear.Ring = null;
                GearRequested?.Invoke();
            }
        }
        if (_gateTip.IsVisible && now >= _gateTipUntil) _gateTip.IsVisible = false;

        // Ask the picture to keep the frames coming while one of these little animations is running.
        View.KeepAwake = (_firstStep && !still) || (_showMe.IsVisible && !still) || _gearHeldSince is not null || _gateTip.IsVisible;
    }

    // The gear is for grown-ups. With the lock on it needs a two-second hold.
    void GearPressed()
    {
        _gearOpened = false;
        if (!_settings.Gate) return;
        _gearHeldSince = View.Now;
        View.KeepAwake = true;      // the ring around the gear grows frame by frame
        View.Wake();
    }

    void GearClicked()
    {
        if (!_settings.Gate) { GearRequested?.Invoke(); return; }
        if (_gearOpened) return;
        _gateTip.IsVisible = true;
        _gateTipUntil = View.Now + 1800;
        View.KeepAwake = true;
        View.Wake();
    }

    // ---------------------------------------------------------------- sizes and places

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyLayout(e.NewSize);
        if (View.Engine.Book is not null) MakeRoom(View.Engine.State);
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

        double round = narrow ? 52 : 56, arrow = narrow ? 66 : 76;
        foreach (RoundButton b in new[] { _back, _gear, _zoomIn, _zoomOut, _whole, _showMe }) b.Diameter = round;
        _prev.Diameter = _next.Diameter = arrow;
        Thickness safe = _safe;
        double edge = (low ? 12 : 22) + safe.Bottom;
        _prev.Margin = new Thickness(14 + safe.Left, 0, 0, edge);
        _next.Margin = new Thickness(0, 0, 14 + safe.Right, edge);
        _pageNo.IsVisible = !low;
        _pageNo.Margin = new Thickness(arrow + 20 + safe.Left, 0, arrow + 20 + safe.Right, edge + 4);
        _back.Margin = new Thickness(12 + safe.Left, 8 + safe.Top, 0, 0);
        _gear.Margin = new Thickness(0, 8 + safe.Top, 12 + safe.Right, 0);
        _gateTip.Margin = new Thickness(0, 72 + safe.Top, 12 + safe.Right, 0);
        // The title sits between the back button and the gear, level with their middles.
        double gearRoom = _showGear ? round + 12 + 12 : 12;
        double sideRoom = 0;
        if (low)
        {
            // Too short for a column (a phone on its side): the zoom buttons go in a row along the top, beside the gear.
            _side.Orientation = Orientation.Horizontal;
            _side.VerticalAlignment = VerticalAlignment.Top;
            _side.Margin = new Thickness(0, 8 + safe.Top, gearRoom + safe.Right, 0);
            int shown = _side.Children.Count(c => c.IsVisible);
            sideRoom = shown * (round + _side.Spacing);
        }
        else
        {
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
