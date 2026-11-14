using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Avalonia;
using Avalonia.Android;

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
public sealed class MainActivity : AvaloniaMainActivity
{
    /// <summary>The screen that is showing, for keeping it awake while a book is open.</summary>
    internal static MainActivity? Current { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Current = this;
        PhoneDevice.Measure(this);
        base.OnCreate(savedInstanceState);
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
        PhoneApp.View?.Resume();
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
