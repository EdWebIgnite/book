using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Reader.Core;

namespace Editor.Core;

/// <summary>
/// Undo and redo for everything. Before each change the editor calls <see cref="Record"/>, which keeps
/// the whole book as it was (as book.json text: a book is a few kilobytes, so hundreds of steps cost
/// nothing). Typing in one box, or dragging one slider, counts as a single step.
/// </summary>
public sealed class History
{
    /// <summary>One remembered state: the book as text, what the change was called, and where the editor was.</summary>
    public sealed record Entry(string Json, string Label, int Page, string Selection);

    readonly List<Entry> _undo = new();
    readonly List<Entry> _redo = new();
    string? _lastKey;
    DateTime _lastTime;

    /// <summary>The most steps kept. Older ones are forgotten.</summary>
    public int Limit { get; set; } = 300;

    /// <summary>Changes with the same key this close together are one step (typing a caption, dragging a box).</summary>
    public TimeSpan MergeWindow { get; set; } = TimeSpan.FromSeconds(1.5);

    /// <summary>Lets tests move the clock.</summary>
    public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoLabel => _undo.Count > 0 ? _undo[^1].Label : null;
    public string? RedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;
    public int Count => _undo.Count;

    /// <summary>
    /// Remember the book as it is, just before a change. With a <paramref name="mergeKey"/>, a change
    /// straight after another with the same key adds nothing new: undo goes back to before both.
    /// </summary>
    public void Record(Book before, string label, int page = 0, string selection = "", string? mergeKey = null)
    {
        DateTime now = Clock();
        if (mergeKey is not null && mergeKey == _lastKey && now - _lastTime < MergeWindow && _undo.Count > 0)
        {
            _lastTime = now;
            _redo.Clear();
            return;
        }
        _lastKey = mergeKey;
        _lastTime = now;
        _undo.Add(new Entry(BookJson.ToJsonString(before), label, page, selection));
        if (_undo.Count > Limit) _undo.RemoveAt(0);
        _redo.Clear();
    }

    /// <summary>Stop the next change from being merged with the last one.</summary>
    public void Break() => _lastKey = null;

    /// <summary>Go back a step. Returns the book to show (a fresh copy) and where the editor was, or null.</summary>
    public (Book Book, Entry Entry)? Undo(Book current, int page, string selection) => Swap(_undo, _redo, current, page, selection);

    public (Book Book, Entry Entry)? Redo(Book current, int page, string selection) => Swap(_redo, _undo, current, page, selection);

    (Book, Entry)? Swap(List<Entry> from, List<Entry> to, Book current, int page, string selection)
    {
        if (from.Count == 0) return null;
        Entry e = from[^1];
        from.RemoveAt(from.Count - 1);
        to.Add(new Entry(BookJson.ToJsonString(current), e.Label, page, selection));
        _lastKey = null;
        return (BookJson.Parse(e.Json), e);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _lastKey = null;
    }
}

/// <summary>
/// The safety net. While a book has changes that aren't saved, the editor writes a recovery copy every
/// little while, in its own folder (not in the book's folder, and never over book.json). If the editor
/// or the computer stops before the book is saved, opening the book again offers the recovery copy.
/// </summary>
public sealed class Recovery
{
    public Recovery(string? folder = null) => Folder = folder ?? Path.Combine(EditorPaths.AppData, "recovery");

    public string Folder { get; }

    string FileFor(string bookFolder)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(bookFolder).ToLowerInvariant()));
        return Path.Combine(Folder, Convert.ToHexString(hash, 0, 8).ToLowerInvariant() + ".json");
    }

    /// <summary>Keep a recovery copy of the book. Returns false if it couldn't be written (the editor carries on).</summary>
    public bool Write(BookProject project)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var o = new JsonObject
            {
                ["folder"] = project.Folder,
                ["saved"] = DateTime.UtcNow.ToString("o"),
                ["book"] = JsonNode.Parse(project.ToJson()),
            };
            string file = FileFor(project.Folder), temp = file + ".tmp";
            File.WriteAllText(temp, o.ToJsonString());
            File.Move(temp, file, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>A recovery copy for this book folder that holds something different from book.json, or null.</summary>
    public (Book Book, DateTime Saved)? Find(string bookFolder)
    {
        try
        {
            string file = FileFor(bookFolder);
            if (!File.Exists(file)) return null;
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject o || o["book"] is not JsonObject b) return null;
            Book book = BookJson.Parse(b);
            string onDisk = File.Exists(Path.Combine(bookFolder, "book.json")) ? File.ReadAllText(Path.Combine(bookFolder, "book.json")) : "";
            if (BookJson.ToJsonString(book) == onDisk) { Clear(bookFolder); return null; }
            DateTime saved = DateTime.TryParse(o["saved"]?.GetValue<string>(), null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t) ? t : File.GetLastWriteTimeUtc(file);
            return (book, saved);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException) { return null; }
    }

    public void Clear(string bookFolder)
    {
        try { File.Delete(FileFor(bookFolder)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>The books opened lately, newest first, for the Open menu.</summary>
public sealed class RecentBooks
{
    public RecentBooks(string? file = null) => File_ = file ?? Path.Combine(EditorPaths.AppData, "recent.json");

    readonly string File_;
    public int Most { get; set; } = 12;

    public List<string> Load()
    {
        try
        {
            if (!File.Exists(File_)) return new List<string>();
            return (JsonNode.Parse(File.ReadAllText(File_)) as JsonArray ?? new JsonArray())
                .Select(n => n?.GetValue<string>()).OfType<string>().ToList();
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException) { return new List<string>(); }
    }

    /// <summary>The list without folders that are gone.</summary>
    public List<string> Existing() => Load().Where(Directory.Exists).ToList();

    public void Add(string folder)
    {
        string full = Path.GetFullPath(folder);
        var list = Load().Where(f => !string.Equals(Path.GetFullPath(f), full, StringComparison.OrdinalIgnoreCase)).Prepend(full).Take(Most).ToList();
        Save(list);
    }

    public void Remove(string folder)
    {
        string full = Path.GetFullPath(folder);
        Save(Load().Where(f => !string.Equals(Path.GetFullPath(f), full, StringComparison.OrdinalIgnoreCase)).ToList());
    }

    void Save(List<string> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
            var a = new JsonArray();
            foreach (string f in list) a.Add((JsonNode)JsonValue.Create(f));
            File.WriteAllText(File_, a.ToJsonString());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>Where the editor keeps its own files: %APPDATA%\BookEditor on Windows, ~/.config/BookEditor on Linux.</summary>
public static class EditorPaths
{
    /// <summary>Tests and the screenshot hook point this somewhere else so they never touch the real files.</summary>
    public static string? Override { get; set; }

    public static string AppData
    {
        get
        {
            if (Override is not null) return Override;
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            if (string.IsNullOrEmpty(root)) root = Path.GetTempPath();
            return Path.Combine(root, "BookEditor");
        }
    }

    /// <summary>Where new books go unless the author picks somewhere else: Documents\Picture books.</summary>
    public static string DefaultBooksFolder
    {
        get
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.DoNotVerify);
            if (string.IsNullOrEmpty(docs)) docs = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(docs, "Picture books");
        }
    }
}
