using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Editor.Core;
using Reader.Core;

namespace BookEditor;

/// <summary>What the screenshot hook was asked for.</summary>
sealed class ShotOptions
{
    public string Path = "";
    public string? Book;
    public int Page = 1;
    public int? Stop;
    public int? Hotspot;
    public string Screen = "main";
    public double Width = 1440, Height = 900;
    public string? Device;
    public double WaitMs = 1500;
    public string? Do;
    public string? Models;
    public int? Tour;
    public double Scale = 1;

    public const string Usage = """
        BookEditor [book folder]
          Opens the editor, with the book in that folder if one is given.

        BookEditor --time-words recording.wav --words "the words" [--from s] [--to s] [--language en|af|auto] [--models folder]
          Works out when each word starts in a recording, as "Time words automatically" does, and prints
          the times. The speech recogniser must already be downloaded (or be in --models).

        BookEditor --make-icons <folder>
          Draws the reader's and the editor's icons and writes the Windows .ico files and the Android
          launcher pictures into the folder (see the README for where they go).

        BookEditor --shot out.png [options]
          Draws one screen of the editor to a PNG and exits, with no window. Settings, recent books and
          safety copies are not touched.
            --book <folder>      the book to open
            --page N             the page to show (from 1)
            --stop N             select that stop (from 1)
            --find N             select that thing to find (from 1)
            --screen <name>      main (the default), welcome, welcome-offer, preview, check, export, help,
                                 newbook, bookfile, about, close (Close the book: asks first if there are changes)
            --tour N             with the step-by-step walkthrough showing, at step N (from 1)
            --models <folder>    where the speech recogniser's file is, for "autotime" below (otherwise
                                 a fresh folder, as on a computer that hasn't downloaded it yet)
            --device <id>        for --screen preview: window, s22-upright, s22-sideways, cheap-upright,
                                 cheap-sideways, tablet-sideways, tablet-upright
            --wait ms            for --screen preview: reader time before the picture (1500)
            --size WxH           the window's size (1440x900)
            --scale S            the screen's scaling, as Windows display scaling (1.25 for 125%): the
                                 window keeps its size in pixels, so everything in it is S times bigger
            --do "steps"         act out mouse and keys first, through the window's real input path:
                                 click x,y; move x,y; drag x,y x,y; key Name (ctrl+Z, shift+Right...); type text; save;
                                 autotime (time the selected recording's words, really, and wait for it);
                                 timeall (the same for the whole book); download ms (start downloading
                                 the speech recogniser for real and take the picture that many ms in);
                                 wait ms (real time, for things that are working in the background);
                                 say text / problem text (a message at the bottom, as after an action or a failure);
                                 surround grey|light|paper (the colour round the page)
        """;

    public static ShotOptions? Parse(string[] args)
    {
        int at = Array.IndexOf(args, "--shot");
        if (at < 0) return null;
        var o = new ShotOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException(args[i] + " needs a value.");
            int Int() => int.TryParse(Next(), out int n) ? n : throw new ArgumentException(args[i] + " needs a whole number.");
            switch (args[i])
            {
                case "--shot": o.Path = Next(); break;
                case "--book": o.Book = Next(); break;
                case "--page": o.Page = Int(); break;
                case "--stop": o.Stop = Int(); break;
                case "--find": o.Hotspot = Int(); break;
                case "--screen": o.Screen = Next(); break;
                case "--device": o.Device = Next(); break;
                case "--wait": o.WaitMs = Int(); break;
                case "--do": o.Do = Next(); break;
                case "--models": o.Models = Next(); break;
                case "--tour": o.Tour = Int(); break;
                case "--scale":
                    if (!double.TryParse(Next(), NumberStyles.Float, CultureInfo.InvariantCulture, out o.Scale) || o.Scale < 0.5 || o.Scale > 4)
                        throw new ArgumentException("--scale wants a number such as 1.25.");
                    break;
                case "--size":
                    string[] wh = Next().Split('x');
                    if (wh.Length != 2 || !double.TryParse(wh[0], CultureInfo.InvariantCulture, out o.Width) || !double.TryParse(wh[1], CultureInfo.InvariantCulture, out o.Height))
                        throw new ArgumentException("--size wants WxH, for example 1440x900.");
                    break;
            }
        }
        return o;
    }
}

/// <summary>
/// The screenshot hook: the editor's window with no screen attached, put in the state asked for and
/// saved as one PNG. Used to look at the editor's screens on a machine with no display.
/// </summary>
static class Screenshot
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

    /// <summary>Mouse and keys through the window's input, as a real mouse and keyboard would arrive. Returns a problem, or null.</summary>
    static string? ActOut(string script, MainWindow window)
    {
        foreach (string step in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] w = step.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            // "timed <step>": do the step, let the window lay itself out and draw, and print how long that took.
            if (w[0].Equals("timed", StringComparison.OrdinalIgnoreCase) && w.Length == 2)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                if (ActOut(w[1], window) is string bad) return bad;
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"timed: {w[1]}: {clock.Elapsed.TotalMilliseconds:0.0} ms"));
                continue;
            }
            switch (w[0].ToLowerInvariant())
            {
                case "click" when w.Length == 2 && TryPoint(w[1], out Point at):
                    window.MouseMove(at);
                    window.MouseDown(at, Avalonia.Input.MouseButton.Left);
                    window.MouseUp(at, Avalonia.Input.MouseButton.Left);
                    break;
                case "move" when w.Length == 2 && TryPoint(w[1], out Point over):
                    window.MouseMove(over);
                    break;
                case "drag" when w.Length == 2 && w[1].Split(' ') is { Length: 2 } pts && TryPoint(pts[0], out Point from) && TryPoint(pts[1], out Point to):
                    window.MouseMove(from);
                    window.MouseDown(from, Avalonia.Input.MouseButton.Left);
                    for (int i = 1; i <= 10; i++) { window.MouseMove(new Point(from.X + (to.X - from.X) * i / 10, from.Y + (to.Y - from.Y) * i / 10)); Dispatcher.UIThread.RunJobs(); }
                    window.MouseUp(to, Avalonia.Input.MouseButton.Left);
                    break;
                case "key" when w.Length == 2:
                {
                    var mods = Avalonia.Input.RawInputModifiers.None;
                    string name = w[1];
                    while (name.Contains('+'))
                    {
                        string m = name[..name.IndexOf('+')].ToLowerInvariant();
                        name = name[(name.IndexOf('+') + 1)..];
                        mods |= m switch { "ctrl" => Avalonia.Input.RawInputModifiers.Control, "shift" => Avalonia.Input.RawInputModifiers.Shift, "alt" => Avalonia.Input.RawInputModifiers.Alt, _ => 0 };
                    }
                    if (!Enum.TryParse(name, true, out Avalonia.Input.Key key)) return "Unknown key: " + name;
                    window.KeyPress(key, mods, Avalonia.Input.PhysicalKey.None, null);
                    window.KeyRelease(key, mods, Avalonia.Input.PhysicalKey.None, null);
                    break;
                }
                case "type" when w.Length == 2:
                    window.KeyTextInput(w[1]);
                    break;
                case "typeeach" when w.Length == 2:
                {
                    // One key at a time, each laid out and drawn before the next, timed: how typing feels.
                    var times = new List<double>();
                    foreach (char c in w[1])
                    {
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        window.KeyTextInput(c.ToString());
                        Dispatcher.UIThread.RunJobs();
                        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                        Dispatcher.UIThread.RunJobs();
                        times.Add(clock.Elapsed.TotalMilliseconds);
                    }
                    times.Sort();
                    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"timed: typing {times.Count} keys one at a time: median {times[times.Count / 2]:0.0} ms a key, worst {times[^1]:0.0} ms"));
                    break;
                }
                case "save":
                    window.Save();
                    break;
                case "live":
                    // From here on, pictures load in the background as in the real editor (for timing steps).
                    window.ShownPictures.Synchronous = false;
                    break;
                case "caption":
                    // Put the keyboard in the selected stop's words box, at its end (as clicking into it would).
                    if (window.Inspector.CaptionBox is not Avalonia.Controls.TextBox caption) return "caption needs a stop selected (--stop).";
                    caption.Focus();
                    caption.CaretIndex = caption.Text?.Length ?? 0;
                    break;
                case "words":
                    // Select the page's printed words box (as clicking it would).
                    if (window.State.Page?.Text is null) return "words needs a page with printed words (--page).";
                    window.State.Select(Sel.Words, 0);
                    break;
                case "autotime":
                {
                    VoiceTarget? t = window.State.Stop is not null ? new VoiceTarget(window.State.PageIndex, false, window.State.SelIndex)
                                   : window.State.Hotspot is not null ? new VoiceTarget(window.State.PageIndex, true, window.State.SelIndex) : null;
                    if (t is null) return "autotime needs a stop or a thing to find selected (--stop or --find).";
                    WaitFor(window.Timing.TimeOne(t.Value));
                    break;
                }
                case "timeall":
                    WaitFor(window.Timing.TimeAll());
                    break;
                case "download" when w.Length == 2 && int.TryParse(w[1], out int ms):
                    _download = window.Timing.StartDownload();
                    Pump(ms);
                    break;
                case "say" when w.Length == 2:
                    window.Say(w[1]);
                    break;
                case "problem" when w.Length == 2:
                    window.Problem(w[1]);
                    break;
                case "surround" when w.Length == 2 && EditorSettings.Surrounds.Contains(w[1]):
                    Ui.Surround = w[1];
                    window.Canvas.InvalidateVisual();
                    break;
                case "wait" when w.Length == 2 && int.TryParse(w[1], out int wait):
                    Pump(wait);
                    break;
                default:
                    return "Couldn't follow this step: " + step;
            }
            Dispatcher.UIThread.RunJobs();
        }
        return null;
    }

    static CancellationTokenSource? _download;

    /// <summary>Let real time pass with the window's work (and anything working in the background) carrying on.</summary>
    static void Pump(int ms)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < ms) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(15); }
    }

    static void WaitFor(Task task)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && clock.Elapsed < TimeSpan.FromMinutes(10)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(15); }
        Dispatcher.UIThread.RunJobs();
    }

    public static int Take(ShotOptions o)
    {
        try
        {
            EditorPaths.Override = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "book-editor-shot-" + Environment.ProcessId);
            AppBuilder.Configure<EditorApp>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();
            // At a display scaling, a window of so many pixels has fewer units of room to lay things out in.
            var window = new MainWindow(screenshotMode: true) { Width = o.Width / o.Scale, Height = o.Height / o.Scale };
            if (o.Models is not null) window.Timing.Model = new SpeechModel(o.Models);
            window.Show();
            if (Math.Abs(o.Scale - 1) > 0.001) window.SetRenderScaling(o.Scale);
            Dispatcher.UIThread.RunJobs();

            if (o.Book is not null)
            {
                var opening = System.Diagnostics.Stopwatch.StartNew();
                window.OpenBook(o.Book).GetAwaiter().GetResult();
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                if (o.Do?.Contains("timed", StringComparison.OrdinalIgnoreCase) == true)
                    Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"timed: opening the book and drawing it: {opening.Elapsed.TotalMilliseconds:0.0} ms"));
                if (!window.State.HasBook) { Console.Error.WriteLine("Couldn't open the book " + o.Book); return 1; }
                window.State.GoToPage(o.Page - 1);
                if (o.Stop is int s) window.State.Select(Sel.Stop, s - 1);
                if (o.Hotspot is int h) window.State.Select(Sel.Hotspot, h - 1);
            }
            Dispatcher.UIThread.RunJobs();
            if (o.Do is not null && ActOut(o.Do, window) is string problem) { Console.Error.WriteLine(problem); return 2; }

            switch (o.Screen)
            {
                case "check": window.ShowCheckNow(); break;
                case "export": window.ShowExportNow(); break;
                case "help": window.ShowHelpNow(); break;
                case "newbook": _ = window.NewBook(); break;
                case "bookfile": window.ShowBookFileNow(); break;
                case "about": window.ShowAboutNow(); break;
                case "close": _ = window.CloseBook(); break;
                case "welcome-offer": window.OfferTour(); break;
                case "preview":
                    if (o.Device is not null) window.Preview.Device = Editor.Core.Device.Find(o.Device);
                    window.OpenPreview(fromStop: o.Stop is not null);
                    for (double t = 0; t < o.WaitMs; t += 16)
                    {
                        window.ShotClock += 16;
                        window.Preview.Screen?.View.Tick();
                    }
                    break;
            }
            Dispatcher.UIThread.RunJobs();
            if (o.Tour is int step)
            {
                window.Tour.Start(step - 1);
                Dispatcher.UIThread.RunJobs();
                window.CaptureRenderedFrame();
                Dispatcher.UIThread.RunJobs();
                window.Tour.Refresh();
            }
            window.CaptureRenderedFrame();
            Dispatcher.UIThread.RunJobs();
            window.Tour.Refresh();
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
            _download?.Cancel();
            if (frame is null) { Console.Error.WriteLine("Nothing was drawn."); return 1; }
            string path = System.IO.Path.GetFullPath(o.Path);
            if (System.IO.Path.GetDirectoryName(path) is { Length: > 0 } dir) Directory.CreateDirectory(dir);
            frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            Console.WriteLine("saved " + path);
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Couldn't take the screenshot: " + e);
            return 1;
        }
        finally
        {
            try { if (EditorPaths.Override is string d && Directory.Exists(d)) Directory.Delete(d, true); } catch (IOException) { }
        }
    }
}
