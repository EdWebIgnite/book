using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Reader.App;
using Reader.Audio;
using Reader.Core;
using Reader.Ui;

namespace Phone;

/// <summary>
/// The reader set up for a phone or tablet: the shared shelf, reader and settings (<see cref="AppView"/>)
/// filling the screen, touch first, with only the grown-ups' settings behind the gear.
/// </summary>
public sealed class PhoneApp : Avalonia.Application
{
    /// <summary>The shelf, reader and settings, once they exist.</summary>
    internal static AppView? View { get; private set; }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        // The shelf and the settings are light, like the browser version, whatever the phone's own theme is.
        RequestedThemeVariant = ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            // Phone limits: no picture bigger than most graphics chips can draw in one go (page 12 of the
            // puddle book is 4800 pixels wide), and only as many big pictures held as the phone has room for.
            LoadedBook.MaxPictureSide = 4096;
            LoadedBook.FullPicturesKept = PhoneDevice.FullPicturesToKeep;

            // A fresh install starts with the grown-up lock on the gear, so a child can't wander into the settings.
            bool firstRun = !File.Exists(SettingsStore.DefaultPath);
            ReaderSettings settings = SettingsStore.Load();
            if (firstRun) settings.Gate = true;

            var setup = new AppSetup
            {
                NoBooksMessage = "Couldn't get the books ready. Try closing the app and opening it again.",
                Touch = true,
                CanOpenFolders = false,
                SaveSettings = true,
                DeviceReducedMotion = PhoneDevice.ReducedMotion,
                UnitsPerCm = PhoneDevice.UnitsPerCm,
            };
            var view = new AppView(setup, settings, new DesktopSound());
            View = view;
            view.ReadingChanged += reading => MainActivity.Current?.KeepScreenOn(reading);
            view.AttachedToVisualTree += (_, _) => Attach(view);
            single.MainView = view;

            // The books are unpacked off the screen's thread (only the first time takes a moment).
            view.Say("Getting the books ready…");
            Android.Content.Context context = Android.App.Application.Context;
            Task.Run(() => BookUnpacker.Unpack(context)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                setup.Content = t.IsCompletedSuccessfully ? t.Result : null;
                view.LoadShelf();
            }));
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Once on screen: draw behind the system bars and keep clear of them, and take the Back button.</summary>
    static void Attach(AppView view)
    {
        if (TopLevel.GetTopLevel(view) is not TopLevel top) return;
        TopLevel.SetAutoSafeAreaPadding(view, false);
        if (top.InsetsManager is { } insets)
        {
            insets.DisplayEdgeToEdgePreference = true;
            view.SafeArea = insets.SafeAreaPadding;
            insets.SafeAreaChanged += (_, e) => view.SafeArea = e.SafeAreaPadding;
        }
        // Back: closes the settings, or leaves the book for the shelf. Only from the shelf does it leave the app,
        // so a child is never thrown out of the middle of a story.
        top.BackRequested += (_, e) => { if (view.GoBack()) e.Handled = true; };
    }
}
