using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Reader.Core;
using Reader.Ui;

namespace Player;

/// <summary>
/// The screenshot hook: build the same window the Player shows, with no screen attached, put it in
/// the state asked for, and save one frame as a PNG. Time is made up rather than read from a clock,
/// so the same command always draws the same picture.
/// </summary>
static class Screenshot
{
    public static int Take(Options o)
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
            view.LoadPicturesAtOnce = true;

            window.Show();
            if (o.Scale > 0 && Math.Abs(o.Scale - 1) > 0.001) window.SetRenderScaling(o.Scale);
            Dispatcher.UIThread.RunJobs();
            window.LoadShelf(coversNow: true);

            void Pass(double ms)
            {
                for (double t = 0; t < ms; t += 16)
                {
                    clock += 16;
                    view.Tick();
                }
                Dispatcher.UIThread.RunJobs();
            }

            if (o.Book is not null)
            {
                if (window.ResolveBook(o.Book) is not string folder)
                {
                    Console.Error.WriteLine($"Couldn't find the book \"{o.Book}\".");
                    return 1;
                }
                int page = o.Page;
                int? stop = o.Stop;
                bool placeGiven = o.PlaceGiven;
                o.Page = 1;
                o.Stop = null;
                o.PlaceGiven = false;
                window.OpenBookNow(folder);
                Dispatcher.UIThread.RunJobs();
                // With no page asked for, the book stays as it opens: on its cover, if it has one.
                if (placeGiven) window.JumpTo(page, stop);
                Pass(16);

                ReaderEngine engine = window.Screen.Engine;
                if (engine.Exploring)
                {
                    if (o.Look is (double x, double y))
                    {
                        view.Do(e =>
                        {
                            RectD cam = e.TakeCamera();
                            RectD pr = e.PageRects[e.CurrentStep.Page];
                            e.SetCam(new RectD(pr.X + x * pr.W - cam.W / 2, pr.Y + y * pr.H - cam.H / 2, cam.W, cam.H));
                        });
                    }
                    if (o.Zoom > 0 && Math.Abs(o.Zoom - 1) > 0.001)
                    {
                        view.Do(e =>
                        {
                            RectD cam = e.TakeCamera();
                            double midY = e.Insets.Top + (e.ViewHeight - e.Insets.Top - e.Insets.Bottom) / 2;
                            e.SetCam(e.ZoomAt(cam, o.Zoom, e.ViewWidth / 2, midY));
                        });
                    }
                    foreach (int n in o.Find)
                    {
                        if (n >= 1 && n <= engine.CurrentPage.Hotspots.Count) view.Do(e => e.React(n - 1));
                    }
                    window.Screen.Refresh();
                }
                Pass(o.WaitMs);
                if (o.Finish)
                {
                    view.Do(e => e.JumpTo(e.Book!.Pages.Count - 1, e.Steps[^1].Stop));
                    Pass(400);
                    view.Do(e => e.Next());
                    Pass(100);
                }
            }
            if (o.Do is not null)
            {
                string? problem = ActOut(o.Do, window, Pass, ms => clock += ms);
                if (problem is not null)
                {
                    Console.Error.WriteLine(problem);
                    return 2;
                }
            }
            if (o.ShowSettings) window.ToggleSettings(true);

            Dispatcher.UIThread.RunJobs();
            // Draw once more with everything settled: tiles and layer pictures asked for in the first frame are here now.
            view.InvalidateVisual();
            window.CaptureRenderedFrame();
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
            if (frame is null)
            {
                Console.Error.WriteLine("Nothing was drawn.");
                return 1;
            }
            string path = Path.GetFullPath(o.Shot!);
            if (Path.GetDirectoryName(path) is { Length: > 0 } dir) Directory.CreateDirectory(dir);
            frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            Console.WriteLine("saved " + path);
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Couldn't take the screenshot: " + e.Message);
            return 1;
        }
    }

    /// <summary>
    /// Act out a list of touches and key presses through the window's real input path, the same way a
    /// mouse and keyboard would arrive. Returns what was wrong with the list, or null.
    /// </summary>
    static string? ActOut(string script, MainWindow window, Action<double> pass, Action<double> skip)
    {
        static bool TryPoint(string text, out Point p)
        {
            string[] xy = text.Split(',');
            p = default;
            if (xy.Length != 2 || !double.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                               || !double.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y)) return false;
            p = new Point(x, y);
            return true;
        }

        foreach (string step in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] words = step.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (words[0].ToLowerInvariant())
            {
                case "tap" when words.Length == 2 && TryPoint(words[1], out Point at):
                    window.MouseMove(at);
                    window.MouseDown(at, MouseButton.Left);
                    Dispatcher.UIThread.RunJobs();
                    skip(60);
                    window.MouseUp(at, MouseButton.Left);
                    pass(16);
                    break;
                case "hold" when words.Length == 3 && TryPoint(words[1], out Point on)
                                 && double.TryParse(words[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double held):
                    window.MouseMove(on);
                    window.MouseDown(on, MouseButton.Left);
                    pass(held);
                    window.MouseUp(on, MouseButton.Left);
                    pass(16);
                    break;
                case "drag" when words.Length == 3 && TryPoint(words[1], out Point from) && TryPoint(words[2], out Point to):
                    window.MouseMove(from);
                    window.MouseDown(from, MouseButton.Left);
                    for (int i = 1; i <= 10; i++)
                    {
                        skip(20);
                        window.MouseMove(new Point(from.X + (to.X - from.X) * i / 10, from.Y + (to.Y - from.Y) * i / 10));
                        Dispatcher.UIThread.RunJobs();
                    }
                    skip(5);
                    window.MouseUp(to, MouseButton.Left);
                    pass(16);
                    break;
                case "wheel" when words.Length == 3 && TryPoint(words[1], out Point over)
                                  && double.TryParse(words[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double notches):
                    window.MouseMove(over);
                    window.MouseWheel(over, new Vector(0, notches));
                    pass(16);
                    break;
                case "key" when words.Length == 2 && Enum.TryParse(words[1], ignoreCase: true, out Key key):
                    window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
                    window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
                    pass(16);
                    break;
                case "wait" when words.Length == 2 && double.TryParse(words[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double ms):
                    pass(ms);
                    break;
                case "record" when words.Length == 3 && double.TryParse(words[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double span):
                {
                    // Every frame for this long, at sixty frames a second, saved as numbered pictures: for
                    // checking that motion is smooth frame by frame.
                    string folder = Path.GetFullPath(words[2]);
                    Directory.CreateDirectory(folder);
                    int frames = (int)Math.Round(span / (1000.0 / 60));
                    for (int f = 0; f < frames; f++)
                    {
                        skip(1000.0 / 60);
                        window.Screen.View.Tick();
                        Dispatcher.UIThread.RunJobs();
                        using var frame = window.CaptureRenderedFrame();
                        frame?.Save(Path.Combine(folder, $"frame{f:D4}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                    }
                    break;
                }
                case "settle":
                    // A book opened from the shelf loads on another thread: give it real time, up to ten seconds.
                    for (int i = 0; i < 500 && window.Busy; i++)
                    {
                        Thread.Sleep(20);
                        Dispatcher.UIThread.RunJobs();
                    }
                    pass(16);
                    break;
                default:
                    return "Couldn't follow this step: " + step;
            }
        }
        return null;
    }
}
