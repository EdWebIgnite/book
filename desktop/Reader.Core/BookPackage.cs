using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Reader.Core;

/// <summary>
/// A book as one file, for sending to a phone: a zip holding the book's folder, named with the
/// <see cref="Extension"/> ending. The Book Editor makes these when it exports. The apps take them in
/// with <see cref="Import(string, string, BookPackage.Limits?)"/>, which unpacks them into a folder of
/// their own laid out like the content folder (library.json and books/&lt;id&gt;), so the shelf can list
/// them the same way as the books that come with the app.
///
/// A book file can come from anywhere (a chat, an email, a download), so nothing in it is trusted:
/// no name may lead out of the book's folder, sizes are limited while unpacking (not just as the zip
/// claims them), and the book must read properly and have its pictures before it is put on the shelf.
/// </summary>
public static class BookPackage
{
    /// <summary>The ending of a book file.</summary>
    public const string Extension = ".picturebook";

    /// <summary>How big a book file may be once unpacked.</summary>
    public sealed record Limits
    {
        /// <summary>All the files together. The sample books are 2 to 6 MB; a big book with tiles and voices is a few hundred.</summary>
        public long MaxTotalBytes { get; init; } = 1536L * 1024 * 1024;
        /// <summary>Any one file.</summary>
        public long MaxFileBytes { get; init; } = 400L * 1024 * 1024;
        /// <summary>How many files. A tiled scene is a few hundred tiles.</summary>
        public int MaxFiles { get; init; } = 20_000;

        public static readonly Limits Default = new();
    }

    /// <summary>What happened. <see cref="Message"/> is ready to show to a parent as it is.</summary>
    public sealed record Result(bool Ok, string Message, string? Id = null, string? Title = null, string? Folder = null, bool Replaced = false)
    {
        public static Result Fail(string message) => new(false, message);
    }

    sealed class Refusal(string message) : Exception(message);

    // ---------------------------------------------------------------- making one

    /// <summary>Pack a book's folder into a book file. The file holds the folder itself, so unzipping it gives a folder named after the book.</summary>
    public static void Create(string bookFolder, string packagePath)
    {
        if (File.Exists(packagePath)) File.Delete(packagePath);
        ZipFile.CreateFromDirectory(bookFolder, packagePath, CompressionLevel.Optimal, includeBaseDirectory: true);
    }

    /// <summary>True when the file name looks like a book file (or a zip, which is what one is).</summary>
    public static bool LooksLikePackage(string? path) =>
        path is not null && (path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    // ---------------------------------------------------------------- taking one in

    /// <summary>
    /// Unpack a book file into <paramref name="importRoot"/> and add it to that folder's library.json. A
    /// book with the same id as one already there replaces it (a newer version of the same book). Nothing
    /// is changed unless the whole book unpacks and checks out.
    /// </summary>
    public static Result Import(string packagePath, string importRoot, Limits? limits = null)
    {
        try
        {
            using FileStream file = File.OpenRead(packagePath);
            return Import(file, importRoot, limits, Path.GetFileNameWithoutExtension(packagePath));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Result.Fail(Strings.T("import.cantRead"));
        }
    }

    /// <summary>The same, from a stream that can seek (a file, or a copy in memory).</summary>
    public static Result Import(Stream package, string importRoot, Limits? limits = null, string? fileName = null)
    {
        limits ??= Limits.Default;
        string? incoming = null;
        string prefix = "";
        try
        {
            ZipArchive zip;
            try { zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true); }
            catch (InvalidDataException) { return Result.Fail(NotABook); }
            using (zip)
            {
                if (zip.Entries.Count > limits.MaxFiles) return Result.Fail(Strings.T("import.tooManyFiles"));
                prefix = FindBook(zip);

                Directory.CreateDirectory(importRoot);
                // Left over from a time the app was stopped half way through.
                foreach (string stale in Directory.EnumerateDirectories(importRoot, ".incoming-*").Concat(Directory.EnumerateDirectories(importRoot, ".old-*"))) TryDelete(stale);
                incoming = Path.Combine(importRoot, ".incoming-" + Guid.NewGuid().ToString("N")[..12]);
                Directory.CreateDirectory(incoming);
                Unpack(zip, prefix, incoming, limits);
            }

            Book book;
            try { book = BookJson.Load(incoming); }
            catch (JsonException) { return Result.Fail(Strings.T("import.damaged")); }
            if (book.Pages.Count == 0) return Result.Fail(Strings.T("import.noPages"));
            List<string> missing = MissingPictures(book, incoming);
            if (missing.Count > 0)
            {
                string list = string.Join(", ", missing.Take(3)) + (missing.Count > 3 ? $" and {missing.Count - 3} more" : "");
                return Result.Fail(Strings.T("import.missing", list));
            }

            string id = SafeId(book.Id) ?? SafeId(prefix.TrimEnd('/')) ?? SafeId(fileName) ?? "book";
            string title = string.IsNullOrWhiteSpace(book.Title) ? id : book.Title;
            string books = Path.Combine(importRoot, "books");
            Directory.CreateDirectory(books);
            string target = Path.Combine(books, id);
            bool replaced = Directory.Exists(target);
            if (replaced)
            {
                string old = Path.Combine(importRoot, ".old-" + Guid.NewGuid().ToString("N")[..12]);
                Directory.Move(target, old);
                Directory.Move(incoming, target);
                TryDelete(old);
            }
            else Directory.Move(incoming, target);
            incoming = null;
            AddToLibrary(importRoot, id);
            return new Result(true, Strings.T(replaced ? "import.updated" : "import.added", title), id, title, target, replaced);
        }
        catch (Refusal r) { return Result.Fail(r.Message); }
        catch (InvalidDataException) { return Result.Fail(NotABook); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Result.Fail(Strings.T("import.noRoom"));
        }
        finally
        {
            if (incoming is not null) TryDelete(incoming);
        }
    }

    static string NotABook => Strings.T("import.notABook");

    /// <summary>Where book.json is: at the top of the zip, or in the one folder at the top. Returns that folder, ending in "/", or "".</summary>
    static string FindBook(ZipArchive zip)
    {
        var found = new List<string>();
        foreach (ZipArchiveEntry e in zip.Entries)
        {
            string name = e.FullName.Replace('\\', '/');
            if (name.Equals("book.json", StringComparison.OrdinalIgnoreCase)) return "";
            string[] parts = name.Split('/');
            if (parts.Length == 2 && parts[1].Equals("book.json", StringComparison.OrdinalIgnoreCase) && parts[0].Length > 0) found.Add(parts[0] + "/");
        }
        if (found.Count == 0) throw new Refusal(Strings.T("import.noBook"));
        if (found.Count > 1) throw new Refusal(Strings.T("import.twoBooks"));
        return found[0];
    }

    static void Unpack(ZipArchive zip, string prefix, string destination, Limits limits)
    {
        long total = 0;
        var buffer = new byte[81920];
        foreach (ZipArchiveEntry e in zip.Entries)
        {
            string name = e.FullName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string rel = name[prefix.Length..];
            if (rel.Length == 0 || rel.EndsWith('/')) continue;                         // a folder
            if (!SafeName(rel)) throw new Refusal(Strings.T("import.oddNames"));
            int unixType = (e.ExternalAttributes >> 16) & 0xF000;
            if (unixType == 0xA000) throw new Refusal(Strings.T("import.links"));
            if (e.Length > limits.MaxFileBytes) throw new Refusal(TooBig);
            string? path = BookLibrary.SafePath(destination, rel);
            if (path is null) throw new Refusal(Strings.T("import.oddNames"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using Stream input = e.Open();
            using FileStream output = File.Create(path);
            long written = 0;
            int n;
            // The sizes a zip claims can't be trusted, so what is really unpacked is counted.
            while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                written += n;
                total += n;
                if (written > limits.MaxFileBytes || total > limits.MaxTotalBytes) throw new Refusal(TooBig);
                output.Write(buffer, 0, n);
            }
        }
    }

    static string TooBig => Strings.T("import.tooBigBook");

    /// <summary>A name inside the book: no drive, no going up, no starting at the top of the disk.</summary>
    static bool SafeName(string rel)
    {
        if (rel.StartsWith('/') || rel.Contains(':') || rel.Contains('\0')) return false;
        foreach (string part in rel.Split('/'))
        {
            if (part.Length == 0 || part == "." || part == "..") return false;
        }
        return true;
    }

    /// <summary>The pictures a book can't do without (its pages, cover and pictures on top) that aren't there or aren't pictures.</summary>
    public static List<string> MissingPictures(Book book, string folder)
    {
        var missing = new List<string>();
        void Need(string? name)
        {
            if (string.IsNullOrEmpty(name) || missing.Contains(name)) return;
            string? path = BookLibrary.SafePath(folder, name);
            if (path is null || !File.Exists(path) || !IsPicture(path)) missing.Add(name);
        }
        if (!string.IsNullOrEmpty(book.Cover)) Need(book.Cover);
        foreach (Page p in book.Pages)
        {
            Need(p.Image);
            foreach (Layer l in p.Layers) Need(l.Image);
        }
        return missing;
    }

    /// <summary>Starts like a JPEG, PNG or WebP file.</summary>
    static bool IsPicture(string path)
    {
        Span<byte> head = stackalloc byte[12];
        using FileStream f = File.OpenRead(path);
        int n = f.Read(head);
        if (n >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return true;
        if (n >= 8 && head[0] == 0x89 && head[1] == (byte)'P' && head[2] == (byte)'N' && head[3] == (byte)'G') return true;
        if (n >= 12 && head[0] == (byte)'R' && head[1] == (byte)'I' && head[2] == (byte)'F' && head[3] == (byte)'F'
            && head[8] == (byte)'W' && head[9] == (byte)'E' && head[10] == (byte)'B' && head[11] == (byte)'P') return true;
        return false;
    }

    /// <summary>A folder name made from a book's id: lower case letters, digits and dashes. Null when nothing is left.</summary>
    public static string? SafeId(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var sb = new System.Text.StringBuilder();
        foreach (char c in text.Trim().ToLowerInvariant()) sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        string id = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        if (id.Length > 60) id = id[..60].Trim('-');
        return id.Length == 0 ? null : id;
    }

    // ---------------------------------------------------------------- the books taken in

    /// <summary>The books taken in so far, in the order they were added. Damaged ones are left out.</summary>
    public static List<ShelfEntry> List(string importRoot)
    {
        if (!File.Exists(Path.Combine(importRoot, "library.json"))) return new List<ShelfEntry>();
        try { return BookLibrary.List(importRoot); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new List<ShelfEntry>(); }
    }

    /// <summary>Take a book back off the shelf: its folder is deleted and it leaves library.json. False if there was no such book.</summary>
    public static bool Remove(string importRoot, string id)
    {
        if (SafeId(id) != id) return false;
        string folder = Path.Combine(importRoot, "books", id);
        bool had = Directory.Exists(folder);
        if (had) Directory.Delete(folder, recursive: true);
        string libraryFile = Path.Combine(importRoot, "library.json");
        if (File.Exists(libraryFile) && JsonNode.Parse(File.ReadAllText(libraryFile)) is JsonObject lib && lib["books"] is JsonArray list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is JsonValue v && v.TryGetValue(out string? s) && s == id) { list.RemoveAt(i); had = true; }
            }
            WriteLibrary(libraryFile, lib);
        }
        return had;
    }

    static void AddToLibrary(string importRoot, string id)
    {
        string libraryFile = Path.Combine(importRoot, "library.json");
        JsonObject lib = new();
        try { if (File.Exists(libraryFile) && JsonNode.Parse(File.ReadAllText(libraryFile)) is JsonObject o) lib = o; }
        catch (JsonException) { /* a damaged list is started again; the books' folders are still there */ }
        if (lib["books"] is not JsonArray list) lib["books"] = list = new JsonArray();
        if (!list.Any(b => b is JsonValue v && v.TryGetValue(out string? s) && s == id)) list.Add((JsonNode)JsonValue.Create(id));
        WriteLibrary(libraryFile, lib);
    }

    static void WriteLibrary(string file, JsonObject lib)
    {
        string temp = file + ".saving";
        File.WriteAllText(temp, lib.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        File.Move(temp, file, overwrite: true);
    }

    static void TryDelete(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* left for next time */ }
    }
}
