using System.Runtime.InteropServices;

namespace Player;

/// <summary>
/// The computer's own language, for "As this device". The program is built without culture data (to
/// keep it small), so it is asked of Windows directly, or read from the usual settings on Linux and macOS.
/// </summary>
static class DeviceLanguage
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetUserDefaultLocaleName(char[] name, int size);

    /// <summary>A language code such as "af-ZA" or "en_GB", or "en" when it can't be told.</summary>
    public static string Get()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var buffer = new char[85];
                int n = GetUserDefaultLocaleName(buffer, buffer.Length);
                if (n > 1) return new string(buffer, 0, n - 1);
            }
        }
        catch (Exception) { }
        foreach (string name in new[] { "LC_ALL", "LC_MESSAGES", "LANG" })
        {
            string? v = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(v) && v != "C" && v != "POSIX") return v;
        }
        return "en";
    }
}
