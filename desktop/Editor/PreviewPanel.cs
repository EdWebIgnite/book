using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Editor.Core;
using Reader.Core;
using Reader.Ui;

namespace BookEditor;

/// <summary>
/// Play the book as it is now, in the real reader the apps use, sized as a chosen screen: a Windows
/// window, a big phone upright or sideways, a cheap phone, a tablet. Nothing needs saving first.
/// </summary>
sealed class PreviewPanel : UserControl
{
    readonly EditorState _state;
    readonly MainWindow _window;
    readonly ReaderSettings _settings = new() { ReduceMotion = "off" };
    readonly Border _frame;
    readonly Viewbox _box;
    readonly TextBlock _note;
    readonly ComboBox _deviceBox;
    ReaderScreen? _screen;
    LoadedBook? _book;
    Device _device = Device.All[0];
    int _page, _stop = -1;
    bool _fromStop;
    bool _night;
    string _language = "en";

    public PreviewPanel(EditorState state, MainWindow window)
    {
        _state = state;
        _window = window;
        Background = Ui.Stage;
        var devices = Device.All.Select(d => (d.Id, d.Name)).ToList();
        var top = new WrapPanel { Margin = new Thickness(12, 8), VerticalAlignment = VerticalAlignment.Center, ItemSpacing = 12, LineSpacing = 4 };
        void Add(Control c) { c.VerticalAlignment = VerticalAlignment.Center; top.Children.Add(c); }
        Add(Ui.Label("Preview", 15, Ui.AccentDeep, FontWeight.ExtraBold, wrap: false));
        _deviceBox = Ui.Choice<string>(devices, _device.Id, id => { if (_device.Id == id) return; _device = Device.Find(id); Restart(); });
        _deviceBox.Width = 250;
        ToolTip.SetTip(_deviceBox, "The screen to play on. Phones show one page at a time when upright, and the open book when sideways.");
        Add(_deviceBox);
        Add(Ui.Check("Open book", _settings.BookLook, v => { _settings.BookLook = v; Restart(); }, "The open-book look with the page turn. Off: flat pictures that slide across."));
        Add(Ui.Check("Read to me", _settings.ReadToMe, v => { _settings.ReadToMe = v; Restart(); }, "Play the recordings and light up the words"));
        Add(Ui.Check("Watch mode", _settings.AutoPlay, v => { _settings.AutoPlay = v; _screen?.Refresh(); }, "Go on by itself, as the parents' Watch mode does"));
        Add(Ui.Check("Ask lines", _settings.Prompts, v => { _settings.Prompts = v; _screen?.Refresh(); _screen?.Engine.Refresh(); }, "Show the questions for the grown-up"));
        Add(Ui.Check("Bedtime colours", false, v => { _night = v; Restart(); }, "The reader's bedtime colours, as a family who has switched them on sees the book"));
        var language = Ui.Choice<string>(Strings.Languages.Select(l => (l.Code, l.Name)).ToList(), _language, v => { _language = v; Restart(); });
        language.Width = 120;
        ToolTip.SetTip(language, "The language of the reader's own buttons and messages (the book's words stay as they are)");
        Add(language);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Ui.Button("From the start", () => { _page = 0; _stop = -1; _fromStop = false; Restart(fromStart: true); }, "Play the book from its first page", small: true),
                Ui.Button("Replay", () => Restart(), "Play again from where it started", small: true),
                Ui.Button("Back to editing", () => _window.ClosePreview(), "Esc", primary: true),
            },
        };
        _frame = new Border { Background = Ui.Brush("#1f2b24"), ClipToBounds = true };
        _box = new Viewbox
        {
            Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Margin = new Thickness(24, 16),
            Child = new Border { Background = Ui.Brush("#1d1e20"), BorderBrush = Ui.Brush("#1d1e20"), BorderThickness = new Thickness(10), CornerRadius = new CornerRadius(22), Child = _frame, ClipToBounds = true, BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 8, Blur = 30, Color = Color.FromArgb(90, 0, 0, 0) }) },
        };
        _note = Ui.Help("This is the reader the apps use. Right arrow or Space goes on, Left goes back, W shows the whole page. Esc comes back to editing.");
        _note.Foreground = Ui.OnStage;
        _note.HorizontalAlignment = HorizontalAlignment.Center;
        _note.Margin = new Thickness(0, 0, 0, 12);
        var bar = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Right);
        bar.Children.Add(buttons);
        bar.Children.Add(top);
        var dock = new DockPanel();
        DockPanel.SetDock(_note, Dock.Bottom);
        dock.Children.Add(new Border { Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = bar, [DockPanel.DockProperty] = Dock.Top });
        dock.Children.Add(_note);
        dock.Children.Add(_box);
        Content = dock;
    }

    public ReaderScreen? Screen => _screen;

    /// <summary>The choice of screen, for the walkthrough to point at.</summary>
    public Control DeviceBox => _deviceBox;

    public Device Device
    {
        get => _device;
        set
        {
            _device = value;
            int i = Device.All.ToList().FindIndex(d => d.Id == value.Id);
            if (i >= 0 && _deviceBox.SelectedIndex != i) _deviceBox.SelectedIndex = i;
        }
    }

    /// <summary>Show the book from a page, and from a stop on it (playing the move into that stop) or from the page's start.</summary>
    public void Start(int page, int stop, bool fromStop)
    {
        _page = page;
        _stop = stop;
        _fromStop = fromStop;
        Restart();
    }

    void Restart(bool fromStart = false)
    {
        Stop();
        // The reader's bedtime colours and language, only while it plays here: the editor round it keeps its own.
        Background = Ui.Stage;
        _note.Foreground = Ui.OnStage;
        Palette.Night = _night;
        Strings.Language = _language;
        if (!_state.HasBook || _state.Book.Pages.Count == 0) return;
        LoadedBook.MaxPictureSide = _device.Phone ? Pictures.PhoneLimit : 0;
        LoadedBook.FullPicturesKept = _device.Phone ? 2 : 4;
        Book copy = BookJson.Parse(_state.Project!.ToJson());
        try
        {
            _book = LoadedBook.Load(_state.Project.Folder, copy);
        }
        catch (Exception e)
        {
            _frame.Child = new TextBlock { Text = "The book can't be played yet: " + e.Message, Foreground = Ui.Brush("#fbf5e6"), FontSize = 15, Margin = new Thickness(30), TextWrapping = TextWrapping.Wrap, Width = 400 };
            _frame.Width = _device.Width;
            _frame.Height = _device.Height;
            return;
        }
        _screen = new ReaderScreen(_settings, _window.Sound)
        {
            ShowGear = false, AutoOnePage = _device.Phone, EndAgainText = "Play again", EndBackText = "Back to editing",
            Width = _device.Width, Height = _device.Height,
        };
        _screen.View.LoadPicturesAtOnce = _window.ScreenshotMode;
        if (_window.ScreenshotMode) { _screen.View.Clock = () => _window.ShotClock; _screen.View.AutoFrames = false; }
        _screen.Engine.UnitsPerCm = _device.UnitsPerCm;
        _screen.SafeArea = new Thickness(_device.Safe.Left, _device.Safe.Top, _device.Safe.Right, _device.Safe.Bottom);
        _screen.CloseRequested += () => _window.ClosePreview();
        _frame.Width = _device.Width;
        _frame.Height = _device.Height;
        _frame.Child = _screen;
        _screen.Measure(new Size(_device.Width, _device.Height));
        _screen.Arrange(new Rect(0, 0, _device.Width, _device.Height));
        _screen.SetBook(_book);
        _screen.Title = copy.Title;
        if (fromStart) return;
        ReaderView view = _screen.View;
        int page = Math.Clamp(_page, 0, copy.Pages.Count - 1);
        Page p = copy.Pages[page];
        if (p.Explore && !_fromStop) { view.Do(e => e.JumpTo(page, p.Stops.Count > 0 ? -1 : Step.Exploring)); return; }
        if (!_fromStop || _stop < 0 || _stop >= p.Stops.Count) { view.Do(e => e.JumpTo(page, -1)); return; }
        // Play the move into the stop: start on the step before it, then go on.
        view.Do(e =>
        {
            e.JumpTo(page, _stop);
            if (e.StepIndex > 0)
            {
                Step before = e.Steps[e.StepIndex - 1];
                e.JumpTo(before.Page, before.Stop);
                e.Next();
            }
        });
    }

    public void Stop()
    {
        if (_screen is not null)
        {
            _screen.View.ClearBook();
            _frame.Child = null;
            _screen = null;
        }
        _book?.Dispose();
        _book = null;
        Palette.Night = false;
        Strings.Language = "en";
        LoadedBook.MaxPictureSide = 0;
        LoadedBook.FullPicturesKept = 4;
    }

    public void FocusReader() => _screen?.View.Focus();
}
