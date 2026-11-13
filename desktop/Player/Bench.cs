using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
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
            if (o.Set.Count > 0)
            {
                var json = new JsonObject();
                foreach (var (key, value) in o.Set)
                {
                    if (value is "true" or "false") json[key] = value == "true";
                    else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) json[key] = number;
                    else json[key] = value;
                }
                settings.ApplyJson(json);
            }

            var window = new MainWindow(o, settings, new NoSound(), saveSettings: false) { Width = o.Width, Height = o.Height };
            ReaderView view = window.Screen.View;
            double clock = 100_000;
            view.Clock = () => clock;
            view.AutoFrames = false;
            view.LoadPicturesAtOnce = true;
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
            var watch = new Stopwatch();

            void Frame()
            {
                clock += frameMs;
                int before = view.RenderCount;
                bool wasMoving = engine.Animating;
                watch.Restart();
                view.Tick();
                Dispatcher.UIThread.RunJobs();
                using (window.CaptureRenderedFrame()) { }
                watch.Stop();
                if (view.RenderCount == before) return;          // nothing changed: no frame was drawn
                double ms = watch.Elapsed.TotalMilliseconds;
                all.Add(ms);
                if (wasMoving || engine.Animating) moving.Add(ms);
            }

            // Warm up: the first frames pay for things that only happen once (fonts, first use of each picture).
            for (int i = 0; i < 30; i++) Frame();
            all.Clear();
            moving.Clear();

            int lastPage = Math.Min(o.Page > 1 ? o.Page : 3, engine.Book!.Pages.Count);
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

            Console.WriteLine($"bench: {book}, pages 1 to {lastPage}, {taps} taps, window {o.Width}x{o.Height}");
            Report("all frames drawn", all);
            Report("frames while the camera moved", moving);
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
