using System.Globalization;

namespace Player;

/// <summary>What was asked for on the command line.</summary>
sealed class Options
{
    /// <summary>The folder that holds library.json and books/. Found automatically when not given.</summary>
    public string? Content;
    /// <summary>A book to open straight away: an id from the library, or the path of a book folder.</summary>
    public string? Book;
    /// <summary>Page to open at, counting from 1.</summary>
    public int Page = 1;
    /// <summary>Stop to open at, counting from 1. -1 is the whole page, -2 is exploring, null is however the page opens.</summary>
    public int? Stop;

    // The screenshot hook: draw one state with no screen attached, save it and exit.
    public string? Shot;
    public double WaitMs = 1000;
    public int Width = 1280, Height = 800;
    public double Scale = 1;
    public bool ShowSettings, Finish;
    public double Zoom = 1;
    public (double X, double Y)? Look;
    public List<int> Find = new();
    public List<(string Key, string Value)> Set = new();
    /// <summary>Touches and key presses to act out before the picture is taken, separated by semicolons.</summary>
    public string? Do;
    /// <summary>Play a fixed sequence with no window and print how long the frames took.</summary>
    public bool Bench;

    public string? Problem;
    public bool Help;

    public const string Usage = """
        Book Reader

          BookReader [--content <folder>] [--book <id or folder>] [--page <n>] [--stop <n>]

            --content <folder>   where library.json and books/ are (found automatically when left out)
            --book <id|folder>   open this book straight away
            --page <n>           start on this page (1 is the first)
            --stop <n>           start on this stop (1 is the first; -1 the whole page; -2 exploring)

        Draw one screen to a picture file and exit, with no window:

          BookReader --shot <file.png> [--book ...] [--page <n>] [--stop <n>] [--wait <ms>] [--size WxH]

            --wait <ms>          let this much reader time pass first (1000 when left out)
            --size WxH           window size (1280x800 when left out)
            --scale <n>          display scaling, for example 2 for a high-density screen
            --settings           with the settings panel open
            --zoom <n>           exploring: zoom in by this much first
            --look <x,y>         exploring: centre the view here (fractions of the page, like book.json)
            --find <n>           exploring: tap the nth thing to find (may be given more than once)
            --finish             show The End
            --set <key>=<value>  change a setting for this picture, for example --set reduceMotion=on
            --do "<steps>"       act these out first, as real touches and key presses, separated by semicolons:
                                   tap 900,400          press and let go at that point
                                   hold 1240,36 2100    press, keep pressing for that many milliseconds, let go
                                   drag 800,400 300,400 press, slide (over 200 ms) and let go
                                   wheel 640,400 -1     turn the wheel one notch (negative is towards you)
                                   key Right            press a key (the names Avalonia uses: Right, Space, S, Escape, ...)
                                   wait 1500            let this many milliseconds of reader time pass
                                   settle               wait for a book that is still loading

          Screenshots always start from the default settings and never change the saved ones.
        """;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string? Value()
            {
                if (i + 1 < args.Length) return args[++i];
                o.Problem = a + " needs a value.";
                return null;
            }
            switch (a)
            {
                case "--help" or "-h" or "/?": o.Help = true; break;
                case "--content": o.Content = Value(); break;
                case "--book": o.Book = Value(); break;
                case "--page": o.Page = Int(Value(), a, o) ?? 1; break;
                case "--stop": o.Stop = Int(Value(), a, o); break;
                case "--shot": o.Shot = Value(); break;
                case "--wait": o.WaitMs = Num(Value(), a, o) ?? 1000; break;
                case "--scale": o.Scale = Num(Value(), a, o) ?? 1; break;
                case "--zoom": o.Zoom = Num(Value(), a, o) ?? 1; break;
                case "--settings": o.ShowSettings = true; break;
                case "--finish": o.Finish = true; break;
                case "--do": o.Do = Value(); break;
                case "--bench": o.Bench = true; break;
                case "--find": if (Int(Value(), a, o) is int n) o.Find.Add(n); break;
                case "--size":
                {
                    string[] parts = (Value() ?? "").ToLowerInvariant().Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) && w >= 200 && h >= 200) { o.Width = w; o.Height = h; }
                    else o.Problem ??= "--size should look like 1280x800.";
                    break;
                }
                case "--look":
                {
                    string[] parts = (Value() ?? "").Split(',');
                    if (parts.Length == 2 && TryNum(parts[0], out double x) && TryNum(parts[1], out double y)) o.Look = (x, y);
                    else o.Problem ??= "--look should look like 0.5,0.25.";
                    break;
                }
                case "--set":
                {
                    string pair = Value() ?? "";
                    int eq = pair.IndexOf('=');
                    if (eq > 0) o.Set.Add((pair[..eq], pair[(eq + 1)..]));
                    else o.Problem ??= "--set should look like key=value.";
                    break;
                }
                default:
                    o.Problem ??= "Unknown option: " + a;
                    break;
            }
        }
        return o;
    }

    static bool TryNum(string? s, out double v) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    static double? Num(string? s, string name, Options o)
    {
        if (s is null) return null;
        if (TryNum(s, out double v)) return v;
        o.Problem ??= name + " needs a number.";
        return null;
    }

    static int? Int(string? s, string name, Options o)
    {
        if (s is null) return null;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
        o.Problem ??= name + " needs a whole number.";
        return null;
    }
}
