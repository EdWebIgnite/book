using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
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
sealed partial class MainWindow : Window
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
    readonly Border _toast;
    readonly TextBlock _toastText;
    readonly Border _dropHint;
    readonly Panel _workspace;
    readonly Control _welcome;
    readonly Border _canvasBar;
    readonly Control _help;
    readonly Control _empty;
    DispatcherTimer? _toastTimer;
    (string Kind, string Json)? _clipboard;
    bool _closingConfirmed;
    bool _updatingTitle;

    /// <summary>What the editor remembers about itself between runs (never about a book).</summary>
    public EditorSettings Settings { get; }

    /// <summary>The smallest the window can be: it fits a 1366 x 768 laptop at 125% scaling, taskbar and all.</summary>
    public const double SmallestWidth = 960, SmallestHeight = 560;

    public MainWindow(bool screenshotMode = false)
    {
        ScreenshotMode = screenshotMode;
        Title = EditorBrand.Name;
        Width = 1440;
        Height = 900;
        MinWidth = SmallestWidth;
        MinHeight = SmallestHeight;
        Background = Ui.Bg;
        FontFamily = Ui.Font;
        Settings = EditorSettings.Load();
        Ui.Surround = Settings.Surround;
        try { Icon = Reader.Ui.AppIcon.ForWindow(editor: true); } catch (Exception) { }
        _recovery = new Recovery();
        _recent = new RecentBooks();
        _images.Synchronous = screenshotMode;
        SoundShapes.Synchronous = screenshotMode;

        Sound = screenshotMode ? new DesktopSoundStandIn() : new DesktopSoundWrapper(new DesktopSound());
        if (screenshotMode) MicrophoneProblem = "";
        else if (!Microphone.Available(out string why)) MicrophoneProblem = why;

        Timing = new AutoTiming(this, _state);
        _canvas = new PageCanvas(_state, _images);
        _canvas.Overlays.Crease = Settings.Crease;
        _canvas.Overlays.Frames.Clear();
        foreach (string f in Settings.Frames) if (Editor.Core.Device.All.Any(d => d.Id == f)) _canvas.Overlays.Frames.Add(f);
        _canvas.ZoomChanged += RefreshStatus;
        _pages = new PageList(_state, _images, this) { Width = 196 };
        _inspector = new Inspector(_state, this) { Width = 356 };
        _timeline = new Timeline(_state, _images, this);
        _preview = new PreviewPanel(_state, this) { IsVisible = false };
        _check = new CheckPanel(_state, this) { IsVisible = false, Width = 440, HorizontalAlignment = HorizontalAlignment.Right };
        _export = new ExportPanel(_state, this) { IsVisible = false };

        // ---- the top bar: the name, the menus, the tools, the book's title, the output
        _title = new TextBox
        {
            MinWidth = 120, MaxWidth = 420, FontSize = 14, FontWeight = FontWeight.Bold, PlaceholderText = "Book title", VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(12, 0),
        };
        ToolTip.SetTip(_title, "The book's title, as it shows on the shelf");
        _title.TextChanged += (_, _) =>
        {
            if (_updatingTitle || !_state.HasBook) return;
            string t = _title.Text ?? "";
            if (t == _state.Book.Title) return;
            _state.Change("Title", () => _state.Book.Title = t, mergeKey: "title", source: _title);
        };
        var top = TopBar();

        // ---- above the page: what dragging makes, and what is shown over the picture
        _canvasBar = new Border { Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 0), Height = 44 };
        _empty = EmptyBook();
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
        _about = AboutSheet();
        _about.IsVisible = false;

        _toastText = new TextBlock { Foreground = Ui.Brush("#fbf5e6"), FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxWidth = 640, VerticalAlignment = VerticalAlignment.Center };
        _toast = new Border
        {
            Background = Ui.AccentDeep, CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 10), IsVisible = false, IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 24), Child = _toastText,
            BoxShadow = Ui.Lifted,
        };
        _dropHint = new Border
        {
            IsVisible = false, IsHitTestVisible = false, Margin = new Thickness(16), BorderBrush = Ui.Accent, BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromArgb(215, 233, 240, 228)),
            Child = new TextBlock { Text = "Drop pictures to add pages, a sound to use it as the selected stop's recording, a book folder or a .picturebook file to open it", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Ui.AccentDeep, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 640, TextAlignment = TextAlignment.Center },
        };

        _bookFile = BookFileSheet();
        _bookFile.IsVisible = false;
        Tour = new Walkthrough(this);
        var layers = new Panel { Children = { _workspace, _welcome, _preview, _export, _help, _about, _bookFile, Timing.Panel, Tour.Layer, _dialogs, _dropHint, _toast } };
        var status = StatusBar();
        var rootDock = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(status, Dock.Bottom);
        rootDock.Children.Add(top);
        rootDock.Children.Add(status);
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
        if (!screenshotMode) PlaceWindow();
        RefreshChrome();
        RefreshCanvasBar();
    }


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
    public AutoTiming Timing { get; }
    public Dialogs Dialogs => _dialogs;
    public Walkthrough Tour { get; }
    public PageList Pages => _pages;
    public ExportPanel ExportScreen => _export;
    public bool CheckShowing => _check.IsVisible;
    public bool ExportShowing => _export.IsVisible;
    public bool PreviewShowing => _preview.IsVisible;

    // ---------------------------------------------------------------- keeping the screens up to date

    void OnChanged(Why why, object? source)
    {
        _canvas.InvalidateVisual();
        if (why is Why.Book or Why.Structure or Why.Selection) RefreshCanvasBar();
        RefreshChrome();
        if (why == Why.Book && source != _title) SetTitleBox();
        if (why == Why.Book) FillRecentMenu();
    }

    void SetTitleBox()
    {
        _updatingTitle = true;
        _title.Text = _state.HasBook ? _state.Book.Title : "";
        _updatingTitle = false;
    }

    public void FocusForKeys() => _canvas.Focus();

    // ---------------------------------------------------------------- opening, new, saving

    /// <summary>True when it's all right to put the current book away: saved, or the author said so.</summary>
    async Task<bool> OkToLeave()
    {
        if (!_state.HasBook || !_state.Dirty) return true;
        int a = await _dialogs.Ask("Save your changes?", $"\"{(_state.Book.Title.Length > 0 ? _state.Book.Title : "This book")}\" has changes that aren't saved yet. If you don't save, they are lost.", new[] { "Save", "Don't save", "Cancel" }, 0, 2);
        if (a == 2) return false;
        if (a == 0) Save();
        else _recovery.Clear(_state.Project!.Folder);
        return true;
    }

    public async Task OpenFolderDialog()
    {
        if (!await OkToLeave()) return;
        string? folder = await PickFolder("Open a book folder");
        if (folder is null) return;
        // A book file chosen by mistake in a folder picker can't happen, but a folder with no book in it and no pictures can.
        await OpenBook(folder, askFirst: false);
    }

    public async Task OpenBook(string folder, bool askFirst = true)
    {
        if (askFirst && !await OkToLeave()) return;
        BookProject project;
        try { project = BookProject.Open(folder); }
        catch (Exception e)
        {
            Problem($"\"{Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}\" couldn't be opened as a book. {e.Message}");
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
        var where = Ui.Label(parent, 12.5, Ui.Text, wrap: false);
        where.VerticalAlignment = VerticalAlignment.Center;
        var problem = Ui.Label("", 12.5, Ui.Bad, FontWeight.Bold);
        problem.IsVisible = false;
        var create = Ui.Button("Make the book", () => { }, "Enter", primary: true);
        var change = Ui.Button("Change...", async () => { if (await PickFolder("Where should the book's folder go?") is string f) { parent = f; where.Text = f; } }, small: true);
        var whereRow = new DockPanel { Children = { new Border { [DockPanel.DockProperty] = Dock.Right, Margin = new Thickness(8, 0, 0, 0), Child = change },
            new Border { Background = Ui.Raised, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6), Child = where } } };
        var form = new StackPanel
        {
            Children =
            {
                Ui.Title("Start a new book"),
                Ui.FieldName("What is the book called?"), title, problem,
                Ui.FieldName("Its folder goes in"), whereRow,
                Ui.Help("A folder named after the title is made there. Everything the book uses (pictures, sounds, recordings) is kept in it."),
                Dialogs.Buttons(create, Ui.Button("Cancel", () => _dialogs.Close(), "Esc")),
            },
        };
        title.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; create.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); } };
        title.TextChanged += (_, _) => problem.IsVisible = false;
        create.Click += (_, _) =>
        {
            string name = (title.Text ?? "").Trim();
            if (name.Length == 0) { problem.Text = "Type the book's title first."; problem.IsVisible = true; title.Focus(); return; }
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
            catch (Exception e) { problem.Text = "The book's folder couldn't be made there: " + e.Message + " Try another place with Change."; problem.IsVisible = true; }
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
            RefreshStatus();
        }
        catch (Exception e)
        {
            // The recovery copy still holds the changes, so nothing is lost.
            _recovery.Write(_state.Project!);
            Problem("The book couldn't be saved: " + e.Message + " Nothing is lost: your changes are kept in a safety copy, and the book offers them back when it is opened again.", 12);
        }
    }

    void Autosave()
    {
        if (_state.HasBook && _state.Dirty) _recovery.Write(_state.Project!);
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (!ScreenshotMode) RememberWindow();
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
            var r = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false, SuggestedStartLocation = await StartFolder() });
            string? path = r.Count > 0 ? r[0].TryGetLocalPath() : null;
            if (path is not null) Remember(path);
            return path;
        }
        catch (Exception e) { Problem("The folder picker couldn't open: " + e.Message); return null; }
    }

    public async Task<string[]> PickFiles(string title, string[] patterns, string kind, bool many)
    {
        try
        {
            var r = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title, AllowMultiple = many, SuggestedStartLocation = await StartFolder(),
                FileTypeFilter = new[] { new FilePickerFileType(kind) { Patterns = patterns } },
            });
            string[] paths = r.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
            if (paths.Length > 0) Remember(paths[0]);
            return paths;
        }
        catch (Exception e) { Problem("The file picker couldn't open: " + e.Message); return Array.Empty<string>(); }
    }

    /// <summary>Where a picker starts: the folder last picked from, or the book's own folder.</summary>
    async Task<IStorageFolder?> StartFolder()
    {
        string? at = Settings.LastFolder is string f && Directory.Exists(f) ? f : _state.Project?.Folder;
        if (at is null) return null;
        try { return await StorageProvider.TryGetFolderFromPathAsync(at); }
        catch (Exception) { return null; }
    }

    void Remember(string path)
    {
        Settings.Picked(path);
        if (!ScreenshotMode) Settings.Save();
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
    Page JoinPage(string left, string right, string stem) => _state.Project!.JoinPage(left, right, stem);

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
            case Sel.Words when p.Text is not null: _state.Change("Take the words off the page", () => { p.Text = null; _state.SetSelectionQuietly(Sel.None, -1); }, structure: true); break;
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
            case Sel.Words when p.Text is PageText t: _state.Change("Nudge", () => t.Box = Move(t.Box), mergeKey: "nudge"); break;
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
        if (paths.FirstOrDefault(f => f.EndsWith(BookPackage.Extension, StringComparison.OrdinalIgnoreCase)) is string package) { _ = OpenBookFile(package); return; }
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
            Say("That sound is now this stop's recording. Press \"Time words automatically\" (or Tap along) to time the words.");
        }
        else if (sounds.Count > 0 && _state.Hotspot is Hotspot h)
        {
            string name = _state.Project!.Import(sounds[0], "voice", $"p{_state.PageIndex + 1}-find{_state.SelIndex + 1}{Path.GetExtension(sounds[0])}");
            _state.Change("Add a recording", () => h.Voice = new Voice { File = name }, structure: true);
            Say("That sound now reads this thing's words when it is found. Press \"Time words automatically\" to time them.");
        }
        else if (sounds.Count > 0) Say("Select a stop (or a thing to find) first, then drop its recording on the window.");
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

    // ---------------------------------------------------------------- the book file, as text

    Control _bookFile = null!;
    readonly TextBox _bookFileText = new()
    {
        IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Cascadia Mono,Consolas,DejaVu Sans Mono,monospace"),
        FontSize = 12.5, Background = Ui.Field, Foreground = Ui.Text, MinHeight = 200,
    };
    readonly TextBlock _bookFileNote = Ui.Help("");

    Control BookFileSheet()
    {
        var copy = Ui.Button("Copy all", CopyBookFile, "Copy the text, to paste into an email or a message", primary: true, small: true);
        var head = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Ui.Help("This is the book as the editor writes it to book.json in the book's folder. It's here to help sort out problems, for example to send to someone who is helping you. It can't be changed here."),
                _bookFileNote,
                Ui.Row(copy),
            },
        };
        var body = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        body.Children.Add(head);
        body.Children.Add(new ScrollViewer { Content = _bookFileText, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
        return Sheet("The book file", body, () => _bookFile.IsVisible = false, 820, stretch: true);
    }

    public void ShowBookFile()
    {
        if (!_state.HasBook) { Say("Open a book first."); return; }
        _help.IsVisible = false;
        string json = _state.Project!.ToJson();
        _bookFileText.Text = json;
        int lines = json.Count(c => c == '\n') + 1;
        _bookFileNote.Text = (_state.Dirty ? "Shown with the changes that aren't saved yet: Save writes exactly this." : "This is what is saved now.") + $"  {lines} lines, {Exporter.Size(System.Text.Encoding.UTF8.GetByteCount(json))}.  Folder: {_state.Project.Folder}";
        _bookFile.IsVisible = true;
    }

    async void CopyBookFile()
    {
        try
        {
            if (Clipboard is not Avalonia.Input.Platform.IClipboard clipboard) throw new InvalidOperationException();
            await clipboard.SetTextAsync(_bookFileText.Text ?? "");
            Say("Copied the book file. Paste it wherever it's wanted.", 3);
        }
        catch (Exception) { Say("The text couldn't be copied. Select it with the mouse and press Ctrl+C instead."); }
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
        if (_about.IsVisible) { if (e.Key == Key.Escape) { _about.IsVisible = false; e.Handled = true; } return; }
        if (_bookFile.IsVisible) { if (e.Key == Key.Escape) { _bookFile.IsVisible = false; e.Handled = true; } return; }
        if (_export.IsVisible) { if (e.Key == Key.Escape) { CloseExport(); e.Handled = true; } return; }
        if (e.Key == Key.F1) { ShowHelp(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.N) { _ = NewBook(); e.Handled = true; return; }
        if (ctrl && shift && e.Key == Key.O) { _ = OpenBookFileDialog(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.O) { _ = OpenFolderDialog(); e.Handled = true; return; }
        if (!_state.HasBook) return;
        if (ctrl && e.Key == Key.W) { _ = CloseBook(); e.Handled = true; return; }
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
            case Key.OemPlus or Key.Add when ctrl: _canvas.ZoomBy(1.25); break;
            case Key.OemMinus or Key.Subtract when ctrl: _canvas.ZoomBy(1 / 1.25); break;
            case Key.D0 or Key.NumPad0 when ctrl: _canvas.Fit(); break;
            case Key.D1 or Key.NumPad1 when ctrl: _canvas.ActualSize(); break;
            case Key.Home: _state.GoToPage(0); break;
            case Key.End: _state.GoToPage(_state.Book.Pages.Count - 1); break;
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
    public void ShowHelpNow() { FillTourButtons(); _help.IsVisible = true; }
    public void ShowAboutNow() => ShowAbout();
    public void ShowBookFileNow() => ShowBookFile();
    public Button? PlayButton { get; private set; }
    public Button? CheckButton { get; private set; }
    public Button? ExportButton { get; private set; }
    public Button? HelpButton { get; private set; }
    public Button? NewButton { get; private set; }
    public Control Welcome_ => _welcome;
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
