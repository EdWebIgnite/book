using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Reader.Audio;
using Reader.Core;

namespace Player;

sealed class App : Application
{
    /// <summary>What the command line asked for. Set before the app starts.</summary>
    public static Options Options { get; set; } = new();

    public override void Initialize()
    {
        // The app's skin: the Fluent theme in the app's colours, and the app's own font (see Reader.App/AppSkin.cs).
        Reader.App.AppSkin.Install(this);
        PerfMarks.Mark("skin installed");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var sound = new DesktopSound();
            ReaderSettings settings = SettingsStore.Load();
            if (Options.Set.Count > 0) settings.ApplyJson(Options.SetAsJson());
            PerfMarks.Mark("sound and settings");
            var window = new MainWindow(Options, settings, sound, saveSettings: true);
            PerfMarks.Mark("window made");
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => sound.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
