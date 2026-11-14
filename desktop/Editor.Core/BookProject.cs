using System.Text;
using System.Text.RegularExpressions;
using Reader.Core;

namespace Editor.Core;

/// <summary>
/// A book being edited: its folder on disk and the book in memory. Every picture and sound the book
/// uses lives in the folder, so a picture added in the editor is copied in straight away. Saving only
/// has to write book.json, and nothing can go missing between "add" and "save".
/// </summary>
public sealed class BookProject
{
    BookProject(string folder, Book book)
    {
        Folder = Path.GetFullPath(folder);
        Book = book;
    }

    /// <summary>The book's folder, holding book.json and everything it uses.</summary>
    public string Folder { get; }

    /// <summary>The book as it is now. Undo and redo swap in a fresh copy, so don't hold on to it.</summary>
    public Book Book { get; set; }

    public string BookFile => Path.Combine(Folder, "book.json");

    /// <summary>The kinds of picture file the editor takes.</summary>
    public static readonly string[] PictureTypes = { ".jpg", ".jpeg", ".png", ".webp" };

    /// <summary>The kinds of sound file the editor takes.</summary>
    public static readonly string[] SoundTypes = { ".wav", ".mp3", ".ogg", ".flac" };

    public static bool IsPicture(string path) => PictureTypes.Contains(Path.GetExtension(path).ToLowerInvariant());
    public static bool IsSound(string path) => SoundTypes.Contains(Path.GetExtension(path).ToLowerInvariant());

    // ---------------------------------------------------------------- opening, making and saving

    /// <summary>
    /// Open a book folder. A folder with no book.json, but with pictures in it, becomes a new book with
    /// one page per picture (in name order, leaving out anything called cover).
    /// </summary>
    public static BookProject Open(string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("That folder isn't there any more: " + folder);
        string file = Path.Combine(folder, "book.json");
        if (File.Exists(file)) return new BookProject(folder, BookJson.Parse(File.ReadAllText(file)));

        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)));
        var book = new Book { Id = Slug.Make(name), Title = name, FocusStyle = FocusStyles.Vignette };
        var pictures = Directory.GetFiles(folder).Where(IsPicture)
            .Select(Path.GetFileName).OfType<string>()
            .OrderBy(n => n, NaturalOrder.Instance).ToList();
        string? cover = pictures.FirstOrDefault(n => Path.GetFileNameWithoutExtension(n).Equals("cover", StringComparison.OrdinalIgnoreCase));
        if (cover is not null) book.Cover = cover;
        foreach (string p in pictures.Where(p => p != cover)) book.Pages.Add(new Page { Image = p });
        return new BookProject(folder, book);
    }

    /// <summary>
    /// Start a new book in a new folder named after the title, inside <paramref name="parentFolder"/>.
    /// book.json is written at once, so the folder is a book from the start.
    /// </summary>
    public static BookProject Create(string parentFolder, string title)
    {
        string id = Slug.Make(title);
        if (id.Length == 0) id = "my-book";
        string folder = Path.Combine(parentFolder, id);
        for (int n = 2; Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any(); n++) folder = Path.Combine(parentFolder, $"{id}-{n}");
        Directory.CreateDirectory(folder);
        var project = new BookProject(folder, new Book { Id = Path.GetFileName(folder), Title = title, Author = "", FocusStyle = FocusStyles.Vignette });
        project.Save();
        return project;
    }

    /// <summary>What would be written to book.json now.</summary>
    public string ToJson()
    {
        if (string.IsNullOrEmpty(Book.Id)) Book.Id = Slug.Make(Book.Title) is { Length: > 0 } s ? s : "my-book";
        return BookJson.ToJsonString(Book);
    }

    /// <summary>
    /// Write book.json. The new text goes to a temporary file first and then takes the old one's place,
    /// so a crash or a full disk half way through can never leave a broken book. The version before is
    /// kept as book.json.bak.
    /// </summary>
    public void Save()
    {
        string json = ToJson();
        string temp = BookFile + ".saving";
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        if (File.Exists(BookFile))
        {
            string backup = BookFile + ".bak";
            File.Copy(BookFile, backup, overwrite: true);
            File.Move(temp, BookFile, overwrite: true);
        }
        else File.Move(temp, BookFile);
    }

    /// <summary>True when the book in memory differs from book.json on disk.</summary>
    public bool DiffersFromDisk()
    {
        try { return !File.Exists(BookFile) || File.ReadAllText(BookFile) != ToJson(); }
        catch (IOException) { return true; }
    }

    // ---------------------------------------------------------------- files in the folder

    /// <summary>The full path of a file the book names, or null when the name would leave the folder.</summary>
    public string? PathOf(string? name) => BookLibrary.SafePath(Folder, name);

    public bool Has(string? name) => PathOf(name) is string p && File.Exists(p);

    /// <summary>
    /// Bring a file into the book's folder and return the name the book should use for it. A file that
    /// is already inside the folder is used where it is. Otherwise it is copied in, into
    /// <paramref name="subfolder"/> if given, under a tidy name that no other file has.
    /// </summary>
    public string Import(string sourcePath, string? subfolder = null, string? preferredName = null)
    {
        string full = Path.GetFullPath(sourcePath);
        if (Inside(full)) return Relative(full);
        string name = UniqueName(preferredName ?? Path.GetFileName(full), subfolder);
        string target = PathOf(name)!;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(full, target);
        return name;
    }

    /// <summary>
    /// A file name, tidied (spaces and odd characters become dashes) and made unique in the folder. It is
    /// only reserved once something is written under it.
    /// </summary>
    public string UniqueName(string wanted, string? subfolder = null)
    {
        string ext = Path.GetExtension(wanted).ToLowerInvariant();
        if (ext == ".jpeg") ext = ".jpg";
        string stem = Regex.Replace(Path.GetFileNameWithoutExtension(wanted), @"[^\w\-]+", "-").Trim('-');
        if (stem.Length == 0) stem = "file";
        string prefix = string.IsNullOrEmpty(subfolder) ? "" : subfolder.Trim('/') + "/";
        string name = prefix + stem + ext;
        for (int n = 2; File.Exists(PathOf(name)!) || Directory.Exists(PathOf(name)!); n++) name = $"{prefix}{stem}-{n}{ext}";
        return name;
    }

    bool Inside(string fullPath)
    {
        string root = Folder.EndsWith(Path.DirectorySeparatorChar) ? Folder : Folder + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.Ordinal);
    }

    /// <summary>A path inside the folder as the book writes it: relative, with forward slashes.</summary>
    public string Relative(string fullPath) => Path.GetRelativePath(Folder, fullPath).Replace('\\', '/');

    /// <summary>Every file the book names, relative to its folder. Tiles are listed by their folder, ending in "/".</summary>
    public SortedSet<string> UsedFiles()
    {
        var used = new SortedSet<string>(StringComparer.Ordinal);
        void Add(string? n) { if (!string.IsNullOrEmpty(n)) used.Add(n.Replace('\\', '/')); }
        Book b = Book;
        Add(b.Cover);
        foreach (Page p in b.Pages)
        {
            Add(p.Image);
            if (p.Spread is SpreadSource s) { Add(s.Left); Add(s.Right); }
            foreach (Stop st in p.Stops) { Add(st.Sound); Add(st.Voice?.File); }
            foreach (Hotspot h in p.Hotspots) Add(h.Sound);
            foreach (Layer l in p.Layers) Add(l.Image);
            if (p.Tiles is TileInfo t && TilesFolder(t) is string tf) used.Add(tf);
        }
        return used;
    }

    /// <summary>The folder a tiled scene's tiles are in ("page1_tiles/"), from its path pattern.</summary>
    public static string? TilesFolder(TileInfo t)
    {
        int brace = t.Path.IndexOf('{');
        if (brace <= 0) return null;
        string head = t.Path[..brace];
        int slash = head.LastIndexOf('/');
        return slash > 0 ? head[..(slash + 1)] : null;
    }

    /// <summary>
    /// Files in the folder that the book doesn't use: pictures replaced, recordings re-done. The book's own
    /// files (book.json and its backup) and hidden files don't count.
    /// </summary>
    public List<string> UnusedFiles()
    {
        SortedSet<string> used = UsedFiles();
        var tileFolders = used.Where(u => u.EndsWith('/')).ToList();
        var unused = new List<string>();
        foreach (string full in Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories))
        {
            string rel = Relative(full);
            string fileName = Path.GetFileName(rel);
            if (fileName.StartsWith('.') || rel.StartsWith("_unused/", StringComparison.Ordinal)) continue;
            if (fileName is "book.json" or "book.json.bak" or "book.json.saving") continue;
            if (used.Contains(rel) || tileFolders.Any(t => rel.StartsWith(t, StringComparison.Ordinal))) continue;
            unused.Add(rel);
        }
        unused.Sort(NaturalOrder.Instance);
        return unused;
    }

    /// <summary>
    /// Move files the book doesn't use into an "_unused" folder inside the book's folder. Nothing is
    /// deleted, so a file moved by mistake can be put back by hand. Returns how many were moved.
    /// </summary>
    public int MoveUnusedAside()
    {
        int moved = 0;
        foreach (string rel in UnusedFiles())
        {
            string from = PathOf(rel)!, to = Path.Combine(Folder, "_unused", rel);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            if (File.Exists(to)) to = Path.Combine(Path.GetDirectoryName(to)!, Path.GetFileNameWithoutExtension(to) + "-" + DateTime.Now.ToString("HHmmss") + Path.GetExtension(to));
            File.Move(from, to);
            moved++;
        }
        return moved;
    }
}

/// <summary>Book ids and folder names: lower case, dashes between words.</summary>
public static class Slug
{
    public static string Make(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder();
        foreach (char c in text)
        {
            char plain = Plain(char.ToLowerInvariant(c));
            sb.Append(char.IsAsciiLetterOrDigit(plain) ? plain : '-');
        }
        return Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
    }

    // The programs run without the culture tables, so accents are taken off by hand for the common letters.
    const string Accented = "àáâãäåāèéêëēìíîïīòóôõöøōùúûüūýÿçñß";
    const string Unaccented = "aaaaaaaeeeeeiiiiioooooooouuuuuyycns";

    static char Plain(char c)
    {
        int i = Accented.IndexOf(c);
        return i >= 0 ? Unaccented[i] : c;
    }
}

/// <summary>Sorts "page2" before "page10", the way people expect.</summary>
public sealed class NaturalOrder : IComparer<string>
{
    public static readonly NaturalOrder Instance = new();

    public int Compare(string? a, string? b)
    {
        if (a is null || b is null) return string.Compare(a, b, StringComparison.Ordinal);
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                string na = a[si..i].TrimStart('0'), nb = b[sj..j].TrimStart('0');
                int c = na.Length != nb.Length ? na.Length.CompareTo(nb.Length) : string.CompareOrdinal(na, nb);
                if (c != 0) return c;
            }
            else
            {
                int c = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                if (c != 0) return c;
                i++; j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
