using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Reader.Core;

namespace Reader.Tests;

/// <summary>The app around the reader: the strings table, the grown-ups' gate, children and their progress, and how it is kept.</summary>
public class AppStateTests
{
    // ---------------------------------------------------------------- strings

    [Fact]
    public void Every_string_is_in_every_language_with_the_same_blanks()
    {
        foreach (var (key, row) in Strings.All)
        {
            Assert.True(row.Length == Strings.Languages.Length, $"{key} has {row.Length} languages");
            foreach (string text in row) Assert.False(string.IsNullOrWhiteSpace(text), $"{key} has an empty translation");
            var blanks = row.Select(t => string.Join(",", Regex.Matches(t, @"\{\d\}").Select(m => m.Value).OrderBy(v => v))).Distinct().ToList();
            Assert.True(blanks.Count == 1, $"{key} fills in different blanks in different languages");
        }
    }

    [Fact]
    public void No_long_dashes_in_the_app_s_words()
    {
        foreach (var (key, row) in Strings.All)
            foreach (string text in row) Assert.DoesNotContain("—", text);
    }

    [Fact]
    public void The_language_follows_the_device_unless_one_is_chosen()
    {
        Assert.Equal("af", Strings.Resolve("auto", "af-ZA"));
        Assert.Equal("af", Strings.Resolve("auto", "af_ZA.UTF-8"));
        Assert.Equal("en", Strings.Resolve("auto", "en-GB"));
        Assert.Equal("en", Strings.Resolve("auto", "zu-ZA"));      // a language the app doesn't speak yet
        Assert.Equal("en", Strings.Resolve("auto", null));
        Assert.Equal("en", Strings.Resolve("en", "af-ZA"));
        Assert.Equal("af", Strings.Resolve("af", "en-US"));
        Assert.Equal("en", Strings.Resolve("xx", "xx"));
    }

    [Fact]
    public void Words_come_in_the_language_in_use()
    {
        string before = Strings.Language;
        try
        {
            Strings.Language = "af";
            Assert.Equal("Lia se boeke", Strings.BooksOf("Lia"));
            Assert.Equal("My boeke", Strings.BooksOf(""));
            Assert.Equal("12 Desember", Strings.Date(new DateOnly(2026, 12, 12)));
            Strings.Language = "en";
            Assert.Equal("Lia’s books", Strings.BooksOf("Lia"));
            Assert.Equal("Page 7 of 12", Strings.T("shelf.page", 7, 12));
            Assert.Equal("no.such.key", Strings.T("no.such.key"));
            Strings.Language = "fr";
            Assert.Equal("en", Strings.Language);
        }
        finally { Strings.Language = before; }
    }

    [Fact]
    public void A_book_s_language_is_shown_only_when_the_shelf_mixes_them()
    {
        ShelfEntry E(string lang) => new("x", "X", "/x", "/x/c.jpg") { Language = lang };
        Assert.Equal("AF", Strings.LanguageTag("af"));
        Assert.Equal("EN", Strings.LanguageTag("en-ZA"));
        Assert.Equal("", Strings.LanguageTag(""));
        Assert.False(ShelfEntry.MixedLanguages(new[] { E("en"), E("en"), E("") }));
        Assert.True(ShelfEntry.MixedLanguages(new[] { E("en"), E("af") }));
        Assert.False(ShelfEntry.MixedLanguages(new[] { E("en-GB"), E("en") }));
    }

    // ---------------------------------------------------------------- the gate

    [Fact]
    public void The_gate_opens_for_the_numbers_it_asks_for()
    {
        var gate = new GateChallenge(new Random(1));
        Assert.Equal(3, gate.Digits.Distinct().Count());
        Assert.Equal(string.Join(", ", gate.Digits.Select(d => Strings.For("en", "number." + d))), gate.Words("en"));
        Assert.DoesNotContain(gate.Words("af"), gate.Words("en"));
        Assert.Equal(GateChallenge.Outcome.More, gate.Tap(gate.Digits[0], 0));
        Assert.Equal(GateChallenge.Outcome.More, gate.Tap(gate.Digits[1], 0));
        Assert.Equal(GateChallenge.Outcome.Opened, gate.Tap(gate.Digits[2], 0));
    }

    [Fact]
    public void A_wrong_try_brings_new_numbers_and_three_bring_a_rest()
    {
        var gate = new GateChallenge(new Random(2));
        int Wrong(int d) => (d + 1) % 10;
        for (int miss = 1; miss <= 3; miss++)
        {
            int[] asked = gate.Digits.ToArray();
            gate.Tap(Wrong(asked[0]), 0);
            gate.Tap(asked[1], 0);
            GateChallenge.Outcome o = gate.Tap(asked[2], 0);
            Assert.False(asked.SequenceEqual(gate.Digits));
            Assert.True(gate.JustMissed);
            Assert.Equal(miss < 3 ? GateChallenge.Outcome.Missed : GateChallenge.Outcome.Resting, o);
        }
        Assert.True(gate.Resting(1000));
        Assert.Equal(29, gate.RestSeconds(1000));
        Assert.Equal(GateChallenge.Outcome.Resting, gate.Tap(gate.Digits[0], 10_000));
        Assert.False(gate.Resting(30_001));
        int[] d = gate.Digits.ToArray();
        gate.Tap(d[0], 30_001);
        gate.Tap(9 - d[1] == d[1] ? 0 : d[1], 30_001);
        gate.Delete();
        Assert.Single(gate.Typed);
        gate.Tap(d[1], 30_001);
        Assert.Equal(GateChallenge.Outcome.Opened, gate.Tap(d[2], 30_001));
    }

    // ---------------------------------------------------------------- children

    [Fact]
    public void There_is_room_for_five_children_with_short_names()
    {
        var s = new AppState();
        for (int i = 0; i < 5; i++) Assert.NotNull(s.Add("Child " + i, s.FreeAvatar()));
        Assert.Null(s.Add("Six", 0));
        Assert.False(s.CanAdd);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, s.Profiles.Select(p => p.Avatar));
        Assert.Equal("p5", s.Current);
        Assert.Equal("Bartholomew", AppState.CleanName("  Bartholomew  "));
        Assert.Equal("Anna Maria L", AppState.CleanName("Anna   Maria\nLouisa"));
        Assert.Equal(AppState.MaxNameLength, AppState.CleanName(new string('x', 40)).Length);
        s.Remove("p5");
        Assert.Equal("p1", s.Current);
        Assert.Equal("p5", s.Add("New", 0)!.Id);
    }

    [Fact]
    public void Each_child_keeps_their_own_place()
    {
        var s = new AppState();
        s.Add("Lia", 0);
        s.Add("Thabo", 1);
        var t = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
        s.Choose("p1");
        s.KeepPlace("moss", 9, 6, 1, 12, t);
        Assert.Equal(6, s.CurrentProfile!.For("moss")!.Page);
        Assert.Equal("moss", s.CarryOn(new[] { "moss", "sea" }));
        Assert.Null(s.CarryOn(new[] { "sea" }));            // a book no longer on the shelf isn't offered
        s.Choose("p2");
        Assert.Null(s.CurrentProfile!.For("moss"));
        Assert.Null(s.CarryOn(new[] { "moss" }));
        // The start of a book (its cover or the first step) is no place to carry on from.
        s.Choose("p1");
        s.KeepPlace("moss", 0, 0, Step.Cover, 12, t);
        Assert.False(s.CurrentProfile!.For("moss")!.Partway);
    }

    [Fact]
    public void The_End_marks_a_book_finished_and_read_next_skips_finished_books()
    {
        var s = new AppState();
        s.Add("Lia", 0);
        var t = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
        s.KeepPlace("a", 5, 3, 0, 8, t);
        s.MarkFinished("a", 8, t);
        BookProgress p = s.CurrentProfile!.For("a")!;
        Assert.True(p.Finished);
        Assert.False(p.Partway);
        Assert.Equal(1, p.TimesFinished);
        string[] shelf = { "a", "b", "c", "d" };
        s.MarkFinished("b", 4, t);
        Assert.Equal("c", s.ReadNext(shelf, "a"));
        s.MarkFinished("c", 4, t);
        s.MarkFinished("d", 4, t);
        Assert.Null(s.ReadNext(shelf, "a"));
        Assert.Equal("a", s.ReadNext(shelf, "d") ?? "a");
        // Reading a finished book again: it stays finished and gets a place as well.
        s.KeepPlace("a", 4, 2, 0, 8, t);
        Assert.True(s.CurrentProfile!.For("a")!.Finished && s.CurrentProfile.For("a")!.Partway);
        Assert.Equal(4, s.CurrentProfile.FinishedCount);
        Assert.Equal(1, s.CurrentProfile.StartedCount);
    }

    [Fact]
    public void The_days_read_are_counted_for_the_week()
    {
        var s = new AppState();
        s.Add("Lia", 0);
        var today = new DateOnly(2026, 11, 18);
        foreach (int back in new[] { 0, 1, 1, 3, 6, 7, 20 })
            s.KeepPlace("a", 3, 1, 0, 5, today.AddDays(-back).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Local).ToUniversalTime());
        Assert.Equal(4, s.CurrentProfile!.DaysThisWeek(today));
    }

    [Fact]
    public void Children_and_progress_are_written_and_read_back()
    {
        var s = new AppState { Welcomed = true, Language = "af", Bedtime = true };
        s.Add("Lia", 3, "6-8");
        s.Add("Thabo", 1);
        var t = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
        s.KeepPlace("moss", 9, 6, 1, 12, t);
        s.MarkFinished("sea", 9, t);
        AppState back = AppState.FromJson(JsonNode.Parse(s.ToJson().ToJsonString())!.AsObject());
        Assert.True(back.Welcomed);
        Assert.Equal("af", back.Language);
        Assert.True(back.Bedtime);
        Assert.Equal("p2", back.Current);
        Assert.Equal(new[] { "Lia", "Thabo" }, back.Profiles.Select(p => p.Name));
        Assert.Equal("6-8", back.Profiles[0].Age);
        Assert.Equal(3, back.Profiles[0].Avatar);
        BookProgress moss = back.Profiles[1].For("moss")!;
        Assert.Equal((6, 1, 12), (moss.Page, moss.Stop, moss.PageCount));
        Assert.Equal(t, moss.LastRead);
        Assert.True(back.Profiles[1].For("sea")!.Finished);
        Assert.Single(back.Profiles[1].Days);
    }

    [Fact]
    public void A_hand_edited_or_odd_file_cannot_break_the_app()
    {
        AppState s = AppState.FromJson(JsonNode.Parse("""
            { "welcomed": "yes", "language": 5, "current": "nobody",
              "profiles": [ { "id": "p1", "name": "A very long name indeed", "avatar": 99, "age": "100",
                              "books": { "x": { "page": "seven", "finished": true }, "y": 3 }, "days": ["2026-13-01", "2026-11-01"] },
                            { "id": "p1", "name": "Twin" }, { "name": "No id" }, 7,
                            { "id": "p2" }, { "id": "p3" }, { "id": "p4" }, { "id": "p5" }, { "id": "p6" } ] }
            """)!.AsObject());
        Assert.False(s.Welcomed);
        Assert.Equal("auto", s.Language);
        Assert.Equal(5, s.Profiles.Count);
        Assert.Equal("p1", s.Current);
        ChildProfile p = s.Profiles[0];
        Assert.Equal("A very long", p.Name);
        Assert.Equal(AppState.AvatarCount - 1, p.Avatar);
        Assert.Equal("3-5", p.Age);
        Assert.True(p.For("x")!.Finished);
        Assert.False(p.For("x")!.Partway);
        Assert.Null(p.For("y"));
        Assert.Single(p.Days);
    }

    [Fact]
    public void The_file_is_swapped_in_whole_and_a_damaged_one_falls_back()
    {
        string dir = Path.Combine(Path.GetTempPath(), "reader-state-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(dir, "profiles.json");
            Assert.False(AppStateStore.Load(path).Welcomed);                 // nothing yet: a fresh start
            var s = new AppState { Welcomed = true };
            s.Add("Lia", 0);
            Assert.True(AppStateStore.Save(s, path));
            s.Add("Thabo", 1);
            Assert.True(AppStateStore.Save(s, path));
            Assert.False(File.Exists(path + ".tmp"));
            Assert.Equal(2, AppStateStore.Load(path).Profiles.Count);
            // Half a file (the battery ran out mid-write in an older version, or the disk is damaged): the one before.
            File.WriteAllText(path, "{ \"welcomed\": true, \"profi");
            AppState back = AppStateStore.Load(path);
            Assert.True(back.Welcomed);
            Assert.Single(back.Profiles);
            // Both damaged: a fresh start rather than a crash.
            File.WriteAllText(path + ".bak", "not json");
            Assert.Empty(AppStateStore.Load(path).Profiles);
            // A damaged file is not kept as the fallback when the next save comes.
            Assert.True(AppStateStore.Save(s, path));
            Assert.Equal(2, AppStateStore.Load(path).Profiles.Count);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    // ---------------------------------------------------------------- the book's language

    [Fact]
    public void A_book_can_say_its_language_and_older_books_are_unchanged()
    {
        Book b = BookJson.Parse("""{ "id": "x", "title": "T", "language": " af ", "pages": [ { "image": "a.jpg", "stops": [] } ], "illustrator": "Kim" }""");
        Assert.Equal("af", b.Language);
        JsonObject saved = BookJson.Serialize(b);
        Assert.Equal("af", saved["language"]!.GetValue<string>());
        Assert.Equal("Kim", saved["illustrator"]!.GetValue<string>());
        Book none = BookJson.Parse("""{ "id": "x", "title": "T", "pages": [ { "image": "a.jpg", "stops": [] } ] }""");
        Assert.Equal("", none.Language);
        Assert.Null(BookJson.Serialize(none)["language"]);
    }

    [Fact]
    public void The_sample_books_say_their_language()
    {
        foreach (string id in Kit.BookIds())
        {
            Book b = Kit.Load(id);
            Assert.False(string.IsNullOrEmpty(b.Language), id + " has no language");
            ShelfEntry e = BookLibrary.Describe(Kit.BookFolder(id));
            Assert.Equal(b.Language, e.Language);
            Assert.Equal(b.Pages.Count, e.PageCount);
        }
    }

    // ---------------------------------------------------------------- tap a word to hear it

    static Stop Spoken(string caption, double? end, params double[] words) => new()
    {
        Box = new RectD(0.1, 0.1, 0.3, 0.3),
        Caption = caption,
        Voice = new Voice { File = "voice/p1-s1.wav", Start = 0.2, End = end, Words = words.ToList() },
    };

    [Fact]
    public void A_word_s_slice_runs_to_the_next_word()
    {
        Voice v = Spoken("Drip. Drip. Splash!", 2.4, 0.3, 0.8, 1.3).Voice!;
        Assert.Equal((0.3, 0.8), ReadAlong.WordSlice(v, "Drip. Drip. Splash!", 0));
        Assert.Equal((1.3, 2.4), ReadAlong.WordSlice(v, "Drip. Drip. Splash!", 2));
        Assert.Null(ReadAlong.WordSlice(v, "Drip. Drip. Splash!", 3));
        Assert.Null(ReadAlong.WordSlice(v, "Drip. Drip.", 0));               // times that don't fit the words
        Assert.True(ReadAlong.Timed(v, "Drip. Drip. Splash!"));
        Assert.False(ReadAlong.Timed(new Voice { File = "a.wav" }, "Drip."));
        // A long pause after the last word isn't part of it.
        Voice late = Spoken("Drip.", 9, 0.3).Voice!;
        var (from, to) = ReadAlong.WordSlice(late, "Drip.", 0)!.Value;
        Assert.Equal(0.3, from, 6);
        Assert.Equal(1.9, to, 6);
    }

    [Fact]
    public void Tapping_a_word_says_just_that_word_and_lights_it()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.5, 0.5), Spoken("Drip. Drip. Splash!", 2.4, 0.3, 0.8, 1.3), Kit.StopAt(0.1, 0.6));
        Rig rig = Kit.Open(book, s => { s.WholePageFirst = false; s.ReadToMe = false; });
        rig.NextAndArrive();
        Assert.True(rig.Engine.WordsCanBeHeard);
        Assert.True(rig.Engine.SayWord(2));
        Assert.Contains("voice voice/p1-s1.wav 1.3-2.4", rig.Sound.Calls);
        Assert.Equal(2, rig.Engine.SaidWord);
        rig.Run(1300);
        Assert.Equal(-1, rig.Engine.SaidWord);
        Assert.False(rig.Engine.SayWord(5));
    }

    [Fact]
    public void Tapping_a_word_while_the_story_is_read_stops_the_reading()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.5, 0.5), Spoken("Drip. Drip. Splash!", 2.4, 0.3, 0.8, 1.3));
        Rig rig = Kit.Open(book, s => s.WholePageFirst = false);
        rig.NextAndArrive();
        Assert.True(rig.Engine.Reading);
        rig.Engine.SayWord(0);
        Assert.False(rig.Engine.Reading);
        Assert.Equal(0, rig.Engine.SaidWord);
        Assert.Contains("voice voice/p1-s1.wav 0.3-0.8", rig.Sound.Calls);
    }

    [Fact]
    public void Without_timings_a_word_cannot_be_tapped()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.5, 0.5), Kit.StopAt(0.1, 0.6));
        Rig rig = Kit.Open(book, s => s.WholePageFirst = false);
        rig.NextAndArrive();
        Assert.False(rig.Engine.WordsCanBeHeard);
        Assert.False(rig.Engine.SayWord(0));
        Assert.DoesNotContain(rig.Sound.Calls, c => c.StartsWith("voice "));
    }
}
