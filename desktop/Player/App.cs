using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Reader.Audio;
using Reader.Core;

namespace Player;

sealed class App : Application
{
    /// <summary>What the command line asked for. Set before the app starts.</summary>
    public static Options Options { get; set; } = new();

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        // The shelf and the settings panel are light, like the browser version, whatever the computer's own theme is.
        RequestedThemeVariant = ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var sound = new DesktopSound();
            ReaderSettings settings = SettingsStore.Load();
            if (Options.Set.Count > 0) settings.ApplyJson(Options.SetAsJson());
            var window = new MainWindow(Options, settings, sound, saveSettings: true);
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => sound.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
