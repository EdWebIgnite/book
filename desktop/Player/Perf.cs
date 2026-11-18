using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Reader.Core;
using Reader.Ui;

namespace Player;

/// <summary>
/// The measuring hook (--perf): times the things around the frames that the bench doesn't, with no
/// window, and prints the numbers. Like the bench it paints without a graphics card, so painting times
/// are only for comparing two builds; the time spent on the screen's thread before painting, the
/// allocations and the memory are what a phone sees too.
///
///   open      each book: reading it and making its smaller pictures, then the first frame
///   explore   panning and zooming over the big explore pages (the waterhole's 6400-wide scene, the sea's tiles)
///   shelf     the shelf: books settling in and lifting under the pointer
///   memory    reading the biggest book from start to end: the most memory used, collections, and what is left after going back to the shelf
///   idle      how many frames are drawn while nothing moves, on each screen
/// </summary>
static class Perf
{
    public static int Run(Options o)
    {
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        SynchronizationContext.SetSynchronizationContext(new AvaloniaSynchronizationContext());
        string what = o.Perf ?? "open";
        try
        {
            return what switch
            {
                "open" => Open(o),
                "explore" => Explore(o),
                "shelf" => Shelf(o),
                "memory" => Memory(o),
                "idle" => Idle(o),
                "covers" => Covers(o),
                "readalong" => ReadAlong(o),
                "audio" => Audio(o),
                _ => Fail("Unknown --perf: " + what + " (open, explore, shelf, memory or idle)"),
            };
        }
        catch (Exception e)
        {
            return Fail("The measurement failed: " + e);
        }
    }

    static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    sealed class Rig
    {
        public required MainWindow Window;
        public double Clock = 100_000;
        public ReaderView View => Window.Screen.View;
        public ReaderEngine Engine => Window.Screen.Engine;

        public void Paint() => AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        /// <summary>One frame of made-up time: move on, record the drawing, paint. Returns (before painting, whole) in ms and the bytes allocated before painting.</summary>
        public (double Record, double Whole, long Bytes, bool Drawn) Frame(double ms = 1000.0 / 60)
        {
            Clock += ms;
            int before = View.RenderCount;
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            var w = Stopwatch.StartNew();
            View.Tick();
            Dispatcher.UIThread.RunJobs();
            double rec = w.Elapsed.TotalMilliseconds;
            long b = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Paint();
            double whole = w.Elapsed.TotalMilliseconds;
            Dispatcher.UIThread.RunJobs();
            return (rec, whole, b, View.RenderCount != before);
        }

        public void WaitForPictures()
        {
            for (int i = 0; i < 300 && View.PicturesLoading > 0; i++) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }
        }
    }

    /// <summary>A voice that plays in made-up time: it is where the rig's clock says it is.</summary>
    sealed class PretendVoice : ISoundOutput
    {
        public Func<double> Clock = () => 0;
        double _startedAt = double.NaN, _from, _to;
        public void Tap() { }
        public void Zoom(bool zoomIn) { }
        public void Find() { }
        public void Locked() { }
        public void Party() { }
        public void Whoosh(double milliseconds) { }
        public void PageTurn() { }
        public void Rain(double level) { }
        public void Thunder(double delaySeconds) { }
        public void PlayClip(string fileName) { }
        public void StopClip() { }
        public bool ClipPlaying => false;
        public void PlayVoice(string fileName, double fromSeconds, double toSeconds) { _startedAt = Clock(); _from = fromSeconds; _to = toSeconds > 0 ? toSeconds : fromSeconds + 8; }
        public void StopVoice() => _startedAt = double.NaN;
        public bool VoicePlaying => !double.IsNaN(_startedAt) && VoicePosition < _to;
        public double VoicePosition => double.IsNaN(_startedAt) ? -1 : _from + (Clock() - _startedAt) / 1000;
    }

    static Rig MakeRig(Options o, bool animate = false, ISoundOutput? sound = null)
    {
        Motion.Instant = !animate;
        var settings = new ReaderSettings();
        if (o.Set.Count > 0) settings.ApplyJson(o.SetAsJson());
        var state = new AppState { Welcomed = true };
        state.Add("", 0);
        var window = new MainWindow(o, settings, sound ?? new NoSound(), saveSettings: false, state: state) { Width = o.Width, Height = o.Height };
        var rig = new Rig { Window = window };
        window.Screen.View.Clock = () => rig.Clock;
        window.Screen.View.AutoFrames = false;
        window.Show();
        if (o.Scale > 0 && Math.Abs(o.Scale - 1) > 0.001) window.SetRenderScaling(o.Scale);
        Dispatcher.UIThread.RunJobs();
        return rig;
    }

    // ---------------------------------------------------------------- opening a book

    static int Open(Options o)
    {
        var rig = MakeRig(o);
        rig.Window.LoadShelf(coversNow: true);
        rig.Paint();
        string[] books = o.Book is string one ? new[] { one } : new[] { "biggest-puddle", "captain-megawatt", "deep-sea", "waterhole" };
        Console.WriteLine($"open: window {o.Width}x{o.Height} at scale {o.Scale}{(o.Touch ? ", as the phone app" : "")}, {Environment.ProcessorCount} processors");
        foreach (string id in books)
        {
            if (rig.Window.ResolveBook(id) is not string folder) return Fail("No book " + id);
            for (int run = 0; run < 3; run++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                long mem = Process.GetCurrentProcess().WorkingSet64;
                var w = Stopwatch.StartNew();
                // As AppView.OpenBook does it: the book is read on another thread, then shown.
                Task opening = rig.Window.App.OpenBook(folder);
                // OpenBook carries on on the screen's thread once the book is read, so keep that thread turning.
                while (!opening.IsCompleted)
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(1);
                }
                opening.GetAwaiter().GetResult();
                double loaded = w.Elapsed.TotalMilliseconds;
                rig.Frame(0);
                double firstFrame = w.Elapsed.TotalMilliseconds;
                long after = Process.GetCurrentProcess().WorkingSet64;
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"  {id,-17} run {run + 1}: shown after {loaded,6:0} ms, first frame painted at {firstFrame,6:0} ms, working set +{(after - mem) / 1048576.0:0} MB"));
                rig.Window.App.CloseBook();
                Dispatcher.UIThread.RunJobs();
            }
            // The parts: reading the pictures (any thread), then putting the book on screen (the screen's thread).
            var parts = Stopwatch.StartNew();
            LoadedBook alone = LoadedBook.Load(folder);
            double read = parts.Elapsed.TotalMilliseconds;
            alone.Dispose();
            parts.Restart();
            rig.Window.OpenBookNow(folder);
            double shown = parts.Elapsed.TotalMilliseconds;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {id,-17} reading the pictures {read:0} ms; reading and showing on the screen's thread {shown:0} ms"));
            rig.Window.App.CloseBook();
            Dispatcher.UIThread.RunJobs();
        }
        return 0;
    }

    // ---------------------------------------------------------------- frames, counted

    sealed class Frames
    {
        readonly List<double> _record = new(), _whole = new();
        readonly List<long> _bytes = new();
        int _gc0 = GC.CollectionCount(0), _gc2 = GC.CollectionCount(2);

        /// <summary>Count frames where the picture wasn't drawn too (the rest of the window may still have changed).</summary>
        public bool Every;
        int _pictureDrawn;

        public void Add((double Record, double Whole, long Bytes, bool Drawn) f)
        {
            if (f.Drawn) _pictureDrawn++;
            if (!f.Drawn && !Every) return;
            _record.Add(f.Record);
            _whole.Add(f.Whole);
            _bytes.Add(f.Bytes);
        }

        public void Print(string what)
        {
            Console.WriteLine(Every ? $"  {what}: {_whole.Count} frames, the picture drawn in {_pictureDrawn} of them" : $"  {what}: {_whole.Count} frames drawn");
            if (_whole.Count == 0) return;
            Line("    before painting (the screen's thread)", _record);
            Line("    whole frame, painted without a graphics card", _whole);
            var b = _bytes.OrderBy(x => x).ToList();
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"    allocated before painting: median {b[b.Count / 2] / 1024.0:0.0} KB a frame, most {b[^1] / 1024.0:0.0} KB; " +
                $"collections {GC.CollectionCount(0) - _gc0} (full: {GC.CollectionCount(2) - _gc2})"));
        }

        static void Line(string what, List<double> ms)
        {
            var s = ms.OrderBy(x => x).ToList();
            double At(double share) => s[Math.Min(s.Count - 1, (int)(s.Count * share))];
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{what}: median {At(0.5):0.00} ms, 95th {At(0.95):0.00}, 99th {At(0.99):0.00}, worst {s[^1]:0.00}; over 8.3 ms {ms.Count(x => x > 1000 / 120.0)}, over 16.7 ms {ms.Count(x => x > 1000 / 60.0)}"));
        }
    }

    // ---------------------------------------------------------------- exploring

    static int Explore(Options o)
    {
        var rig = MakeRig(o);
        rig.Window.LoadShelf(coversNow: true);
        Console.WriteLine($"explore: window {o.Width}x{o.Height} at scale {o.Scale}{(o.Touch ? ", as the phone app" : "")}");
        foreach (var (id, page) in new[] { ("waterhole", 8), ("deep-sea", 1), ("biggest-puddle", 12) })
        {
            if (rig.Window.ResolveBook(id) is not string folder) return Fail("No book " + id);
            rig.Window.OpenBookNow(folder);
            Dispatcher.UIThread.RunJobs();
            rig.Window.JumpTo(page, -2);
            for (int i = 0; i < 120; i++) rig.Frame();
            rig.WaitForPictures();
            for (int i = 0; i < 30; i++) rig.Frame();

            var frames = new Frames();
            double cx = o.Width / 2.0, cy = o.Height / 2.0;
            // Zoom in with the wheel, a notch at a time, letting each settle a little.
            for (int n = 0; n < 3; n++)
            {
                rig.Window.MouseMove(new Point(cx, cy));
                rig.Window.MouseWheel(new Point(cx, cy), new Vector(0, 1), RawInputModifiers.Control);
                for (int i = 0; i < 30; i++) frames.Add(rig.Frame());
            }
            // Pan: slow drags right and left across the scene, as a finger would.
            for (int pass = 0; pass < 4; pass++)
            {
                double dir = pass % 2 == 0 ? -1 : 1;
                var from = new Point(cx - dir * o.Width * 0.3, cy);
                rig.Window.MouseMove(from);
                rig.Window.MouseDown(from, MouseButton.Left);
                for (int i = 1; i <= 60; i++)
                {
                    var at = new Point(from.X + dir * o.Width * 0.6 * i / 60, cy + Math.Sin(i / 9.0) * 30);
                    rig.Window.MouseMove(at, RawInputModifiers.LeftMouseButton);
                    frames.Add(rig.Frame());
                }
                rig.Window.MouseUp(new Point(from.X + dir * o.Width * 0.6, cy), MouseButton.Left);
                // The glide after letting go.
                for (int i = 0; i < 60; i++) frames.Add(rig.Frame());
                rig.WaitForPictures();
            }
            frames.Print($"{id} page {page}");
            rig.Window.App.CloseBook();
            Dispatcher.UIThread.RunJobs();
        }
        return 0;
    }

    // ---------------------------------------------------------------- the shelf

    static int Shelf(Options o)
    {
        var rig = MakeRig(o, animate: true);
        rig.Window.LoadShelf(coversNow: true);
        rig.Window.App.ShowShelf();
        Console.WriteLine($"shelf: window {o.Width}x{o.Height} at scale {o.Scale}{(o.Touch ? ", as the phone app" : "")}");
        // The shelf's own animations run on real time, so frames are drawn at a real 60 a second here.
        var settle = new Frames();
        void Run(Frames into, double ms)
        {
            var clock = Stopwatch.StartNew();
            double next = 0;
            while (clock.Elapsed.TotalMilliseconds < ms)
            {
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                var w = Stopwatch.StartNew();
                Dispatcher.UIThread.RunJobs();
                double rec = w.Elapsed.TotalMilliseconds;
                long b = GC.GetAllocatedBytesForCurrentThread() - bytes;
                rig.Paint();
                into.Add((rec, w.Elapsed.TotalMilliseconds, b, true));
                next += 1000.0 / 60;
                double wait = next - clock.Elapsed.TotalMilliseconds;
                if (wait > 0) Thread.Sleep(TimeSpan.FromMilliseconds(wait));
            }
        }
        // The books settle in as the shelf appears (it is shown again, as after closing a book).
        rig.Window.App.Shelf.IsVisible = false;
        Dispatcher.UIThread.RunJobs();
        rig.Window.App.Shelf.IsVisible = true;
        Run(settle, 900);
        settle.Print("books settling in");

        // The pointer moving across the covers, each lifting and going down again.
        var hover = new Frames();
        for (int x = 80; x < o.Width - 80; x += 40)
        {
            rig.Window.MouseMove(new Point(x, o.Height * 0.45));
            Run(hover, 80);
        }
        hover.Print("pointer over the covers");
        return 0;
    }

    // ---------------------------------------------------------------- memory over a whole book

    static int Memory(Options o)
    {
        var rig = MakeRig(o);
        rig.Window.LoadShelf(coversNow: true);
        string id = o.Book ?? "waterhole";
        if (rig.Window.ResolveBook(id) is not string folder) return Fail("No book " + id);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var me = Process.GetCurrentProcess();
        me.Refresh();
        long start = me.WorkingSet64, managedStart = GC.GetTotalMemory(true);
        int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        long peak = start;
        var frames = new Frames();

        rig.Window.OpenBookNow(folder);
        Dispatcher.UIThread.RunJobs();
        var tapAt = new Point(o.Width * 0.75, o.Height * 0.5);
        int taps = 0;
        int lastPage = rig.Engine.Book!.Pages.Count - 1;
        while (!rig.Engine.Finished && taps < 400)
        {
            rig.Window.MouseMove(tapAt);
            rig.Window.MouseDown(tapAt, MouseButton.Left);
            rig.Clock += 50;
            rig.Window.MouseUp(tapAt, MouseButton.Left);
            taps++;
            double rested = 0;
            for (int i = 0; i < 1200 && rested < 400; i++)
            {
                try { frames.Add(rig.Frame()); }
                catch (Exception) { Console.Error.WriteLine($"failed at page {rig.Engine.CurrentStep.Page + 1}, step {rig.Engine.StepIndex}, turning {rig.Engine.GetFlip() is not null}"); throw; }
                rested = rig.Engine.Animating ? 0 : rested + 1000.0 / 60;
            }
            if (taps % 5 == 0)
            {
                me.Refresh();
                peak = Math.Max(peak, me.WorkingSet64);
            }
            // An explore page doesn't go on by tapping on the right: go on with the arrow once it has been looked at.
            if (rig.Engine.Exploring && rig.Engine.CurrentStep.Page == lastPage) break;
            if (rig.Engine.Exploring) rig.View.Do(e => e.Next());
        }
        me.Refresh();
        peak = Math.Max(peak, me.WorkingSet64);
        long peakOs = me.PeakWorkingSet64;
        int g0 = GC.CollectionCount(0) - gc0, g1 = GC.CollectionCount(1) - gc1, g2 = GC.CollectionCount(2) - gc2;
        frames.Print($"{id}: read to page {rig.Engine.CurrentStep.Page + 1} of {lastPage + 1} in {taps} taps");
        Console.WriteLine($"  collections while reading: {g0} gen 0, {g1} gen 1, {g2} gen 2 (the headless painter makes some of these itself)");

        rig.Window.App.CloseBook();
        for (int i = 0; i < 20; i++) { Dispatcher.UIThread.RunJobs(); rig.Paint(); }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Dispatcher.UIThread.RunJobs();
        me.Refresh();
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  working set: {start / 1048576.0:0} MB on the shelf before, {peak / 1048576.0:0} MB at the most while reading (the process's own peak {peakOs / 1048576.0:0} MB), " +
            $"{me.WorkingSet64 / 1048576.0:0} MB back on the shelf; managed heap {managedStart / 1048576.0:0.0} MB before, {GC.GetTotalMemory(true) / 1048576.0:0.0} MB after"));

        // Open and close it a few more times: what is left over each time is a leak.
        for (int round = 0; round < 3; round++)
        {
            rig.Window.OpenBookNow(folder);
            for (int i = 0; i < 60; i++) rig.Frame();
            rig.Window.App.CloseBook();
            for (int i = 0; i < 10; i++) { Dispatcher.UIThread.RunJobs(); rig.Paint(); }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Dispatcher.UIThread.RunJobs();
            me.Refresh();
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  after opening and closing it again ({round + 1}): {me.WorkingSet64 / 1048576.0:0} MB, managed heap {GC.GetTotalMemory(true) / 1048576.0:0.0} MB"));
        }
        return 0;
    }

    // ---------------------------------------------------------------- sound files

    /// <summary>
    /// How long a recording takes to read before it can start (it is read on another thread, never the
    /// screen's), the first time and when it was read before (a word tapped after the stop was read aloud).
    /// --book names the sound file here.
    /// </summary>
    static int Audio(Options o)
    {
        if (o.Book is not string file || !File.Exists(file)) return Fail("--perf audio needs --book <a sound file>");
        var kept = new Reader.Audio.DecodedSounds();
        for (int i = 0; i < 4; i++)
        {
            var w = Stopwatch.StartNew();
            float[]? samples = kept.Get(file, () => Reader.Audio.DesktopSound.ReadSoundFile(file));
            double first = w.Elapsed.TotalMilliseconds;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"audio: {Path.GetFileName(file)} read {(i == 0 ? "the first time" : "again")}: {first:0.0} ms, {(samples?.Length ?? 0) / 2.0 / 48000:0.0} s of sound"));
        }
        for (int i = 0; i < 3; i++)
        {
            var w = Stopwatch.StartNew();
            float[]? samples = Reader.Audio.DesktopSound.ReadSoundFile(file);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"audio: read again without keeping it: {w.Elapsed.TotalMilliseconds:0.0} ms"));
        }
        return 0;
    }

    // ---------------------------------------------------------------- read-along

    /// <summary>
    /// A stop read aloud with its words lighting up one by one in the pop-up: the frames while the voice
    /// plays, and the frames where the lit word moves on (the caption is laid out again then).
    /// </summary>
    static int ReadAlong(Options o)
    {
        if (o.Book is null) return Fail("--perf readalong needs --book <a book folder whose stop has a timed recording> and --page/--stop");
        var voice = new PretendVoice();
        var rig = MakeRig(o, sound: voice);
        voice.Clock = () => rig.Clock;
        rig.Window.LoadShelf(coversNow: true);
        if (rig.Window.ResolveBook(o.Book) is not string folder) return Fail("No book " + o.Book);
        rig.Window.OpenBookNow(folder);
        Dispatcher.UIThread.RunJobs();
        rig.Window.JumpTo(o.Page, o.Stop);
        var all = new Frames { Every = true };
        var wordChanges = new Frames { Every = true };
        int lastWord = -2;
        for (int i = 0; i < 600; i++)
        {
            var f = rig.Frame();
            int word = rig.Engine.Reading ? rig.Engine.ReadAlongWord() : -1;
            all.Add(f);
            if (word != lastWord) wordChanges.Add(f);
            lastWord = word;
        }
        Console.WriteLine($"readalong: {o.Book} page {o.Page} stop {o.Stop}, window {o.Width}x{o.Height}");
        all.Print("every frame while the voice reads");
        wordChanges.Print("the frames where the lit word moved on");
        return 0;
    }

    // ---------------------------------------------------------------- the cover, made while loading

    /// <summary>
    /// A check rather than a measurement: a book opened from the shelf has its cover and the printed words of
    /// its first page made while it loads, off the screen's thread. Every frame of the cover swinging open
    /// must come out exactly as when they are made on the screen's thread. Prints any frame that differs.
    /// </summary>
    static int Covers(Options o)
    {
        int bad = 0;
        foreach (string id in new[] { "biggest-puddle", "captain-megawatt", "waterhole", "deep-sea" })
        {
            List<string> Run(bool whileLoading)
            {
                // A new window each time, so the second opening doesn't carry on from where the first got to.
                var rig = MakeRig(o);
                rig.View.LoadPicturesAtOnce = true;
                rig.Window.LoadShelf(coversNow: true);
                string folder = rig.Window.ResolveBook(id) ?? throw new InvalidOperationException("No book " + id);
                if (whileLoading)
                {
                    Task opening = rig.Window.App.OpenBook(folder);
                    while (!opening.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
                    opening.GetAwaiter().GetResult();
                }
                else rig.Window.OpenBookNow(folder);
                Dispatcher.UIThread.RunJobs();
                var hashes = new List<string>();
                void Shot()
                {
                    rig.View.Tick();
                    Dispatcher.UIThread.RunJobs();
                    rig.View.InvalidateVisual();
                    using var frame = rig.Window.CaptureRenderedFrame();
                    using var ms = new MemoryStream();
                    frame!.Save(ms);
                    hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ms.ToArray()))[..16]);
                }
                for (int i = 0; i < 10; i++) { rig.Clock += 1000.0 / 60; Shot(); }
                rig.View.Do(e => e.Next());
                for (int i = 0; i < 90; i++) { rig.Clock += 1000.0 / 60; Shot(); }
                rig.Window.App.CloseBook();
                rig.Window.Close();
                Dispatcher.UIThread.RunJobs();
                return hashes;
            }
            List<string> a = Run(true), b = Run(false), c = Run(false);
            if (!b.SequenceEqual(c)) Console.WriteLine($"covers: {id}: two openings on the screen's thread differ, so this check can't tell");
            int differ = a.Zip(b).Count(p => p.First != p.Second);
            bad += differ;
            Console.WriteLine($"covers: {id}: {a.Count} frames, {differ} differ");
        }
        return bad == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- idle

    /// <summary>
    /// The screens at rest: how often the reader's picture is drawn and how much the screen's thread does in
    /// two seconds of nothing happening. (The real window's CPU is measured from outside; see the README.)
    /// </summary>
    static int Idle(Options o)
    {
        var rig = MakeRig(o);
        rig.Window.LoadShelf(coversNow: true);
        if (rig.Window.ResolveBook(o.Book ?? "biggest-puddle") is not string folder) return Fail("No book");
        rig.Window.OpenBookNow(folder);
        rig.Window.JumpTo(2, 1);
        for (int i = 0; i < 200; i++) rig.Frame();
        rig.WaitForPictures();
        for (int i = 0; i < 60; i++) rig.Frame();
        int drawn = 0;
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 120; i++) if (rig.Frame().Drawn) drawn++;
        Console.WriteLine($"idle: reading at rest, 120 frames' time: the picture was drawn {drawn} times, {(GC.GetAllocatedBytesForCurrentThread() - bytes) / 1024} KB allocated");
        // What one frame of the still page costs to paint, forced (the same work every moving frame does at least).
        var paint = new List<double>();
        for (int i = 0; i < 160; i++)
        {
            rig.View.InvalidateVisual();
            var pw = Stopwatch.StartNew();
            Dispatcher.UIThread.RunJobs();
            rig.Paint();
            if (i >= 40) paint.Add(pw.Elapsed.TotalMilliseconds);
        }
        paint.Sort();
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  the still page painted again (without a graphics card): median {paint[paint.Count / 2]:0.00} ms"));
        // The ten-a-second check that looks for anything starting to move by itself.
        bytes = GC.GetAllocatedBytesForCurrentThread();
        var w = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) { rig.Clock += 100; rig.View.Tick(); }
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  the idle check: {w.Elapsed.TotalMilliseconds / 100 * 1000:0} microseconds and {(GC.GetAllocatedBytesForCurrentThread() - bytes) / 100} bytes each time (ten a second)"));
        return 0;
    }
}
