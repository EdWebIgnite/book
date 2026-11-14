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

    public const string Usage = """
        BookEditor [book folder]
          Opens the editor, with the book in that folder if one is given.

        BookEditor --shot out.png [options]
          Draws one screen of the editor to a PNG and exits, with no window. Settings, recent books and
          safety copies are not touched.
            --book <folder>      the book to open
            --page N             the page to show (from 1)
            --stop N             select that stop (from 1)
            --find N             select that thing to find (from 1)
            --screen <name>      main (the default), welcome, preview, check, export, help, newbook
            --device <id>        for --screen preview: window, s22-upright, s22-sideways, cheap-upright,
                                 cheap-sideways, tablet-sideways, tablet-upright
            --wait ms            for --screen preview: reader time before the picture (1500)
            --size WxH           the window's size (1440x900)
            --do "steps"         act out mouse and keys first, through the window's real input path:
                                 click x,y; drag x,y x,y; key Name (ctrl+Z, shift+Right...); type text; save
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
            switch (w[0].ToLowerInvariant())
            {
                case "click" when w.Length == 2 && TryPoint(w[1], out Point at):
                    window.MouseMove(at);
                    window.MouseDown(at, Avalonia.Input.MouseButton.Left);
                    window.MouseUp(at, Avalonia.Input.MouseButton.Left);
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
                case "save":
                    window.Save();
                    break;
                default:
                    return "Couldn't follow this step: " + step;
            }
            Dispatcher.UIThread.RunJobs();
        }
        return null;
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
            var window = new MainWindow(screenshotMode: true) { Width = o.Width, Height = o.Height };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            if (o.Book is not null)
            {
                window.OpenBook(o.Book).GetAwaiter().GetResult();
                Dispatcher.UIThread.RunJobs();
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
            window.CaptureRenderedFrame();
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
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
