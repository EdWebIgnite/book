using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using AndroidX.Core.View;

namespace Phone;

/// <summary>The Android app itself. It starts Avalonia with the reader in it (see <see cref="PhoneApp"/>).</summary>
[Application(Label = "Book Reader", Icon = "@drawable/icon", Theme = "@style/ReaderTheme", AllowBackup = false)]
public sealed class AndroidApp : AvaloniaAndroidApplication<PhoneApp>
{
    public AndroidApp(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        // Room on the graphics chip for a few page pictures, sized to the phone: the usual allowance is less
        // than two big pages, and going over it means pictures are sent to the chip again and again while
        // the camera moves, which shows as stutter.
        base.CustomizeAppBuilder(builder).With(new SkiaOptions { MaxGpuResourceSizeBytes = PhoneDevice.GpuCacheBytes(this) });
}

/// <summary>
/// The one screen. It turns freely with the phone (upright reads one page at a time, sideways shows the
/// open book) and passes the phone's own events on to the reader: going to the background, the screen
/// locking, running short of memory.
/// </summary>
[Activity(
    Label = "Book Reader", MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTask,
    ScreenOrientation = ScreenOrientation.FullUser,
    // Turning the phone or folding it only changes the size of the screen: the reader handles that itself
    // and keeps its place, rather than Android throwing the screen away and starting it again.
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode | ConfigChanges.Density | ConfigChanges.Keyboard
        | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation)]
// A book file opened from Files, WhatsApp, Drive or an email comes here to be added to the shelf. Phones
// rarely know the .picturebook ending, so the app also offers itself for zip files and files of unknown
// type; anything that isn't a book is turned down with a friendly message.
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" }, DataMimeTypes = new[] { "application/zip", "application/x-zip-compressed", "application/x-zip", "application/octet-stream", "application/x-picturebook" })]
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" }, DataHost = "*", DataMimeType = "*/*",
    DataPathPatterns = new[] { ".*\\.picturebook", ".*\\..*\\.picturebook", ".*\\..*\\..*\\.picturebook", ".*\\..*\\..*\\..*\\.picturebook" })]
[IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault },
    DataMimeTypes = new[] { "application/zip", "application/x-zip-compressed", "application/x-zip", "application/octet-stream", "application/x-picturebook" })]
public sealed class MainActivity : AvaloniaMainActivity
{
    /// <summary>The screen that is showing, for keeping it awake while a book is open.</summary>
    internal static MainActivity? Current { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Current = this;
        PhoneDevice.Measure(this);
        base.OnCreate(savedInstanceState);
        // Started by opening a book file (not when Android brings the screen back after it was put away).
        if (savedInstanceState is null) TakeBookFile(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        TakeBookFile(intent);
    }

    /// <summary>A book file sent or opened with the app: hand it to the shelf to be checked and added.</summary>
    void TakeBookFile(Intent? intent)
    {
        if (intent is null) return;
        Android.Net.Uri? uri = intent.Action switch
        {
            Intent.ActionView => intent.Data,
            Intent.ActionSend => OperatingSystem.IsAndroidVersionAtLeast(33)
                ? intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(Android.Net.Uri))) as Android.Net.Uri
#pragma warning disable CA1422 // the old way, for Android 12 and older
                : intent.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri,
#pragma warning restore CA1422
            _ => null,
        };
        if (uri is null) return;
        // Only once: turning the phone or coming back to the app must not add it again.
        intent.SetAction(Intent.ActionMain);
        intent.SetData(null);
        PhoneApp.OpenBookFile(this, uri);
    }

    protected override void OnPause()
    {
        // Into the background, or the screen went off: hold the place, stop every sound.
        PhoneApp.View?.Pause();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ApplyFullScreen();
        PhoneApp.View?.Resume();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        // Back from a dialog, the notification shade or another app: hide the bars again.
        if (hasFocus) ApplyFullScreen();
    }

    bool _fullScreen;

    /// <summary>
    /// While a book is open the story has the whole screen: the status bar and the navigation bar are
    /// hidden. A swipe in from the edge brings them back for a moment, over the picture, and they hide
    /// again by themselves. On the shelf they show as usual.
    /// </summary>
    internal void FullScreen(bool on)
    {
        _fullScreen = on;
        ApplyFullScreen();
    }

    void ApplyFullScreen()
    {
        try
        {
            if (Window is not Window window || window.DecorView is not View decor) return;
            WindowInsetsControllerCompat? bars = WindowCompat.GetInsetsController(window, decor);
            if (bars is null) return;
            if (_fullScreen)
            {
                bars.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
                bars.Hide(WindowInsetsCompat.Type.SystemBars());
            }
            else
            {
                bars.Show(WindowInsetsCompat.Type.SystemBars());
            }
        }
        catch (Exception)
        {
            // A phone that won't hide its bars just keeps them; the reader keeps clear of them either way.
        }
    }

    public override void OnTrimMemory([GeneratedEnum] TrimMemory level)
    {
        base.OnTrimMemory(level);
        // Running low while showing, or sent to the background: give back the pictures that can be loaded again.
        if (level >= TrimMemory.RunningLow) PhoneApp.View?.ReleaseMemory();
    }

    public override void OnLowMemory()
    {
        base.OnLowMemory();
        PhoneApp.View?.ReleaseMemory();
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(Current, this)) Current = null;
        base.OnDestroy();
    }

    /// <summary>Keep the screen on while a book is open, so it doesn't go dark in the middle of a story.</summary>
    internal void KeepScreenOn(bool on)
    {
        if (on) Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        else Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
    }
}
