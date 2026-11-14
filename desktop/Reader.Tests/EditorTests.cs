using System.Text.Json.Nodes;
using Editor.Core;
using Reader.Core;
using SkiaSharp;

namespace Reader.Tests;

/// <summary>A folder of its own under the temp folder, deleted afterwards.</summary>
sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "editor-tests-" + Guid.NewGuid().ToString("N"));
    public TempFolder() => Directory.CreateDirectory(Path);
    public string this[string name] => System.IO.Path.Combine(Path, name);
    public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } }

    public static void CopyFolder(string from, string to)
    {
        foreach (string dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(System.IO.Path.Combine(to, System.IO.Path.GetRelativePath(from, dir)));
        Directory.CreateDirectory(to);
        foreach (string f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories)) File.Copy(f, System.IO.Path.Combine(to, System.IO.Path.GetRelativePath(from, f)));
    }
}

public class EditorBookTests
{
    /// <summary>Every value in <paramref name="original"/> is in <paramref name="saved"/> at the same place, numbers to four places.</summary>
    static void AssertNothingLost(JsonNode? original, JsonNode? saved, string at)
    {
        switch (original)
        {
            case JsonObject o:
                Assert.True(saved is JsonObject, at + " is no longer an object");
                foreach (var (key, value) in o)
                {
                    Assert.True(((JsonObject)saved!).ContainsKey(key), $"{at}.{key} was lost");
                    AssertNothingLost(value, saved![key], $"{at}.{key}");
                }
                break;
            case JsonArray a:
                Assert.True(saved is JsonArray b && b.Count == a.Count, at + " changed length");
                for (int i = 0; i < a.Count; i++) AssertNothingLost(a[i], saved![i], $"{at}[{i}]");
                break;
            case JsonValue v when v.TryGetValue(out double d):
                Assert.Equal(BookJson.Round4(d), BookJson.Round4(saved!.GetValue<double>()));
                break;
            default:
                Assert.Equal(original?.ToJsonString(), saved?.ToJsonString());
                break;
        }
    }

    [Theory]
    [MemberData(nameof(BookJsonTests.Books), MemberType = typeof(BookJsonTests))]
    public void Every_sample_book_opens_and_saves_in_the_editor_without_losing_anything(string id)
    {
        using var temp = new TempFolder();
        string folder = temp[id];
        TempFolder.CopyFolder(Kit.BookFolder(id), folder);
        JsonNode original = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "book.json")))!;

        BookProject project = BookProject.Open(folder);
        Assert.False(project.DiffersFromDisk() && BookJson.ToJsonString(BookJson.Parse(original.ToJsonString())) == File.ReadAllText(Path.Combine(folder, "book.json")));
        project.Save();
        JsonNode saved = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "book.json")))!;
        AssertNothingLost(original, saved, "$");
        // Saving again changes nothing, and the version before is kept.
        string first = File.ReadAllText(Path.Combine(folder, "book.json"));
        BookProject.Open(folder).Save();
        Assert.Equal(first, File.ReadAllText(Path.Combine(folder, "book.json")));
        Assert.True(File.Exists(Path.Combine(folder, "book.json.bak")));
        // Nothing the book uses is reported unused, and Check book finds no missing files.
        Assert.Empty(project.UnusedFiles());
        Assert.DoesNotContain(BookChecker.Check(project), i => i.Level == Severity.Problem);
    }

    [Fact]
    public void A_folder_of_pictures_with_no_book_json_becomes_a_book()
    {
        using var temp = new TempFolder();
        string folder = temp["My Lovely Book"];
        Directory.CreateDirectory(folder);
        foreach (string n in new[] { "page10.png", "page2.png", "page1.png", "cover.png" }) Pictures.MakePlain(Path.Combine(folder, n), 40, 25, SKColors.Red);
        BookProject p = BookProject.Open(folder);
        Assert.Equal("my-lovely-book", p.Book.Id);
        Assert.Equal("cover.png", p.Book.Cover);
        Assert.Equal(new[] { "page1.png", "page2.png", "page10.png" }, p.Book.Pages.Select(x => x.Image));
    }

    [Fact]
    public void A_new_book_gets_a_folder_named_after_its_title_and_a_book_json_at_once()
    {
        using var temp = new TempFolder();
        BookProject a = BookProject.Create(temp.Path, "Moss and the Moon!");
        Assert.Equal(Path.Combine(temp.Path, "moss-and-the-moon"), a.Folder);
        Assert.True(File.Exists(a.BookFile));
        BookProject b = BookProject.Create(temp.Path, "Moss and the Moon!");
        Assert.Equal(Path.Combine(temp.Path, "moss-and-the-moon-2"), b.Folder);
        Assert.Equal("moss-and-the-moon-2", b.Book.Id);
    }

    [Fact]
    public void Files_brought_in_are_copied_under_tidy_names_that_never_clash()
    {
        using var temp = new TempFolder();
        BookProject p = BookProject.Create(temp.Path, "T");
        string outside = temp["My Picture (final).PNG"];
        Pictures.MakePlain(outside, 10, 10, SKColors.Blue);
        Assert.Equal("My-Picture-final.png", p.Import(outside));
        Assert.Equal("My-Picture-final-2.png", p.Import(outside));
        Assert.Equal("voice/My-Picture-final.png", p.Import(outside, "voice"));
        // A file already in the folder is used where it is.
        Assert.Equal("My-Picture-final.png", p.Import(Path.Combine(p.Folder, "My-Picture-final.png")));
        Assert.Null(p.PathOf("../escape.png"));
    }

    [Fact]
    public void Unused_files_are_found_and_moved_aside_not_deleted()
    {
        using var temp = new TempFolder();
        BookProject p = BookProject.Create(temp.Path, "T");
        Pictures.MakePlain(Path.Combine(p.Folder, "a.jpg"), 10, 10, SKColors.Blue);
        Pictures.MakePlain(Path.Combine(p.Folder, "old.jpg"), 10, 10, SKColors.Blue);
        Directory.CreateDirectory(Path.Combine(p.Folder, "voice"));
        File.WriteAllText(Path.Combine(p.Folder, "voice", "take1.wav"), "x");
        File.WriteAllText(Path.Combine(p.Folder, "voice", "take2.wav"), "x");
        p.Book.Pages.Add(new Page { Image = "a.jpg", Stops = { new Stop { Box = new RectD(0, 0, 1, 1), Voice = new Voice { File = "voice/take2.wav" } } } });
        p.Save();
        Assert.Equal(new[] { "old.jpg", "voice/take1.wav" }, p.UnusedFiles());
        Assert.Equal(2, p.MoveUnusedAside());
        Assert.Empty(p.UnusedFiles());
        Assert.True(File.Exists(Path.Combine(p.Folder, "_unused", "voice", "take1.wav")));
    }

    [Fact]
    public void Undo_and_redo_go_back_and_forth_and_typing_counts_as_one_step()
    {
        var clock = new DateTime(2026, 1, 1);
        var h = new History { Clock = () => clock };
        Book book = Kit.OnePage(Kit.StopAt(0.1, 0.1));
        h.Record(book, "Add a stop");
        book.Pages[0].Stops.Add(Kit.StopAt(0.5, 0.5));
        for (int i = 0; i < 5; i++)
        {
            h.Record(book, "Words", mergeKey: "caption");
            book.Pages[0].Stops[1].Caption = "Hello"[..(i + 1)];
            clock += TimeSpan.FromMilliseconds(300);
        }
        Assert.Equal(2, h.Count);
        var back = h.Undo(book, 0, "")!.Value;
        Assert.Null(back.Book.Pages[0].Stops[1].Caption);
        Assert.Equal("Words", back.Entry.Label);
        var again = h.Undo(back.Book, 0, "")!.Value;
        Assert.Single(again.Book.Pages[0].Stops);
        Assert.False(h.CanUndo);
        var forward = h.Redo(again.Book, 0, "")!.Value;
        Assert.Equal(2, forward.Book.Pages[0].Stops.Count);
        forward = h.Redo(forward.Book, 0, "")!.Value;
        Assert.Equal("Hello", forward.Book.Pages[0].Stops[1].Caption);
        // A new change after undoing forgets what could have been redone.
        h.Undo(forward.Book, 0, "");
        h.Record(forward.Book, "Something else");
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void A_recovery_copy_is_offered_only_when_it_differs_from_the_saved_book()
    {
        using var temp = new TempFolder();
        var recovery = new Recovery(temp["recovery"]);
        BookProject p = BookProject.Create(temp.Path, "Safe");
        Assert.Null(recovery.Find(p.Folder));
        p.Book.Title = "Safe and sound";
        Assert.True(recovery.Write(p));
        var found = recovery.Find(p.Folder);
        Assert.NotNull(found);
        Assert.Equal("Safe and sound", found!.Value.Book.Title);
        p.Save();
        Assert.Null(recovery.Find(p.Folder));        // now the same as the saved book: nothing to offer, and it is tidied away
        Assert.Empty(Directory.GetFiles(temp["recovery"]));
    }

    [Fact]
    public void Recent_books_are_newest_first_without_repeats()
    {
        using var temp = new TempFolder();
        var recent = new RecentBooks(temp["recent.json"]) { Most = 3 };
        foreach (string n in new[] { "a", "b", "c", "a", "d" }) { Directory.CreateDirectory(temp[n]); recent.Add(temp[n]); }
        Assert.Equal(new[] { temp["d"], temp["a"], temp["c"] }, recent.Load());
        Directory.Delete(temp["a"]);
        Assert.Equal(new[] { temp["d"], temp["c"] }, recent.Existing());
    }

    [Fact]
    public void Natural_order_puts_page2_before_page10()
    {
        var names = new List<string> { "page10.jpg", "Page2.jpg", "page1.jpg", "page02b.jpg" };
        names.Sort(NaturalOrder.Instance);
        Assert.Equal(new[] { "page1.jpg", "Page2.jpg", "page02b.jpg", "page10.jpg" }, names);
        Assert.Equal("moss-the-puddle", Slug.Make("  Moss & the Puddle! "));
        Assert.Equal("cafe", Slug.Make("Café"));
    }
}

public class EditorPictureTests
{
    [Fact]
    public void Left_and_right_pages_join_into_one_spread()
    {
        using var temp = new TempFolder();
        Pictures.MakePlain(temp["l.png"], 400, 500, SKColors.Red);
        Pictures.MakePlain(temp["r.png"], 200, 250, SKColors.Blue);       // half the size: scaled up to match
        var size = Pictures.JoinSpread(temp["l.png"], temp["r.png"], temp["spread.jpg"]);
        Assert.Equal((800, 500), size);
        Assert.Equal((800, 500), Pictures.Size(temp["spread.jpg"]));
        SKColor left = Pictures.PixelAt(temp["spread.jpg"], 100, 250), right = Pictures.PixelAt(temp["spread.jpg"], 700, 250);
        Assert.True(left.Red > 200 && left.Blue < 60, left.ToString());
        Assert.True(right.Blue > 200 && right.Red < 60, right.ToString());
    }

    [Fact]
    public void Pictures_are_made_smaller_to_fit_and_keep_their_shape()
    {
        using var temp = new TempFolder();
        Pictures.MakePlain(temp["big.png"], 5000, 2500, SKColors.Green);
        Assert.True(Pictures.HasAlpha(temp["big.png"]) is false);
        var made = Pictures.SaveSmaller(temp["big.png"], temp["small.jpg"], 4096);
        Assert.Equal((4096, 2048), made);
        Assert.Equal((4096, 2048), Pictures.Size(temp["small.jpg"]));
        Assert.Equal((100, 50), Pictures.Fit((100, 50), 4096));
    }

    [Fact]
    public void A_big_scene_is_cut_into_tiles_the_reader_can_find()
    {
        using var temp = new TempFolder();
        Pictures.MakePlain(temp["scene.png"], 2500, 1100, SKColors.Teal);
        var (tiles, overview) = Pictures.CutTiles(temp["scene.png"], temp.Path, "scene");
        Assert.Equal("scene_overview.jpg", overview);
        Assert.Equal(2500, tiles.Width);
        Assert.Equal(1100, tiles.Height);
        Assert.Equal(2, tiles.Levels);
        // Level 0: 3 x 2 tiles; level 1 (half size, 1250 x 550): 2 x 1.
        for (int x = 0; x < 3; x++) for (int y = 0; y < 2; y++) Assert.True(File.Exists(Path.Combine(temp.Path, tiles.PathFor(0, x, y))), tiles.PathFor(0, x, y));
        Assert.True(File.Exists(Path.Combine(temp.Path, tiles.PathFor(1, 1, 0))));
        Assert.False(File.Exists(Path.Combine(temp.Path, tiles.PathFor(1, 0, 1))));
        Assert.Equal((452, 1024), Pictures.Size(Path.Combine(temp.Path, tiles.PathFor(0, 2, 0))));
        Assert.Equal((2048, 901), Pictures.Size(temp[overview]));
    }
}

public class EditorCheckAndExportTests
{
    static BookProject Sample(TempFolder temp)
    {
        BookProject p = BookProject.Create(temp.Path, "Check me");
        Pictures.MakePlain(Path.Combine(p.Folder, "p1.png"), 1600, 1000, SKColors.Orange);
        Pictures.MakePlain(Path.Combine(p.Folder, "p2.jpg"), 800, 400, SKColors.Orange);         // a different shape, and small
        Pictures.MakePlain(Path.Combine(p.Folder, "scene.jpg"), 5000, 1250, SKColors.Navy);     // a big explore scene
        Pictures.MakePlain(Path.Combine(p.Folder, "huge.png"), 4800, 3000, SKColors.Orange);    // a big guided page
        Pictures.MakePlain(Path.Combine(p.Folder, "cover.png"), 1200, 1800, SKColors.Purple);
        Pictures.MakePlain(Path.Combine(p.Folder, "forgotten.png"), 10, 10, SKColors.Purple);
        Directory.CreateDirectory(Path.Combine(p.Folder, "voice"));
        Editor.Core.Wav.Write(Path.Combine(p.Folder, "voice", "s1.wav"), new float[4800], 48000);
        p.Book.Cover = "cover.png";
        p.Book.Pages.Add(new Page
        {
            Image = "p1.png",
            Stops =
            {
                new Stop { Box = new RectD(0.1, 0.1, 0.3, 0.3), Caption = "Moss had new boots.", Voice = new Voice { File = "voice/s1.wav", End = 0.1, Words = { 0.0, 0.02, 0.04, 0.06 } } },
                new Stop { Box = new RectD(0.8, 0.8, 0.4, 0.3), Caption = string.Join(' ', Enumerable.Repeat("Splash went the very big muddy puddle.", 6)) },
                new Stop { Box = new RectD(0.2, 0.2, 0.2, 0.2), Sound = "gone.wav" },
            },
            Effects = { new PageEffect { Type = PageEffect.Lightning, Stop = 9 } },
        });
        p.Book.Pages.Add(new Page { Image = "p2.jpg" });
        p.Book.Pages.Add(new Page { Image = "scene.jpg", Explore = true, Hotspots = { new Hotspot { Box = new RectD(0.1, 0.1, 0.1, 0.1), Story = 2 } } });
        p.Book.Pages.Add(new Page { Image = "huge.png", Stops = { new Stop { Box = new RectD(0, 0, 0.5, 0.5), Caption = "Hello there." } } });
        p.Book.Pages.Add(new Page { Image = "missing.jpg" });
        p.Save();
        return p;
    }

    [Fact]
    public void Check_book_finds_what_will_go_wrong()
    {
        using var temp = new TempFolder();
        BookProject p = Sample(temp);
        List<Issue> issues = BookChecker.Check(p);
        string all = string.Join("\n", issues.Select(i => i.Level + ": " + i.Text));
        bool Has(Severity s, string text, int page = -2) => issues.Any(i => i.Level == s && i.Text.Contains(text) && (page == -2 || i.Page == page));

        Assert.True(Has(Severity.Problem, "Page 5's picture is missing", 4), all);
        Assert.True(Has(Severity.Problem, "the sound file is missing: gone.wav", 0), all);
        Assert.True(Has(Severity.Warning, "stop 2 reaches outside the picture", 0), all);
        Assert.True(Has(Severity.Warning, "lines on a phone held sideways", 0), all);
        Assert.True(Has(Severity.Warning, "Page 2's picture is only 800 x 400", 1), all);
        Assert.True(Has(Severity.Warning, "Page 2 is a different shape", 1), all);
        Assert.True(Has(Severity.Warning, "Page 3's picture is 5000 x 1250", 2), all);
        Assert.True(Has(Severity.Tip, "Page 4's picture is 4800 x 3000", 3), all);
        Assert.True(Has(Severity.Warning, "story steps should be numbered 1, 2, 3", 2), all);
        Assert.True(Has(Severity.Warning, "stop 2 has no recording, but other stops do", 0), all);
        Assert.True(Has(Severity.Warning, "set to a stop the page doesn't have", 0), all);
        Assert.True(Has(Severity.Tip, "Not used by the book: forgotten.png"), all);
        // The first stop is fine: its four words have four times.
        Assert.False(issues.Any(i => i.Page == 0 && i.Kind == Spot.Stop && i.Index == 0), all);
        // Problems come first.
        Assert.Equal(Severity.Problem, issues[0].Level);
        // No dashes of the long kind in anything an author reads.
        Assert.DoesNotContain(issues, i => i.Text.Contains('—') || i.Text.Contains('–'));
    }

    [Fact]
    public void Word_timings_that_no_longer_fit_the_words_are_reported()
    {
        using var temp = new TempFolder();
        BookProject p = Sample(temp);
        p.Book.Pages[0].Stops[0].Caption = "Moss had new red boots.";
        Assert.Contains(BookChecker.Check(p), i => i.Page == 0 && i.Index == 0 && i.Text.Contains("word timings don't match"));
    }

    [Fact]
    public void Export_makes_a_folder_and_zip_ready_for_the_apps()
    {
        using var temp = new TempFolder();
        BookProject p = Sample(temp);
        p.Book.Pages.RemoveAt(4);                                  // the missing picture
        p.Book.Pages[1].Spread = new SpreadSource { Left = "l.png", Right = "r.png" };
        ExportResult r = Exporter.Export(p, temp["out"], ExportOptions.Phone);

        Assert.Equal(Path.Combine(temp["out"], "check-me"), r.Folder);
        Assert.True(File.Exists(r.ZipFile));
        Book exported = BookJson.Load(r.Folder);
        // Every file the exported book names is there, and nothing else but book.json.
        var named = new BookProjectView(r.Folder, exported).Used();
        foreach (string f in named.Where(n => !n.EndsWith('/'))) Assert.True(File.Exists(Path.Combine(r.Folder, f)), f);
        Assert.DoesNotContain("forgotten.png", Directory.GetFiles(r.Folder).Select(Path.GetFileName));
        Assert.False(File.Exists(Path.Combine(r.Folder, "book.json.bak")));

        // A guided page with no see-through parts became JPEG; the huge one was made phone size.
        Assert.Equal("p1.jpg", exported.Pages[0].Image);
        Assert.Equal("huge.jpg", exported.Pages[3].Image);
        Assert.Equal((4096, 2560), Pictures.Size(Path.Combine(r.Folder, "huge.jpg")));
        // The big explore scene was cut into tiles and its overview is the page's picture.
        Assert.NotNull(exported.Pages[2].Tiles);
        Assert.Equal(5000, exported.Pages[2].Tiles!.Width);
        Assert.True(File.Exists(Path.Combine(r.Folder, exported.Pages[2].Tiles!.PathFor(0, 4, 1))));
        // The cover is smaller, the stops are untouched, and the editor's notes are gone.
        Assert.Equal("cover.jpg", exported.Cover);
        Assert.Equal(p.Book.Pages[0].Stops[0].Box, exported.Pages[0].Stops[0].Box);
        Assert.Null(exported.Pages[1].Spread);
        Assert.Equal("voice/s1.wav", exported.Pages[0].Stops[0].Voice!.File);
        // The book being edited is unchanged.
        Assert.Equal("p1.png", p.Book.Pages[0].Image);
        Assert.NotNull(p.Book.Pages[1].Spread);

        // Into a reader's content folder: copied, and listed in library.json once.
        string content = temp["content"];
        Directory.CreateDirectory(content);
        File.WriteAllText(Path.Combine(content, "library.json"), """{ "books": [ "biggest-puddle" ] }""");
        Exporter.AddToLibrary(r.Folder, content);
        Exporter.AddToLibrary(r.Folder, content);
        Assert.Equal(new[] { "biggest-puddle", "check-me" }, BookLibraryIds(content));
        Assert.True(File.Exists(Path.Combine(content, "books", "check-me", "book.json")));
    }

    [Fact]
    public void Export_keeping_originals_copies_pictures_as_they_are()
    {
        using var temp = new TempFolder();
        BookProject p = Sample(temp);
        p.Book.Pages.RemoveAt(4);
        ExportResult r = Exporter.Export(p, temp["out"], ExportOptions.Originals with { Zip = false });
        Assert.Null(r.ZipFile);
        Book exported = BookJson.Load(r.Folder);
        Assert.Equal("huge.png", exported.Pages[3].Image);
        Assert.Null(exported.Pages[2].Tiles);
    }

    [Fact]
    public void A_sample_book_exports_and_still_has_every_file()
    {
        using var temp = new TempFolder();
        TempFolder.CopyFolder(Kit.BookFolder("deep-sea"), temp["deep-sea"]);
        ExportResult r = Exporter.Export(BookProject.Open(temp["deep-sea"]), temp["out"], ExportOptions.Phone with { Zip = false });
        Book exported = BookJson.Load(r.Folder);
        Assert.NotNull(exported.Pages[0].Tiles);
        Assert.True(File.Exists(Path.Combine(r.Folder, exported.Pages[0].Tiles!.PathFor(0, 0, 0))));
        Assert.DoesNotContain(BookChecker.Check(BookProject.Open(r.Folder)), i => i.Level == Severity.Problem);
    }

    static IEnumerable<string> BookLibraryIds(string content) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(content, "library.json")))!["books"]!.AsArray().Select(n => n!.GetValue<string>());

    /// <summary>The files an exported book names.</summary>
    sealed class BookProjectView
    {
        readonly BookProject _p;
        public BookProjectView(string folder, Book book) { _p = BookProject.Open(folder); _p.Book = book; }
        public IEnumerable<string> Used() => _p.UsedFiles();
    }
}

public class EditorNarrationTests
{
    [Fact]
    public void Tapping_along_gives_each_word_its_time_in_order()
    {
        var t = new TapAlong(4, startSeconds: 0.2);
        Assert.Equal(0, t.Tap(0.1));         // before the start trim: moved to the start
        Assert.Equal(1, t.Tap(0.6));
        Assert.Equal(2, t.Tap(0.61));        // a double press: kept just after the word before
        t.Back();
        Assert.Equal(2, t.Tap(1.0));
        Assert.Equal(3, t.Tap(1.5));
        Assert.Equal(-1, t.Tap(2.0));
        Assert.True(t.Done);
        Assert.Equal(new[] { 0.2, 0.6, 1.0, 1.5 }, t.Times);
    }

    [Fact]
    public void Stopping_early_keeps_the_old_times_that_still_come_later()
    {
        var t = new TapAlong(4);
        t.Tap(0.5);
        t.Tap(0.9);
        Assert.Equal(new[] { 0.5, 0.9, 1.4, 2.0 }, t.Result(new[] { 0.1, 0.2, 1.4, 2.0 }));
        Assert.Equal(new[] { 0.5, 0.9 }, t.Result(new[] { 0.1, 0.2, 0.3, 2.0 }));
    }

    [Fact]
    public void Nudging_a_word_never_passes_its_neighbours()
    {
        var times = new List<double> { 0.5, 1.0, 1.5 };
        TapAlong.Nudge(times, 1, -0.8);
        Assert.Equal(0.55, times[1], 3);
        TapAlong.Nudge(times, 1, 5);
        Assert.Equal(1.45, times[1], 3);
        TapAlong.Nudge(times, 0, -1, min: 0.2);
        Assert.Equal(0.2, times[0], 3);
        TapAlong.Nudge(times, 2, 9, max: 2.0);
        Assert.Equal(2.0, times[2], 3);
    }

    [Fact]
    public void Spacing_words_evenly_gives_longer_words_more_time()
    {
        List<double> times = TapAlong.Spread("I splashed", 1, 3);
        Assert.Equal(2, times.Count);
        Assert.Equal(1, times[0]);
        Assert.True(times[1] > 1.4 && times[1] < 1.8, times[1].ToString());
    }

    [Fact]
    public void A_recording_is_written_as_a_wav_and_its_quiet_ends_found()
    {
        using var temp = new TempFolder();
        var samples = new float[48000];
        for (int i = 12000; i < 36000; i++) samples[i] = (float)Math.Sin(i * 0.05) * 0.5f;
        Editor.Core.Wav.Write(temp["take.wav"], samples, 48000);
        var back = Editor.Core.Wav.Read(temp["take.wav"])!.Value;
        Assert.Equal(48000, back.SampleRate);
        Assert.Equal(48000, back.Samples.Length);
        Assert.Equal(samples[12010], back.Samples[12010], 3);
        var (start, end) = Editor.Core.Wav.Sounding(back.Samples, 48000);
        Assert.InRange(start, 0.1, 0.25);
        Assert.InRange(end, 0.75, 0.9);
        float[] peaks = Editor.Core.Wav.Peaks(back.Samples, 10);
        Assert.Equal(0, peaks[0]);
        Assert.True(peaks[5] > 0.4f);
        // The reader's own sound reading can open it too.
        float[]? decoded = Reader.Audio.DesktopSound.ReadSoundFile(temp["take.wav"], 48000, 1);
        Assert.NotNull(decoded);
        Assert.InRange(decoded!.Length, 47000, 49000);
    }

    [Fact]
    public void The_screen_frame_of_a_stop_has_the_screen_s_shape_and_holds_the_stop()
    {
        var box = new RectD(0.4, 0.4, 0.1, 0.2);
        foreach (Device d in Device.All)
        {
            RectD v = Framing.Visible(box, 1.6, d, caption: false);
            Assert.True(v.X <= box.X && v.Right >= box.Right && v.Y <= box.Y && v.Bottom >= box.Bottom, d.Name);
            var (top, bottom) = d.Insets(false);
            double shape = (d.Width - d.Safe.Left - d.Safe.Right) / (d.Height - top - bottom);
            Assert.Equal(shape, v.W * 1.6 / v.H, 3);
        }
        Assert.Equal(1, Framing.HalfOf(new RectD(0.1, 0, 0.2, 1), 1.6));
        Assert.Equal(2, Framing.HalfOf(new RectD(0.1, 0, 0.2, 1), 0.66));
        Assert.True(Framing.CrossesCrease(new RectD(0.4, 0, 0.2, 1), 1.6));
        Assert.Equal(1, Framing.CaptionLines("Drip.", Device.Find("s22-sideways")));
    }
}

public class EditorPagePlanTests
{
    [Fact]
    public void Pictures_named_as_left_and_right_pages_are_paired_and_the_rest_stay_single()
    {
        var items = PagePlan.Make(new[] { "/x/page3-right.png", "/x/page10.jpg", "/x/page3-left.png", "/x/page2.jpg", "/x/pond_L.jpg", "/x/pond_R.jpg", "/x/lonely-left.png" });
        Assert.Equal(5, items.Count);
        Assert.Equal("/x/lonely-left.png", items[0].Single);      // no partner: a page of its own
        Assert.Equal("/x/page2.jpg", items[1].Single);
        Assert.True(items[2].IsPair);
        Assert.Equal("/x/page3-left.png", items[2].Left);
        Assert.Equal("/x/page3-right.png", items[2].Right);
        Assert.Equal("/x/page10.jpg", items[3].Single);
        Assert.Equal(("/x/pond_L.jpg", "/x/pond_R.jpg"), (items[4].Left, items[4].Right));
        Assert.Equal("page3", PagePlan.Stem("/x/page3-left.png"));
    }

    [Fact]
    public void Boxes_on_two_pages_land_on_the_matching_half_of_the_joined_spread()
    {
        // A 1000 x 1000 left page and a 500 x 500 right page (scaled up to 1000 x 1000 when joined).
        RectD l = PagePlan.OnSpread(new RectD(0.5, 0.2, 0.5, 0.5), false, (1000, 1000), (500, 500));
        Assert.Equal(new RectD(0.25, 0.2, 0.25, 0.5), l);
        RectD r = PagePlan.OnSpread(new RectD(0, 0, 1, 1), true, (1000, 1000), (500, 500));
        Assert.Equal(new RectD(0.5, 0, 0.5, 1), r);
    }
}
