using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Reader.Core;

namespace Reader.App;

/// <summary>
/// The settings panel: a port of the one in web/index.html, built from the same table of controls.
/// The top part is what parents would see in the app; the fine tuning below is for trying things out.
/// Changes apply straight away.
/// </summary>
public sealed class SettingsPanel : UserControl
{
    // What the panel shows. Each entry becomes one control.
    abstract record Row;
    sealed record Group(string Title, string Note) : Row;
    sealed record Section(string Title) : Row;
    abstract record Control_(string Key, string Label, string? Help, Func<ReaderSettings, bool>? ShowIf) : Row;
    sealed record Choice(string Key, string Label, (string Value, string Text)[] Options, Func<ReaderSettings, string> Get, Action<ReaderSettings, string> Set,
        string? Help = null, bool Preset = false, Func<ReaderSettings, bool>? ShowIf = null) : Control_(Key, Label, Help, ShowIf);
    sealed record Check(string Key, string Label, Func<ReaderSettings, bool> Get, Action<ReaderSettings, bool> Set,
        string? Help = null, Func<ReaderSettings, bool>? ShowIf = null) : Control_(Key, Label, Help, ShowIf);
    sealed record Range(string Key, string Label, double Min, double Max, double Step, Func<ReaderSettings, double> Get, Action<ReaderSettings, double> Set,
        string Unit = "", string? Help = null, Func<ReaderSettings, bool>? ShowIf = null) : Control_(Key, Label, Help, ShowIf);

    static readonly Row[] Controls =
    {
        new Group("For parents", "These would sit behind the grown-up lock in the app."),
        new Choice("age", "Child's age", new[] { ("3-5", "3 to 5"), ("6-8", "6 to 8"), ("9+", "9 and up") }, s => s.Age, (s, v) => s.Age = v,
            Help: "Sets move speed, how soon hints appear and how big touch areas are.", Preset: true),
        new Choice("reduceMotion", "Camera movement",
            new[] { ("auto", "Follow this device's setting"), ("off", "Full movement"), ("on", "Reduced: fades instead of flying") },
            s => s.ReduceMotion, (s, v) => s.ReduceMotion = v,
            Help: "Reduced movement helps children who feel dizzy or unwell with zooming."),
        new Check("sound", "Touch and movement sounds", s => s.Sound, (s, v) => s.Sound = v),
        new Choice("extras", "Things to find",
            new[] { ("on", "Everything, any time"), ("afterStory", "The story first, then the rest"), ("off", "Only the story") },
            s => s.Extras, (s, v) => s.Extras = v,
            Help: "In scenes with a story inside, this controls the just-for-fun extras."),
        new Check("prompts", "Show questions for the grown-up", s => s.Prompts, (s, v) => s.Prompts = v,
            Help: "A small \"Ask\" line at some stops, to get you talking about the story together."),
        new Check("bookLook", "Show as an open book", s => s.BookLook, (s, v) => s.BookLook = v,
            Help: "On: each picture lies across an open book with a crease, and pages curl over. Off: just the flat picture, and pages slide across."),
        new Choice("captionSize", "Word size", new[] { ("small", "Small"), ("medium", "Medium"), ("large", "Large") }, s => s.CaptionSize, (s, v) => s.CaptionSize = v),
        new Check("wholePageFirst", "Show whole page before its stops", s => s.WholePageFirst, (s, v) => s.WholePageFirst = v),
        new Check("wholePageLast", "Show whole page again after its stops", s => s.WholePageLast, (s, v) => s.WholePageLast = v),
        new Check("autoPlay", "Watch mode: move on by itself", s => s.AutoPlay, (s, v) => s.AutoPlay = v),
        new Range("autoSeconds", "Seconds per stop", 1, 12, 0.5, s => s.AutoSeconds, (s, v) => s.AutoSeconds = v, Unit: " s", ShowIf: s => s.AutoPlay),
        new Check("gate", "Lock the gear button (hold for 2 seconds)", s => s.Gate, (s, v) => s.Gate = v,
            Help: "Off here so the playground is quick to use. S on a keyboard always opens this panel."),

        new Group("Fine tuning", "Playground only. Children and parents never see these."),
        new Section("Camera move"),
        new Range("moveMs", "Move speed", 150, 2500, 50, s => s.MoveMs, (s, v) => s.MoveMs = v, Unit: " ms",
            Help: "The time for an ordinary hop between two stops."),
        new Check("scaleTime", "Longer moves take longer", s => s.ScaleTime, (s, v) => s.ScaleTime = v,
            Help: "Keeps the speed even. Zooming is always limited to about double per quarter second."),
        new Choice("easing", "Feel", new[]
            {
                ("easeInOutCubic", "Smooth start and stop"), ("easeOutCubic", "Quick start, gentle stop"), ("easeInOutSine", "Soft and floaty"),
                ("easeOutBack", "Small overshoot (bouncy)"), ("spring", "Spring (wobbly)"), ("linear", "Robot (constant speed)"),
            },
            s => s.Easing, (s, v) => s.Easing = v,
            Help: "Bouncy and wobbly are the ones most likely to bother children who are sensitive to motion."),
        new Choice("zoomPath", "Zoom math", new[] { ("smooth", "Even zoom speed"), ("straight", "Straight line between boxes") }, s => s.ZoomPath, (s, v) => s.ZoomPath = v),

        new Section("Between pages"),
        new Choice("pageTurn", "Page change",
            new[] { ("flip", "Flip like a book page"), ("fly", "Fly straight across"), ("zoomOut", "Zoom out, across, then in"), ("cut", "Jump cut") },
            s => s.PageTurn, (s, v) => s.PageTurn = v,
            Help: "The flip needs the book look. Without it, pages fly across instead."),
        new Range("arcAmount", "How far to zoom out", 0, 1.5, 0.05, s => s.ArcAmount, (s, v) => s.ArcAmount = v, ShowIf: s => s.PageTurn == "zoomOut"),

        new Section("Focus"),
        new Range("padding", "Space around the focus box", 0, 0.12, 0.005, s => s.Padding, (s, v) => s.Padding = v),
        new Range("dim", "Dim strength (books or stops set to Dim)", 0, 1, 0.02, s => s.Dim, (s, v) => s.Dim = v),
        new Range("vignette", "Soft edge strength (set to Soft edges)", 0, 1, 0.02, s => s.Vignette, (s, v) => s.Vignette = v),
        new Range("spotlight", "Spotlight darkness (stops set to Spotlight)", 0.5, 1, 0.01, s => s.Spotlight, (s, v) => s.Spotlight = v),

        new Section("Movement while resting on a stop"),
        new Choice("drift", "Drift", new[] { ("off", "Off (still)"), ("zoomIn", "Slow push in"), ("pan", "Slow pan sideways") }, s => s.Drift, (s, v) => s.Drift = v,
            Help: "Keeps still art feeling alive. Stops with their own pan ignore this."),
        new Range("driftAmount", "Drift amount", 0.01, 0.2, 0.01, s => s.DriftAmount, (s, v) => s.DriftAmount = v, ShowIf: s => s.Drift != "off"),
        new Range("driftSeconds", "Drift time", 2, 15, 0.5, s => s.DriftSeconds, (s, v) => s.DriftSeconds = v, Unit: " s", ShowIf: s => s.Drift != "off"),

        new Section("Explore pages"),
        new Range("glide", "Glide after a flick", 0, 1, 0.05, s => s.Glide, (s, v) => s.Glide = v),
        new Range("hintSeconds", "Hint after idle for", 2, 20, 1, s => s.HintSeconds, (s, v) => s.HintSeconds = v, Unit: " s"),
        new Range("minTargetCm", "Smallest touch area", 0.8, 3, 0.1, s => s.MinTargetCm, (s, v) => s.MinTargetCm = v, Unit: " cm",
            Help: "Small things to find get a touch area at least this big. 2 cm is the usual advice for young children."),
        new Check("doubleTapZoom", "Double-tap zooms in", s => s.DoubleTapZoom, (s, v) => s.DoubleTapZoom = v,
            Help: "Off for younger children, who double-tap by accident. The + and − buttons always work."),
    };

    static readonly IBrush Ink = new ImmutableSolidColorBrush(Color.Parse("#222222"));
    static readonly IBrush Grey = new ImmutableSolidColorBrush(Color.Parse("#666666"));
    static readonly IBrush HelpGrey = new ImmutableSolidColorBrush(Color.Parse("#777777"));

    readonly ReaderSettings _settings;
    readonly StackPanel _rows = new();
    readonly TextBlock _copied;
    bool _building;

    readonly bool _parentsOnly;
    readonly StackPanel _body;

    /// <param name="parentsOnly">
    /// The phone and tablet app: only the part for parents, none of the playground's fine tuning, no
    /// keyboard help and no copying.
    /// </param>
    public SettingsPanel(ReaderSettings settings, bool parentsOnly = false)
    {
        _settings = settings;
        _parentsOnly = parentsOnly;
        HorizontalAlignment = HorizontalAlignment.Right;

        var close = new Button { Content = "Close ✕", FontSize = 14, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Focusable = false };
        ToolTip.SetTip(close, "Close (S or Esc)");
        close.Click += (_, _) => CloseRequested?.Invoke();

        var heading = new TextBlock { Text = "Settings", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Ink, Margin = new Thickness(0, 0, 0, 4) };
        var sub = new TextBlock
        {
            Text = parentsOnly ? "Changes apply straight away."
                : "Changes apply straight away. The top part is what parents would see in the app. The fine tuning below is only for this playground.",
            FontSize = 13, Foreground = Grey, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14),
        };

        var replay = new Button { Content = "Replay this move", Focusable = false };
        replay.Click += (_, _) => ReplayRequested?.Invoke();
        var reset = new Button { Content = "Reset", Focusable = false };
        reset.Click += (_, _) => { _settings.Reset(); Build(); Changed?.Invoke("*"); };
        var copy = new Button { Content = "Copy settings", Focusable = false };
        copy.Click += (_, _) => CopyRequested?.Invoke(_settings.ToJsonString());
        _copied = new TextBlock { FontSize = 13, Foreground = new ImmutableSolidColorBrush(Color.Parse("#22aa77")), VerticalAlignment = VerticalAlignment.Center };
        var actions = new WrapPanel { Margin = new Thickness(0, 18, 0, 0), Children = { replay, reset, copy, _copied } };
        foreach (Control c in actions.Children) c.Margin = new Thickness(0, 0, 8, 8);

        var keys = new TextBlock
        {
            FontSize = 12, Foreground = new ImmutableSolidColorBrush(Color.Parse("#555555")), LineHeight = 21, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
            Text = "→ or Space: next.  ←: back.  W: whole page.  S: this panel.  Esc: back to the books.\n" +
                   "F: show how many frames a second are being drawn.\n" +
                   "Mouse wheel moves through stops. Trackpad pinch or Ctrl+wheel zooms out and in.\n" +
                   "On a touch screen: tap right or left, or swipe.\n" +
                   "Explore pages: drag to move, the + and − buttons (or the wheel) to zoom, tap things to find them.",
        };

        if (parentsOnly)
        {
            actions.Children.Remove(replay);
            actions.Children.Remove(copy);
            keys.IsVisible = false;
        }
        var body = _body = new StackPanel { Margin = new Thickness(18, 16, 18, 24), Children = { new Panel { Children = { heading, close } }, sub, _rows, actions, keys } };
        Content = new Border
        {
            Background = Brushes.White,
            BoxShadow = new BoxShadows(new BoxShadow { OffsetX = -4, Blur = 20, Color = Color.FromArgb(77, 0, 0, 0) }),
            Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = body },
        };
        // A touch on the panel is never a touch on the picture behind it.
        PointerPressed += (_, e) => e.Handled = true;
        PointerWheelChanged += (_, e) => e.Handled = true;
        Build();
    }

    /// <summary>Room kept clear for a notch, rounded corners or the system bars.</summary>
    public Thickness SafeArea
    {
        set => _body.Margin = new Thickness(18, 16 + value.Top, 18 + value.Right, 24 + value.Bottom);
    }

    /// <summary>A setting changed. The key is the browser's name for it, or "*" after a reset.</summary>
    public event Action<string>? Changed;
    public event Action? CloseRequested;
    public event Action? ReplayRequested;
    /// <summary>Copy settings was pressed; the text is the settings as JSON.</summary>
    public event Action<string>? CopyRequested;
    /// <summary>A control has finished with the keyboard: hand the arrow keys back to the reader.</summary>
    public event Action? Done;

    /// <summary>Show a short note beside the Copy button ("Copied").</summary>
    public void Note(string text) => _copied.Text = text;

    /// <summary>Rebuild every control from the settings (after a reset, or when the panel opens).</summary>
    public void Build()
    {
        _building = true;
        _rows.Children.Clear();
        foreach (Row row in Controls)
        {
            switch (row)
            {
                case Group g when _parentsOnly && g.Title != "For parents":
                    _building = false;
                    return;
                case Group g:
                    _rows.Children.Add(new Border
                    {
                        Background = new ImmutableSolidColorBrush(Color.Parse("#f4eee6")), Margin = new Thickness(-18, 22, -18, 0), Padding = new Thickness(18, 10),
                        Child = new StackPanel
                        {
                            Children =
                            {
                                new TextBlock { Text = g.Title, FontSize = 16, FontWeight = FontWeight.ExtraBold, Foreground = Ink },
                                new TextBlock { Text = g.Note, FontSize = 12, Foreground = new ImmutableSolidColorBrush(Color.Parse("#6b5b4b")), Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap },
                            },
                        },
                    });
                    break;
                case Section s:
                    _rows.Children.Add(new TextBlock { Text = s.Title.ToUpperInvariant(), FontSize = 13, FontWeight = FontWeight.Bold, LetterSpacing = 0.65, Foreground = ShelfView.Orange, Margin = new Thickness(0, 18, 0, 6) });
                    break;
                case Control_ c when c.ShowIf is null || c.ShowIf(_settings):
                    _rows.Children.Add(BuildControl(c));
                    break;
            }
        }
        _building = false;
    }

    Control BuildControl(Control_ c)
    {
        var row = new StackPanel { Margin = new Thickness(0, 10) };
        var label = new TextBlock { Text = c.Label, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Ink, TextWrapping = TextWrapping.Wrap };
        switch (c)
        {
            case Range r:
            {
                var output = new TextBlock { FontSize = 14, Foreground = new ImmutableSolidColorBrush(Color.Parse("#555555")), HorizontalAlignment = HorizontalAlignment.Right };
                var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                Grid.SetColumn(output, 1);
                head.Children.Add(label);
                head.Children.Add(output);
                string Show(double v) => Snap(v, r).ToString("0.######", CultureInfo.InvariantCulture) + r.Unit;
                var slider = new Slider
                {
                    Minimum = r.Min, Maximum = r.Max, Value = Math.Clamp(r.Get(_settings), r.Min, r.Max),
                    TickFrequency = r.Step, IsSnapToTickEnabled = true, SmallChange = r.Step, LargeChange = r.Step * 5, Margin = new Thickness(0, -6, 0, -8),
                };
                output.Text = Show(r.Get(_settings));
                slider.ValueChanged += (_, e) =>
                {
                    if (_building) return;
                    double v = Snap(e.NewValue, r);
                    if (Math.Abs(v - r.Get(_settings)) < 1e-9) return;
                    r.Set(_settings, v);
                    output.Text = Show(v);
                    Changed?.Invoke(r.Key);
                };
                // Letting go of the slider hands the arrow keys back to the reader.
                slider.AddHandler(PointerReleasedEvent, (_, _) => Done?.Invoke(), Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
                row.Children.Add(head);
                row.Children.Add(slider);
                break;
            }
            case Choice ch:
            {
                var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 4, 0, 0), FontSize = 14 };
                foreach (var (_, text) in ch.Options) box.Items.Add(text);
                box.SelectedIndex = Math.Max(0, Array.FindIndex(ch.Options, o => o.Value == ch.Get(_settings)));
                box.SelectionChanged += (_, _) =>
                {
                    if (_building || box.SelectedIndex < 0) return;
                    string value = ch.Options[box.SelectedIndex].Value;
                    if (ch.Preset) _settings.ApplyAgePreset(value);   // an age band sets several things at once
                    else ch.Set(_settings, value);
                    Changed?.Invoke(ch.Key);
                    Done?.Invoke();
                    // Other controls may appear, go away or show new values. Rebuild once this event is finished with.
                    Avalonia.Threading.Dispatcher.UIThread.Post(Build);
                };
                row.Children.Add(label);
                row.Children.Add(box);
                break;
            }
            case Check k:
            {
                var tick = new CheckBox { IsChecked = k.Get(_settings), Content = label, Padding = new Thickness(8, 0, 0, 0), MinHeight = 24 };
                tick.IsCheckedChanged += (_, _) =>
                {
                    if (_building) return;
                    k.Set(_settings, tick.IsChecked == true);
                    Changed?.Invoke(k.Key);
                    Done?.Invoke();
                    Avalonia.Threading.Dispatcher.UIThread.Post(Build);
                };
                row.Children.Add(tick);
                break;
            }
        }
        if (c.Help is not null) row.Children.Add(new TextBlock { Text = c.Help, FontSize = 12, Foreground = HelpGrey, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
        return row;
    }

    /// <summary>The nearest step, without the long tails floating-point sums leave behind (0.30000000000000004).</summary>
    static double Snap(double v, Range r)
    {
        double snapped = r.Min + Math.Round((v - r.Min) / r.Step) * r.Step;
        return Math.Round(Math.Clamp(snapped, r.Min, r.Max), 6);
    }
}
