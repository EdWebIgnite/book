using System.Text.Json;
using System.Text.Json.Nodes;

namespace Reader.Core;

/// <summary>Where a child is in one book, and whether they have read it to the end.</summary>
public sealed class BookProgress
{
    /// <summary>The page they were on (0 is the first), or -1 when there is no place to carry on from.</summary>
    public int Page { get; set; } = -1;
    /// <summary>The stop on that page, as the engine numbers them (see <see cref="Step"/>).</summary>
    public int Stop { get; set; }
    /// <summary>How many pages the book had when the place was kept, for "Page 7 of 12".</summary>
    public int PageCount { get; set; }
    public bool Finished { get; set; }
    public int TimesFinished { get; set; }
    /// <summary>When the book was last read (UTC), so the shelf can offer the latest one to carry on with.</summary>
    public DateTime LastRead { get; set; }

    /// <summary>Part of the way through: there is a place to carry on from.</summary>
    public bool Partway => Page >= 0;
}

/// <summary>One child: a nickname, a drawn picture, an age band and their place in each book. Never a photo, a birthday or an account.</summary>
public sealed class ChildProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Which of the drawn pictures (0 to <see cref="AppState.AvatarCount"/> - 1).</summary>
    public int Avatar { get; set; }
    /// <summary>The age band, one of <see cref="ReaderSettings.AgePresets"/>' keys.</summary>
    public string Age { get; set; } = "3-5";
    public Dictionary<string, BookProgress> Books { get; } = new();
    /// <summary>The days this child read on (local dates), newest last, for the grown-ups' "read on 3 days this week". Only the last few months are kept.</summary>
    public List<DateOnly> Days { get; } = new();

    public BookProgress? For(string bookId) => Books.TryGetValue(bookId, out BookProgress? p) ? p : null;

    /// <summary>How many books this child has read to the end.</summary>
    public int FinishedCount => Books.Values.Count(b => b.Finished);

    /// <summary>How many books are part of the way through.</summary>
    public int StartedCount => Books.Values.Count(b => b.Partway);

    /// <summary>Days read in the seven days up to and including <paramref name="today"/>.</summary>
    public int DaysThisWeek(DateOnly today) => Days.Count(d => d <= today && d > today.AddDays(-7));

    internal void ReadOn(DateOnly day)
    {
        if (Days.Count > 0 && Days[^1] == day) return;
        if (!Days.Contains(day)) Days.Add(day);
        Days.Sort();
        while (Days.Count > 120) Days.RemoveAt(0);
    }
}

/// <summary>
/// What the app remembers besides the reading settings: the children's profiles and their progress,
/// whether the welcome has been seen, the app's language and bedtime colours. Kept in one small file,
/// profiles.json, beside settings.json, on the device only.
/// </summary>
public sealed class AppState
{
    public const int MaxProfiles = 5, AvatarCount = 8, MaxNameLength = 12, CurrentVersion = 1;

    /// <summary>The first-run welcome has been seen (or skipped).</summary>
    public bool Welcomed { get; set; }
    /// <summary>"auto" follows the device; otherwise a language code from <see cref="Strings.Languages"/>.</summary>
    public string Language { get; set; } = "auto";
    public bool Bedtime { get; set; }
    /// <summary>The id of the child who is reading.</summary>
    public string Current { get; set; } = "";
    public List<ChildProfile> Profiles { get; } = new();

    public ChildProfile? CurrentProfile => Profiles.FirstOrDefault(p => p.Id == Current) ?? Profiles.FirstOrDefault();

    public bool CanAdd => Profiles.Count < MaxProfiles;

    /// <summary>A name as it is kept: trimmed, one line, no longer than fits under a picture.</summary>
    public static string CleanName(string? name)
    {
        string s = string.Join(" ", (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var info = new System.Globalization.StringInfo(s);
        return info.LengthInTextElements > MaxNameLength ? info.SubstringByTextElements(0, MaxNameLength).TrimEnd() : s;
    }

    /// <summary>Add a child and make them the one reading. Null when there are already five.</summary>
    public ChildProfile? Add(string? name, int avatar, string age = "3-5")
    {
        if (!CanAdd) return null;
        int n = 1;
        while (Profiles.Any(p => p.Id == "p" + n)) n++;
        var profile = new ChildProfile { Id = "p" + n, Name = CleanName(name), Avatar = Math.Clamp(avatar, 0, AvatarCount - 1), Age = ReaderSettings.AgePresets.ContainsKey(age) ? age : "3-5" };
        Profiles.Add(profile);
        Current = profile.Id;
        return profile;
    }

    /// <summary>The first picture nobody has yet, so two children start out looking different.</summary>
    public int FreeAvatar()
    {
        for (int i = 0; i < AvatarCount; i++) if (Profiles.All(p => p.Avatar != i)) return i;
        return 0;
    }

    /// <summary>Remove a child and their progress. The next child, if any, becomes the one reading.</summary>
    public void Remove(string id)
    {
        Profiles.RemoveAll(p => p.Id == id);
        if (Current == id) Current = Profiles.FirstOrDefault()?.Id ?? "";
    }

    // ---------------------------------------------------------------- progress

    /// <summary>
    /// Keep the child's place in a book. The very start of a book (its cover, or step 0) is not a place
    /// to carry on from, so it clears the place instead.
    /// </summary>
    public void KeepPlace(string bookId, int stepIndex, int page, int stop, int pageCount, DateTime nowUtc)
    {
        if (CurrentProfile is not ChildProfile child || string.IsNullOrEmpty(bookId)) return;
        if (!child.Books.TryGetValue(bookId, out BookProgress? p)) child.Books[bookId] = p = new BookProgress();
        p.LastRead = nowUtc;
        p.PageCount = pageCount;
        child.ReadOn(DateOnly.FromDateTime(nowUtc.ToLocalTime()));
        if (stepIndex <= 0 || stop == Step.Cover) { p.Page = -1; p.Stop = 0; }
        else { p.Page = page; p.Stop = stop; }
    }

    /// <summary>The child reached The End: the book is finished and there is no place to carry on from.</summary>
    public void MarkFinished(string bookId, int pageCount, DateTime nowUtc)
    {
        if (CurrentProfile is not ChildProfile child || string.IsNullOrEmpty(bookId)) return;
        if (!child.Books.TryGetValue(bookId, out BookProgress? p)) child.Books[bookId] = p = new BookProgress();
        p.Finished = true;
        p.TimesFinished++;
        p.Page = -1;
        p.Stop = 0;
        p.PageCount = pageCount;
        p.LastRead = nowUtc;
        child.ReadOn(DateOnly.FromDateTime(nowUtc.ToLocalTime()));
    }

    /// <summary>Make another child the one reading. False when there is no such child.</summary>
    public bool Choose(string id)
    {
        if (Profiles.All(p => p.Id != id)) return false;
        Current = id;
        return true;
    }

    /// <summary>The book to offer under "Carry on reading": the one read most recently that is part of the way through and still on the shelf.</summary>
    public string? CarryOn(IEnumerable<string> shelf)
    {
        if (CurrentProfile is not ChildProfile child) return null;
        return shelf.Where(id => child.For(id) is { Partway: true })
            .OrderByDescending(id => child.For(id)!.LastRead).FirstOrDefault();
    }

    /// <summary>
    /// "Read next" at The End: the next book along the shelf from this one (going round to the start)
    /// that the child has not finished. Null when every other book is finished.
    /// </summary>
    public string? ReadNext(IReadOnlyList<string> shelf, string currentId)
    {
        ChildProfile? child = CurrentProfile;
        int at = -1;
        for (int i = 0; i < shelf.Count; i++) if (shelf[i] == currentId) { at = i; break; }
        for (int k = 1; k <= shelf.Count; k++)
        {
            string id = shelf[(at + k + shelf.Count) % shelf.Count];
            if (id == currentId) continue;
            if (child?.For(id) is not { Finished: true }) return id;
        }
        return null;
    }

    // ---------------------------------------------------------------- JSON

    public JsonObject ToJson()
    {
        var profiles = new JsonArray();
        foreach (ChildProfile p in Profiles)
        {
            var books = new JsonObject();
            foreach (var (id, b) in p.Books)
            {
                var o = new JsonObject { ["finished"] = b.Finished, ["timesFinished"] = b.TimesFinished, ["pageCount"] = b.PageCount, ["lastRead"] = b.LastRead.ToString("o", System.Globalization.CultureInfo.InvariantCulture) };
                if (b.Partway) { o["page"] = b.Page; o["stop"] = b.Stop; }
                books[id] = o;
            }
            var days = new JsonArray();
            foreach (DateOnly d in p.Days) days.Add((JsonNode)d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            profiles.Add((JsonNode)new JsonObject { ["id"] = p.Id, ["name"] = p.Name, ["avatar"] = p.Avatar, ["age"] = p.Age, ["books"] = books, ["days"] = days });
        }
        return new JsonObject
        {
            ["version"] = CurrentVersion, ["welcomed"] = Welcomed, ["language"] = Language, ["bedtime"] = Bedtime,
            ["current"] = Current, ["profiles"] = profiles,
        };
    }

    /// <summary>Read what was saved. Anything missing or of the wrong kind is left at its starting value, so a hand-edited file can't break the app.</summary>
    public static AppState FromJson(JsonObject o)
    {
        var s = new AppState
        {
            Welcomed = Flag(o["welcomed"]),
            Language = Text(o["language"]) is { Length: > 0 } lang ? lang : "auto",
            Bedtime = Flag(o["bedtime"]),
            Current = Text(o["current"]) ?? "",
        };
        if (o["profiles"] is JsonArray list)
        {
            foreach (JsonNode? item in list)
            {
                if (item is not JsonObject p || s.Profiles.Count >= MaxProfiles) continue;
                string id = Text(p["id"]) ?? "";
                if (id.Length == 0 || s.Profiles.Any(x => x.Id == id)) continue;
                string age = Text(p["age"]) ?? "3-5";
                var child = new ChildProfile
                {
                    Id = id, Name = CleanName(Text(p["name"])), Avatar = Math.Clamp(Int(p["avatar"], 0), 0, AvatarCount - 1),
                    Age = ReaderSettings.AgePresets.ContainsKey(age) ? age : "3-5",
                };
                if (p["books"] is JsonObject books)
                {
                    foreach (var (bookId, node) in books)
                    {
                        if (node is not JsonObject b) continue;
                        var progress = new BookProgress
                        {
                            Finished = Flag(b["finished"]), TimesFinished = Math.Max(0, Int(b["timesFinished"], 0)),
                            PageCount = Math.Max(0, Int(b["pageCount"], 0)), Page = Math.Max(-1, Int(b["page"], -1)), Stop = Int(b["stop"], 0),
                        };
                        if (DateTime.TryParse(Text(b["lastRead"]), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime when)) progress.LastRead = when;
                        child.Books[bookId] = progress;
                    }
                }
                if (p["days"] is JsonArray days)
                {
                    foreach (JsonNode? d in days)
                        if (DateOnly.TryParseExact(Text(d), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateOnly day)) child.ReadOn(day);
                }
                s.Profiles.Add(child);
            }
        }
        if (s.Profiles.All(p => p.Id != s.Current)) s.Current = s.Profiles.FirstOrDefault()?.Id ?? "";
        return s;
    }

    static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : null;
    static bool Flag(JsonNode? n) => n is JsonValue v && v.TryGetValue(out bool b) && b;
    static int Int(JsonNode? n, int fallback) => n is JsonValue v && v.TryGetValue(out double d) && double.IsFinite(d) ? (int)Math.Round(d) : fallback;
}

/// <summary>
/// Keeps <see cref="AppState"/> between runs. Writing is safe against a crash or a flat battery half
/// way: the new file is written beside the old one and swapped in, and the one before is kept as
/// profiles.json.bak. Reading puts up with a damaged file: it falls back to the .bak, then to a fresh start.
/// </summary>
public static class AppStateStore
{
    /// <summary>profiles.json beside settings.json: %APPDATA%\BookReader on Windows, ~/.config/BookReader on Linux, the app's own files on Android.</summary>
    public static string DefaultPath => Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath)!, "profiles.json");

    public static AppState Load(string? path = null)
    {
        path ??= DefaultPath;
        return Read(path) ?? Read(path + ".bak") ?? new AppState();
    }

    static AppState? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonNode.Parse(File.ReadAllText(path)) is JsonObject o ? AppState.FromJson(o) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Returns false when the file could not be written. The app carries on either way.</summary>
    public static bool Save(AppState state, string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string temp = path + ".tmp";
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(state.ToJson().ToJsonString(ReaderSettings.JsonOut));
                file.Write(bytes, 0, bytes.Length);
                file.Flush(flushToDisk: true);
            }
            if (File.Exists(path))
            {
                // Only a file that still reads is worth keeping as the fallback.
                if (Read(path) is not null) File.Copy(path, path + ".bak", overwrite: true);
                File.Move(temp, path, overwrite: true);
            }
            else File.Move(temp, path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
