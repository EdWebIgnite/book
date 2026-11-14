using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
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
    public string NoBooksMessage { get; init; } = "Couldn't find any books.";
    /// <summary>A phone or tablet: touch first, one page at a time when held upright, the grown-ups' settings only.</summary>
    public bool Touch { get; init; }
    /// <summary>Show the "Open a book folder" button (the Windows program).</summary>
    public bool CanOpenFolders { get; init; }
    /// <summary>Keep the settings between runs.</summary>
    public bool SaveSettings { get; init; } = true;
    /// <summary>The device asks for less motion.</summary>
    public bool DeviceReducedMotion { get; init; }
    /// <summary>Screen units per centimetre, for the smallest touch target. Null leaves the desktop's 37.8.</summary>
    public double? UnitsPerCm { get; init; }
}

/// <summary>
/// The whole reading app as one view: the bookshelf, the reader screen over it when a book is open,
/// and the settings panel down the right-hand side. The Windows program shows it in a window; the
/// phone and tablet app shows it on the screen. It does what the script at the bottom of web/index.html does.
/// </summary>
public sealed class AppView : UserControl
{
    /// <summary>Settings that change which steps a book has or where the camera rests, so the reader must re-plan.</summary>
    static readonly HashSet<string> NeedRefresh = new() { "padding", "dim", "vignette", "spotlight", "wholePageFirst", "wholePageLast", "bookLook", "*" };

    readonly AppSetup _setup;
    readonly ReaderSettings _settings;
    readonly ShelfView _shelf;
    readonly ReaderScreen _screen;
    readonly SettingsPanel _panel;
    LoadedBook? _book;
    string? _content;
    bool _opening;

    public AppView(AppSetup setup, ReaderSettings settings, ISoundOutput sound)
    {
        _setup = setup;
        _settings = settings;
        Background = new SolidColorBrush(Color.Parse("#1c1b22"));     // the reader's dark backdrop, so nothing flashes white

        _shelf = new ShelfView(forTouch: setup.Touch);
        _screen = new ReaderScreen(settings, sound) { IsVisible = false, AutoOnePage = setup.Touch };
        _screen.Engine.DeviceReducedMotion = setup.DeviceReducedMotion;
        if (setup.UnitsPerCm is double perCm) _screen.Engine.UnitsPerCm = perCm;
        // On a touch screen a tap anywhere goes on; going back is a swipe or the back arrow.
        _screen.View.Gestures.TapAnywhereGoesOn = setup.Touch;
        _screen.CloseRequested += () => CloseBook();
        _screen.GearRequested += () => ToggleSettings();

        _panel = new SettingsPanel(settings, parentsOnly: setup.Touch) { IsVisible = false };
        _panel.Changed += SettingChanged;
        _panel.CloseRequested += () => ToggleSettings(false);
        _panel.ReplayRequested += () => _screen.View.Do(e => e.Replay());
        _panel.CopyRequested += CopySettings;
        _panel.Done += FocusReader;

        _shelf.BookChosen += entry => _ = OpenBook(entry.Folder);
        _shelf.OpenFolderRequested += () => _ = PickFolder();

        Content = new Panel { Children = { _shelf, _screen, _panel } };
    }

    public ReaderScreen Screen => _screen;

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
            _shelf.SafeArea = value;
            _screen.SafeArea = value;
            _panel.SafeArea = value;
        }
    }

    // ---------------------------------------------------------------- the shelf

    /// <summary>Show a line of words on the shelf, such as "Getting the books ready". Null clears it.</summary>
    public void Say(string? message) => _shelf.Say(message);

    /// <summary>List the books in the content folder.</summary>
    public void LoadShelf(bool coversNow = false)
    {
        _content = _setup.Content;
        if (_content is null)
        {
            _shelf.Show(Array.Empty<ShelfEntry>());
            _shelf.Say(_setup.NoBooksMessage);
            return;
        }
        ShowContent(_content, coversNow);
    }

    void ShowContent(string content, bool coversNow = false)
    {
        try
        {
            var problems = new List<string>();
            List<ShelfEntry> books = BookLibrary.List(content, problems);
            _shelf.Show(books, coversNow);
            _shelf.Say(problems.Count > 0 ? "Couldn't load: " + string.Join("; ", problems)
                     : books.Count == 0 ? "There are no books in " + content + " yet." : null);
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            _shelf.Show(Array.Empty<ShelfEntry>());
            _shelf.Say("Couldn't load books: " + e.Message);
        }
    }

    /// <summary>A book asked for by name: an id from the library, or a folder.</summary>
    public string? ResolveBook(string book)
    {
        if (_content is not null)
        {
            string inLibrary = Path.Combine(_content, "books", book);
            if (File.Exists(Path.Combine(inLibrary, "book.json"))) return inLibrary;
        }
        string folder = Path.GetFullPath(book);
        if (File.Exists(Path.Combine(folder, "book.json"))) return folder;
        _shelf.Say($"Couldn't find the book \"{book}\".");
        return null;
    }

    public async Task PickFolder()
    {
        if (!_setup.CanOpenFolders || TopLevel.GetTopLevel(this) is not TopLevel top) return;
        try
        {
            var picked = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Open a book folder", AllowMultiple = false });
            string? folder = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
            if (folder is null) return;
            if (File.Exists(Path.Combine(folder, "book.json"))) await OpenBook(folder);
            else if (File.Exists(Path.Combine(folder, "library.json"))) { _content = folder; ShowContent(folder); }   // a whole shelf of books
            else _shelf.Say("That folder has no book.json in it, so it isn't a book.");
        }
        catch (Exception e)
        {
            _shelf.Say("Couldn't open the folder: " + e.Message);
        }
    }

    // ---------------------------------------------------------------- opening and closing a book

    /// <summary>Load a book in the background and show it. <paramref name="then"/> runs once it is showing.</summary>
    public async Task OpenBook(string folder, Action? then = null)
    {
        if (_opening) return;
        _opening = true;
        try
        {
            _shelf.Say(null);
            LoadedBook book = await LoadedBook.LoadAsync(folder);
            ShowBook(book);
            then?.Invoke();
        }
        catch (Exception e)
        {
            _shelf.Say("Couldn't open that book. " + e.Message);
        }
        finally
        {
            _opening = false;
        }
    }

    /// <summary>Load a book on the spot. For screenshots, where there is nobody to keep waiting.</summary>
    public void OpenBookNow(string folder) => ShowBook(LoadedBook.Load(folder));

    void ShowBook(LoadedBook book)
    {
        LoadedBook? old = _book;
        _book = book;
        _screen.IsVisible = true;
        _shelf.IsVisible = false;
        _screen.SetBook(book);
        old?.Dispose();
        ReadingChanged?.Invoke(true);
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
        _screen.View.ClearBook();
        _screen.IsVisible = false;
        _shelf.IsVisible = true;
        _book?.Dispose();
        _book = null;
        ReadingChanged?.Invoke(false);
        return true;
    }

    /// <summary>
    /// The phone's back button. It closes the settings if they are open, otherwise it goes from the book
    /// back to the shelf. On the shelf it returns false, so the system can leave the app: the app is
    /// never left straight from the middle of a book.
    /// </summary>
    public bool GoBack()
    {
        if (_panel.IsVisible) { ToggleSettings(false); return true; }
        return CloseBook();
    }

    void FocusReader()
    {
        if (ReaderShowing) _screen.View.Focus();
    }

    // ---------------------------------------------------------------- the app in the background

    /// <summary>The app has gone to the background or the screen is locked: hold the place, stop all sound and motion.</summary>
    public void Pause()
    {
        _screen.View.Pause();
        if (_setup.SaveSettings) SettingsStore.Save(_settings);
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

    public void ToggleSettings(bool? show = null)
    {
        bool on = show ?? !_panel.IsVisible;
        if (on) _panel.Build();
        _panel.IsVisible = on;
        if (!on) FocusReader();
    }

    void SettingChanged(string key)
    {
        if (_setup.SaveSettings) SettingsStore.Save(_settings);
        if (NeedRefresh.Contains(key)) _screen.View.Do(e => e.Refresh());
        if (key.StartsWith("drift", StringComparison.Ordinal)) _screen.View.Do(e => e.RestartRest());
        _screen.Refresh();
        _screen.View.InvalidateVisual();
    }

    async void CopySettings(string json)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not IClipboard clipboard) throw new InvalidOperationException("no clipboard");
            await clipboard.SetTextAsync(json);
            _panel.Note("Copied");
        }
        catch (Exception)
        {
            _panel.Note("Couldn't reach the clipboard");
        }
        DispatcherTimer.RunOnce(() => _panel.Note(""), TimeSpan.FromSeconds(2.5));
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _panel.Width = Math.Min(360, e.NewSize.Width * 0.92);
    }

    // ---------------------------------------------------------------- keys

    /// <summary>Keyboard shortcuts, wherever the focus is. Returns true when the key was used.</summary>
    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        bool plain = (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0;
        if (key == Key.S && plain && !_setup.Touch) { ToggleSettings(); return true; }
        if (key == Key.F && plain) { _screen.View.ShowFrameRate = !_screen.View.ShowFrameRate; return true; }
        if (key == Key.O && modifiers.HasFlag(KeyModifiers.Control) && !ReaderShowing) { _ = PickFolder(); return true; }
        if (key == Key.Escape) return GoBack();
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
        _screen.View.ClearBook();
        _book?.Dispose();
        _book = null;
    }
}
