namespace Reader.Core;

/// <summary>
/// The app's name as people see it: the window title, the welcome, the wordmark and the Android label.
/// Change it here and everything follows (the Android label in Phone/MainActivity.cs reads this constant too).
/// The program files, folders and the Android application id keep their old names on purpose, so
/// installed copies keep updating and their saved settings and books stay where they are.
/// </summary>
public static class Brand
{
    public const string Name = "Dassie";

    /// <summary>Shown in About. Raised by hand with each release.</summary>
    public const string Version = "1.0";
}
