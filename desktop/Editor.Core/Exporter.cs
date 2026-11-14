using System.IO.Compression;
using System.Text.Json.Nodes;
using Reader.Core;

namespace Editor.Core;

/// <summary>Choices for Export.</summary>
public sealed record ExportOptions
{
    /// <summary>The longest side a page picture may have (0: leave pictures as they are). 4096 is what phones can draw.</summary>
    public int PageMaxSide { get; init; } = Pictures.PhoneLimit;
    public int CoverMaxSide { get; init; } = 1800;
    /// <summary>Pictures on top keep their see-through parts, so they stay PNG, but are made no bigger than this.</summary>
    public int LayerMaxSide { get; init; } = 2048;
    /// <summary>Page pictures with no see-through parts become JPEG, which is far smaller than PNG for paintings.</summary>
    public bool PagesAsJpeg { get; init; } = true;
    public int JpegQuality { get; init; } = 85;
    /// <summary>Also pack the exported folder into a zip file beside it.</summary>
    public bool Zip { get; init; } = true;

    public static readonly ExportOptions Phone = new();
    public static readonly ExportOptions SmallDownload = new() { PageMaxSide = 2560, CoverMaxSide = 1400, LayerMaxSide = 1600, JpegQuality = 80 };
    public static readonly ExportOptions Originals = new() { PageMaxSide = 0, CoverMaxSide = 0, LayerMaxSide = 0, PagesAsJpeg = false };
}

/// <summary>What Export did.</summary>
public sealed record ExportResult(string Folder, string? ZipFile, int Files, long BytesBefore, long BytesAfter, List<string> Notes);

/// <summary>
/// Export: a copy of the book ready for the apps. Pictures are made a sensible size for phones (big
/// explore scenes are cut into tiles so they stay sharp), only the files the book uses are copied, the
/// editor's own notes are left out, and the folder can be packed into a zip.
/// </summary>
public static class Exporter
{
    public static ExportResult Export(BookProject project, string destinationParent, ExportOptions options, IProgress<string>? progress = null)
    {
        Book book = BookJson.Parse(project.ToJson());     // a copy: the book being edited is not touched
        string id = string.IsNullOrEmpty(book.Id) ? "my-book" : book.Id;
        Directory.CreateDirectory(destinationParent);
        string final = Path.Combine(Path.GetFullPath(destinationParent), id);
        if (Path.GetFullPath(final).TrimEnd(Path.DirectorySeparatorChar) == project.Folder.TrimEnd(Path.DirectorySeparatorChar))
            throw new IOException("Export into a different folder from the book itself.");
        string work = final + ".exporting";
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);
        var notes = new List<string>();
        long before = 0;
        var done = new Dictionary<string, string>(StringComparer.Ordinal);   // source name -> exported name

        string Out(string name) => Path.Combine(work, name.Replace('/', Path.DirectorySeparatorChar));
        string Copy(string name)
        {
            if (done.TryGetValue(name, out string? already)) return already;
            string src = project.PathOf(name) ?? throw new IOException("A file name leaves the book's folder: " + name);
            if (!File.Exists(src)) { notes.Add("Missing, left out: " + name); return done[name] = name; }
            before += new FileInfo(src).Length;
            Directory.CreateDirectory(Path.GetDirectoryName(Out(name))!);
            File.Copy(src, Out(name), true);
            return done[name] = name;
        }
        string Picture(string name, int maxSide, bool jpeg)
        {
            if (done.TryGetValue(name, out string? already)) return already;
            string? src = project.PathOf(name);
            if (src is null || !File.Exists(src)) { notes.Add("Missing, left out: " + name); return done[name] = name; }
            var size = Pictures.Size(src);
            bool alpha = Pictures.HasAlpha(src);
            bool shrink = size is (int w, int h) && maxSide > 0 && Math.Max(w, h) > maxSide;
            bool convert = jpeg && !alpha && Path.GetExtension(name).ToLowerInvariant() is ".png" or ".webp";
            if (!shrink && !convert) return Copy(name);
            before += new FileInfo(src).Length;
            string outName = convert ? Path.ChangeExtension(name, ".jpg") : name;
            while (done.ContainsValue(outName) || File.Exists(Out(outName))) outName = Path.ChangeExtension(outName, null) + "-1" + Path.GetExtension(outName);
            var made = Pictures.SaveSmaller(src, Out(outName), shrink ? maxSide : 0, options.JpegQuality);
            if (shrink) notes.Add($"{name}: made smaller, {size!.Value.Width} x {size.Value.Height} to {made.Width} x {made.Height}.");
            return done[name] = outName;
        }

        string coverSource = book.Cover;
        for (int i = 0; i < book.Pages.Count; i++)
        {
            Page p = book.Pages[i];
            progress?.Report($"Page {i + 1} of {book.Pages.Count}");
            p.Spread = null;                                   // the editor's note of how the spread was made
            if (p.Tiles is TileInfo tiles)
            {
                // Already cut into tiles: copy the overview and every tile as they are.
                p.Image = Copy(p.Image);
                if (BookProject.TilesFolder(tiles) is string tf && Directory.Exists(Path.Combine(project.Folder, tf)))
                {
                    foreach (string f in Directory.EnumerateFiles(Path.Combine(project.Folder, tf), "*", SearchOption.AllDirectories)) Copy(project.Relative(f));
                }
                else notes.Add($"Page {i + 1}: its tiles are missing.");
                continue;
            }
            string? src = project.PathOf(p.Image);
            var size = src is not null && File.Exists(src) ? Pictures.Size(src) : null;
            if (p.Explore && size is (int w, int h) && options.PageMaxSide > 0 && Math.Max(w, h) > options.PageMaxSide)
            {
                // A big scene to explore: cut into tiles so a phone loads only what the child is looking at.
                before += new FileInfo(src!).Length;
                string stem = Path.ChangeExtension(p.Image, null).Replace('/', '-');
                var cut = Pictures.CutTiles(src!, work, stem, maxSide: 0);
                notes.Add($"Page {i + 1}: a big scene ({w} x {h}), cut into tiles.");
                done[p.Image] = cut.Overview;
                p.Image = cut.Overview;
                p.Tiles = cut.Tiles;
                continue;
            }
            p.Image = Picture(p.Image, options.PageMaxSide, options.PagesAsJpeg);
        }
        if (!string.IsNullOrEmpty(coverSource))
            book.Cover = done.TryGetValue(coverSource, out string? coverName) ? coverName : Picture(coverSource, options.CoverMaxSide, options.PagesAsJpeg);

        // Sounds, recordings and pictures on top that are missing are left out of the exported book, so the
        // apps never look for a file that isn't there. (Check book lists them.)
        bool Present(string name) => project.Has(name);
        foreach (Page p in book.Pages)
        {
            p.Layers.RemoveAll(l => { bool gone = !Present(l.Image); if (gone) notes.Add("Missing, left out: " + l.Image); return gone; });
            foreach (Layer l in p.Layers) l.Image = Picture(l.Image, options.LayerMaxSide, jpeg: false);
            foreach (Stop s in p.Stops)
            {
                if (!string.IsNullOrEmpty(s.Sound)) s.Sound = Present(s.Sound) ? Copy(s.Sound) : Note(s.Sound);
                if (s.Voice is Voice v) { if (Present(v.File)) v.File = Copy(v.File); else { Note(v.File); s.Voice = null; } }
            }
            foreach (Hotspot h in p.Hotspots) if (!string.IsNullOrEmpty(h.Sound)) h.Sound = Present(h.Sound) ? Copy(h.Sound) : Note(h.Sound);
        }
        string? Note(string name) { notes.Add("Missing, left out: " + name); return null; }

        File.WriteAllText(Path.Combine(work, "book.json"), BookJson.ToJsonString(book));
        if (Directory.Exists(final)) Directory.Delete(final, true);
        Directory.Move(work, final);

        string? zip = null;
        if (options.Zip)
        {
            progress?.Report("Packing the zip file");
            zip = final + ".zip";
            if (File.Exists(zip)) File.Delete(zip);
            // The zip holds the book's folder, so unzipping it anywhere gives a folder named after the book.
            ZipFile.CreateFromDirectory(final, zip, CompressionLevel.Optimal, includeBaseDirectory: true);
        }
        long after = Directory.EnumerateFiles(final, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
        int files = Directory.EnumerateFiles(final, "*", SearchOption.AllDirectories).Count();
        return new ExportResult(final, zip, files, before, after, notes);
    }

    /// <summary>
    /// Put an exported book into a reader content folder (the repo's content/, or the one beside
    /// BookReader.exe): copied to content/books/&lt;id&gt; and added to the end of library.json if it isn't
    /// listed. The phone app packs content/books when it is built, so it has the book from its next build.
    /// </summary>
    public static string AddToLibrary(string exportedFolder, string contentFolder)
    {
        string id = Path.GetFileName(Path.TrimEndingDirectorySeparator(exportedFolder));
        string target = Path.Combine(contentFolder, "books", id);
        if (Directory.Exists(target)) Directory.Delete(target, true);
        CopyFolder(exportedFolder, target);

        string libraryFile = Path.Combine(contentFolder, "library.json");
        JsonObject library = File.Exists(libraryFile) && JsonNode.Parse(File.ReadAllText(libraryFile)) is JsonObject o ? o : new JsonObject();
        if (library["books"] is not JsonArray books) library["books"] = books = new JsonArray();
        if (!books.Any(b => b is JsonValue v && v.TryGetValue(out string? s) && s == id)) books.Add((JsonNode)JsonValue.Create(id));
        File.WriteAllText(libraryFile, library.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
        return target;
    }

    static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), true);
    }

    /// <summary>A size in bytes as people say it: "12.4 MB".</summary>
    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} bytes",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.0} MB",
    };
}
