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

    public PreviewPanel(EditorState state, MainWindow window)
    {
        _state = state;
        _window = window;
        Background = Ui.Bg;
        var devices = Device.All.Select(d => (d.Id, d.Name)).ToList();
        var top = new WrapPanel { Margin = new Thickness(12, 8), VerticalAlignment = VerticalAlignment.Center };
        void Add(Control c) { c.Margin = new Thickness(0, 0, 10, 0); c.VerticalAlignment = VerticalAlignment.Center; top.Children.Add(c); }
        Add(Ui.Label("Preview", 15, Ui.Accent, FontWeight.ExtraBold, wrap: false));
        _deviceBox = Ui.Choice<string>(devices, _device.Id, id => { if (_device.Id == id) return; _device = Device.Find(id); Restart(); });
        _deviceBox.Width = 280;
        ToolTip.SetTip(_deviceBox, "The screen to play on. Phones show one page at a time when upright, and the open book when sideways.");
        Add(_deviceBox);
        Add(Ui.Check("Open book", _settings.BookLook, v => { _settings.BookLook = v; Restart(); }, "The open-book look with the page turn. Off: flat pictures that slide across."));
        Add(Ui.Check("Read to me", _settings.ReadToMe, v => { _settings.ReadToMe = v; Restart(); }, "Play the recordings and light up the words"));
        Add(Ui.Check("Watch mode", _settings.AutoPlay, v => { _settings.AutoPlay = v; _screen?.Refresh(); }, "Go on by itself, as the parents' Watch mode does"));
        Add(Ui.Check("Ask lines", _settings.Prompts, v => { _settings.Prompts = v; _screen?.Refresh(); _screen?.Engine.Refresh(); }, "Show the questions for the grown-up"));
        Add(Ui.Button("From the start", () => { _page = 0; _stop = -1; _fromStop = false; Restart(fromStart: true); }, small: true));
        Add(Ui.Button("Replay", () => Restart(), "Play again from where it started", small: true));
        Add(Ui.Button("Close", () => _window.ClosePreview(), "Esc", primary: true));
        _frame = new Border { Background = Brushes.Black, ClipToBounds = true };
        _box = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Margin = new Thickness(16), Child = new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(6), CornerRadius = new CornerRadius(18), Child = _frame, ClipToBounds = true } };
        _note = Ui.Help("This is the reader the apps use. Right arrow or Space goes on, Left goes back, W shows the whole page. Esc comes back to editing.");
        _note.HorizontalAlignment = HorizontalAlignment.Center;
        _note.Margin = new Thickness(0, 0, 0, 10);
        var dock = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(_note, Dock.Bottom);
        dock.Children.Add(new Border { Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = top, [DockPanel.DockProperty] = Dock.Top });
        dock.Children.Add(_note);
        dock.Children.Add(_box);
        Content = dock;
    }

    public ReaderScreen? Screen => _screen;

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
            _frame.Child = new TextBlock { Text = "The book can't be played yet: " + e.Message, Foreground = Brushes.White, Margin = new Thickness(30), TextWrapping = TextWrapping.Wrap, Width = 400 };
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
        LoadedBook.MaxPictureSide = 0;
        LoadedBook.FullPicturesKept = 4;
    }

    public void FocusReader() => _screen?.View.Focus();
}
