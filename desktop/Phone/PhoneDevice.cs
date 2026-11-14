using Android.App;
using Android.Content;

namespace Phone;

/// <summary>What the reader needs to know about the phone it is on: how big a centimetre is, how much memory it may use.</summary>
static class PhoneDevice
{
    /// <summary>Screen units (Android's density-independent pixels) in a real centimetre on this screen.</summary>
    public static double UnitsPerCm { get; private set; } = 160 / 2.54;

    /// <summary>How many megabytes the app may use, as the phone says (its memory class). Cheap phones say 128 or less.</summary>
    public static int MemoryClassMb { get; private set; } = 128;

    /// <summary>The phone counts itself as one with little memory.</summary>
    public static bool LowRam { get; private set; }

    /// <summary>The phone asks for animations to be switched off (Settings, Accessibility, Remove animations).</summary>
    public static bool ReducedMotion { get; private set; }

    public static void Measure(Activity activity)
    {
        try
        {
            var metrics = activity.Resources?.DisplayMetrics;
            if (metrics is not null && metrics.Density > 0)
            {
                // One unit is 1/160 of an inch at the screen's nominal density. The real density (xdpi) says how
                // big that really is; some phones report nonsense there, so it is only trusted when it is close.
                double nominal = (int)metrics.DensityDpi, real = metrics.Xdpi;
                double dpi = real > nominal * 0.6 && real < nominal * 1.6 ? real : nominal;
                UnitsPerCm = dpi / 2.54 / metrics.Density;
            }
            if (activity.GetSystemService(Context.ActivityService) is ActivityManager am)
            {
                MemoryClassMb = am.MemoryClass;
                LowRam = am.IsLowRamDevice;
            }
            float scale = Android.Provider.Settings.Global.GetFloat(activity.ContentResolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f);
            ReducedMotion = scale == 0f;
        }
        catch (Exception)
        {
            // Anything that can't be read keeps its safe default.
        }
    }

    /// <summary>
    /// How many full-size page pictures to keep at once. The page being read and the next one always fit;
    /// phones with more room keep the one before as well, so going back is instant too.
    /// </summary>
    public static int FullPicturesToKeep => LowRam || MemoryClassMb < 192 ? 2 : MemoryClassMb < 256 ? 3 : 4;

    /// <summary>Room on the graphics chip for pictures: a quarter of what the app may use, between 48 and 256 MB.</summary>
    public static long GpuCacheBytes(Context context)
    {
        int mb = 128;
        try { if (context.GetSystemService(Context.ActivityService) is ActivityManager am) mb = am.MemoryClass; }
        catch (Exception) { /* keep the default */ }
        return Math.Clamp(mb / 4, 48, 256) * 1024L * 1024;
    }
}
