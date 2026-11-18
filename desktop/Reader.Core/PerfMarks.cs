using System.Diagnostics;
using System.Globalization;

namespace Reader.Core;

/// <summary>
/// Milestones of starting up and opening books, written as "perf: 412 ms  first frame" with the time since
/// the program started. Off unless <see cref="On"/> is set: the Windows program sets it when the
/// BOOKREADER_PERF environment variable is 1, the phone app in a Debug build (it shows in adb logcat).
/// Costs nothing while off.
/// </summary>
public static class PerfMarks
{
    static readonly HashSet<string> Once = new();

    public static bool On { get; set; }

    /// <summary>Milliseconds since the process started.</summary>
    public static double SinceStart
    {
        get
        {
            try { return (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds; }
            catch (Exception) { return Environment.TickCount64; }
        }
    }

    /// <summary>Note that something has happened.</summary>
    public static void Mark(string what)
    {
        if (!On) return;
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture, $"perf: {SinceStart,7:0} ms  {what}"));
    }

    /// <summary>The same, only the first time it happens.</summary>
    public static void MarkOnce(string what)
    {
        if (!On) return;
        lock (Once) if (!Once.Add(what)) return;
        Mark(what);
    }
}
