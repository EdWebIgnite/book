using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace BookEditor;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ShotOptions? shot;
        try { shot = ShotOptions.Parse(args); }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(ShotOptions.Usage);
            return 2;
        }
        if (args.Contains("--help") || args.Contains("-h")) { Console.WriteLine(ShotOptions.Usage); return 0; }
        if (shot is not null) return Screenshot.Take(shot);
        EditorApp.OpenAtStart = args.FirstOrDefault(a => !a.StartsWith('-') && Directory.Exists(a));
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<EditorApp>()
        .UsePlatformDetect()
        .With(new SkiaOptions { MaxGpuResourceSizeBytes = 512L * 1024 * 1024 });
}

sealed class EditorApp : Application
{
    /// <summary>A book folder given on the command line (or by dropping a folder on BookEditor.exe).</summary>
    public static string? OpenAtStart { get; set; }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
        Resources["SystemAccentColor"] = Ui.AccentColor;
        Resources["SystemAccentColorDark1"] = Avalonia.Media.Color.Parse("#e0761f");
        Resources["SystemAccentColorLight1"] = Avalonia.Media.Color.Parse("#ffa04f");
        // The folding sections on the right: cards on the panel, not black bars.
        foreach (string key in new[] { "ExpanderHeaderBackground", "ExpanderHeaderBackgroundPointerOver", "ExpanderHeaderBackgroundPressed", "ExpanderContentBackground" })
            Resources[key] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(key.Contains("PointerOver") ? "#262a33" : "#20232b"));
        foreach (string key in new[] { "ExpanderHeaderBorderBrush", "ExpanderHeaderBorderBrushPointerOver", "ExpanderHeaderBorderBrushPressed", "ExpanderContentBorderBrush" })
            Resources[key] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#343946"));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (OpenAtStart is string folder) window.Opened += (_, _) => _ = window.OpenBook(folder);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
