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

            // Book files are taken into a folder of the screenshot's own, never the real one.
            string shotBooks = Path.Combine(Path.GetTempPath(), "book-reader-shot-" + Environment.ProcessId);
            o.ImportFolder ??= shotBooks;
            // Little animations (covers lifting, the books settling) land at once, so the picture shows where they end up.
            Motion.Instant = !o.Animate;
            var settings = new ReaderSettings();
            if (o.Set.Count > 0) settings.ApplyJson(o.SetAsJson());

            var window = new MainWindow(o, settings, new NoSound(), saveSettings: false, state: ShotState(o)) { Width = o.Width, Height = o.Height };
            ReaderView view = window.Screen.View;
            double clock = 100_000;
            view.Clock = () => clock;
            view.AutoFrames = false;
            view.LoadPicturesAtOnce = true;

            window.Show();
            if (o.Scale > 0 && Math.Abs(o.Scale - 1) > 0.001) window.SetRenderScaling(o.Scale);
            Dispatcher.UIThread.RunJobs();
            window.LoadShelf(coversNow: true);
            if (o.Import is not null)
            {
                var r = window.App.ImportBookFileNow(o.Import);
                window.LoadShelf(coversNow: true);
                window.App.Say(r.Message, r.Ok ? Reader.App.NoteKind.Good : Reader.App.NoteKind.Problem);
            }

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
            // The settings open first, so the steps can act on them (scroll them, press their buttons).
            if (o.ShowSettings) { window.ToggleSettings(true); Pass(16); }
            if (o.Screen is string screen && ShowScreen(window, screen) is string bad)
            {
                Console.Error.WriteLine(bad);
                return 2;
            }
            if (o.Focus) { window.App.Shelf.FocusFirst(ring: true); Pass(16); }
            if (o.Do is not null)
            {
                string? problem = ActOut(o.Do, window, Pass, ms => clock += ms);
                if (problem is not null)
                {
                    Console.Error.WriteLine(problem);
                    return 2;
                }
            }
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
        finally
        {
            string shotBooks = Path.Combine(Path.GetTempPath(), "book-reader-shot-" + Environment.ProcessId);
            try { if (Directory.Exists(shotBooks)) Directory.Delete(shotBooks, true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// Who is reading in a screenshot: a first run (--welcome), one child with no name (the default),
    /// three children with some reading done (--demo), or a saved profiles.json (--state).
    /// </summary>
    static AppState ShotState(Options o)
    {
        AppState s;
        if (o.StateFile is string file) s = AppStateStore.Load(file);
        else if (o.Welcome) s = new AppState();
        else if (o.Demo) s = Demo(o);
        else
        {
            s = new AppState { Welcomed = true };
            s.Add("", 0);
        }
        s.Bedtime = o.Bedtime;
        if (o.Language is not null) s.Language = "auto";
        return s;
    }

    /// <summary>Lia, Thabo and Mila. Lia is part of the way through Moss and has finished the waterhole book.</summary>
    static AppState Demo(Options o)
    {
        var s = new AppState { Welcomed = true };
        s.Add("Lia", 0, "3-5");
        s.Add("Thabo", 1, "6-8");
        s.Add("Mila", 2, "3-5");
        string? content = BookLibrary.FindContent(o.Content, AppContext.BaseDirectory, Environment.CurrentDirectory);
        int Pages(string id)
        {
            try { return content is null ? 12 : BookJson.Load(Path.Combine(content, "books", id)).Pages.Count; }
            catch (Exception) { return 12; }
        }
        var today = DateTime.UtcNow.Date.AddHours(12);
        s.Choose("p1");
        s.MarkFinished("waterhole", Pages("waterhole"), today.AddDays(-5));
        s.KeepPlace("deep-sea", 4, 2, 0, Pages("deep-sea"), today.AddDays(-3));
        s.MarkFinished("deep-sea", Pages("deep-sea"), today.AddDays(-2));
        s.KeepPlace("deep-sea", 3, 1, 0, Pages("deep-sea"), today.AddDays(-1));
        s.KeepPlace("biggest-puddle", 20, 6, 1, Pages("biggest-puddle"), today);
        s.Choose("p2");
        s.MarkFinished("captain-megawatt", Pages("captain-megawatt"), today.AddDays(-1));
        s.Choose("p" + Math.Clamp(o.Child, 1, 3));
        return s;
    }

    /// <summary>Open one of the screens around the reader. Returns what was wrong with the name, or null.</summary>
    internal static string? ShowScreen(MainWindow window, string screen)
    {
        Reader.App.AppView app = window.App;
        string[] parts = screen.Split(':', 2);
        switch (parts[0])
        {
            case "welcome":
                app.ShowWelcome(parts.Length > 1 && int.TryParse(parts[1], out int page) ? Math.Clamp(page - 1, 0, Reader.App.WelcomeView.InfoPages) : 0);
                break;
            case "profiles":
                app.ShowProfiles(canClose: true);
                break;
            case "gate":
                app.WithGate(() => app.ShowParents());
                break;
            case "shelf":
                app.ShowShelf();
                break;
            default:
                if (!parts[0].StartsWith("parents", StringComparison.Ordinal)) return "Unknown screen: " + screen;
                string section = parts[0].Contains('/') ? parts[0][(parts[0].IndexOf('/') + 1)..] : "home";
                Reader.App.ParentsView.Section? s = section switch
                {
                    "home" => Reader.App.ParentsView.Section.Home,
                    "reading" => Reader.App.ParentsView.Section.Reading,
                    "books" => Reader.App.ParentsView.Section.Books,
                    "language" => Reader.App.ParentsView.Section.Language,
                    "about" => Reader.App.ParentsView.Section.About,
                    "child" => Reader.App.ParentsView.Section.Child,
                    "new" => Reader.App.ParentsView.Section.NewChild,
                    "tuning" => Reader.App.ParentsView.Section.Tuning,
                    _ => null,
                };
                if (s is null) return "Unknown screen: " + screen;
                app.ShowParents(s.Value, app.State.CurrentProfile?.Id);
                break;
        }
        Dispatcher.UIThread.RunJobs();
        return null;
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
                case "hover" when words.Length == 2 && TryPoint(words[1], out Point over_):
                    // The mouse comes to rest over this point, without pressing: a book on the shelf lifts.
                    window.MouseMove(over_);
                    pass(16);
                    break;
                case "press" when words.Length == 2 && TryPoint(words[1], out Point down):
                    // Press and keep pressing, for a picture of a button held down.
                    window.MouseMove(down);
                    window.MouseDown(down, MouseButton.Left);
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
