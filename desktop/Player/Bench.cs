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

            var window = new MainWindow(o, settings, new NoSound(), saveSettings: false) { Width = o.Width, Height = o.Height };
            ReaderView view = window.Screen.View;
            double clock = 100_000;
            view.Clock = () => clock;
            view.AutoFrames = false;
            // Pictures load in the background here, as they do in real use, so that reading a file from
            // disk is not counted as part of a frame.
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.LoadShelf(coversNow: true);

            string book = o.Book ?? "biggest-puddle";
            if (window.ResolveBook(book) is not string folder)
            {
                Console.Error.WriteLine($"Couldn't find the book \"{book}\".");
                return 1;
            }
            window.OpenBookNow(folder);
            Dispatcher.UIThread.RunJobs();
            ReaderEngine engine = window.Screen.Engine;

            const double frameMs = 1000.0 / 60;
            var all = new List<double>();
            var moving = new List<double>();
            var record = new List<double>();      // the part of each frame spent before painting: engine, layout, recording the drawing
            var watch = new Stopwatch();

            void Frame()
            {
                clock += frameMs;
                int before = view.RenderCount;
                bool wasMoving = engine.Animating;
                watch.Restart();
                view.Tick();
                Dispatcher.UIThread.RunJobs();
                double recorded = watch.Elapsed.TotalMilliseconds;
                using (window.CaptureRenderedFrame()) { }
                watch.Stop();
                if (view.RenderCount == before) return;          // nothing changed: no frame was drawn
                double ms = watch.Elapsed.TotalMilliseconds;
                record.Add(recorded);
                all.Add(ms);
                if (wasMoving || engine.Animating) moving.Add(ms);
                // Let pictures that finished loading in the background be handed over between frames.
                Dispatcher.UIThread.RunJobs();
            }

            // Warm up: the first frames pay for things that only happen once (fonts, first use of each picture).
            for (int i = 0; i < 30; i++) Frame();
            for (int i = 0; i < 100 && view.PicturesLoading > 0; i++) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }
            all.Clear();
            moving.Clear();
            record.Clear();

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

            // What the bench itself costs: an empty frame painted and read back, which a real window never does.
            view.ClearBook();
            var empty = new List<double>();
            for (int i = 0; i < 40; i++)
            {
                view.InvalidateVisual();
                watch.Restart();
                Dispatcher.UIThread.RunJobs();
                using (window.CaptureRenderedFrame()) { }
                watch.Stop();
                if (i >= 10) empty.Add(watch.Elapsed.TotalMilliseconds);
            }

            double blank = empty.Count > 0 ? empty.Average() : 0;
            Console.WriteLine($"bench: {book}, pages 1 to {lastPage}, {taps} taps, window {o.Width}x{o.Height}");
            Report("frames drawn", all);
            Report("  of each frame, the part before painting (engine, layout, recording the drawing)", record);
            Report("a blank window, for comparison (what the bench itself costs: painting and reading back an empty frame)", empty);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  so the reader's own drawing took about {Math.Max(0, all.Average() - blank):0.0} ms a frame on average, " +
                $"{Math.Max(0, all.Max() - blank):0.0} ms at worst, and {all.Count(x => x > blank + 20)} frames took more than 20 ms over a blank one"));
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("The bench failed: " + e.Message);
            return 1;
        }
    }

    static void Report(string what, List<double> ms)
    {
        if (ms.Count == 0) { Console.WriteLine($"  {what}: none"); return; }
        var sorted = ms.OrderBy(x => x).ToList();
        string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        Console.WriteLine($"  {what}: {ms.Count} frames, average {F(ms.Average())} ms, worst {F(sorted[^1])} ms, " +
                          $"95th percentile {F(sorted[(int)(sorted.Count * 0.95)])} ms, over 20 ms: {ms.Count(x => x > 20)}");
    }
}
