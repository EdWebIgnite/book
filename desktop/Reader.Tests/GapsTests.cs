using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Editor.Core;
using Reader.Core;
using SkiaSharp;

namespace Reader.Tests;

/// <summary>Voices on things to find, and a stop's voice when a book opens straight onto it.</summary>
public class FoundVoiceAndOpeningTests
{
    static Voice V(string file = "voice/a.wav", params double[] words) => new() { File = file, Start = 0.1, End = 2, Words = words.ToList() };

    [Fact]
    public void A_thing_to_finds_voice_is_written_and_read_back()
    {
        var page = new Page { Image = "scene.jpg", Explore = true, Hotspots = { new Hotspot { Box = new RectD(0.1, 0.1, 0.1, 0.1), Caption = "A crab!", Voice = V("voice/p1-find1.wav", 0.2, 0.7) } } };
        var book = new Book { Id = "x", Title = "X", Pages = { page } };
        JsonNode saved = BookJson.Serialize(book);
        Assert.Equal("""{"file":"voice/p1-find1.wav","start":0.1,"end":2,"words":[0.2,0.7]}""", saved["pages"]![0]!["hotspots"]![0]!["voice"]!.ToJsonString());
        Hotspot back = BookJson.Parse(saved.ToJsonString()).Pages[0].Hotspots[0];
        Assert.Equal("voice/p1-find1.wav", back.Voice!.File);
        Assert.Equal(new[] { 0.2, 0.7 }, back.Voice.Words);
        Assert.Null(back.Extra);                     // "voice" is known, not kept as an unknown field

        // Without one, nothing is written.
        page.Hotspots[0].Voice = null;
        Assert.Null(BookJson.Serialize(book)["pages"]![0]!["hotspots"]![0]!["voice"]);
    }

    static Book Explore(Voice? voice) => new()
    {
        Id = "t", Title = "T",
        Pages = { new Page { Image = "s.jpg", Explore = true, Hotspots = { new Hotspot { Box = new RectD(0.4, 0.4, 0.1, 0.1), Caption = "Here is the crab.", Voice = voice } } } },
    };

    [Fact]
    public void Finding_a_thing_reads_its_words_and_lights_them_up()
    {
        Rig rig = Kit.Open(Explore(V("voice/crab.wav", 0.1, 0.5, 0.9, 1.3)));
        Assert.True(rig.Engine.Exploring);
        rig.Engine.React(0);
        Assert.Contains("voice voice/crab.wav 0.1-2", rig.Sound.Calls);
        Assert.True(rig.Engine.Reading);
        Assert.Equal("Here is the crab.", rig.Engine.State.Caption);
        rig.Run(1000);
        Assert.Equal(2, rig.Engine.ReadAlongWord());            // 0.1 + 1.0 s into the file: the third word
        // The message stays while the voice reads, and finding a thing is no cue to go on.
        rig.Run(1200);
        Assert.False(rig.Engine.ReadingDone);
        Assert.False(rig.Engine.State.ReadingDone);
    }

    [Fact]
    public void A_found_message_stays_up_until_a_long_voice_has_finished()
    {
        var long_ = new Voice { File = "voice/long.wav", End = 9, Words = { 0, 2, 4, 6 } };
        Book book = Explore(long_);
        book.Pages[0].Hotspots[0].Caption = "Hi.";              // short words would normally go after 4.5 s
        Rig rig = Kit.Open(book);
        rig.Engine.React(0);
        rig.Run(7000);
        Assert.Equal("Hi.", rig.Engine.State.Caption);
        rig.Run(5000);
        Assert.Equal("", rig.Engine.State.Caption);
    }

    [Fact]
    public void Without_read_to_me_a_found_thing_is_silent()
    {
        Rig rig = Kit.Open(Explore(V()), s => s.ReadToMe = false);
        rig.Engine.React(0);
        Assert.DoesNotContain(rig.Sound.Calls, c => c.StartsWith("voice"));
    }

    static Book OpensOnAStop() => new()
    {
        Id = "t", Title = "T",
        Pages =
        {
            new Page
            {
                Image = "p.jpg", OpenWhole = false,
                Stops =
                {
                    new Stop { Box = new RectD(0.1, 0.1, 0.3, 0.3), Caption = "Moss had new boots.", Sound = "sounds/splash.wav", Voice = new Voice { File = "voice/p1-s1.wav", End = 2, Words = { 0, 0.4, 0.8, 1.2 } } },
                    new Stop { Box = new RectD(0.5, 0.5, 0.3, 0.3), Caption = "Two." },
                },
            },
        },
    };

    [Fact]
    public void A_book_that_opens_straight_onto_a_stop_reads_it_aloud()
    {
        Rig rig = Kit.Open(OpensOnAStop());
        Assert.Equal(0, rig.Engine.CurrentStep.Stop);
        Assert.Contains("voice voice/p1-s1.wav 0-2", rig.Sound.Calls);
        Assert.Contains("clip sounds/splash.wav", rig.Sound.Calls);
        Assert.True(rig.Engine.Reading);
        // Only once.
        rig.Run(500);
        Assert.Equal(1, rig.Sound.Calls.Count(c => c.StartsWith("voice ")));
    }

    [Fact]
    public void Opening_onto_a_stop_waits_until_the_reader_is_showing()
    {
        var sound = new FakeSound();
        var engine = new ReaderEngine(new ReaderSettings { ReduceMotion = "off" }, sound) { Visible = false };
        engine.SetViewSize(1000, 700);
        Book book = OpensOnAStop();
        engine.SetTime(1000);
        engine.SetBook(book, Kit.Aspects(book));
        engine.Update(1016);
        Assert.DoesNotContain(sound.Calls, c => c.StartsWith("voice"));
        engine.Visible = true;
        engine.Update(1032);
        Assert.Contains(sound.Calls, c => c.StartsWith("voice voice/p1-s1.wav"));
    }

    [Fact]
    public void Jumping_onto_a_stop_reads_it_and_going_on_stops_it()
    {
        Book book = OpensOnAStop();
        book.Pages[0].OpenWhole = true;
        Rig rig = Kit.Open(book);
        Assert.DoesNotContain(rig.Sound.Calls, c => c.StartsWith("voice"));      // the whole page first: nothing to read
        rig.Jump(0, 0);
        Assert.Contains("voice voice/p1-s1.wav 0-2", rig.Sound.Calls);
        rig.Next();
        Assert.False(rig.Engine.Reading);
    }
}

/// <summary>Book files: making one, taking one in, and turning away anything unsafe or broken.</summary>
public class BookPackageTests
{
    static string MakeBook(TempFolder temp, string id = "my-book", string title = "My Book")
    {
        string folder = temp["src-" + id];
        Directory.CreateDirectory(folder);
        Pictures.MakePlain(Path.Combine(folder, "p1.jpg"), 320, 200, SKColors.Orange);
        Pictures.MakePlain(Path.Combine(folder, "cover.jpg"), 200, 300, SKColors.Purple);
        var book = new Book { Id = id, Title = title, Cover = "cover.jpg", Pages = { new Page { Image = "p1.jpg", Stops = { new Stop { Box = new RectD(0, 0, 1, 1), Caption = "Hello." } } } } };
        File.WriteAllText(Path.Combine(folder, "book.json"), BookJson.ToJsonString(book));
        return folder;
    }

    static string Zip(TempFolder temp, string name, params (string Entry, byte[] Data)[] entries)
    {
        string path = temp[name];
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entry, data) in entries)
        {
            using Stream s = zip.CreateEntry(entry).Open();
            s.Write(data);
        }
        return path;
    }

    static byte[] Text(string s) => Encoding.UTF8.GetBytes(s);
    static byte[] Jpeg(TempFolder temp)
    {
        string p = temp["tiny-" + Guid.NewGuid().ToString("N") + ".jpg"];
        Pictures.MakePlain(p, 16, 10, SKColors.Green);
        return File.ReadAllBytes(p);
    }
    const string OnePageJson = """{ "id": "z", "title": "Z", "pages": [ { "image": "p1.jpg", "stops": [] } ] }""";

    [Fact]
    public void A_book_file_goes_on_the_shelf_and_can_be_taken_off_again()
    {
        using var temp = new TempFolder();
        string package = temp["my-book" + BookPackage.Extension];
        BookPackage.Create(MakeBook(temp), package);
        string root = temp["imported"];

        BookPackage.Result r = BookPackage.Import(package, root);
        Assert.True(r.Ok, r.Message);
        Assert.Equal("Added \"My Book\" to the shelf.", r.Message);
        Assert.Equal("my-book", r.Id);
        Assert.True(File.Exists(Path.Combine(root, "books", "my-book", "book.json")));
        ShelfEntry entry = Assert.Single(BookPackage.List(root));
        Assert.Equal("My Book", entry.Title);
        Assert.Empty(Directory.GetDirectories(root, ".*"));        // nothing left over

        // The same book again replaces it.
        BookPackage.Result again = BookPackage.Import(package, root);
        Assert.True(again.Ok && again.Replaced);
        Assert.StartsWith("Updated", again.Message);
        Assert.Single(BookPackage.List(root));

        Assert.True(BookPackage.Remove(root, "my-book"));
        Assert.Empty(BookPackage.List(root));
        Assert.False(Directory.Exists(Path.Combine(root, "books", "my-book")));
        Assert.False(BookPackage.Remove(root, "../outside"));
    }

    [Fact]
    public void A_zip_with_the_book_at_the_top_works_too()
    {
        using var temp = new TempFolder();
        string zip = Zip(temp, "flat.zip", ("book.json", Text(OnePageJson)), ("p1.jpg", Jpeg(temp)));
        BookPackage.Result r = BookPackage.Import(zip, temp["imported"]);
        Assert.True(r.Ok, r.Message);
        Assert.Equal("z", r.Id);
    }

    [Theory]
    [InlineData("book/../../evil.txt")]
    [InlineData("book/../book.json.txt")]
    [InlineData("book//double.jpg")]
    [InlineData("book/C:/evil.txt")]
    [InlineData("book/sub/../../x.jpg")]
    public void Names_that_try_to_leave_the_folder_are_refused(string bad)
    {
        using var temp = new TempFolder();
        string zip = Zip(temp, "bad.picturebook", ("book/book.json", Text(OnePageJson)), ("book/p1.jpg", Jpeg(temp)), (bad, Text("nope")));
        string root = temp["imported"];
        BookPackage.Result r = BookPackage.Import(zip, root);
        Assert.False(r.Ok);
        Assert.Contains("keep the phone safe", r.Message);
        Assert.False(File.Exists(temp["evil.txt"]));
        Assert.Empty(BookPackage.List(root));
        Assert.False(Directory.Exists(Path.Combine(root, "books")) && Directory.EnumerateFileSystemEntries(Path.Combine(root, "books")).Any());
        Assert.Empty(Directory.GetDirectories(root, ".incoming-*"));
    }

    [Fact]
    public void A_link_inside_the_zip_is_refused()
    {
        using var temp = new TempFolder();
        string path = temp["link.picturebook"];
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (Stream s = zip.CreateEntry("book/book.json").Open()) s.Write(Text(OnePageJson));
            using (Stream s = zip.CreateEntry("book/p1.jpg").Open()) s.Write(Jpeg(temp));
            ZipArchiveEntry link = zip.CreateEntry("book/sounds");
            link.ExternalAttributes = unchecked((int)(0xA1FFu << 16));       // a Unix symbolic link
            using (Stream s = link.Open()) s.Write(Text("/etc"));
        }
        BookPackage.Result r = BookPackage.Import(path, temp["imported"]);
        Assert.False(r.Ok);
        Assert.Contains("links", r.Message);
    }

    [Fact]
    public void Broken_or_wrong_files_get_a_plain_answer()
    {
        using var temp = new TempFolder();
        string root = temp["imported"];
        File.WriteAllText(temp["photo.picturebook"], "this is not a zip");
        BookPackage.Result notZip = BookPackage.Import(temp["photo.picturebook"], root);
        Assert.False(notZip.Ok);
        Assert.Contains("isn’t a picture book", notZip.Message);

        BookPackage.Result noBook = BookPackage.Import(Zip(temp, "nobook.zip", ("holiday/photo.jpg", Jpeg(temp))), root);
        Assert.Contains("no book in that file", noBook.Message);

        BookPackage.Result damaged = BookPackage.Import(Zip(temp, "damaged.zip", ("b/book.json", Text("{ \"pages\": [ ")), ("b/p1.jpg", Jpeg(temp))), root);
        Assert.Contains("damaged", damaged.Message);

        BookPackage.Result noPages = BookPackage.Import(Zip(temp, "empty.zip", ("b/book.json", Text("""{ "id": "e", "pages": [] }"""))), root);
        Assert.Contains("no pages", noPages.Message);

        BookPackage.Result missing = BookPackage.Import(Zip(temp, "missing.zip", ("b/book.json", Text(OnePageJson))), root);
        Assert.Contains("pictures are missing", missing.Message);
        Assert.Contains("p1.jpg", missing.Message);

        BookPackage.Result fake = BookPackage.Import(Zip(temp, "fake.zip", ("b/book.json", Text(OnePageJson)), ("b/p1.jpg", Text("not a picture at all"))), root);
        Assert.Contains("pictures are missing", fake.Message);

        BookPackage.Result two = BookPackage.Import(Zip(temp, "two.zip", ("a/book.json", Text(OnePageJson)), ("b/book.json", Text(OnePageJson))), root);
        Assert.Contains("more than one book", two.Message);

        Assert.Empty(BookPackage.List(root));
        // None of the messages are technical.
        foreach (var r in new[] { notZip, noBook, damaged, noPages, missing, fake, two })
        {
            Assert.DoesNotContain("Exception", r.Message);
            Assert.DoesNotContain("\u2014", r.Message);
        }
    }

    [Fact]
    public void Sizes_are_counted_while_unpacking()
    {
        using var temp = new TempFolder();
        string zip = Zip(temp, "big.zip", ("b/book.json", Text(OnePageJson)), ("b/p1.jpg", Jpeg(temp)), ("b/sounds/huge.wav", new byte[200_000]));
        var tight = new BookPackage.Limits { MaxTotalBytes = 100_000 };
        BookPackage.Result r = BookPackage.Import(zip, temp["imported"], tight);
        Assert.False(r.Ok);
        Assert.Contains("too big", r.Message);
        var oneFile = new BookPackage.Limits { MaxFileBytes = 50_000 };
        Assert.Contains("too big", BookPackage.Import(zip, temp["imported"], oneFile).Message);
        Assert.True(BookPackage.Import(zip, temp["imported"]).Ok);
    }

    [Theory]
    [InlineData("Moss & the Puddle!", "moss-the-puddle")]
    [InlineData("../../etc", "etc")]
    [InlineData("   ", null)]
    [InlineData("Ünïcode", "n-code")]
    public void Ids_become_safe_folder_names(string id, string? expected) => Assert.Equal(expected, BookPackage.SafeId(id));

    [Fact]
    public void Export_makes_a_book_file_the_apps_can_take_in()
    {
        using var temp = new TempFolder();
        BookProject p = BookProject.Open(MakeBook(temp, "export-me", "Export me"));
        Directory.CreateDirectory(Path.Combine(p.Folder, "voice"));
        Editor.Core.Wav.Write(Path.Combine(p.Folder, "voice", "crab.wav"), new float[1600], 16000);
        p.Book.Pages.Add(new Page { Image = "p1.jpg", Explore = true, Hotspots = { new Hotspot { Box = new RectD(0, 0, 0.2, 0.2), Caption = "A crab.", Voice = new Voice { File = "voice/crab.wav" } } } });
        ExportResult r = Exporter.Export(p, temp["out"], ExportOptions.Phone);
        Assert.Equal(Path.Combine(temp["out"], "export-me" + BookPackage.Extension), r.BookFile);
        Assert.True(File.Exists(Path.Combine(r.Folder, "voice", "crab.wav")), "a found thing's voice is exported");
        BookPackage.Result taken = BookPackage.Import(r.BookFile!, temp["phone"]);
        Assert.True(taken.Ok, taken.Message);
        Book onPhone = BookJson.Load(taken.Folder!);
        Assert.Equal("voice/crab.wav", onPhone.Pages[1].Hotspots[0].Voice!.File);
    }
}

/// <summary>Lining up what the speech recogniser heard with the words of a caption.</summary>
public class WordAlignerTests
{
    /// <summary>Heard words, one every 0.5 s from <paramref name="start"/>, each lasting 0.4 s.</summary>
    static List<HeardWord> Heard(double start, params string[] words) =>
        words.Select((w, i) => new HeardWord(w, start + i * 0.5, start + i * 0.5 + 0.4)).ToList();

    static void Increasing(IReadOnlyList<double> t)
    {
        for (int i = 1; i < t.Count; i++) Assert.True(t[i] >= t[i - 1] + 0.049, $"word {i} at {t[i]} is not after {t[i - 1]}");
    }

    [Fact]
    public void Words_heard_exactly_get_their_own_times()
    {
        var r = WordAligner.Align("Moss had new boots.", Heard(1, "Moss", "had", "new", "boots."), 0, 5)!;
        Assert.Equal(new[] { 1.0, 1.5, 2.0, 2.5 }, r.Times);
        Assert.Equal(4, r.Clear);
        Assert.Equal(0, r.Guessed);
    }

    [Fact]
    public void Punctuation_capitals_and_numbers_written_differently_still_match()
    {
        var r = WordAligner.Align("\"Splash!\" said Moss, 3 times.", Heard(0, "splash", "Said", "moss", "three", "times"), 0, 5)!;
        Assert.Equal(new[] { 0.0, 0.5, 1.0, 1.5, 2.0 }, r.Times);
        Assert.Equal(5, r.Clear);
        // Afrikaans numbers too.
        Assert.Equal("3", WordAligner.Normal("drie"));
        Assert.Equal("3", WordAligner.Normal("3,"));
        Assert.Equal("ne", WordAligner.Normal("Nê?"));
    }

    [Fact]
    public void A_misheard_word_keeps_its_place()
    {
        var r = WordAligner.Align("Moss looked at the puddle", Heard(0, "must", "looked", "at", "the", "poodle"), 0, 4)!;
        Assert.Equal(new[] { 0.0, 0.5, 1.0, 1.5, 2.0 }, r.Times);
        Assert.True(r.Clear >= 3);
    }

    [Fact]
    public void A_word_not_heard_is_placed_between_its_neighbours()
    {
        var r = WordAligner.Align("the very big puddle", Heard(0, "the", "big", "puddle"), 0, 3)!;
        Assert.Equal(0.0, r.Times[0]);
        Assert.InRange(r.Times[1], 0.4, 0.5);          // after "the" ends (0.4), before "big" (0.5)
        Assert.Equal(0.5, r.Times[2]);
        Assert.Equal(1.0, r.Times[3]);
        Assert.Equal(1, r.Guessed);
        Increasing(r.Times);
    }

    [Fact]
    public void A_first_word_not_heard_goes_before_the_rest()
    {
        // As heard from a real recording: the rough ends can come before the starts once those are put right.
        var heard = new List<HeardWord> { new("A", 0.38, -0.1), new("frog", 1.07, 0.42), new("on", 1.12, 1.25), new("a", 1.29, 1.4), new("lily", 1.53, 1.7), new("pad.", 1.79, 2.1) };
        var r = WordAligner.Align("Ribbit! A frog on a lily pad.", heard, 0, 2.5)!;
        Assert.Equal(new[] { 0.0, 0.38, 1.07, 1.12, 1.29, 1.53, 1.79 }, r.Times);
        Assert.Equal(6, r.Clear);
    }

    [Fact]
    public void Extra_sounds_heard_are_left_out()
    {
        var r = WordAligner.Align("Look at that", Heard(0, "um", "look", "at", "at", "that"), 0, 4)!;
        Assert.Equal(0.5, r.Times[0]);
        Assert.True(r.Times[1] is 1.0 or 1.5);
        Assert.Equal(2.0, r.Times[2]);
    }

    [Fact]
    public void One_word_heard_as_two_and_two_heard_as_one()
    {
        var split = WordAligner.Align("a raindrop fell", Heard(0, "a", "rain", "drop", "fell"), 0, 4)!;
        Assert.Equal(new[] { 0.0, 0.5, 1.5 }, split.Times);

        var joined = new List<HeardWord> { new("into", 0, 0.6), new("the", 0.7, 0.9), new("puddle", 1.0, 1.5) };
        var r = WordAligner.Align("in to the puddle", joined, 0, 3)!;
        Assert.Equal(0.0, r.Times[0]);
        Assert.InRange(r.Times[1], 0.2, 0.4);          // shared out within "into"
        Assert.Equal(0.7, r.Times[2]);
        Assert.Equal(1.0, r.Times[3]);
    }

    [Fact]
    public void Nothing_heard_or_nothing_alike_gives_nothing()
    {
        Assert.Null(WordAligner.Align("Moss had new boots.", new List<HeardWord>(), 0, 3));
        Assert.Null(WordAligner.Align("Moss had new boots.", Heard(0, "xyzzy", "qwop"), 0, 3));
        Assert.Null(WordAligner.Align("", Heard(0, "hello"), 0, 3));
    }

    [Fact]
    public void Times_stay_inside_the_trim_and_in_order()
    {
        // Heard words before the start trim and after the end trim don't count, and every time is inside.
        var heard = new List<HeardWord> { new("cough", 0.1, 0.3), new("one", 1.0, 1.2), new("two", 1.2, 1.25), new("three", 1.22, 1.5), new("bye", 9, 9.5) };
        var r = WordAligner.Align("one two three four", heard, 0.8, 2.0)!;
        Assert.All(r.Times, t => Assert.InRange(t, 0.8, 1.95));
        Increasing(r.Times);
        Assert.Equal(1.0, r.Times[0]);
        Assert.True(r.Times[3] > r.Times[2]);           // "four" wasn't heard: after "three", before the end
    }

    [Fact]
    public void Unheard_words_at_the_start_and_end_spread_into_the_trimmed_part()
    {
        var r = WordAligner.Align("once upon a time there was", Heard(1.0, "a", "time"), 0.5, 4.0)!;
        Assert.Equal(6, r.Times.Count);
        Assert.True(r.Times[0] >= 0.5 && r.Times[1] < 1.0);
        Assert.Equal(1.0, r.Times[2]);
        Assert.Equal(1.5, r.Times[3]);
        Assert.True(r.Times[4] >= 1.9 && r.Times[5] < 4.0);
        Increasing(r.Times);
    }

    [Fact]
    public void Pieces_of_words_are_joined_and_markers_left_out()
    {
        var pieces = new List<HeardPieces.Piece>
        {
            new("[_BEG_]", 0, 0), new(" Moss", 0.0, 0.3, 0.2), new(" pud", 1.5, 1.7), new("dle", 1.7, 1.9), new(".", 1.9, 2.0), new("[_TT_100]", 2, 2),
            new(" It", 3.0, 3.1),
        };
        List<HeardWord> words = HeardPieces.ToWords(pieces, offset: 10);
        Assert.Equal(new[] { "Moss", "puddle.", "It" }, words.Select(w => w.Text));
        Assert.Equal(10.1, words[0].Start, 3);          // halfway between the piece's start and its attention point
        Assert.Equal(11.5, words[1].Start, 3);
        Assert.Equal(12.0, words[1].End, 3);
    }

    [Fact]
    public void A_start_in_a_pause_moves_to_where_the_sound_begins()
    {
        const int rate = 16000;
        var samples = new float[rate * 3];
        void Tone(double from, double to) { for (int i = (int)(from * rate); i < (int)(to * rate); i++) samples[i] = 0.5f * MathF.Sin(i * 0.3f); }
        Tone(0.50, 0.90);
        Tone(1.20, 1.80);
        Tone(1.80, 2.40);                                // runs straight on from the word before
        var heard = new List<HeardWord> { new("a", 0.35, 0.9), new("b", 1.30, 1.8), new("c", 1.85, 2.4) };
        List<HeardWord> snapped = Onsets.Snap(samples, rate, heard);
        Assert.Equal(0.50, snapped[0].Start, 2);         // was in the quiet before: on to the sound
        Assert.Equal(1.20, snapped[1].Start, 2);         // was inside the word: back to its start
        Assert.Equal(1.85, snapped[2].Start, 2);         // no pause before it: left alone
    }
}

/// <summary>Downloading the speech recogniser's file.</summary>
public class SpeechModelTests
{
    sealed class FakeServer(byte[] data, bool ranges = true) : HttpMessageHandler
    {
        public int Requests;
        public CancellationTokenSource? CancelAfterFirst;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Requests++;
            long from = ranges && request.Headers.Range?.Ranges.FirstOrDefault()?.From is long f ? f : 0;
            var body = new MemoryStream(data, (int)from, data.Length - (int)from);
            var r = new HttpResponseMessage(from > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK) { Content = new StreamContent(body) };
            CancelAfterFirst?.Cancel();
            return Task.FromResult(r);
        }
    }

    static (byte[] Data, SpeechModel.Source Source) Payload(int size = 300_000)
    {
        var data = new byte[size];
        new Random(4).NextBytes(data);
        string sum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();
        return (data, new SpeechModel.Source("https://example.invalid/model.bin", size, sum));
    }

    [Fact]
    public async Task It_downloads_checks_and_keeps_the_file()
    {
        using var temp = new TempFolder();
        var (data, source) = Payload();
        var model = new SpeechModel(temp["models"], source);
        Assert.False(model.Ready);
        var seen = new List<long>();
        await model.Download(new Progress<(long Done, long Total)>(p => seen.Add(p.Done)), CancellationToken.None, new FakeServer(data));
        Assert.True(model.Ready);
        Assert.Equal(data, File.ReadAllBytes(model.FilePath));
        Assert.False(File.Exists(model.FilePath + ".part"));
        // Ready: no second download.
        var server = new FakeServer(data);
        await model.Download(null, CancellationToken.None, server);
        Assert.Equal(0, server.Requests);
    }

    [Fact]
    public async Task A_damaged_download_is_thrown_away()
    {
        using var temp = new TempFolder();
        var (data, source) = Payload();
        var model = new SpeechModel(temp["models"], source with { Sha256 = new string('0', 64) });
        var e = await Assert.ThrowsAsync<IOException>(() => model.Download(null, CancellationToken.None, new FakeServer(data)));
        Assert.Contains("damaged", e.Message);
        Assert.False(model.Ready);
        Assert.False(File.Exists(model.FilePath + ".part"));
    }

    [Fact]
    public async Task A_stopped_download_carries_on_from_where_it_got_to()
    {
        using var temp = new TempFolder();
        var (data, source) = Payload();
        var model = new SpeechModel(temp["models"], source);
        Directory.CreateDirectory(model.Folder);
        File.WriteAllBytes(model.FilePath + ".part", data[..120_000]);        // what a stopped download left
        var server = new FakeServer(data);
        await model.Download(null, CancellationToken.None, server);
        Assert.True(model.Ready);
        Assert.Equal(data, File.ReadAllBytes(model.FilePath));
    }

    [Fact]
    public async Task Cancelling_stops_the_download()
    {
        using var temp = new TempFolder();
        var (data, source) = Payload();
        var model = new SpeechModel(temp["models"], source);
        using var cancel = new CancellationTokenSource();
        var server = new FakeServer(data) { CancelAfterFirst = cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.Download(null, cancel.Token, server));
        Assert.False(model.Ready);
    }

    [Fact]
    public void The_real_download_is_the_size_people_are_told()
    {
        var model = new SpeechModel(Path.GetTempPath());
        Assert.InRange(model.Megabytes, 100, 200);
        Assert.Equal(64, SpeechModel.Standard.Sha256.Length);
    }
}

/// <summary>Joined spread pictures don't pile up when a half is replaced, and people's own pictures are never deleted.</summary>
public class JoinedPictureTests
{
    [Fact]
    public void Old_joined_spreads_are_tidied_away_and_originals_kept()
    {
        using var temp = new TempFolder();
        BookProject p = BookProject.Create(temp.Path, "Spreads");
        foreach (string n in new[] { "page3-left.jpg", "page3-right.jpg", "new-left.jpg", "newer-left.jpg" })
            Pictures.MakePlain(Path.Combine(p.Folder, n), 200, 300, SKColors.Teal);
        // A picture someone brought in that happens to have a joined-looking name is still theirs.
        Pictures.MakePlain(Path.Combine(p.Folder, "mine-spread.jpg"), 400, 300, SKColors.Teal);

        Page page = p.JoinPage("page3-left.jpg", "page3-right.jpg", "page3");
        p.Book.Pages.Add(page);
        string first = page.Image;
        Assert.True(Pictures.IsJoined(p.PathOf(first)!));
        Assert.False(Pictures.IsJoined(p.PathOf("page3-left.jpg")!));
        p.Save();

        // Replace the left half twice. Each makes a new joined picture (so undo can still go back).
        string second = p.JoinPage("new-left.jpg", "page3-right.jpg", "page3").Image;
        string third = p.JoinPage("newer-left.jpg", "page3-right.jpg", "page3").Image;
        Assert.Equal(3, new[] { first, second, third }.Distinct().Count());
        page.Image = third;
        page.Spread = new SpreadSource { Left = "newer-left.jpg", Right = "page3-right.jpg" };

        // Undo could still go back to the second one: that is kept, as is the first (the last saved book).
        Book undoable = BookJson.Parse(BookJson.ToJsonString(p.Book));
        undoable.Pages[0].Image = second;
        p.Save();
        List<string> gone = p.TidyJoinedPictures(new[] { undoable });
        Assert.Empty(gone);

        // Saved again with no undo left: the first and second go; every picture brought in stays.
        p.Save();
        gone = p.TidyJoinedPictures();
        Assert.Equal(new[] { first, second }.OrderBy(x => x), gone.OrderBy(x => x));
        foreach (string n in new[] { "page3-left.jpg", "page3-right.jpg", "new-left.jpg", "newer-left.jpg", "mine-spread.jpg", third })
            Assert.True(File.Exists(Path.Combine(p.Folder, n)), n + " was deleted");
    }

    [Fact]
    public void A_joined_spread_still_opens_as_a_picture()
    {
        using var temp = new TempFolder();
        Pictures.MakePlain(temp["l.jpg"], 100, 80, SKColors.Red);
        Pictures.MakePlain(temp["r.jpg"], 100, 80, SKColors.Blue);
        Pictures.JoinSpread(temp["l.jpg"], temp["r.jpg"], temp["s.jpg"]);
        Assert.Equal((200, 80), Pictures.Size(temp["s.jpg"]));
        Pictures.MarkJoined(temp["s.jpg"]);                          // marking twice changes nothing
        Assert.Equal((200, 80), Pictures.Size(temp["s.jpg"]));
        Assert.True(Pictures.IsJoined(temp["s.jpg"]));
    }

    [Fact]
    public void Check_book_and_unused_files_know_about_voices_on_things_to_find()
    {
        using var temp = new TempFolder();
        BookProject p = BookProject.Create(temp.Path, "Finds");
        Pictures.MakePlain(Path.Combine(p.Folder, "scene.jpg"), 1600, 800, SKColors.Navy);
        Directory.CreateDirectory(Path.Combine(p.Folder, "voice"));
        Editor.Core.Wav.Write(Path.Combine(p.Folder, "voice", "crab.wav"), new float[1600], 16000);
        p.Book.Pages.Add(new Page
        {
            Image = "scene.jpg", Explore = true,
            Hotspots =
            {
                new Hotspot { Box = new RectD(0.1, 0.1, 0.1, 0.1), Caption = "A crab.", Voice = new Voice { File = "voice/crab.wav" } },
                new Hotspot { Box = new RectD(0.5, 0.1, 0.1, 0.1), Caption = "A fish.", Voice = new Voice { File = "voice/gone.wav" } },
            },
        });
        Assert.DoesNotContain("voice/crab.wav", p.UnusedFiles());
        var issues = BookChecker.Check(p);
        Assert.Contains(issues, i => i.Level == Severity.Problem && i.Text.Contains("recording is missing: voice/gone.wav"));
        Assert.Contains(issues, i => i.Level == Severity.Tip && i.Text.Contains("thing to find 1") && i.Text.Contains("aren't timed"));
    }
}
