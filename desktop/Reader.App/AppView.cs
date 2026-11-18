using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Reader.Core;
using Reader.Ui;

namespace Reader.App;

/// <summary>How the app is set up: where the books are, and what kind of device it is on.</summary>
public sealed class AppSetup
{
    /// <summary>The folder that holds library.json and books/. Null when no books could be found.</summary>
    public string? Content { get; set; }
    /// <summary>What to tell the person when <see cref="Content"/> is null.</summary>
    public string NoBooksMessage { get; init; } = "";
    /// <summary>A phone or tablet: touch first, one page at a time when held upright.</summary>
    public bool Touch { get; init; }
    /// <summary>Offer "Open a book folder" in the grown-ups' Books page (the Windows program).</summary>
    public bool CanOpenFolders { get; init; }
    /// <summary>Keep the settings, the children and their progress between runs.</summary>
    public bool SaveSettings { get; init; } = true;
    /// <summary>The device asks for less motion.</summary>
    public bool DeviceReducedMotion { get; init; }
    /// <summary>Screen units per centimetre, for the smallest touch target. Null leaves the desktop's 37.8.</summary>
    public double? UnitsPerCm { get; init; }
    /// <summary>
    /// Where books added from a book file are kept (laid out like a content folder). Null: books can't be
    /// added from files. On the phone this is in the app's own storage, so the books survive updates.
    /// </summary>
    public string? ImportFolder { get; init; }
    /// <summary>The children, their progress, the language and bedtime colours. Loaded by the program (from profiles.json).</summary>
    public AppState? State { get; init; }
    /// <summary>Where <see cref="State"/> is kept. Null: not kept (screenshots).</summary>
    public string? StatePath { get; init; }
    /// <summary>The device's own language ("af", "en-ZA", ...), for "As this device".</summary>
    public string DeviceLanguage { get; init; } = "en";
}

/// <summary>
/// The whole reading app as one view: the first-run welcome, "Who is reading?", the bookshelf, the
/// reader screen over it when a book is open, the grown-ups' gate and the grown-ups' area, and how
/// they hand over to each other. It also keeps each child's place in each book. The Windows program
/// shows it in a window; the phone and tablet app shows it on the screen.
/// </summary>
public sealed class AppView : UserControl, IParentsHost
{
    /// <summary>Settings that change which steps a book has or where the camera rests, so the reader must re-plan.</summary>
    static readonly HashSet<string> NeedRefresh = new() { "padding", "dim", "vignette", "spotlight", "wholePageFirst", "wholePageLast", "bookLook", "*" };

    readonly AppSetup _setup;
    readonly ReaderSettings _settings;
    readonly AppState _state;
    readonly Panel _main = new();
    readonly Panel _layer = new();
    ShelfView _shelf;
    readonly ReaderScreen _screen;
    LoadedBook? _book;
    string? _content;
    bool _opening;
    Thickness _safe;
    readonly List<ShelfEntry> _books = new();

    // The overlays, at most one of each.
    WelcomeView? _welcome;
    ProfilesView? _profiles;
    GateView? _gate;
    ParentsView? _parents;

    // The book being read, for keeping the child's place.
    string? _bookId;
    int _lastStep = -1, _lastPage = -1;
    bool _finishMarked;
    ShelfEntry? _nextBook;

    public AppView(AppSetup setup, ReaderSettings settings, ISoundOutput sound)
    {
        _setup = setup;
        _settings = settings;
        _state = setup.State ?? new AppState();
        Kit.Keyboard = !setup.Touch;
        Strings.Language = Strings.Resolve(_state.Language, setup.DeviceLanguage);
        Palette.Night = _state.Bedtime;
        AppSkin.Apply();
        Background = Palette.ForestBrush;     // the reader's dark backdrop, so nothing flashes white
        FontFamily = Palette.Font;

        _shelf = MakeShelf();
        _screen = new ReaderScreen(settings, sound) { IsVisible = false, AutoOnePage = setup.Touch };
        _screen.Engine.DeviceReducedMotion = setup.DeviceReducedMotion;
        Motion.Reduced = _screen.Engine.Reduced;
        if (setup.UnitsPerCm is double perCm) _screen.Engine.UnitsPerCm = perCm;
        // On a touch screen a tap anywhere goes on; going back is a swipe or the back arrow.
        _screen.View.Gestures.TapAnywhereGoesOn = setup.Touch;
        _screen.CloseRequested += () => CloseBook();
        _screen.GearRequested += () => WithGate(() => ShowParents());
        _screen.NextBookRequested += () => { if (_nextBook is ShelfEntry next) _ = OpenBook(next.Folder); };
        _screen.StateChanged += KeepPlace;

        _main.Children.Add(_shelf);
        _main.Children.Add(_screen);
        Content = new Panel { Children = { _main, _layer } };

        // The child's own age band sets how fast the pictures move and how soon hints come.
        if (_state.CurrentProfile is ChildProfile child && child.Age != _settings.Age) _settings.ApplyAgePreset(child.Age);
    }

    ShelfView MakeShelf()
    {
        var shelf = new ShelfView(forTouch: _setup.Touch) { SafeArea = _safe };
        shelf.BookChosen += entry => _ = OpenBook(entry.Folder);
        shelf.ChildRequested += () => ShowProfiles(canClose: true);
        shelf.GrownUpsRequested += () => WithGate(() => ShowParents());
        shelf.AddBookRequested += () => WithGate(() => ShowParents(ParentsView.Section.Books));
        return shelf;
    }

    public ReaderScreen Screen => _screen;
    public AppState State => _state;
    public ReaderSettings Settings => _settings;

    /// <summary>True while a book is still loading.</summary>
    public bool Busy => _opening;
    public bool ReaderShowing => _screen.IsVisible;

    /// <summary>Raised with true when a book is opened and false when it is closed: the phone keeps its screen awake while reading.</summary>
    public event Action<bool>? ReadingChanged;

    /// <summary>Room kept clear for a notch, rounded corners or the system bars.</summary>
    public Thickness SafeArea
    {
        set
        {
            _safe = value;
            _shelf.SafeArea = value;
            _screen.SafeArea = value;
            if (_welcome is not null) _welcome.SafeArea = value;
            if (_profiles is not null) _profiles.SafeArea = value;
            if (_gate is not null) _gate.SafeArea = value;
            if (_parents is not null) _parents.SafeArea = value;
        }
    }

    void SaveState()
    {
        if (_setup.SaveSettings && _setup.StatePath is string path) AppStateStore.Save(_state, path);
    }

    void SaveSettings()
    {
        if (_setup.SaveSettings) SettingsStore.Save(_settings);
    }

    // ---------------------------------------------------------------- starting

    /// <summary>
    /// What comes first: the welcome on the very first run, "Who is reading?" when there is more than one
    /// child, otherwise the shelf. An install from before there were children (a tester's) gets the
    /// welcome once too, which ends by making the first child.
    /// </summary>
    public void Start()
    {
        if (!_state.Welcomed) { ShowWelcome(); return; }
        if (_state.Profiles.Count == 0) { _state.Add("", 0, _settings.Age); SaveState(); RefreshShelf(); }
        if (_state.Profiles.Count > 1) ShowProfiles(canClose: false);
    }

    // ---------------------------------------------------------------- the shelf

    /// <summary>Show a line of words on the shelf, such as "Getting the books ready". Null clears it.</summary>
    public void Say(string? message, NoteKind kind = NoteKind.Info) => _shelf.Say(message, kind);

    /// <summary>List the books in the content folder.</summary>
    public void LoadShelf(bool coversNow = false)
    {
        _content = _setup.Content;
        ShowContent(_content, coversNow);
        if (_content is null && _books.Count == 0 && _setup.NoBooksMessage.Length > 0) _shelf.Say(_setup.NoBooksMessage, NoteKind.Problem);
    }

    bool _coversNow;

    void ShowContent(string? content, bool coversNow = false)
    {
        _coversNow |= coversNow;
        _books.Clear();
        string? message = null;
        if (content is not null)
        {
            try
            {
                var problems = new List<string>();
                _books.AddRange(BookLibrary.List(content, problems));
                if (problems.Count > 0) message = Strings.T("shelf.someMissing", string.Join("; ", problems));
            }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                message = Strings.T("shelf.cantLoad");
            }
        }
        // Books added from book files come after the ones that came with the app.
        if (_setup.ImportFolder is string import) _books.AddRange(BookPackage.List(import));
        RefreshShelf();
        _shelf.Say(message, message is null ? NoteKind.Info : NoteKind.Problem);
    }

    /// <summary>Show the shelf again for the child reading now: their name, their place in each book.</summary>
    void RefreshShelf()
    {
        ChildProfile? child = _state.CurrentProfile;
        string? carry = _state.CarryOn(_books.Select(b => b.Id));
        var model = new ShelfModel(_books, id => child?.For(id), child, _state.Profiles.Count, _books.FirstOrDefault(b => b.Id == carry));
        _shelf.Show(model, _coversNow);
    }

    /// <summary>A book asked for by name: an id from the library, or a folder.</summary>
    public string? ResolveBook(string book)
    {
        if (_content is not null)
        {
            string inLibrary = Path.Combine(_content, "books", book);
            if (File.Exists(Path.Combine(inLibrary, "book.json"))) return inLibrary;
        }
        if (_books.FirstOrDefault(b => b.Id == book) is ShelfEntry e) return e.Folder;
        string folder = Path.GetFullPath(book);
        if (File.Exists(Path.Combine(folder, "book.json"))) return folder;
        _shelf.Say(Strings.T("shelf.noSuchBook", book), NoteKind.Problem);
        return null;
    }

    public async Task PickFolder()
    {
        if (!_setup.CanOpenFolders || TopLevel.GetTopLevel(this) is not TopLevel top) return;
        try
        {
            var picked = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Strings.T("books.folder"), AllowMultiple = false });
            string? folder = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
            if (folder is null) return;
            CloseOverlays();
            if (File.Exists(Path.Combine(folder, "book.json"))) await OpenBook(folder);
            else if (File.Exists(Path.Combine(folder, "library.json"))) { _content = folder; ShowContent(folder); }   // a whole shelf of books
            else _shelf.Say(Strings.T("shelf.notABookFolder"), NoteKind.Problem);
        }
        catch (Exception)
        {
            _shelf.Say(Strings.T("shelf.cantOpenFolder"), NoteKind.Problem);
        }
    }

    // ---------------------------------------------------------------- books from book files

    /// <summary>Raised after a book file was taken in (or turned down), with what to tell the person.</summary>
    public event Action<BookPackage.Result>? BookImported;

    void BookNote(string text, bool good)
    {
        if (_parents is null) return;
        _parents.BookNote = (text, good);
        if (_parents.Current == ParentsView.Section.Books) _parents.Refresh();
    }

    /// <summary>
    /// Take in a book file: copied to the app's storage first (it may come from a chat or a download that
    /// can only be read once), checked, unpacked and put on the shelf. The shelf shows what happened.
    /// </summary>
    public async Task<BookPackage.Result> ImportBook(Func<Task<Stream>> open, string? name = null)
    {
        if (_setup.ImportFolder is not string root) return BookPackage.Result.Fail(Strings.T("import.cantHere"));
        _shelf.Say(Strings.T("shelf.adding"), NoteKind.Busy);
        BookNote(Strings.T("shelf.adding"), true);
        BookPackage.Result result;
        try
        {
            Stream source = await open();
            result = await Task.Run(() =>
            {
                Directory.CreateDirectory(root);
                string copy = Path.Combine(root, ".download-" + Guid.NewGuid().ToString("N")[..12]);
                try
                {
                    using (source)
                    using (FileStream f = File.Create(copy))
                    {
                        // A book file is never bigger than its unpacked limit, so a copy that goes past it is stopped there.
                        long most = BookPackage.Limits.Default.MaxTotalBytes, total = 0;
                        var buffer = new byte[81920];
                        int n;
                        while ((n = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            total += n;
                            if (total > most) return BookPackage.Result.Fail(Strings.T("import.tooBig"));
                            f.Write(buffer, 0, n);
                        }
                    }
                    return BookPackage.Import(copy, root, null);
                }
                finally { try { File.Delete(copy); } catch (IOException) { } }
            });
        }
        catch (Exception)
        {
            // Whatever the system's file reading threw (the phone's own exceptions included), the parent gets a plain answer.
            result = BookPackage.Result.Fail(Strings.T("import.cantReadHere"));
        }
        AfterImport(result);
        return result;
    }

    /// <summary>Take in a book file that is already on this computer (opened with the program, or dropped on its window).</summary>
    public Task<BookPackage.Result> ImportBookFile(string path) => ImportBook(() => Task.FromResult<Stream>(File.OpenRead(path)), Path.GetFileName(path));

    /// <summary>The same on the spot, for screenshots.</summary>
    public BookPackage.Result ImportBookFileNow(string path)
    {
        BookPackage.Result r = _setup.ImportFolder is string root ? BookPackage.Import(path, root) : BookPackage.Result.Fail(Strings.T("import.cantHere"));
        AfterImport(r);
        return r;
    }

    void AfterImport(BookPackage.Result result)
    {
        if (result.Ok && ReaderShowing) CloseBook();
        ShowContent(_content);
        _shelf.Say(result.Message, result.Ok ? NoteKind.Good : NoteKind.Problem);
        BookNote(result.Message, result.Ok);
        BookImported?.Invoke(result);
    }

    /// <summary>Show the system's file picker and take in the book file chosen.</summary>
    public async Task PickBookFile()
    {
        if (_setup.ImportFolder is null || TopLevel.GetTopLevel(this) is not TopLevel top) return;
        IReadOnlyList<IStorageFile> picked;
        try
        {
            picked = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Strings.T("books.pickTitle"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    // Phones often don't know the .picturebook ending, so any file can be chosen there and is checked once picked.
                    new FilePickerFileType(Strings.T("books.fileType")) { Patterns = new[] { "*" + BookPackage.Extension, "*.zip" }, MimeTypes = new[] { "*/*" } },
                },
            });
        }
        catch (Exception)
        {
            BookNote(Strings.T("shelf.cantListFiles"), false);
            return;
        }
        if (picked.Count == 0) return;
        IStorageFile file = picked[0];
        await ImportBook(() => file.OpenReadAsync(), file.Name);
    }

    // ---------------------------------------------------------------- opening and closing a book

    /// <summary>
    /// Load a book in the background and show it, at the child's place in it if they have one.
    /// <paramref name="then"/> runs once it is showing.
    /// </summary>
    public async Task OpenBook(string folder, Action? then = null)
    {
        if (_opening) return;
        _opening = true;
        // A big book can take a moment: after a short wait the shelf says it is on its way.
        bool done = false;
        DispatcherTimer.RunOnce(() => { if (!done) _shelf.Say(Strings.T("shelf.opening"), NoteKind.Busy); }, TimeSpan.FromMilliseconds(400));
        try
        {
            LoadedBook book = await LoadedBook.LoadAsync(folder);
            done = true;
            _shelf.Say(null);
            ShowBook(book);
            then?.Invoke();
        }
        catch (Exception)
        {
            done = true;
            _shelf.Say(Strings.T("shelf.wontOpen"), NoteKind.Problem);
        }
        finally
        {
            done = true;
            _opening = false;
        }
    }

    /// <summary>Load a book on the spot. For screenshots, where there is nobody to keep waiting.</summary>
    public void OpenBookNow(string folder) => ShowBook(LoadedBook.Load(folder));

    void ShowBook(LoadedBook book)
    {
        CloseOverlays(keepParents: false);
        LoadedBook? old = _book;
        _book = book;
        string full = Path.GetFullPath(book.Folder);
        ShelfEntry? entry = _books.FirstOrDefault(b => Path.GetFullPath(b.Folder) == full);
        _bookId = entry?.Id ?? (!string.IsNullOrEmpty(book.Book.Id) ? book.Book.Id : Path.GetFileName(Path.TrimEndingDirectorySeparator(full)));
        // Where the child was, read before the book opens (opening it is a step of its own).
        BookProgress? place = _state.CurrentProfile?.For(_bookId);
        (int Page, int Stop)? resume = place is { Partway: true } p && p.Page < book.Book.Pages.Count ? (p.Page, p.Stop) : null;
        _lastStep = _lastPage = -1;
        _finishMarked = false;

        _screen.ReaderName = _state.CurrentProfile?.Name ?? "";
        ChooseNext();
        _screen.IsVisible = true;
        _shelf.IsVisible = false;
        _screen.SetBook(book);
        if (resume is (int page, int stop)) _screen.View.Do(e => e.JumpTo(page, stop));
        old?.Dispose();
        ReadingChanged?.Invoke(true);
    }

    /// <summary>"Read next" at The End: the next book along the shelf that this child hasn't finished.</summary>
    void ChooseNext()
    {
        string? id = _bookId is null ? null : _state.ReadNext(_books.Select(b => b.Id).ToList(), _bookId);
        _nextBook = _books.FirstOrDefault(b => b.Id == id);
        Bitmap? cover = null;
        if (_nextBook is ShelfEntry next)
        {
            try { using FileStream f = File.OpenRead(next.CoverPath); cover = Bitmap.DecodeToWidth(f, 120, BitmapInterpolationMode.HighQuality); }
            catch (Exception) { cover = null; }
        }
        _screen.SetNextBook(_nextBook?.Title, cover);
    }

    /// <summary>
    /// Keep the child's place as they read: every step is noted, and written down when the page changes,
    /// the book is closed or the app goes to the background. Reaching The End marks the book finished.
    /// </summary>
    void KeepPlace(ReaderState st)
    {
        if (_bookId is not string id || !ReaderShowing || _screen.Engine.Steps.Count == 0) return;
        if (st.Finished)
        {
            if (_finishMarked) return;
            _finishMarked = true;
            _state.MarkFinished(id, st.PageCount, DateTime.UtcNow);
            SaveState();
            return;
        }
        _finishMarked = false;
        if (st.StepIndex == _lastStep) return;
        _lastStep = st.StepIndex;
        Step step = _screen.Engine.Steps[Math.Clamp(st.StepIndex, 0, _screen.Engine.Steps.Count - 1)];
        _state.KeepPlace(id, st.StepIndex, step.Page, step.Stop, st.PageCount, DateTime.UtcNow);
        if (step.Page != _lastPage)
        {
            _lastPage = step.Page;
            SaveState();
        }
    }

    /// <summary>Page and stop as a person counts them: 1 is the first, -1 the whole page, -2 exploring, null however the page opens.</summary>
    public void JumpTo(int pageNumber, int? stopNumber)
    {
        ReaderEngine e = _screen.Engine;
        if (e.Book is null || e.Steps.Count == 0) return;
        int page = Math.Clamp(pageNumber - 1, 0, e.Book.Pages.Count - 1);
        int stop = stopNumber switch
        {
            > 0 => stopNumber.Value - 1,
            < 0 => stopNumber.Value,
            _ => e.Steps.First(s => s.Page == page && s.Stop != Step.Cover).Stop,      // a page asked for by number is past the cover
        };
        _screen.View.Do(engine => engine.JumpTo(page, stop));
    }

    /// <summary>Back to the shelf. Returns false if the shelf was already showing.</summary>
    public bool CloseBook()
    {
        if (!ReaderShowing) return false;
        SaveState();
        _screen.View.ClearBook();
        _screen.IsVisible = false;
        _shelf.IsVisible = true;
        _book?.Dispose();
        _book = null;
        _bookId = null;
        RefreshShelf();
        ReadingChanged?.Invoke(false);
        return true;
    }

    /// <summary>
    /// The phone's back button and Esc. It closes the gate or the grown-ups' area (a page at a time),
    /// goes back a page of the welcome, closes "Who is reading?", or goes from the book back to the shelf.
    /// On the shelf it returns false, so the system can leave the app: the app is never left straight
    /// from the middle of a book.
    /// </summary>
    public bool GoBack()
    {
        if (_gate is not null) { CloseGate(); return true; }
        if (_parents is not null) return _parents.GoBack();
        if (_welcome is not null) return _welcome.GoBack();
        if (_profiles is not null) { if (_profilesCanClose) CloseProfiles(); return true; }
        return CloseBook();
    }

    void FocusReader()
    {
        if (ReaderShowing && _layer.Children.Count == 0) _screen.View.Focus();
    }

    // ---------------------------------------------------------------- the welcome

    /// <summary>The first-run welcome (or, from the grown-ups' area, the same pages again).</summary>
    public void ShowWelcome(int page = 0)
    {
        CloseOverlays(keepParents: false);
        bool makeChild = !_state.Welcomed || _state.Profiles.Count == 0;
        var covers = new List<Bitmap?>();
        foreach (ShelfEntry b in _books.Take(3))
        {
            try { using FileStream f = File.OpenRead(b.CoverPath); covers.Add(Bitmap.DecodeToWidth(f, 360, BitmapInterpolationMode.HighQuality)); }
            catch (Exception) { covers.Add(null); }
        }
        _welcome = new WelcomeView(covers, makeChild, _settings.Age, page) { SafeArea = _safe };
        _welcome.LanguageChosen += code =>
        {
            int at = _welcome?.Page ?? 0;
            SetLanguage(code);
            ShowWelcome(at);
        };
        _welcome.Finished += child =>
        {
            _state.Welcomed = true;
            if (child is var (name, avatar, age))
            {
                _state.Add(name, avatar, age);
                _settings.ApplyAgePreset(age);
                SaveSettings();
                SettingChanged("age");
            }
            if (_state.Profiles.Count == 0) _state.Add("", 0, _settings.Age);
            SaveState();
            Remove(ref _welcome);
            RefreshShelf();
            _shelf.FocusFirst();
        };
        _layer.Children.Add(_welcome);
    }

    // ---------------------------------------------------------------- who is reading

    bool _profilesCanClose;

    public void ShowProfiles(bool canClose)
    {
        Remove(ref _profiles);
        _profilesCanClose = canClose;
        _profiles = new ProfilesView(_state, canClose) { SafeArea = _safe };
        _profiles.Chosen += child =>
        {
            _state.Choose(child.Id);
            if (child.Age != _settings.Age) { _settings.ApplyAgePreset(child.Age); SaveSettings(); SettingChanged("age"); }
            SaveState();
            CloseProfiles();
        };
        _profiles.AddRequested += () => WithGate(() => ShowParents(ParentsView.Section.NewChild));
        _profiles.GrownUpsRequested += () => WithGate(() => ShowParents());
        _profiles.Closed += CloseProfiles;
        _layer.Children.Insert(0, _profiles);
    }

    void CloseProfiles()
    {
        Remove(ref _profiles);
        RefreshShelf();
    }

    // ---------------------------------------------------------------- the gate and the grown-ups' area

    /// <summary>Show the grown-ups' gate, then do <paramref name="then"/> once it is passed. Inside the grown-ups' area it isn't asked again.</summary>
    public void WithGate(Action then)
    {
        if (_parents is not null) { then(); return; }
        CloseGate();
        _gate = new GateView { SafeArea = _safe };
        _gate.Opened += () => { CloseGate(); then(); };
        _gate.Cancelled += CloseGate;
        _layer.Children.Add(_gate);
        _gate.Focus();
    }

    /// <summary>The gate on screen, for screenshots.</summary>
    public GateView? Gate => _gate;

    void CloseGate()
    {
        Remove(ref _gate);
        FocusReader();
    }

    /// <summary>The grown-ups' area, on one of its pages.</summary>
    public void ShowParents(ParentsView.Section section = ParentsView.Section.Home, string? child = null)
    {
        Remove(ref _parents);
        _parents = new ParentsView(this, section, child) { SafeArea = _safe };
        _parents.Closed += CloseParents;
        // Below the gate, above everything else.
        int at = _gate is null ? _layer.Children.Count : _layer.Children.IndexOf(_gate);
        _layer.Children.Insert(at, _parents);
    }

    public ParentsView? Parents => _parents;

    void CloseParents()
    {
        Remove(ref _parents);
        SaveSettings();
        SaveState();
        RefreshShelf();
        FocusReader();
    }

    /// <summary>The settings panel of old: the grown-ups' area (the keyboard's S, and screenshots).</summary>
    public void ToggleSettings(bool? show = null)
    {
        bool on = show ?? _parents is null;
        if (on && _parents is null) ShowParents();
        else if (!on && _parents is not null) CloseParents();
    }

    void Remove<T>(ref T? view) where T : Control
    {
        if (view is not null) _layer.Children.Remove(view);
        view = null;
    }

    /// <summary>Just the shelf, with nothing over it (screenshots).</summary>
    public void ShowShelf()
    {
        CloseOverlays();
        if (_state.Profiles.Count == 0) _state.Add("", 0, _settings.Age);
        _state.Welcomed = true;
        RefreshShelf();
    }

    void CloseOverlays(bool keepParents = false)
    {
        Remove(ref _gate);
        Remove(ref _welcome);
        Remove(ref _profiles);
        if (!keepParents) Remove(ref _parents);
    }

    // ---- what the grown-ups' area asks of the app

    bool IParentsHost.Touch => _setup.Touch;
    bool IParentsHost.CanImport => _setup.ImportFolder is not null;
    bool IParentsHost.CanOpenFolders => _setup.CanOpenFolders;
    IReadOnlyList<ShelfEntry> IParentsHost.Books => _books;
    string IParentsHost.DeviceLanguage => _setup.DeviceLanguage;

    bool IParentsHost.IsAdded(ShelfEntry book) =>
        _setup.ImportFolder is string root && Path.GetFullPath(book.Folder).StartsWith(Path.GetFullPath(root), StringComparison.Ordinal);

    void IParentsHost.SettingChanged(string key) => SettingChanged(key);

    void IParentsHost.ChildrenChanged()
    {
        if (_state.Profiles.Count == 0) _state.Add("", 0, _settings.Age);
        if (_state.CurrentProfile is ChildProfile child && child.Age != _settings.Age) { _settings.ApplyAgePreset(child.Age); SettingChanged("age"); }
        SaveState();
        RefreshShelf();
        if (_profiles is not null) ShowProfiles(_profilesCanClose);
    }

    void IParentsHost.SetLanguage(string choice) => SetLanguage(choice);
    void IParentsHost.SetBedtime(bool on) => SetBedtime(on);
    void IParentsHost.PickBookFile() => _ = PickBookFile();
    void IParentsHost.OpenFolder() => _ = PickFolder();
    void IParentsHost.ShowWelcome() => ShowWelcome();

    bool IParentsHost.RemoveBook(ShelfEntry book)
    {
        if (_setup.ImportFolder is not string root) return false;
        if (ReaderShowing && _book is not null && Path.GetFullPath(_book.Folder) == Path.GetFullPath(book.Folder)) CloseBook();
        bool gone;
        try { gone = BookPackage.Remove(root, Path.GetFileName(book.Folder)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { gone = false; }
        ShowContent(_content);
        return gone;
    }

    Control IParentsHost.MakeTuning()
    {
        var panel = new SettingsPanel(_settings, parentsOnly: false, embedded: true);
        panel.Changed += SettingChanged;
        panel.ReplayRequested += () => _screen.View.Do(e => e.Replay());
        panel.CopyRequested += json => CopySettings(panel, json);
        return panel;
    }

    /// <summary>The app's language: "auto" (as the device) or a language's code. Everything is built again in it at once.</summary>
    public void SetLanguage(string choice)
    {
        _state.Language = choice;
        Strings.Language = Strings.Resolve(choice, _setup.DeviceLanguage);
        SaveState();
        Rebuild();
    }

    /// <summary>Bedtime colours on or off: the same screens in the night palette.</summary>
    public void SetBedtime(bool on)
    {
        _state.Bedtime = on;
        Palette.Night = on;
        AppSkin.Apply();
        SaveState();
        Rebuild();
    }

    /// <summary>Build every screen again in the language and colours now in use, each where it was.</summary>
    void Rebuild()
    {
        Background = Palette.ForestBrush;
        bool shelfShowing = _shelf.IsVisible;
        _main.Children.Remove(_shelf);
        _shelf = MakeShelf();
        _shelf.IsVisible = shelfShowing;
        _main.Children.Insert(0, _shelf);
        RefreshShelf();
        _screen.Restyle();
        if (_profiles is not null) ShowProfiles(_profilesCanClose);
        if (_parents is not null)
        {
            var (section, child, note) = (_parents.Current, _parents.ChildId, _parents.BookNote);
            ShowParents(section, child);
            _parents!.BookNote = note;
        }
    }

    // ---------------------------------------------------------------- the app in the background

    /// <summary>The app has gone to the background or the screen is locked: hold the place, stop all sound and motion.</summary>
    public void Pause()
    {
        _screen.View.Pause();
        SaveSettings();
        SaveState();
    }

    /// <summary>Back in front: carry on from where it was, as if no time had passed.</summary>
    public void Resume() => _screen.View.Resume();

    /// <summary>The system is short of memory: let go of every picture that can be loaded again.</summary>
    public void ReleaseMemory()
    {
        _book?.ReleaseMemory();
        _screen.View.ReleaseMemory();
    }

    // ---------------------------------------------------------------- settings

    void SettingChanged(string key)
    {
        SaveSettings();
        Motion.Reduced = _screen.Engine.Reduced;
        if (NeedRefresh.Contains(key)) _screen.View.Do(e => e.Refresh());
        if (key.StartsWith("drift", StringComparison.Ordinal)) _screen.View.Do(e => e.RestartRest());
        _screen.Refresh();
        _screen.View.InvalidateVisual();
    }

    async void CopySettings(SettingsPanel panel, string json)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not IClipboard clipboard) throw new InvalidOperationException("no clipboard");
            await clipboard.SetTextAsync(json);
            panel.Note("Copied");
        }
        catch (Exception)
        {
            panel.Note("Couldn't reach the clipboard");
        }
        DispatcherTimer.RunOnce(() => panel.Note(""), TimeSpan.FromSeconds(2.5));
    }

    // ---------------------------------------------------------------- keys

    /// <summary>Keyboard shortcuts, wherever the focus is. Returns true when the key was used.</summary>
    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        bool plain = (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0;
        if (_gate is not null) return _gate.HandleKey(key) || key == Key.Escape;
        if (key == Key.Escape) return GoBack();
        if (_layer.Children.Count > 0) return false;     // the welcome, the children or the grown-ups' area: their own keys
        if (key == Key.S && plain && !_setup.Touch) { WithGate(() => ShowParents()); return true; }
        if (key == Key.F && plain) { _screen.View.ShowFrameRate = !_screen.View.ShowFrameRate; return true; }
        if (key == Key.O && modifiers.HasFlag(KeyModifiers.Control) && !ReaderShowing && _setup.CanOpenFolders) { WithGate(() => _ = PickFolder()); return true; }
        // Wherever the keyboard focus happens to be, the reading keys still go to the reader.
        return ReaderShowing && _screen.View.HandleKey(key, modifiers);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Source is not TextBox && HandleKey(e.Key, e.KeyModifiers)) e.Handled = true;
    }

    /// <summary>Put everything away: the app is closing.</summary>
    public void Shutdown()
    {
        SaveState();
        _screen.View.ClearBook();
        _book?.Dispose();
        _book = null;
    }

    /// <summary>The shelf, for screenshots.</summary>
    public ShelfView Shelf => _shelf;
}
