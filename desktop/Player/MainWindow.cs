using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Reader.App;
using Reader.Core;
using Reader.Ui;

namespace Player;

/// <summary>
/// The Player's one window. Everything in it is the shared <see cref="AppView"/> (shelf, reader and
/// settings), set up for a computer: keyboard shortcuts, a folder picker, the whole playground panel.
/// </summary>
sealed class MainWindow : Window
{
    readonly Options _options;

    public MainWindow(Options options, ReaderSettings settings, ISoundOutput sound, bool saveSettings)
    {
        _options = options;
        Title = "Book Reader";
        Background = new SolidColorBrush(Color.Parse("#1c1b22"));     // the reader's dark backdrop, so nothing flashes white
        Width = 1280;
        Height = 800;
        // A phone-sized window is allowed as the phone app (--phone), so its screens can be drawn here.
        MinWidth = options.Touch ? 240 : 420;
        MinHeight = options.Touch ? 240 : 320;

        string? content = BookLibrary.FindContent(options.Content, AppContext.BaseDirectory, Environment.CurrentDirectory);
        var setup = new AppSetup
        {
            Content = content,
            NoBooksMessage = options.Content is not null
                ? $"There is no library.json in {options.Content}. Use \"Open a book folder\" to open a book from anywhere."
                : "Couldn't find any books. Put a \"content\" folder beside the program, start it with --content <folder>, or use \"Open a book folder\".",
            Touch = options.Touch,
            CanOpenFolders = !options.Touch,
            SaveSettings = saveSettings,
            DeviceReducedMotion = DeviceMotion.Reduced(),
            UnitsPerCm = options.UnitsPerCm,
            ImportFolder = options.ImportFolder ?? DefaultImportFolder,
        };
        App = new AppView(setup, settings, sound);
        App.Screen.View.ShowFrameRate = options.ShowFrameRate;
        if (options.SafeArea is { } safe) App.SafeArea = safe;
        Content = App;
        Opened += (_, _) => Start();

        // A book file dropped on the window is added to the shelf and opened.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None);
        AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? new List<string>();
            if (paths.FirstOrDefault(p => File.Exists(p)) is string file) _ = ImportAndOpen(file);
            else if (paths.FirstOrDefault(Directory.Exists) is string folder && File.Exists(Path.Combine(folder, "book.json"))) _ = App.OpenBook(folder);
        });
    }

    /// <summary>Books added from book files: %APPDATA%\BookReader\books on Windows, beside the settings.</summary>
    public static string DefaultImportFolder => Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath)!, "books");

    /// <summary>Add a book file to the shelf and open the book.</summary>
    public async Task ImportAndOpen(string file)
    {
        BookPackage.Result r = await App.ImportBookFile(file);
        if (r.Ok && r.Folder is not null) await App.OpenBook(r.Folder);
    }

    /// <summary>The shelf, reader and settings that fill the window.</summary>
    public AppView App { get; }

    public ReaderScreen Screen => App.Screen;
    public bool Busy => App.Busy;
    public bool ReaderShowing => App.ReaderShowing;

    void Start()
    {
        LoadShelf();
        if (_options.Shot is null && _options.Import is not null) { _ = ImportAndOpen(_options.Import); return; }
        if (_options.Shot is null && _options.Book is not null && App.ResolveBook(_options.Book) is string folder)
            _ = App.OpenBook(folder, () => { if (_options.PlaceGiven) App.JumpTo(_options.Page, _options.Stop); });
    }

    public void LoadShelf(bool coversNow = false) => App.LoadShelf(coversNow);
    public string? ResolveBook(string book) => App.ResolveBook(book);
    public void OpenBookNow(string folder) => App.OpenBookNow(folder);
    public void JumpTo(int pageNumber, int? stopNumber) => App.JumpTo(pageNumber, stopNumber);
    public void ToggleSettings(bool? show = null) => App.ToggleSettings(show);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Source is not TextBox && App.HandleKey(e.Key, e.KeyModifiers)) e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        App.Shutdown();
    }
}
