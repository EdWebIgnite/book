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
/// The bench hook: play a fixed sequence with no window (Moss and the Biggest Puddle, pages 1 to 3,
/// a tap whenever the camera has come to rest) and time every frame that gets drawn. Reader time is
/// made up, sixty frames a second, so the same frames are drawn every run and the numbers from two
/// builds can be compared. What is timed is the work of one frame: moving the engine on, recording
/// the drawing, and painting it.
/// </summary>
static class Bench
{
    public static int Run(Options o)
    {
        try
        {
            AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();

            var settings = new ReaderSettings();
            if (o.Set.Count > 0) settings.ApplyJson(o.SetAsJson());

            var window = new MainWindow(o, settings, new NoSound(), saveSettings: false, state: new AppState { Welcomed = true }) { Width = o.Width, Height = o.Height };
            ReaderView view = window.Screen.View;
            double clock = 100_000;
            view.Clock = () => clock;
            view.AutoFrames = false;
            // Pictures load in the background here, as they do in real use, so that reading a file from
            // disk is not counted as part of a frame.
            window.Show();
            if (o.Scale > 0 && Math.Abs(o.Scale - 1) > 0.001) window.SetRenderScaling(o.Scale);
            Dispatcher.UIThread.RunJobs();
            window.LoadShelf(coversNow: true);

            string book = o.Book ?? "biggest-puddle";
            if (window.ResolveBook(book) is not string folder)
            {
                Console.Error.WriteLine($"Couldn't find the book \"{book}\".");
                return 1;
            }
            // What the program holds before any book is open: the runtime, the drawing library, the window.
            long before = CurrentProcessMemory();
            window.OpenBookNow(folder);
            Dispatcher.UIThread.RunJobs();
            ReaderEngine engine = window.Screen.Engine;

            const double frameMs = 1000.0 / 60;
            var all = new List<double>();
            var moving = new List<double>();
            var record = new List<double>();
            var turns = new List<double>();       // frames while a page was curling over
            var cover = new List<double>();       // frames while the cover was swinging open
            var turnsRecord = new List<double>(); // the part before painting, while a page was turning
            var allocated = new List<double>();   // bytes allocated on the screen's thread by each frame drawn
            var watch = new Stopwatch();

            void Frame()
            {
                clock += frameMs;
                int before = view.RenderCount;
                bool wasMoving = engine.Animating;
                bool turning = engine.GetFlip() is not null, swinging = engine.GetCover() is { Open: > 0 };
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                int gcBefore = GC.CollectionCount(0);
                watch.Restart();
                view.Tick();
                Dispatcher.UIThread.RunJobs();
                double recorded = watch.Elapsed.TotalMilliseconds;
                long bytesRecorded = GC.GetAllocatedBytesForCurrentThread() - bytes;
                int gcDuring = GC.CollectionCount(0) - gcBefore;
                int gcBeforeCapture = GC.CollectionCount(0);
                BenchPaint(window);
                if (SlowLog && recorded > 8) Console.WriteLine($"    slow before painting: {recorded:0.0} ms, {gcDuring} collections, page {engine.CurrentStep.Page + 1} stop {engine.CurrentStep.Stop}, turning {turning}, {bytesRecorded / 1024} KB allocated");
                CaptureGcs += GC.CollectionCount(0) - gcBeforeCapture;
                UiGcs += gcDuring;
                watch.Stop();
                if (view.RenderCount == before) return;          // nothing changed: no frame was drawn
                double ms = watch.Elapsed.TotalMilliseconds;
                allocated.Add(bytesRecorded);
                record.Add(recorded);
                all.Add(ms);
                if (wasMoving || engine.Animating) moving.Add(ms);
                if (turning || engine.GetFlip() is not null) { turns.Add(ms); turnsRecord.Add(recorded); }
                if (swinging || engine.GetCover() is { Open: > 0 }) cover.Add(ms);
                // Let pictures that finished loading in the background be handed over between frames.
                Dispatcher.UIThread.RunJobs();
            }

            // Warm up: the first frames pay for things that only happen once (fonts, first use of each picture).
            for (int i = 0; i < 30; i++) Frame();
            for (int i = 0; i < 100 && view.PicturesLoading > 0; i++) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }
            all.Clear();
            moving.Clear();
            record.Clear();
            turns.Clear();
            cover.Clear();
            allocated.Clear();
            turnsRecord.Clear();
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            long allocatedBefore = GC.GetTotalAllocatedBytes();

            int lastPage = Math.Clamp(o.BenchPages, 1, engine.Book!.Pages.Count);
            var tapAt = new Point(o.Width * 0.75, o.Height * 0.5);
            int taps = 0;
            while (!engine.Finished && engine.CurrentStep.Page < lastPage && taps < 200)
            {
                window.MouseMove(tapAt);
                window.MouseDown(tapAt, MouseButton.Left);
                clock += 50;
                window.MouseUp(tapAt, MouseButton.Left);
                taps++;
                // Let the move (and any pan after it) finish, then rest a moment, as a child would.
                double rested = 0;
                for (int i = 0; i < 1200 && rested < 500; i++)
                {
                    Frame();
                    rested = engine.Animating ? 0 : rested + frameMs;
                }
            }

            int gcs0 = GC.CollectionCount(0) - gc0, gcs1 = GC.CollectionCount(1) - gc1, gcs2 = GC.CollectionCount(2) - gc2;
            long allocatedAll = GC.GetTotalAllocatedBytes() - allocatedBefore;

            // What the bench itself costs: an empty frame painted and read back, which a real window never does.
            view.ClearBook();
            var empty = new List<double>();
            for (int i = 0; i < 40; i++)
            {
                view.InvalidateVisual();
                watch.Restart();
                Dispatcher.UIThread.RunJobs();
                BenchPaint(window);
                watch.Stop();
                if (i >= 10) empty.Add(watch.Elapsed.TotalMilliseconds);
            }

            double blank = empty.Count > 0 ? empty.Average() : 0;
            Console.WriteLine($"bench: {book}, pages 1 to {lastPage}, {taps} taps, window {o.Width}x{o.Height} at scale {o.Scale}{(o.Touch ? ", as the phone app" : "")}");
            Report("frames drawn", all);
            Report("  of each frame, the part before painting (engine, layout, recording the drawing)", record);
            Report("  of which, while a page was turning", turns);
            Report("    the part before painting, while a page was turning", turnsRecord);
            Report("  of which, while the cover was swinging open", cover);
            if (allocated.Count > 0)
            {
                var a = allocated.OrderBy(x => x).ToList();
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"  allocated on the screen's thread before painting: median {a[a.Count / 2] / 1024:0.0} KB a frame, average {a.Average() / 1024:0.0} KB, most {a[^1] / 1024:0.0} KB"));
            }
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  garbage collections while reading: {gcs0} quick (gen 0), {gcs1} gen 1, {gcs2} full (gen 2); {allocatedAll / 1048576.0:0.0} MB allocated in all, every thread ({UiGcs} of them during the reader's own work, {CaptureGcs} while the bench read frames back)"));
            Report("a blank window, for comparison (what the bench itself costs: painting an empty frame)", empty);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  so the reader's own drawing took about {Math.Max(0, all.Average() - blank):0.0} ms a frame on average, " +
                $"{Math.Max(0, all.Max() - blank):0.0} ms at worst, and {all.Count(x => x > blank + 20)} frames took more than 20 ms over a blank one"));
            long peak = System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64;
            string limits = Reader.Ui.LoadedBook.MaxPictureSide > 0 ? $"none over {Reader.Ui.LoadedBook.MaxPictureSide} pixels" : "at their own size";
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  memory: {before / 1048576.0:0} MB before the book opened, {peak / 1048576.0:0} MB at the most while it was read, so the book took up to {(peak - before) / 1048576.0:0} MB (keeping {Reader.Ui.LoadedBook.FullPicturesKept} full-size pictures, {limits})"));
            lock (Gcs.Reasons) foreach (var kv in Gcs.Reasons) Console.WriteLine($"  GC {kv.Key}: {kv.Value}");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("The bench failed: " + e.Message);
            return 1;
        }
    }

    /// <summary>Why each collection happened (reason 7 is a library telling the runtime about memory it holds outside it, as bitmaps do).</summary>
    sealed class GcListener : System.Diagnostics.Tracing.EventListener
    {
        public readonly Dictionary<string, int> Reasons = new();
        protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource s)
        {
            if (s.Name == "Microsoft-Windows-DotNETRuntime") EnableEvents(s, System.Diagnostics.Tracing.EventLevel.Informational, (System.Diagnostics.Tracing.EventKeywords)1);
        }
        protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs e)
        {
            if (e.EventName != "GCStart_V2" || e.Payload is null) return;
            string key = $"gen{e.Payload[1]} reason {e.Payload[2]} type {e.Payload[3]}";
            lock (Reasons) Reasons[key] = Reasons.GetValueOrDefault(key) + 1;
        }
    }
    static readonly GcListener Gcs = new();
    /// <summary>BENCH_READBACK=1 reads every frame back as a picture, as the bench did before; otherwise it is only painted.</summary>
    static readonly bool Readback = Environment.GetEnvironmentVariable("BENCH_READBACK") == "1";
    static void BenchPaint(Avalonia.Controls.Window window)
    {
        if (Readback) { using (window.CaptureRenderedFrame()) { } return; }
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
    /// <summary>BENCH_SLOW=1 lists every frame that took over 8 ms before painting.</summary>
    static readonly bool SlowLog = Environment.GetEnvironmentVariable("BENCH_SLOW") == "1";
    static int CaptureGcs, UiGcs;

    static long CurrentProcessMemory()
    {
        GC.Collect();
        using var me = System.Diagnostics.Process.GetCurrentProcess();
        return me.WorkingSet64;
    }

    static void Report(string what, List<double> ms)
    {
        if (ms.Count == 0) { Console.WriteLine($"  {what}: none"); return; }
        var sorted = ms.OrderBy(x => x).ToList();
        string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        double At(double share) => sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * share))];
        Console.WriteLine($"  {what}: {ms.Count} frames, average {F(ms.Average())} ms, median {F(At(0.5))} ms, 95th percentile {F(At(0.95))} ms, " +
                          $"99th {F(At(0.99))} ms, worst {F(sorted[^1])} ms; over 8.3 ms: {ms.Count(x => x > 1000 / 120.0)}, over 16.7 ms: {ms.Count(x => x > 1000 / 60.0)}, over 20 ms: {ms.Count(x => x > 20)}");
    }
}
