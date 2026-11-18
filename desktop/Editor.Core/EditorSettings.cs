using System.Text.Json;
using System.Text.Json.Nodes;
using Reader.Core;

namespace Editor.Core;

/// <summary>
/// What the editor remembers between runs about itself (never about a book): where its window was and
/// how big, the folder last used in a file picker, the colour round the page, and which screens'
/// frames are shown over the picture. Kept in settings.json in the editor's own folder. A missing or
/// damaged file just means the defaults.
/// </summary>
public sealed class EditorSettings
{
    /// <summary>The window's place and size in screen pixels, or null the first time.</summary>
    public PixelBox? Window { get; set; }
    public bool Maximized { get; set; }

    /// <summary>The folder a file or folder picker last came back from.</summary>
    public string? LastFolder { get; set; }

    /// <summary>The colour round the page: "grey" (a mid grey, the default), "light" (a light grey) or "paper".</summary>
    public string Surround { get; set; } = "grey";

    /// <summary>The screens whose frames are drawn round the selected stop (device ids).</summary>
    public List<string> Frames { get; set; } = new() { "s22-upright", "s22-sideways" };

    /// <summary>Show the middle of a spread and its left and right pages.</summary>
    public bool Crease { get; set; } = true;

    public static readonly string[] Surrounds = { "grey", "light", "paper" };

    /// <summary>The file, in the editor's own folder unless a test gives another.</summary>
    public string File { get; init; } = Path.Combine(EditorPaths.AppData, "settings.json");

    public static EditorSettings Load(string? file = null)
    {
        var s = new EditorSettings { File = file ?? Path.Combine(EditorPaths.AppData, "settings.json") };
        try
        {
            if (!System.IO.File.Exists(s.File)) return s;
            if (JsonNode.Parse(System.IO.File.ReadAllText(s.File)) is not JsonObject o) return s;
            if (o["window"] is JsonObject w && Int(w["x"]) is int x && Int(w["y"]) is int y && Int(w["width"]) is int ww && Int(w["height"]) is int wh && ww > 0 && wh > 0)
                s.Window = new PixelBox(x, y, ww, wh);
            s.Maximized = o["maximized"]?.GetValueKind() == JsonValueKind.True;
            if (o["lastFolder"]?.GetValueKind() == JsonValueKind.String) s.LastFolder = o["lastFolder"]!.GetValue<string>();
            if (o["surround"]?.GetValueKind() == JsonValueKind.String && Surrounds.Contains(o["surround"]!.GetValue<string>())) s.Surround = o["surround"]!.GetValue<string>();
            if (o["frames"] is JsonArray a) s.Frames = a.Where(n => n?.GetValueKind() == JsonValueKind.String).Select(n => n!.GetValue<string>()).Distinct().ToList();
            if (o["crease"]?.GetValueKind() is JsonValueKind.True or JsonValueKind.False) s.Crease = o["crease"]!.GetValue<bool>();
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException or FormatException) { }
        return s;
    }

    static int? Int(JsonNode? n) => n?.GetValueKind() == JsonValueKind.Number && n.GetValue<double>() is double d && Math.Abs(d) < 1_000_000 ? (int)Math.Round(d) : null;

    /// <summary>Write the file. Returns false if it couldn't be (the editor carries on regardless).</summary>
    public bool Save()
    {
        var o = new JsonObject();
        if (Window is PixelBox w) o["window"] = new JsonObject { ["x"] = w.X, ["y"] = w.Y, ["width"] = w.Width, ["height"] = w.Height };
        o["maximized"] = Maximized;
        if (LastFolder is not null) o["lastFolder"] = LastFolder;
        o["surround"] = Surround;
        var frames = new JsonArray();
        foreach (string f in Frames) frames.Add((JsonNode)JsonValue.Create(f));
        o["frames"] = frames;
        o["crease"] = Crease;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(File)!);
            string tmp = File + ".tmp";
            System.IO.File.WriteAllText(tmp, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            System.IO.File.Move(tmp, File, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Remember the folder something was picked from (a file's folder, or a folder itself).</summary>
    public void Picked(string path)
    {
        string? folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) LastFolder = folder;
    }
}

/// <summary>A rectangle in whole screen pixels.</summary>
public readonly record struct PixelBox(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public int Overlap(PixelBox o)
    {
        int w = Math.Min(Right, o.Right) - Math.Max(X, o.X), h = Math.Min(Bottom, o.Bottom) - Math.Max(Y, o.Y);
        return w > 0 && h > 0 ? w * h : 0;
    }
}

/// <summary>Where to put the window when the editor starts.</summary>
public static class WindowPlacement
{
    /// <summary>
    /// The window's remembered place, made to fit the screens there are now: on the screen it overlaps
    /// most (or the first, if it is on none of them, say a second screen that has been unplugged),
    /// no bigger than that screen's working area and no smaller than the minimum, and moved fully onto
    /// it. Null when nothing was remembered, so the window opens in its usual place.
    /// </summary>
    public static PixelBox? Fit(PixelBox? saved, IReadOnlyList<PixelBox> workAreas, int minWidth, int minHeight)
    {
        if (saved is not PixelBox s || workAreas.Count == 0) return null;
        PixelBox screen = workAreas.OrderByDescending(a => a.Overlap(s)).First();
        if (screen.Overlap(s) == 0) screen = workAreas[0];
        int w = Math.Clamp(s.Width, Math.Min(minWidth, screen.Width), screen.Width);
        int h = Math.Clamp(s.Height, Math.Min(minHeight, screen.Height), screen.Height);
        int x = Math.Clamp(s.X, screen.X, screen.Right - w);
        int y = Math.Clamp(s.Y, screen.Y, screen.Bottom - h);
        return new PixelBox(x, y, w, h);
    }

    /// <summary>The window's first size on a screen: as big as is comfortable, leaving a margin, never under the minimum.</summary>
    public static (double Width, double Height) FirstSize(double workWidth, double workHeight, double minWidth, double minHeight)
    {
        double w = Math.Max(minWidth, Math.Min(1600, workWidth - 48)), h = Math.Max(minHeight, Math.Min(1000, workHeight - 48));
        return (Math.Min(w, Math.Max(minWidth, workWidth)), Math.Min(h, Math.Max(minHeight, workHeight)));
    }
}

/// <summary>
/// Opening a book file (.picturebook, made by Export) in the editor: it is unpacked, checked the way
/// the apps check one, into a new folder of its own, which is then the book's folder. The file itself is
/// never changed.
/// </summary>
public static class BookFileOpener
{
    public sealed record Outcome(bool Ok, string Message, string? Folder = null);

    /// <summary>Unpack <paramref name="package"/> into a new folder inside <paramref name="parent"/>, named after the book.</summary>
    public static Outcome Unpack(string package, string parent)
    {
        string scratch = Path.Combine(parent, ".unpacking-" + Guid.NewGuid().ToString("N")[..10]);
        try
        {
            Directory.CreateDirectory(parent);
            BookPackage.Result r = BookPackage.Import(package, scratch);
            if (!r.Ok || r.Folder is null) return new Outcome(false, "That book file couldn't be opened. " + r.Message);
            string target = UniqueFolder(parent, Path.GetFileName(r.Folder));
            Directory.Move(r.Folder, target);
            return new Outcome(true, $"Unpacked \"{r.Title}\" into {target}. This is now its folder: the book file itself is left as it was.", target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new Outcome(false, "That book file couldn't be unpacked here: " + e.Message);
        }
        finally
        {
            try { if (Directory.Exists(scratch)) Directory.Delete(scratch, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>A folder in <paramref name="parent"/> that doesn't exist yet: the name, or the name (2), (3)...</summary>
    public static string UniqueFolder(string parent, string name)
    {
        string first = Path.Combine(parent, name);
        if (!Directory.Exists(first) && !System.IO.File.Exists(first)) return first;
        for (int n = 2; ; n++)
        {
            string next = Path.Combine(parent, $"{name} ({n})");
            if (!Directory.Exists(next) && !System.IO.File.Exists(next)) return next;
        }
    }
}
