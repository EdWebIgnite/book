using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Reader.Core;
using Reader.Ui;

namespace Player;

/// <summary>
/// The Player's one window: the bookshelf, the reader screen over it when a book is open, and the
/// settings panel down the right-hand side. It does what the script at the bottom of web/index.html does.
/// </summary>
sealed class MainWindow : Window
{
    /// <summary>Settings that change which steps a book has or where the camera rests, so the reader must re-plan.</summary>
    static readonly HashSet<string> NeedRefresh = new() { "padding", "dim", "vignette", "spotlight", "wholePageFirst", "wholePageLast", "*" };

    readonly Options _options;
    readonly ReaderSettings _settings;
    readonly bool _saveSettings;
    readonly ShelfView _shelf = new();
    readonly ReaderScreen _screen;
    readonly SettingsPanel _panel;
    LoadedBook? _book;
    string? _content;
    bool _opening;

    public MainWindow(Options options, ReaderSettings settings, ISoundOutput sound, bool saveSettings)
    {
        _options = options;
        _settings = settings;
        _saveSettings = saveSettings;

        Title = "Book Reader";
        Background = new SolidColorBrush(Color.Parse("#1c1b22"));     // the reader's dark backdrop, so nothing flashes white
        Width = 1280;
        Height = 800;
        MinWidth = 420;
        MinHeight = 320;

        _screen = new ReaderScreen(settings, sound) { IsVisible = false };
        _screen.Engine.DeviceReducedMotion = DeviceMotion.Reduced();
        _screen.View.ShowFrameRate = options.ShowFrameRate;
        _screen.CloseRequested += CloseBook;
        _screen.GearRequested += () => ToggleSettings();

        _panel = new SettingsPanel(settings) { IsVisible = false };
        _panel.Changed += SettingChanged;
        _panel.CloseRequested += () => ToggleSettings(false);
        _panel.ReplayRequested += () => _screen.View.Do(e => e.Replay());
        _panel.CopyRequested += CopySettings;
        _panel.Done += FocusReader;

        _shelf.BookChosen += entry => _ = OpenBook(entry.Folder);
        _shelf.OpenFolderRequested += () => _ = PickFolder();

        Content = new Panel { Children = { _shelf, _screen, _panel } };
        Opened += (_, _) => Start();
    }

    public ReaderScreen Screen => _screen;

    /// <summary>True while a book is still loading.</summary>
    public bool Busy => _opening;
    public bool ReaderShowing => _screen.IsVisible;

    void Start()
    {
        LoadShelf();
        if (_options.Shot is null && _options.Book is not null && ResolveBook(_options.Book) is string folder) _ = OpenBook(folder);
    }

    // ---------------------------------------------------------------- the shelf

    /// <summary>Find the content folder and list its books.</summary>
    public void LoadShelf(bool coversNow = false)
    {
        _content = BookLibrary.FindContent(_options.Content, AppContext.BaseDirectory, Environment.CurrentDirectory);
        if (_content is null)
        {
            _shelf.Show(Array.Empty<ShelfEntry>());
            _shelf.Say(_options.Content is not null
                ? $"There is no library.json in {_options.Content}. Use \"Open a book folder\" to open a book from anywhere."
                : "Couldn't find any books. Put a \"content\" folder beside the program, start it with --content <folder>, or use \"Open a book folder\".");
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

    /// <summary>A book named on the command line: an id from the library, or a folder.</summary>
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

    async Task PickFolder()
    {
        try
        {
            var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Open a book folder", AllowMultiple = false });
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

    /// <summary>Load a book in the background and show it.</summary>
    public async Task OpenBook(string folder)
    {
        if (_opening) return;
        _opening = true;
        try
        {
            _shelf.Say(null);
            LoadedBook book = await LoadedBook.LoadAsync(folder);
            ShowBook(book);
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
        if (_options.PlaceGiven) JumpTo(_options.Page, _options.Stop);
        _options.Page = 1;          // only the first book opened starts where the command line said
        _options.Stop = null;
        _options.PlaceGiven = false;
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

    void CloseBook()
    {
        if (!ReaderShowing) return;
        _screen.View.ClearBook();
        _screen.IsVisible = false;
        _shelf.IsVisible = true;
        _book?.Dispose();
        _book = null;
    }

    void FocusReader()
    {
        if (ReaderShowing) _screen.View.Focus();
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
        if (_saveSettings) SettingsStore.Save(_settings);
        if (NeedRefresh.Contains(key)) _screen.View.Do(e => e.Refresh());
        if (key.StartsWith("drift", StringComparison.Ordinal)) _screen.View.Do(e => e.RestartRest());
        _screen.Refresh();
        _screen.View.InvalidateVisual();
    }

    async void CopySettings(string json)
    {
        try
        {
            if (Clipboard is null) throw new InvalidOperationException("no clipboard");
            await Clipboard.SetTextAsync(json);
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is TextBox) return;
        bool plain = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0;

        if (e.Key == Key.S && plain) { ToggleSettings(); e.Handled = true; return; }
        if (e.Key == Key.F && plain) { _screen.View.ShowFrameRate = !_screen.View.ShowFrameRate; e.Handled = true; return; }
        if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !ReaderShowing) { _ = PickFolder(); e.Handled = true; return; }
        if (e.Key == Key.Escape)
        {
            if (_panel.IsVisible) ToggleSettings(false);
            else CloseBook();
            e.Handled = true;
            return;
        }
        // Wherever the keyboard focus happens to be, the reading keys still go to the reader.
        if (ReaderShowing && _screen.View.HandleKey(e.Key, e.KeyModifiers)) e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _screen.View.ClearBook();
        _book?.Dispose();
        _book = null;
    }
}
