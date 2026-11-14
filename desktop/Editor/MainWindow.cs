using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Editor.Core;
using Reader.Audio;
using Reader.Core;

namespace BookEditor;

/// <summary>
/// The editor's window: the book's title and the main commands along the top, the pages down the
/// left, the page being edited in the middle, settings for what is selected on the right, and the
/// camera's journey along the bottom. Preview, Check book and Export open over it.
/// </summary>
sealed class MainWindow : Window
{
    readonly EditorState _state = new();
    readonly Images _images = new();
    readonly Recovery _recovery;
    readonly RecentBooks _recent;
    readonly DispatcherTimer _autosave;

    readonly PageCanvas _canvas;
    readonly PageList _pages;
    readonly Inspector _inspector;
    readonly Timeline _timeline;
    readonly PreviewPanel _preview;
    readonly CheckPanel _check;
    readonly ExportPanel _export;
    readonly Dialogs _dialogs = new();

    readonly TextBox _title;
    readonly TextBlock _status;
    readonly Button _undo, _redo, _save;
    readonly Border _toast;
    readonly TextBlock _toastText;
    readonly Border _dropHint;
    readonly Panel _workspace;
    readonly Control _welcome;
    readonly Border _canvasBar;
    readonly Control _help;
    readonly Border _empty;
    DispatcherTimer? _toastTimer;
    (string Kind, string Json)? _clipboard;
    bool _closingConfirmed;
    bool _updatingTitle;

    public MainWindow(bool screenshotMode = false)
    {
        ScreenshotMode = screenshotMode;
        Title = "Book Editor";
        Width = 1440;
        Height = 900;
        MinWidth = 1000;
        MinHeight = 640;
        Background = Ui.Bg;
        _recovery = new Recovery();
        _recent = new RecentBooks();
        _images.Synchronous = screenshotMode;
        SoundShapes.Synchronous = screenshotMode;

        Sound = screenshotMode ? new DesktopSoundStandIn() : new DesktopSoundWrapper(new DesktopSound());
        if (screenshotMode) MicrophoneProblem = "";
        else if (!Microphone.Available(out string why)) MicrophoneProblem = why;

        _canvas = new PageCanvas(_state, _images);
        _pages = new PageList(_state, _images, this) { Width = 210 };
        _inspector = new Inspector(_state, this) { Width = 380 };
        _timeline = new Timeline(_state, _images, this);
        _preview = new PreviewPanel(_state, this) { IsVisible = false };
        _check = new CheckPanel(_state, this) { IsVisible = false, Width = 460, HorizontalAlignment = HorizontalAlignment.Right };
        _export = new ExportPanel(_state, this) { IsVisible = false };

        // ---- the top bar
        _title = new TextBox { Width = 300, FontSize = 16, FontWeight = FontWeight.Bold, PlaceholderText = "Book title", Background = Brushes.Transparent, BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(_title, "The book's title, as it shows on the shelf");
        _title.TextChanged += (_, _) =>
        {
            if (_updatingTitle || !_state.HasBook) return;
            string t = _title.Text ?? "";
            if (t == _state.Book.Title) return;
            _state.Change("Title", () => _state.Book.Title = t, mergeKey: "title", source: _title);
        };
        _status = Ui.Label("", 12.5, Ui.Muted, wrap: false);
        _status.VerticalAlignment = VerticalAlignment.Center;
        _undo = Ui.Button("Undo", _state.Undo, "Undo (Ctrl+Z)");
        _redo = Ui.Button("Redo", _state.Redo, "Redo (Ctrl+Y)");
        _save = Ui.Button("Save", Save, "Save (Ctrl+S). A safety copy is also kept every few seconds while there are unsaved changes.", primary: true);
        var open = Ui.Button("Open", () => _ = OpenFolderDialog(), "Open a book folder (Ctrl+O)");
        open.ContextFlyout = null;
        var recentButton = Ui.Button("Recent ▾", ShowRecent, "Books opened lately");
        _recentButton = recentButton;
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        left.Children.Add(Ui.Label("Book Editor", 15, Ui.Accent, FontWeight.ExtraBold, wrap: false));
        left.Children[0].VerticalAlignment = VerticalAlignment.Center;
        left.Children[0].Margin = new Thickness(0, 0, 6, 0);
        left.Children.Add(_title);
        left.Children.Add(Ui.Button("New", () => _ = NewBook(), "Start a new book (Ctrl+N)"));
        left.Children.Add(open);
        left.Children.Add(recentButton);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        right.Children.Add(_status);
        right.Children.Add(_undo);
        right.Children.Add(_redo);
        right.Children.Add(Ui.Button("▶ Play", () => OpenPreview(fromStop: true), "Play from the selected stop in the reader (P or F5)"));
        right.Children.Add(Ui.Button("Check book", OpenCheck, "Look for problems (F7)"));
        right.Children.Add(Ui.Button("Export", OpenExport, "Make a copy ready for the apps (Ctrl+E)"));
        right.Children.Add(Ui.Button("?", ShowHelp, "Keys and help (F1)"));
        right.Children.Add(_save);
        var bar = new DockPanel { Margin = new Thickness(12, 8) };
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(right);
        bar.Children.Add(left);
        var top = new Border { Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = bar };

        // ---- above the page: what dragging makes, and what is shown over the picture
        _canvasBar = new Border { Background = Ui.Bg, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(10, 6) };
        _empty = new Border
        {
            IsVisible = false, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 460,
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    Ui.Label("This book has no pages yet", 18, Ui.Text, FontWeight.Bold),
                    Ui.Label("Drag page pictures onto the window, or press \"+ Add pictures\" on the left. A picture can be a whole spread, or add a left and a right page and they are joined into one spread.", 13.5, Ui.Muted),
                },
            },
        };
        var center = new DockPanel();
        DockPanel.SetDock(_canvasBar, Dock.Top);
        center.Children.Add(_canvasBar);
        center.Children.Add(new Panel { Children = { _canvas, _empty } });

        var main = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        main.Children.Add(_pages);
        Grid.SetColumn(center, 1);
        main.Children.Add(center);
        Grid.SetColumn(_inspector, 2);
        main.Children.Add(_inspector);

        var work = new DockPanel();
        DockPanel.SetDock(_timeline, Dock.Bottom);
        work.Children.Add(_timeline);
        work.Children.Add(main);
        _workspace = new Panel { Children = { work, _check } };

        _welcome = Welcome();
        _help = HelpSheet();
        _help.IsVisible = false;

        _toastText = new TextBlock { Foreground = Ui.Bg, FontSize = 14, TextWrapping = TextWrapping.Wrap, MaxWidth = 700 };
        _toast = new Border
        {
            Background = Ui.Brush("#f2f3f6"), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10), IsVisible = false, IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 200), Child = _toastText,
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 6, Blur = 24, Color = Color.FromArgb(128, 0, 0, 0) }),
        };
        _dropHint = new Border
        {
            IsVisible = false, IsHitTestVisible = false, Margin = new Thickness(12), BorderBrush = Ui.Accent, BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromArgb(36, 255, 138, 42)),
            Child = new TextBlock { Text = "Drop pictures to add pages, a sound to use it as the selected stop's recording, or a book folder to open it", FontSize = 20, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 700, TextAlignment = TextAlignment.Center },
        };

        var layers = new Panel { Children = { _workspace, _welcome, _preview, _export, _help, _dialogs, _dropHint, _toast } };
        var rootDock = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        rootDock.Children.Add(top);
        rootDock.Children.Add(layers);
        Content = rootDock;

        _state.Changed += OnChanged;
        _images.Arrived += _canvas.InvalidateVisual;
        _autosave = new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background, (_, _) => Autosave());
        if (!screenshotMode) _autosave.Start();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, (_, e) => { if (e.DataTransfer.Contains(DataFormat.File)) _dropHint.IsVisible = true; });
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => _dropHint.IsVisible = false);
        AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = DragDropEffects.Copy);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(KeyDownEvent, OnKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        RefreshChrome();
    }

    readonly Button _recentButton;
    readonly List<Button> _needBook = new();

    // ---------------------------------------------------------------- shared with the panels

    public bool ScreenshotMode { get; }
    public double ShotClock { get; set; } = 100_000;
    public ISoundOutput Sound { get; }
    public bool SoundAvailable => Sound is DesktopSoundWrapper w && w.Inner.Available;
    public Microphone Recorder { get; } = new();
    public string MicrophoneProblem { get; private set; } = "";
    public EditorState State => _state;
    public PageCanvas Canvas => _canvas;
    public PreviewPanel Preview => _preview;
    public Inspector Inspector => _inspector;

    // ---------------------------------------------------------------- keeping the screens up to date

    void OnChanged(Why why, object? source)
    {
        _canvas.InvalidateVisual();
        if (why is Why.Book or Why.Structure or Why.Selection) RefreshCanvasBar();
        RefreshChrome();
        if (why == Why.Book && source != _title) SetTitleBox();
    }

    void SetTitleBox()
    {
        _updatingTitle = true;
        _title.Text = _state.HasBook ? _state.Book.Title : "";
        _updatingTitle = false;
    }

    void RefreshChrome()
    {
        bool has = _state.HasBook;
        _welcome.IsVisible = !has;
        _workspace.IsVisible = has;
        _title.IsEnabled = has;
        _save.IsEnabled = has;
        foreach (Button b in _needBook) b.IsEnabled = has;
        _undo.IsEnabled = _state.History.CanUndo;
        _redo.IsEnabled = _state.History.CanRedo;
        ToolTip.SetTip(_undo, _state.History.UndoLabel is string u ? $"Undo {u.ToLowerInvariant()} (Ctrl+Z)" : "Nothing to undo");
        ToolTip.SetTip(_redo, _state.History.RedoLabel is string r ? $"Redo {r.ToLowerInvariant()} (Ctrl+Y)" : "Nothing to redo");
        _status.Text = !has ? "" : _state.Dirty ? "Not saved yet" : _state.SavedAt is DateTime t ? $"Saved at {t:HH:mm}" : "No changes";
        _empty.IsVisible = has && _state.Book.Pages.Count == 0;
        Title = has ? $"{(_state.Dirty ? "* " : "")}{(_state.Book.Title.Length > 0 ? _state.Book.Title : "Untitled")} - Book Editor" : "Book Editor";
    }

    void RefreshCanvasBar()
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 10, LineSpacing = 4 };
        if (_state.Page is Page p)
        {
            if (p.Explore)
            {
                row.Children.Add(Ui.Label("Dragging draws", 12.5, Ui.Muted, wrap: false));
                row.Children[^1].VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(Ui.Segments(new List<(string, string, string)>
                {
                    ("stop", "Intro stops", "Stops the camera visits before the child explores"),
                    ("hot", "Things to find", "Things the child taps to find"),
                }, _state.Drawing == DrawKind.Hotspot ? "hot" : "stop", v => { _state.Drawing = v == "hot" ? DrawKind.Hotspot : DrawKind.Stop; RefreshCanvasBar(); }));
            }
            else if (_state.Drawing == DrawKind.Dry)
            {
                row.Children.Add(Ui.Label("Drag on the picture to mark an area the weather misses.", 12.5, Ui.Warn, wrap: false));
            }
            else
            {
                row.Children.Add(Ui.Label("Drag on the picture to draw a stop.", 12.5, Ui.Muted, wrap: false));
            }
            row.Children[^1].VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(new Border { Width = 1, Background = Ui.Line, Margin = new Thickness(4, 2) });
            row.Children.Add(Ui.Check("Spread middle", _canvas.Overlays.Crease, v => { _canvas.Overlays.Crease = v; _canvas.InvalidateVisual(); }, "Show the crease of a double-page spread and its left and right pages"));
            row.Children.Add(Ui.Label("Show what these screens see:", 12.5, Ui.Muted, wrap: false));
            row.Children[^1].VerticalAlignment = VerticalAlignment.Center;
            foreach (var (id, text) in new[] { ("s22-upright", "Phone upright"), ("s22-sideways", "Phone sideways"), ("cheap-sideways", "Small phone"), ("tablet-sideways", "Tablet"), ("window", "Computer") })
            {
                row.Children.Add(Ui.Check(text, _canvas.Overlays.Frames.Contains(id), v =>
                {
                    if (v) _canvas.Overlays.Frames.Add(id); else _canvas.Overlays.Frames.Remove(id);
                    _canvas.InvalidateVisual();
                }, "Around the selected stop, a frame shows what this screen shows. Upright phones show one page of a spread at a time."));
            }
            row.Children.Add(Ui.Button("Fit", _canvas.Fit, "Show the whole page (Ctrl and the mouse wheel zoom; drag with the middle button or Space to move)", small: true));
        }
        foreach (Control c in row.Children) c.VerticalAlignment = VerticalAlignment.Center;
        _canvasBar.Child = row;
    }

    /// <summary>A short message at the bottom of the window that goes away by itself.</summary>
    public void Say(string text, double seconds = 5)
    {
        _toastText.Text = text;
        _toast.IsVisible = true;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer(TimeSpan.FromSeconds(seconds), DispatcherPriority.Normal, (_, _) => { _toast.IsVisible = false; _toastTimer?.Stop(); });
        _toastTimer.Start();
    }

    public void FocusForKeys() => _canvas.Focus();

    // ---------------------------------------------------------------- the welcome screen

    Control Welcome()
    {
        var list = new StackPanel { Spacing = 6 };
        var body = new StackPanel
        {
            Spacing = 12, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Ui.Label("Make a picture book", 26, Ui.Text, FontWeight.ExtraBold),
                Ui.Label("Each book is a folder holding its pictures, sounds and a book.json file. Start a new one, or open a book folder you already have.", 14, Ui.Muted),
                Ui.Row(Ui.Button("Start a new book", () => _ = NewBook(), "Ctrl+N", primary: true), Ui.Button("Open a book folder...", () => _ = OpenFolderDialog(), "Ctrl+O")),
                list,
            },
        };
        void Fill()
        {
            list.Children.Clear();
            var recent = _recent.Existing();
            if (recent.Count > 0)
            {
                list.Children.Add(Ui.Heading("Opened lately"));
                foreach (string f in recent.Take(8)) list.Children.Add(BookLink(f));
            }
            string? content = BookLibrary.FindContent(null, AppContext.BaseDirectory, Environment.CurrentDirectory);
            if (content is not null)
            {
                var samples = new List<string>();
                try { samples = BookLibrary.List(content).Select(b => b.Folder).Where(f => !recent.Contains(Path.GetFullPath(f))).ToList(); } catch (Exception) { }
                if (samples.Count > 0)
                {
                    list.Children.Add(Ui.Heading("Books in the reader's content folder"));
                    foreach (string f in samples) list.Children.Add(BookLink(f));
                }
            }
        }
        var root = new Border { Background = Ui.Bg, Child = new ScrollViewer { Content = new Border { Padding = new Thickness(40), Child = body } } };
        root.AttachedToVisualTree += (_, _) => Fill();
        root.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty && root.IsVisible) Fill(); };
        return root;
    }

    Control BookLink(string folder)
    {
        string title = Path.GetFileName(folder);
        try { title = BookLibrary.Describe(folder).Title; } catch (Exception) { }
        var b = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 8),
            Content = new StackPanel { Children = { Ui.Label(title, 14, Ui.Text, FontWeight.SemiBold), Ui.Label(folder, 11.5, Ui.Muted) } },
        };
        b.Click += (_, _) => _ = OpenBook(folder);
        return b;
    }

    void ShowRecent()
    {
        var menu = new MenuFlyout();
        var recent = _recent.Existing();
        if (recent.Count == 0) menu.Items.Add(new MenuItem { Header = "No books opened yet", IsEnabled = false });
        foreach (string f in recent)
        {
            string title = Path.GetFileName(f);
            try { title = BookLibrary.Describe(f).Title + "   (" + f + ")"; } catch (Exception) { }
            var item = new MenuItem { Header = title };
            item.Click += (_, _) => _ = OpenBook(f);
            menu.Items.Add(item);
        }
        menu.ShowAt(_recentButton);
    }

    // ---------------------------------------------------------------- opening, new, saving

    /// <summary>True when it's all right to put the current book away: saved, or the author said so.</summary>
    async Task<bool> OkToLeave()
    {
        if (!_state.HasBook || !_state.Dirty) return true;
        int a = await _dialogs.Ask("Save your changes?", $"\"{_state.Book.Title}\" has changes that aren't saved.", new[] { "Save", "Don't save", "Cancel" }, 0, 2);
        if (a == 2) return false;
        if (a == 0) Save();
        else _recovery.Clear(_state.Project!.Folder);
        return true;
    }

    public async Task OpenFolderDialog()
    {
        if (!await OkToLeave()) return;
        string? folder = await PickFolder("Open a book folder");
        if (folder is not null) await OpenBook(folder, askFirst: false);
    }

    public async Task OpenBook(string folder, bool askFirst = true)
    {
        if (askFirst && !await OkToLeave()) return;
        BookProject project;
        try { project = BookProject.Open(folder); }
        catch (Exception e)
        {
            Say("That folder couldn't be opened: " + e.Message, 8);
            _recent.Remove(folder);
            return;
        }
        _preview.Stop();
        _state.Open(project);
        _recent.Add(project.Folder);
        SetTitleBox();
        if (!File.Exists(project.BookFile)) Say($"Made a new book from the {project.Book.Pages.Count} pictures in this folder. Save writes its book.json.", 7);
        if (_recovery.Find(project.Folder) is var (book, saved) && !ScreenshotMode)
        {
            int a = await _dialogs.Ask("Get back unsaved changes?",
                $"This book was changed on {saved.ToLocalTime():d MMMM} at {saved.ToLocalTime():HH:mm} but not saved (perhaps the editor or the computer stopped). Do you want those changes back?",
                new[] { "Get them back", "Use the saved book" }, 0, 1);
            if (a == 0) { _state.Recover(book); Say("Your unsaved changes are back. Press Save to keep them."); }
            else _recovery.Clear(project.Folder);
        }
    }

    public async Task NewBook()
    {
        if (!await OkToLeave()) return;
        string parent = EditorPaths.DefaultBooksFolder;
        var title = Ui.TextField("", "For example: Moss and the Biggest Puddle");
        var where = Ui.Label(parent, 12, Ui.Text);
        var create = Ui.Button("Make the book", () => { }, primary: true);
        var form = new StackPanel
        {
            Children =
            {
                Ui.Label("Start a new book", 17, Ui.Text, FontWeight.Bold),
                Ui.FieldName("What is the book called?"), title,
                Ui.FieldName("Its folder goes in"),
                new Border { Background = Ui.Field, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(8, 6), Child = where },
                Ui.Row(Ui.Button("Change...", async () => { if (await PickFolder("Where should the book's folder go?") is string f) { parent = f; where.Text = f; } }, small: true)),
                Ui.Help("A folder named after the title is made there. Everything the book uses (pictures, sounds, recordings) is kept in it."),
                Ui.Row(create, Ui.Button("Cancel", () => _dialogs.Close())),
            },
        };
        create.Click += (_, _) =>
        {
            string name = (title.Text ?? "").Trim();
            if (name.Length == 0) { Say("Type the book's title first."); return; }
            try
            {
                Directory.CreateDirectory(parent);
                BookProject p = BookProject.Create(parent, name);
                _dialogs.Close();
                _state.Open(p);
                _recent.Add(p.Folder);
                SetTitleBox();
                Say("Now add the page pictures: drag them onto the window, or press \"+ Add pictures\".", 7);
            }
            catch (Exception e) { Say("The book's folder couldn't be made: " + e.Message, 8); }
        };
        _dialogs.ShowPanel(form, () => { });
        Dispatcher.UIThread.Post(() => title.Focus());
    }

    public void Save()
    {
        if (!_state.HasBook) return;
        try
        {
            _state.Save();
            _recovery.Clear(_state.Project!.Folder);
            Say("Saved.", 2);
        }
        catch (Exception e)
        {
            // The recovery copy still holds the changes, so nothing is lost.
            _recovery.Write(_state.Project!);
            Say("The book couldn't be saved: " + e.Message + " Your changes are kept in a safety copy.", 10);
        }
    }

    void Autosave()
    {
        if (_state.HasBook && _state.Dirty) _recovery.Write(_state.Project!);
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closingConfirmed || !_state.HasBook || !_state.Dirty || ScreenshotMode) return;
        e.Cancel = true;
        if (await OkToLeave())
        {
            _closingConfirmed = true;
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _preview.Stop();
        Recorder.Dispose();
        (Sound as IDisposable)?.Dispose();
    }

    // ---------------------------------------------------------------- file pickers

    public async Task<string?> PickFolder(string title)
    {
        try
        {
            var r = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            return r.Count > 0 ? r[0].TryGetLocalPath() : null;
        }
        catch (Exception e) { Say("The folder picker couldn't open: " + e.Message); return null; }
    }

    public async Task<string[]> PickFiles(string title, string[] patterns, string kind, bool many)
    {
        try
        {
            var r = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title, AllowMultiple = many,
                FileTypeFilter = new[] { new FilePickerFileType(kind) { Patterns = patterns } },
            });
            return r.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        }
        catch (Exception e) { Say("The file picker couldn't open: " + e.Message); return Array.Empty<string>(); }
    }

    public async Task<string?> PickOneFile(string title, string[] patterns, string kind) => (await PickFiles(title, patterns, kind, false)).FirstOrDefault();

    static readonly string[] PicturePatterns = { "*.jpg", "*.jpeg", "*.png", "*.webp" };
    static readonly string[] SoundPatterns = { "*.wav", "*.mp3", "*.ogg", "*.flac" };

    // ---------------------------------------------------------------- pages

    public async void AddPictures()
    {
        if (!_state.HasBook) return;
        string[] files = await PickFiles("Add page pictures", PicturePatterns, "Pictures", true);
        if (files.Length > 0) AddPictureFiles(files);
    }

    /// <summary>
    /// Add pictures as pages, after the page showing, in name order. Pictures named as a pair (for
    /// example page3-left.png and page3-right.png) are joined into one spread.
    /// </summary>
    public void AddPictureFiles(IEnumerable<string> paths)
    {
        BookProject project = _state.Project!;
        var files = paths.Where(BookProject.IsPicture).ToList();
        if (files.Count == 0) { Say("Those weren't picture files. Pages can be JPEG, PNG or WebP."); return; }
        var pages = new List<Page>();
        var notes = new List<string>();
        foreach (PagePlan.Item item in PagePlan.Make(files))
        {
            try
            {
                pages.Add(item.IsPair
                    ? JoinPage(project.Import(item.Left!), project.Import(item.Right!), PagePlan.Stem(item.Left!))
                    : new Page { Image = project.Import(item.Single!) });
            }
            catch (Exception e) { notes.Add($"{Path.GetFileName(item.Single ?? item.Left)} couldn't be added: {e.Message}"); }
        }
        if (pages.Count == 0) { Say(string.Join(" ", notes), 8); return; }
        int at = _state.Book.Pages.Count == 0 ? 0 : _state.PageIndex + 1;
        _state.Change(pages.Count == 1 ? "Add a page" : "Add pages", () => _state.Book.Pages.InsertRange(at, pages), structure: true);
        _state.GoToPage(at);
        foreach (Page p in pages) notes.AddRange(PictureNotes(p));
        Say((pages.Count == 1 ? "Added a page." : $"Added {pages.Count} pages.") + " Drag on the picture to add stops." + (notes.Count > 0 ? " " + string.Join(" ", notes.Distinct().Take(2)) : ""), notes.Count > 0 ? 9 : 5);
    }

    /// <summary>Join two page pictures (already in the folder) into a new spread picture, and make the page for it.</summary>
    Page JoinPage(string left, string right, string stem)
    {
        BookProject project = _state.Project!;
        stem = System.IO.Path.GetFileName(stem);
        string name = project.UniqueName((stem.Length > 0 ? stem : "spread") + "-spread.jpg");
        Pictures.JoinSpread(project.PathOf(left)!, project.PathOf(right)!, project.PathOf(name)!);
        return new Page { Image = name, Spread = new SpreadSource { Left = left, Right = right } };
    }

    /// <summary>Warnings worth giving straight away when a picture comes in.</summary>
    IEnumerable<string> PictureNotes(Page p)
    {
        var (w, h) = _state.PictureSize(p.Image);
        if (Math.Min(w, h) < Pictures.SoftBelow) yield return $"{p.Image} is only {w} x {h}, so it will look soft close up.";
        var others = _state.Book.Pages.Where(o => o != p && !o.Explore).Select(o => _state.Aspect(o)).ToList();
        if (others.Count > 0)
        {
            double usual = others.GroupBy(a => Math.Round(a, 2)).OrderByDescending(g => g.Count()).First().Key;
            double mine = (double)w / h;
            if (!p.Explore && Math.Abs(mine - usual) / usual > 0.03) yield return $"{p.Image} is a different shape from the other pages.";
        }
    }

    public async void AddLeftRight()
    {
        if (!_state.HasBook) return;
        string? left = await PickOneFile("Choose the LEFT-hand page", PicturePatterns, "Pictures");
        if (left is null) return;
        string? right = await PickOneFile("Now choose the RIGHT-hand page", PicturePatterns, "Pictures");
        if (right is null) return;
        BookProject project = _state.Project!;
        Page page;
        try { page = JoinPage(project.Import(left), project.Import(right), PagePlan.Stem(left)); }
        catch (Exception e) { Say("Those pictures couldn't be joined: " + e.Message, 8); return; }
        int at = _state.Book.Pages.Count == 0 ? 0 : _state.PageIndex + 1;
        _state.Change("Add a spread", () => _state.Book.Pages.Insert(at, page), structure: true);
        _state.GoToPage(at);
        Say("Joined the two pages into one spread. " + string.Join(" ", PictureNotes(page)));
    }

    public async void ReplacePicture()
    {
        if (_state.Page is not Page p) return;
        string? file = await PickOneFile("Choose the new picture for this page", PicturePatterns, "Pictures");
        if (file is null) return;
        double before = _state.Aspect(p);
        string name = _state.Project!.Import(file);
        bool tiled = p.Tiles is not null;
        _state.Change("Replace the picture", () =>
        {
            if (_state.Book.Cover == p.Image) _state.Book.Cover = name;
            p.Image = name;
            p.Spread = null;
            p.Tiles = null;
        }, structure: true);
        double after = _state.Aspect(p);
        string note = Math.Abs(after - before) / before > 0.02 ? " The new picture is a different shape, so check where the stops are." : "";
        Say("Replaced the picture. The stops are where they were." + note + (tiled ? " The old tiles are no longer used; Export cuts new ones if it needs them." : ""), 8);
    }

    public async void ReplaceHalf(bool left)
    {
        if (_state.Page is not Page p || p.Spread is not SpreadSource sp) return;
        string? file = await PickOneFile(left ? "Choose the new LEFT-hand page" : "Choose the new RIGHT-hand page", PicturePatterns, "Pictures");
        if (file is null) return;
        BookProject project = _state.Project!;
        string half = project.Import(file);
        string l = left ? half : sp.Left, r = left ? sp.Right : half;
        if (!project.Has(l) || !project.Has(r)) { Say("The other half is missing from the folder, so the spread can't be joined again. Use Replace picture instead."); return; }
        Page joined;
        try { joined = JoinPage(l, r, PagePlan.Stem(sp.Left)); }
        catch (Exception e) { Say("Those pictures couldn't be joined: " + e.Message, 8); return; }
        _state.Change(left ? "Replace the left page" : "Replace the right page", () =>
        {
            if (_state.Book.Cover == p.Image) _state.Book.Cover = joined.Image;
            p.Image = joined.Image;
            p.Spread = joined.Spread;
        }, structure: true);
        Say("Joined the new page in. The stops are where they were.");
    }

    public void JoinWithNext()
    {
        if (_state.Page is not Page p) return;
        int i = _state.PageIndex;
        if (i + 1 >= _state.Book.Pages.Count) { Say("There is no next page to join with."); return; }
        Page next = _state.Book.Pages[i + 1];
        if (p.Tiles is not null || next.Tiles is not null) { Say("Big scenes cut into tiles can't be joined."); return; }
        BookProject project = _state.Project!;
        var leftSize = _state.PictureSize(p.Image);
        var rightSize = _state.PictureSize(next.Image);
        Page joined;
        try { joined = JoinPage(p.Image, next.Image, Path.GetFileNameWithoutExtension(p.Image)); }
        catch (Exception e) { Say("Those pages couldn't be joined: " + e.Message, 8); return; }
        RectD L(RectD b) => PagePlan.OnSpread(b, false, leftSize, rightSize);
        RectD R(RectD b) => PagePlan.OnSpread(b, true, leftSize, rightSize);
        _state.Change("Join two pages", () =>
        {
            int before = p.Stops.Count;
            int? Later(int? stop) => stop is > 0 ? stop + before : stop;
            foreach (Stop s in p.Stops) { s.Box = L(s.Box); if (s.To is RectD t) s.To = L(t); }
            foreach (Hotspot h in p.Hotspots) h.Box = L(h.Box);
            foreach (Layer l in p.Layers) { l.Box = L(l.Box); if (l.MoveTo is RectD m) l.MoveTo = L(m); }
            foreach (PageEffect e in p.Effects) for (int d = 0; d < e.Dry.Count; d++) e.Dry[d] = L(e.Dry[d]);
            // The next page's stops come after this page's, so anything it set to happen at its stops moves along with them.
            foreach (Stop s in next.Stops) { s.Box = R(s.Box); if (s.To is RectD t) s.To = R(t); p.Stops.Add(s); }
            foreach (Hotspot h in next.Hotspots) { h.Box = R(h.Box); p.Hotspots.Add(h); }
            foreach (Layer l in next.Layers)
            {
                l.Box = R(l.Box);
                if (l.MoveTo is RectD m) l.MoveTo = R(m);
                l.From = Later(l.From); l.To = Later(l.To); l.MoveAt = Later(l.MoveAt);
                p.Layers.Add(l);
            }
            foreach (PageEffect e in next.Effects)
            {
                for (int d = 0; d < e.Dry.Count; d++) e.Dry[d] = R(e.Dry[d]);
                e.From = Later(e.From); e.To = Later(e.To); e.Stop = Later(e.Stop);
                p.Effects.Add(e);
            }
            if (_state.Book.Cover == p.Image || _state.Book.Cover == next.Image) _state.Book.Cover = joined.Image;
            p.Spread = joined.Spread;
            p.Image = joined.Image;
            _state.Book.Pages.RemoveAt(i + 1);
        }, structure: true);
        Say("Joined the two pages into one spread, with the stops of both.");
    }

    public void MovePage(int from, int to)
    {
        if (!_state.HasBook) return;
        var pages = _state.Book.Pages;
        if (from < 0 || from >= pages.Count || to < 0 || to >= pages.Count || from == to) return;
        _state.Change("Move a page", () => { Page p = pages[from]; pages.RemoveAt(from); pages.Insert(to, p); _state.SetPageQuietly(to); _state.SetSelectionQuietly(Sel.None, -1); }, structure: true);
    }

    public void DuplicatePage()
    {
        if (_state.Page is not Page p) return;
        int at = _state.PageIndex + 1;
        Page copy = Copy(p);
        _state.Change("Duplicate the page", () => { _state.Book.Pages.Insert(at, copy); _state.SetPageQuietly(at); _state.SetSelectionQuietly(Sel.None, -1); }, structure: true);
    }

    public void DeletePage()
    {
        if (_state.Page is null) return;
        int i = _state.PageIndex;
        _state.Change("Delete the page", () => { _state.Book.Pages.RemoveAt(i); _state.SetPageQuietly(Math.Max(0, Math.Min(i, _state.Book.Pages.Count - 1))); _state.SetSelectionQuietly(Sel.None, -1); }, structure: true);
        Say("Page deleted. Its picture is still in the folder. Ctrl+Z brings the page back.");
    }

    /// <summary>A deep copy of a page, through the book format, so nothing is shared.</summary>
    static Page Copy(Page p)
    {
        var tmp = new Book { Pages = { p } };
        return BookJson.Parse(BookJson.ToJsonString(tmp)).Pages[0];
    }

    public async void PickCover()
    {
        string? file = await PickOneFile("Choose the cover picture", PicturePatterns, "Pictures");
        if (file is null) return;
        string name = _state.Project!.Import(file);
        _state.Change("Cover", () => _state.Book.Cover = name, structure: true);
    }

    // ---------------------------------------------------------------- stops and things to find

    public void AddWholePageStop()
    {
        if (_state.Page is not Page p) return;
        int at = _state.Selected == Sel.Stop ? _state.SelIndex + 1 : p.Stops.Count;
        _state.Change("Add a whole page stop", () => { p.Stops.Insert(at, new Stop { Box = new RectD(0, 0, 1, 1) }); _state.SetSelectionQuietly(Sel.Stop, at); }, structure: true);
    }

    public void MoveStop(int from, int to)
    {
        if (_state.Page is not Page p || from < 0 || from >= p.Stops.Count || to < 0 || to >= p.Stops.Count || from == to) return;
        _state.Change("Change the order", () => { Stop s = p.Stops[from]; p.Stops.RemoveAt(from); p.Stops.Insert(to, s); _state.SetSelectionQuietly(Sel.Stop, to); }, structure: true);
    }

    static void Nudge(ref RectD b) => b = new RectD(Math.Clamp(b.X + 0.03, 0, Math.Max(0, 1 - b.W)), Math.Clamp(b.Y + 0.03, 0, Math.Max(0, 1 - b.H)), b.W, b.H);

    public void DuplicateSelected()
    {
        Page? p = _state.Page;
        if (p is null) return;
        if (_state.Stop is Stop s)
        {
            int at = _state.SelIndex + 1;
            Stop c = CopyStop(s);
            RectD b = c.Box; Nudge(ref b); c.Box = b;
            if (c.To is RectD t) { Nudge(ref t); c.To = t; }
            c.Voice = null;     // a recording belongs to one stop's words
            _state.Change("Duplicate the stop", () => { p.Stops.Insert(at, c); _state.SetSelectionQuietly(Sel.Stop, at); }, structure: true);
        }
        else if (_state.Hotspot is Hotspot h)
        {
            int at = _state.SelIndex + 1;
            Hotspot c = CopyHotspot(h);
            c.Story = 0;
            RectD b = c.Box; Nudge(ref b); c.Box = b;
            _state.Change("Duplicate the thing to find", () => { p.Hotspots.Insert(at, c); _state.SetSelectionQuietly(Sel.Hotspot, at); }, structure: true);
        }
    }

    static Stop CopyStop(Stop s) => BookJson.Parse(BookJson.ToJsonString(new Book { Pages = { new Page { Stops = { s } } } })).Pages[0].Stops[0];
    static Hotspot CopyHotspot(Hotspot h) => BookJson.Parse(BookJson.ToJsonString(new Book { Pages = { new Page { Explore = true, Hotspots = { h } } } })).Pages[0].Hotspots[0];

    public void CopySelected()
    {
        if (_state.Stop is Stop s) { _clipboard = ("stop", BookJson.ToJsonString(new Book { Pages = { new Page { Stops = { s } } } })); Say("Copied the stop. Go to any page and press Ctrl+V.", 3); }
        else if (_state.Hotspot is Hotspot h) { _clipboard = ("hot", BookJson.ToJsonString(new Book { Pages = { new Page { Explore = true, Hotspots = { h } } } })); Say("Copied. Go to an explore page and press Ctrl+V.", 3); }
    }

    public void Paste()
    {
        if (_state.Page is not Page p || _clipboard is not var (kind, json)) return;
        Page from = BookJson.Parse(json).Pages[0];
        if (kind == "hot")
        {
            if (!p.Explore) { Say("Things to find can only go on an explore page."); return; }
            Hotspot h = from.Hotspots[0];
            h.Story = 0;
            _state.Change("Paste", () => { p.Hotspots.Add(h); _state.SetSelectionQuietly(Sel.Hotspot, p.Hotspots.Count - 1); }, structure: true);
        }
        else
        {
            Stop s = from.Stops[0];
            int at = _state.Selected == Sel.Stop ? _state.SelIndex + 1 : p.Stops.Count;
            _state.Change("Paste", () => { p.Stops.Insert(at, s); _state.SetSelectionQuietly(Sel.Stop, at); }, structure: true);
        }
    }

    public void DeleteSelected()
    {
        if (_state.Page is not Page p) return;
        int i = _state.SelIndex;
        switch (_state.Selected)
        {
            case Sel.Stop when i < p.Stops.Count: _state.Change("Delete the stop", () => { p.Stops.RemoveAt(i); _state.SetSelectionQuietly(Sel.None, -1); }, structure: true); break;
            case Sel.Hotspot when i < p.Hotspots.Count: _state.Change("Delete the thing to find", () => { p.Hotspots.RemoveAt(i); _state.SetSelectionQuietly(Sel.None, -1); }, structure: true); break;
            case Sel.Layer when i < p.Layers.Count: _state.Change("Delete the picture on top", () => { p.Layers.RemoveAt(i); _state.SetSelectionQuietly(Sel.None, -1); }, structure: true); break;
            case Sel.Dry when _state.SelEffect >= 0 && _state.SelEffect < p.Effects.Count:
                int fx = _state.SelEffect;
                _state.Change("Delete the area", () => { p.Effects[fx].Dry.RemoveAt(i); _state.SetSelectionQuietly(Sel.None, -1); }, structure: true);
                break;
        }
    }

    void NudgeSelected(double dx, double dy, bool resize)
    {
        if (_state.Page is not Page p) return;
        RectD Move(RectD b, bool off = false) => resize
            ? new RectD(b.X, b.Y, Math.Max(0.01, b.W + dx), Math.Max(0.01, b.H + dy))
            : new RectD(Math.Clamp(b.X + dx, off ? -b.W : 0, off ? 1 : Math.Max(0, 1 - b.W)), Math.Clamp(b.Y + dy, off ? -b.H : 0, off ? 1 : Math.Max(0, 1 - b.H)), b.W, b.H);
        int i = _state.SelIndex;
        switch (_state.Selected)
        {
            case Sel.Stop: _state.Change("Nudge", () => p.Stops[i].Box = Move(p.Stops[i].Box), mergeKey: "nudge"); break;
            case Sel.Hotspot: _state.Change("Nudge", () => p.Hotspots[i].Box = Move(p.Hotspots[i].Box), mergeKey: "nudge"); break;
            case Sel.Layer: _state.Change("Nudge", () => p.Layers[i].Box = Move(p.Layers[i].Box, true), mergeKey: "nudge"); break;
            case Sel.Dry: int fx = _state.SelEffect; _state.Change("Nudge", () => p.Effects[fx].Dry[i] = Move(p.Effects[fx].Dry[i]), mergeKey: "nudge"); break;
        }
    }

    // ---------------------------------------------------------------- pictures on top, sounds

    public async void AddLayer()
    {
        if (_state.Page is not Page p) return;
        string? file = await PickOneFile("Choose a picture to put on top (a PNG with a see-through background)", PicturePatterns, "Pictures");
        if (file is null) return;
        string name = _state.Project!.Import(file);
        var (iw, ih) = Pictures.Size(_state.Project.PathOf(name)!) ?? (400, 400);
        var (pw, ph) = _state.PictureSize(p.Image);
        double w = Math.Clamp((double)iw / pw, 0.06, 0.35), h = w * ((double)ih / iw) * ((double)pw / ph);
        _state.Change("Add a picture on top", () => { p.Layers.Add(new Layer { Image = name, Box = new RectD(0.5 - w / 2, 0.5 - h / 2, w, h) }); _state.SetSelectionQuietly(Sel.Layer, p.Layers.Count - 1); }, structure: true);
        if (!Pictures.HasAlpha(_state.Project.PathOf(name)!)) Say("That picture has no see-through parts, so it will show as a rectangle. A PNG with a see-through background works best.", 8);
    }

    public async void ReplaceLayer(int index)
    {
        if (_state.Page is not Page p || index >= p.Layers.Count) return;
        string? file = await PickOneFile("Choose the new picture", PicturePatterns, "Pictures");
        if (file is null) return;
        string name = _state.Project!.Import(file);
        _state.Change("Replace the picture on top", () => p.Layers[index].Image = name, structure: true);
    }

    public async void PickSound(Action<string?> set)
    {
        string? file = await PickOneFile("Choose a sound file", SoundPatterns, "Sounds");
        if (file is null) return;
        string name = _state.Project!.Import(file, "sounds");
        _state.Change("Add a sound", () => set(name), structure: true);
    }

    public void PlaySound(string name)
    {
        if (_state.Project?.PathOf(name) is string path && File.Exists(path)) Sound.PlayClip(path);
        if (!SoundAvailable) Say("No sound device is working on this computer, so the sound can't be played here.");
    }

    public void ShowFolder() { if (_state.HasBook) Reveal(_state.Project!.Folder); }

    public void Reveal(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS()) Process.Start("open", path);
            else Process.Start("xdg-open", path);
        }
        catch (Exception) { Say("The folder is " + path, 8); }
    }

    // ---------------------------------------------------------------- dropping files on the window

    void OnDrop(object? sender, DragEventArgs e)
    {
        _dropHint.IsVisible = false;
        var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? new List<string>();
        if (paths.Count == 0) return;
        string? folder = paths.FirstOrDefault(Directory.Exists);
        if (folder is not null)
        {
            if (File.Exists(Path.Combine(folder, "book.json")) || !_state.HasBook) { _ = OpenBook(folder); return; }
            paths = Directory.GetFiles(folder).ToList();
        }
        if (!_state.HasBook) { Say("Open or start a book first, then drop its pictures here."); return; }
        var sounds = paths.Where(BookProject.IsSound).ToList();
        if (sounds.Count > 0 && _state.Stop is Stop s)
        {
            string name = _state.Project!.Import(sounds[0], "voice", $"p{_state.PageIndex + 1}-s{_state.SelIndex + 1}{Path.GetExtension(sounds[0])}");
            _state.Change("Add a recording", () => s.Voice = new Voice { File = name }, structure: true);
            Say("That sound is now this stop's recording. Press Tap along to time the words.");
        }
        else if (sounds.Count > 0) Say("Select a stop first, then drop its recording on the window.");
        if (paths.Any(BookProject.IsPicture)) AddPictureFiles(paths);
    }

    // ---------------------------------------------------------------- preview, check, export, help

    public void OpenPreview(bool fromStop)
    {
        if (!_state.HasBook || _state.Book.Pages.Count == 0) { Say("Add a page first."); return; }
        _preview.IsVisible = true;
        _preview.Start(_state.PageIndex, _state.Selected == Sel.Stop ? _state.SelIndex : -1, fromStop && _state.Selected == Sel.Stop);
        Dispatcher.UIThread.Post(_preview.FocusReader);
    }

    public void ClosePreview()
    {
        _preview.Stop();
        _preview.IsVisible = false;
        _canvas.Focus();
    }

    public void OpenCheck()
    {
        if (!_state.HasBook) return;
        _check.Run();
        _check.IsVisible = true;
    }

    public void CloseCheck() => _check.IsVisible = false;

    public void OpenExport()
    {
        if (!_state.HasBook) return;
        _export.Open();
        _export.IsVisible = true;
    }

    public void CloseExport() => _export.IsVisible = false;

    void ShowHelp() => _help.IsVisible = !_help.IsVisible;

    Control HelpSheet()
    {
        (string, string)[] keys =
        {
            ("Ctrl+S", "Save"), ("Ctrl+Z / Ctrl+Y", "Undo / redo"), ("Ctrl+N / Ctrl+O", "New book / open a book"),
            ("Drag on the picture", "Draw a stop (or a thing to find, on explore pages)"), ("Shift + drag", "Draw over another box"),
            ("Delete", "Remove what is selected"), ("Arrow keys", "Nudge it (Shift: further, Alt: change its size)"),
            ("Ctrl+D", "Duplicate it"), ("Ctrl+C, Ctrl+V", "Copy it, paste it on another page"),
            ("[ and ]", "Select the stop before or after"), ("Page Up / Page Down", "Go to the page before or after"),
            ("Ctrl+Page Up / Down", "Move the page earlier or later"), ("P or F5", "Play from the selected stop"),
            ("F7", "Check book"), ("Ctrl+E", "Export"), ("Ctrl + mouse wheel", "Zoom the picture; Space or the middle button and drag moves it"),
            ("Tap along: Space", "Mark the start of each word; Backspace takes back, Esc stops"),
            ("A chosen word: , and .", "Nudge its time earlier or later"), ("Esc", "Close what is open, or clear the selection"),
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 16, RowSpacing = 6 };
        for (int i = 0; i < keys.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var k = Ui.Label(keys[i].Item1, 13, Ui.Accent, FontWeight.Bold, wrap: false);
            var t = Ui.Label(keys[i].Item2, 13, Ui.Text);
            Grid.SetRow(k, i); Grid.SetRow(t, i); Grid.SetColumn(t, 1);
            grid.Children.Add(k); grid.Children.Add(t);
        }
        var body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new DockPanel { Children = { new Border { [DockPanel.DockProperty] = Dock.Right, Child = Ui.Button("Close", ShowHelp, "Esc or F1", small: true) }, Ui.Label("Keys and tips", 18, Ui.Text, FontWeight.Bold) } },
                grid,
                Ui.Heading("How a book is made"),
                Ui.Help("1. Add the page pictures. A picture can be a whole spread, or add a left and a right page and the editor joins them into one spread picture (it remembers the two halves, so either can be replaced later)."),
                Ui.Help("2. On each page, drag to draw where the camera stops, in reading order. Type the words for each stop. Add a pan, effects, pictures on top or sounds if the page wants them."),
                Ui.Help("3. Record or add the voice reading each stop's words, then Tap along to time the words so they light up as they are read."),
                Ui.Help("4. Press Play to see it in the real reader, on a phone or a computer. Check book lists anything that will go wrong. Export makes the copy for the apps."),
                Ui.Help("Changes are kept in a safety copy every few seconds, so if the computer stops before you save, the book offers them back next time."),
            },
        };
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
            Child = new Border
            {
                Width = 680, Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = new ScrollViewer { Content = body, MaxHeight = 760 },
            },
        };
    }

    // ---------------------------------------------------------------- keys

    void OnKey(object? sender, KeyEventArgs e)
    {
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        object? focused = FocusManager?.GetFocusedElement();
        // Text boxes keep all their keys; lists and sliders keep the arrow keys and Space.
        bool typing = e.Source is TextBox || focused is TextBox
            || (focused is ComboBox or Slider or ListBox && e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Space or Key.Enter or Key.PageUp or Key.PageDown);

        if (_dialogs.HandleKey(e.Key)) { e.Handled = true; return; }
        if (ctrl && e.Key == Key.S) { Save(); e.Handled = true; return; }
        if (_preview.IsVisible)
        {
            if (e.Key == Key.Escape) { ClosePreview(); e.Handled = true; }
            return;   // everything else goes to the reader
        }
        if (_help.IsVisible && e.Key is Key.Escape or Key.F1) { _help.IsVisible = false; e.Handled = true; return; }
        if (_export.IsVisible) { if (e.Key == Key.Escape) { CloseExport(); e.Handled = true; } return; }
        if (e.Key == Key.F1) { ShowHelp(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.N) { _ = NewBook(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.O) { _ = OpenFolderDialog(); e.Handled = true; return; }
        if (!_state.HasBook) return;
        if (_inspector.Narration is NarrationPanel np && !typing && np.HandleKey(e.Key)) { e.Handled = true; return; }
        if (typing) return;   // text boxes keep their own keys, Ctrl+Z included

        switch (e.Key)
        {
            case Key.Z when ctrl && shift: _state.Redo(); break;
            case Key.Z when ctrl: _state.Undo(); break;
            case Key.Y when ctrl: _state.Redo(); break;
            case Key.D when ctrl: DuplicateSelected(); break;
            case Key.C when ctrl: CopySelected(); break;
            case Key.V when ctrl: Paste(); break;
            case Key.E when ctrl: OpenExport(); break;
            case Key.F7: OpenCheck(); break;
            case Key.P or Key.F5: OpenPreview(fromStop: true); break;
            case Key.Escape when _check.IsVisible: CloseCheck(); break;
            case Key.Escape:
                if (_state.Drawing == DrawKind.Dry) { _state.Drawing = DrawKind.Stop; _state.DryTarget = -1; RefreshCanvasBar(); }
                _state.Select(Sel.None, -1);
                break;
            case Key.Delete or Key.Back: DeleteSelected(); break;
            case Key.PageDown when ctrl: MovePage(_state.PageIndex, _state.PageIndex + 1); break;
            case Key.PageUp when ctrl: MovePage(_state.PageIndex, _state.PageIndex - 1); break;
            case Key.PageDown: _state.GoToPage(_state.PageIndex + 1); break;
            case Key.PageUp: _state.GoToPage(_state.PageIndex - 1); break;
            case Key.OemCloseBrackets when _state.Page is Page p && p.Stops.Count > 0:
                _state.Select(Sel.Stop, _state.Selected == Sel.Stop ? Math.Min(p.Stops.Count - 1, _state.SelIndex + 1) : 0); break;
            case Key.OemOpenBrackets when _state.Page is Page p && p.Stops.Count > 0:
                _state.Select(Sel.Stop, _state.Selected == Sel.Stop ? Math.Max(0, _state.SelIndex - 1) : p.Stops.Count - 1); break;
            case Key.Left or Key.Right or Key.Up or Key.Down when _state.Selected != Sel.None:
                double step = shift ? 0.01 : 0.002;
                double dx = e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0, dy = e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0;
                NudgeSelected(dx, dy, e.KeyModifiers.HasFlag(KeyModifiers.Alt));
                break;
            default: return;
        }
        e.Handled = true;
    }

    // ---------------------------------------------------------------- for the screenshot hook

    public void ShowExportNow() => OpenExport();
    public void ShowCheckNow() => OpenCheck();
    public void ShowHelpNow() => _help.IsVisible = true;
}

/// <summary>The real sound output, passed through so the editor can tell whether a device is working.</summary>
sealed class DesktopSoundWrapper : ISoundOutput, IDisposable
{
    public DesktopSoundWrapper(DesktopSound inner) => Inner = inner;
    public DesktopSound Inner { get; }
    public void Tap() => Inner.Tap();
    public void Zoom(bool zoomIn) => Inner.Zoom(zoomIn);
    public void Find() => Inner.Find();
    public void Locked() => Inner.Locked();
    public void Party() => Inner.Party();
    public void Whoosh(double milliseconds) => Inner.Whoosh(milliseconds);
    public void PageTurn() => Inner.PageTurn();
    public void Rain(double level) => Inner.Rain(level);
    public void Thunder(double delaySeconds) => Inner.Thunder(delaySeconds);
    public void PlayClip(string fileName) => Inner.PlayClip(fileName);
    public void StopClip() => Inner.StopClip();
    public bool ClipPlaying => Inner.ClipPlaying;
    public void PlayVoice(string fileName, double fromSeconds, double toSeconds) => Inner.PlayVoice(fileName, fromSeconds, toSeconds);
    public void StopVoice() => Inner.StopVoice();
    public bool VoicePlaying => Inner.VoicePlaying;
    public double VoicePosition => Inner.VoicePosition;
    public void Dispose() => Inner.Dispose();
}

/// <summary>Silence for screenshots: no sound device is opened.</summary>
sealed class DesktopSoundStandIn : ISoundOutput
{
    public void Tap() { }
    public void Zoom(bool zoomIn) { }
    public void Find() { }
    public void Locked() { }
    public void Party() { }
    public void Whoosh(double milliseconds) { }
    public void PageTurn() { }
    public void Rain(double level) { }
    public void Thunder(double delaySeconds) { }
    public void PlayClip(string fileName) { }
    public void StopClip() { }
    public bool ClipPlaying => false;
}
