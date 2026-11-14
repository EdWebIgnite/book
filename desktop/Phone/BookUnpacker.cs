using Android.Content;
using Android.Content.Res;

namespace Phone;

/// <summary>
/// The books travel inside the app. The reader reads them from ordinary files, so the first time the app
/// runs (and after each update) they are copied out to the app's own storage. Nothing is downloaded.
/// </summary>
static class BookUnpacker
{
    const string Folder = "content";

    /// <summary>Unpack the books if they aren't there yet, and return the folder holding library.json. Null if that failed.</summary>
    public static string? Unpack(Context context)
    {
        try
        {
            string root = Path.Combine(context.FilesDir!.AbsolutePath, Folder);
            string marker = Path.Combine(root, ".unpacked");
            string version = Version(context);
            if (File.Exists(marker) && File.ReadAllText(marker) == version && File.Exists(Path.Combine(root, "library.json"))) return root;

            // Unpack beside the old copy and swap it in at the end, so a half-finished copy is never read.
            string fresh = root + ".new";
            if (Directory.Exists(fresh)) Directory.Delete(fresh, recursive: true);
            AssetManager assets = context.Assets!;
            Copy(assets, Folder, fresh);
            File.WriteAllText(Path.Combine(fresh, ".unpacked"), version);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Directory.Move(fresh, root);
            return root;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Java.Lang.Exception)
        {
            return null;
        }
    }

    static void Copy(AssetManager assets, string from, string to)
    {
        string[] names = assets.List(from) ?? Array.Empty<string>();
        Directory.CreateDirectory(to);
        foreach (string name in names)
        {
            string path = from + "/" + name;
            // A folder lists its contents; a file lists nothing.
            if ((assets.List(path) ?? Array.Empty<string>()).Length > 0) { Copy(assets, path, Path.Combine(to, name)); continue; }
            using Stream input = assets.Open(path);
            using FileStream output = File.Create(Path.Combine(to, name));
            input.CopyTo(output);
        }
    }

    /// <summary>Changes whenever the app is installed or updated, so new books in an update are unpacked again.</summary>
    static string Version(Context context)
    {
        var info = context.PackageManager!.GetPackageInfo(context.PackageName!, 0)!;
        return info.LastUpdateTime.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
