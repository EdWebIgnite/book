using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
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
        // The same skin as the Windows program (Reader.App/AppSkin.cs): light, leaf green and cream, whatever the phone's own theme is.
        AppSkin.Install(this, phone: true);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            // Phone limits: no picture bigger than most graphics chips can draw in one go (page 12 of the
            // puddle book is 4800 pixels wide), and only as many big pictures held as the phone has room for.
            LoadedBook.MaxPictureSide = 4096;
            LoadedBook.FullPicturesKept = PhoneDevice.FullPicturesToKeep;

            ReaderSettings settings = SettingsStore.Load();
            // The children, their progress, the language and bedtime colours, kept beside the settings.
            AppState state = AppStateStore.Load();
            string device = DeviceLanguage();
            Strings.Language = Strings.Resolve(state.Language, device);

            var setup = new AppSetup
            {
                NoBooksMessage = Strings.T("shelf.notReady"),
                State = state,
                StatePath = AppStateStore.DefaultPath,
                DeviceLanguage = device,
                Touch = true,
                CanOpenFolders = false,
                SaveSettings = true,
                DeviceReducedMotion = PhoneDevice.ReducedMotion,
                UnitsPerCm = PhoneDevice.UnitsPerCm,
                // Books added from book files: in the app's own storage, apart from the books packed in the
                // app (which are unpacked again after each update), so they survive updates.
                ImportFolder = Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, "imported"),
            };
            var view = new AppView(setup, settings, new DesktopSound());
            View = view;
            view.ReadingChanged += reading =>
            {
                MainActivity.Current?.KeepScreenOn(reading);
                MainActivity.Current?.FullScreen(reading);
            };
            view.AttachedToVisualTree += (_, _) => Attach(view);
            single.MainView = view;

            // The books are unpacked off the screen's thread (only the first time takes a moment).
            view.Say(Strings.T("shelf.gettingReady"), NoteKind.Busy);
            Android.Content.Context context = Android.App.Application.Context;
            Task.Run(() => BookUnpacker.Unpack(context)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                setup.Content = t.IsCompletedSuccessfully ? t.Result : null;
                view.LoadShelf();
                view.Start();
                _shelfReady = true;
                if (_waiting is { } next) { _waiting = null; next(); }
            }));
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>The phone's language, such as "af" or "en".</summary>
    static string DeviceLanguage()
    {
        try { return Java.Util.Locale.Default?.Language ?? "en"; }
        catch (Exception) { return "en"; }
    }

    static bool _shelfReady;
    static Action? _waiting;

    /// <summary>
    /// A book file opened with the app, or sent to it. It is read through Android's content system (the
    /// sending app grants that for this one file), so the app needs no storage permission. If the shelf
    /// isn't ready yet (the app is just starting), it waits for it.
    /// </summary>
    internal static void OpenBookFile(Android.Content.Context context, Android.Net.Uri uri)
    {
        void Go()
        {
            if (View is not AppView view) return;
            Android.Content.ContentResolver? resolver = context.ContentResolver;
            _ = view.ImportBook(() =>
            {
                Stream? s = resolver?.OpenInputStream(uri);
                return s is null ? throw new IOException("no stream") : Task.FromResult(s);
            });
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (_shelfReady && View is not null) Go();
            else _waiting = Go;
        });
    }

    /// <summary>Once on screen: draw behind the system bars and keep clear of them, and take the Back button.</summary>
    static void Attach(AppView view)
    {
        if (TopLevel.GetTopLevel(view) is not TopLevel top) return;
        TopLevel.SetAutoSafeAreaPadding(view, false);
        if (top.InsetsManager is { } insets)
        {
            insets.DisplayEdgeToEdgePreference = true;
            // The system bars and the camera's cutout, as the phone reports them (bars that are hidden take
            // no room), and room for the screen's rounded corners on top.
            view.SafeArea = PhoneDevice.WithCorners(insets.SafeAreaPadding, top.RenderScaling);
            insets.SafeAreaChanged += (_, e) => view.SafeArea = PhoneDevice.WithCorners(e.SafeAreaPadding, top.RenderScaling);
        }
        // Back: closes the settings, or leaves the book for the shelf. Only from the shelf does it leave the app,
        // so a child is never thrown out of the middle of a story.
        top.BackRequested += (_, e) => { if (view.GoBack()) e.Handled = true; };
    }
}
