using System.Runtime.InteropServices;

namespace Player;

/// <summary>
/// Asks the computer whether its owner has switched animations off. The reader's "Camera movement"
/// setting follows this when it is set to "Follow this device's setting".
/// </summary>
static partial class DeviceMotion
{
    const uint GetClientAreaAnimation = 0x1042;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint update);

    /// <summary>
    /// True when the device asks for less motion. On Windows this is Settings, Accessibility, Visual
    /// effects, Animation effects. Other systems are not asked yet, so there it is always false and the
    /// reader's own setting is the way to reduce motion.
    /// </summary>
    public static bool Reduced()
    {
        try
        {
            if (OperatingSystem.IsWindows() && SystemParametersInfo(GetClientAreaAnimation, 0, out bool animations, 0)) return !animations;
        }
        catch (Exception) { /* an old or cut-down Windows: assume full motion */ }
        return false;
    }
}
