using System.Text.Json.Nodes;
using Reader.Core;

namespace Reader.Tests;

/// <summary>Shared helpers: finding the sample books, a made-up clock, and a sound output that just takes notes.</summary>
static class Kit
{
    /// <summary>The repo's content folder, found by walking up from where the tests run.</summary>
    public static string ContentRoot { get; } = FindUp(Path.Combine("content", "library.json"));

    public static string RepoRoot => Path.GetDirectoryName(ContentRoot)!;

    static string FindUp(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, relative))) return Path.Combine(dir.FullName, "content");
        }
        throw new DirectoryNotFoundException("Couldn't find content/library.json above " + AppContext.BaseDirectory);
    }

    public static IEnumerable<string> BookIds()
    {
        var lib = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentRoot, "library.json")))!;
        foreach (JsonNode? id in lib["books"]!.AsArray()) yield return id!.GetValue<string>();
    }

    public static string BookFolder(string id) => Path.Combine(ContentRoot, "books", id);

    public static Book Load(string id) => BookJson.Load(BookFolder(id));

    /// <summary>
    /// Page shapes for the engine. The tests don't decode pictures, and nothing they check depends on
    /// the exact shapes, so guided pages are 1.6 wide and explore scenes twice that. A tiled scene
    /// says how big it is, so that one is exact.
    /// </summary>
    public static double[] Aspects(Book book) =>
        book.Pages.Select(p => p.Tiles is { Height: > 0 } t ? (double)t.Width / t.Height : p.Explore ? 3.2 : 1.6).ToArray();

    /// <summary>An engine showing a book on a 1000 x 700 screen, with the settings a fresh install has.</summary>
    public static Rig Open(Book book, Action<ReaderSettings>? tweak = null) => Open(book, tweak, hasCover: false);

    /// <summary>The same, with the book's cover picture available, so it starts closed on its cover.</summary>
    public static Rig OpenWithCover(string id, Action<ReaderSettings>? tweak = null) => Open(Load(id), tweak, hasCover: true);

    public static Rig Open(Book book, Action<ReaderSettings>? tweak, bool hasCover)
    {
        var settings = new ReaderSettings { ReduceMotion = "off" };
        tweak?.Invoke(settings);
        var sound = new FakeSound();
        var engine = new ReaderEngine(settings, sound);
        engine.SetViewSize(1000, 700);
        var rig = new Rig(engine, sound);
        engine.SetTime(rig.Now);
        engine.SetBook(book, Aspects(book), 0, hasCover);
        engine.Update(rig.Now);
        return rig;
    }

    public static Rig Open(string id, Action<ReaderSettings>? tweak = null) => Open(Load(id), tweak);

    /// <summary>A book with one guided page, for tests that need something particular.</summary>
    public static Book OnePage(params Stop[] stops)
    {
        var page = new Page { Image = "page1.jpg" };
        page.Stops.AddRange(stops);
        return new Book { Id = "test", Title = "Test", FocusStyle = FocusStyles.Vignette, Pages = { page } };
    }

    public static Stop StopAt(double x, double y, double w = 0.3, double h = 0.3) => new() { Box = new RectD(x, y, w, h) };
}

/// <summary>An engine with its own clock. Time only moves when a test says so.</summary>
sealed class Rig
{
    public ReaderEngine Engine { get; }
    public FakeSound Sound { get; }
    public double Now { get; private set; } = 10_000;

    public Rig(ReaderEngine engine, FakeSound sound) { Engine = engine; Sound = sound; }

    /// <summary>Let time pass, a frame at a time.</summary>
    public void Run(double ms, double frameMs = 16)
    {
        double end = Now + ms;
        while (Now < end)
        {
            Now = Math.Min(end, Now + frameMs);
            Engine.Update(Now);
        }
    }

    /// <summary>Run until the camera has stopped (or give up after <paramref name="maxMs"/>). Returns how long it took.</summary>
    public double RunUntilStill(double maxMs = 20_000, double frameMs = 16)
    {
        double start = Now;
        while (Engine.Animating && Now - start < maxMs) Run(frameMs, frameMs);
        return Now - start;
    }

    /// <summary>A deliberate tap on "next": waits out the accidental-double-tap guard first.</summary>
    public void Next()
    {
        Run(320);
        Engine.Next();
    }

    public void Prev()
    {
        Run(320);
        Engine.Prev();
    }

    /// <summary>Tap next and wait for the camera to arrive and any pan to finish.</summary>
    public void NextAndArrive()
    {
        Next();
        RunUntilStill();
    }

    /// <summary>
    /// Go to the whole page shown again after a page's stops, the way a reader gets there: from its last
    /// stop. Returns false if the page has no such step.
    /// </summary>
    public bool JumpToClosing(int page)
    {
        Jump(page, Engine.Book!.Pages[page].Stops.Count - 1);
        NextAndArrive();
        return Engine.CurrentStep is { Closing: true } step && step.Page == page;
    }

    public void Jump(int page, int stop)
    {
        Engine.SetTime(Now);
        Engine.JumpTo(page, stop);
        Engine.Update(Now);
    }
}

/// <summary>Writes down every sound the engine asks for.</summary>
sealed class FakeSound : ISoundOutput
{
    public List<string> Calls { get; } = new();
    public List<double> RainLevels { get; } = new();
    public bool ClipPlaying { get; set; }
    public string? Clip { get; private set; }

    public void Tap() => Calls.Add("tap");
    public void Zoom(bool zoomIn) => Calls.Add(zoomIn ? "zoom in" : "zoom out");
    public void Find() => Calls.Add("find");
    public void Locked() => Calls.Add("locked");
    public void Party() => Calls.Add("party");
    public void Whoosh(double milliseconds) => Calls.Add("whoosh");
    public void PageTurn() => Calls.Add("page turn");
    public void Rain(double level) => RainLevels.Add(level);
    public void Thunder(double delaySeconds) => Calls.Add("thunder " + delaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public void PlayClip(string fileName) { Clip = fileName; Calls.Add("clip " + fileName); }
    public void StopClip() { Clip = null; ClipPlaying = false; }

    public int Count(string call) => Calls.Count(c => c == call);
}
