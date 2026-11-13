using System.Text.Json;
using System.Text.Json.Nodes;

namespace Reader.Core;

/// <summary>One book on the shelf: enough to show its cover and open it.</summary>
public sealed record ShelfEntry(string Id, string Title, string Folder, string CoverPath);

/// <summary>
/// Finds the books in a content folder. The layout is the repo's: content/library.json lists the book
/// folders, and each book lives in content/books/&lt;id&gt;/ with its book.json and pictures.
/// </summary>
public static class BookLibrary
{
    /// <summary>
    /// Every book listed in library.json, in that order. A book whose folder or book.json is missing or
    /// damaged is skipped (and named in <paramref name="problems"/>), so one bad book never empties the shelf.
    /// </summary>
    public static List<ShelfEntry> List(string contentFolder, List<string>? problems = null)
    {
        var entries = new List<ShelfEntry>();
        string libraryFile = Path.Combine(contentFolder, "library.json");
        JsonNode? library = JsonNode.Parse(File.ReadAllText(libraryFile));
        if (library?["books"] is not JsonArray books) throw new JsonException("library.json should hold { \"books\": [ ... ] }.");
        foreach (JsonNode? item in books)
        {
            if (item is not JsonValue value || !value.TryGetValue(out string? id) || string.IsNullOrWhiteSpace(id)) continue;
            string folder = Path.Combine(contentFolder, "books", id);
            try
            {
                entries.Add(Describe(folder, id));
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                problems?.Add(id + ": " + e.Message);
            }
        }
        return entries;
    }

    /// <summary>The shelf entry for one book folder (any folder with a book.json in it).</summary>
    public static ShelfEntry Describe(string bookFolder, string? id = null)
    {
        Book book = BookJson.Load(bookFolder);
        id ??= !string.IsNullOrEmpty(book.Id) ? book.Id : Path.GetFileName(Path.TrimEndingDirectorySeparator(bookFolder));
        string title = !string.IsNullOrEmpty(book.Title) ? book.Title : id;
        return new ShelfEntry(id, title, bookFolder, Path.Combine(bookFolder, book.CoverOrFirstPage));
    }

    /// <summary>
    /// Where the books are. In order: the folder asked for, a "content" folder beside the program, then
    /// the nearest content/library.json found by walking up from the program's folder and from the
    /// current folder (which is how it works straight from the repo). Null when there is none.
    /// </summary>
    public static string? FindContent(string? asked, string programFolder, string currentFolder)
    {
        if (!string.IsNullOrWhiteSpace(asked))
        {
            string full = Path.GetFullPath(asked, currentFolder);
            return File.Exists(Path.Combine(full, "library.json")) ? full : null;
        }
        string beside = Path.Combine(programFolder, "content");
        if (File.Exists(Path.Combine(beside, "library.json"))) return beside;
        foreach (string start in new[] { programFolder, currentFolder })
        {
            for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, "library.json"))) return candidate;
            }
        }
        return null;
    }

    /// <summary>
    /// The full path of a file named in a book, or null if the name tries to leave the book's folder.
    /// Books can come from anywhere, so a name like "../../secret" must not be followed.
    /// </summary>
    public static string? SafePath(string bookFolder, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string root = Path.GetFullPath(bookFolder);
        string full = Path.GetFullPath(Path.Combine(root, name));
        string rootWithSlash = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(rootWithSlash, StringComparison.Ordinal) ? full : null;
    }
}
