using Reader.Core;

namespace Editor.Core;

/// <summary>
/// A screen a book will be read on. Sizes are in the screen units the reader lays out in (Android's dp,
/// Windows' scaled pixels), the same numbers the Player's --size and --scale take.
/// </summary>
/// <param name="Phone">Behaves as the phone app: one page at a time when upright, compact buttons when sideways.</param>
/// <param name="Safe">Room kept for a notch and the system bars: left, top, right, bottom.</param>
public sealed record Device(string Id, string Name, double Width, double Height, double Scale, bool Phone,
    (double Left, double Top, double Right, double Bottom) Safe, double UnitsPerCm)
{
    public bool Upright => Height > Width;

    /// <summary>The preview's choices, and the frames Check book and the page overlays use.</summary>
    public static readonly IReadOnlyList<Device> All = new[]
    {
        new Device("window", "Windows window", 1280, 800, 1, false, (0, 0, 0, 0), ReaderEngine.DefaultUnitsPerCm),
        new Device("s22-upright", "Galaxy S22 Ultra, upright", 411, 882, 3.5, true, (0, 40, 0, 24), 56),
        new Device("s22-sideways", "Galaxy S22 Ultra, sideways", 882, 411, 3.5, true, (40, 24, 24, 0), 56),
        new Device("cheap-upright", "Cheap phone 720 x 1600, upright", 360, 800, 2, true, (0, 28, 0, 24), 53),
        new Device("cheap-sideways", "Cheap phone 720 x 1600, sideways", 800, 360, 2, true, (28, 24, 24, 0), 53),
        new Device("tablet-sideways", "10 inch tablet, sideways", 1280, 800, 1.5, true, (0, 24, 0, 24), 59),
        new Device("tablet-upright", "10 inch tablet, upright", 800, 1280, 1.5, true, (0, 24, 0, 24), 59),
    };

    public static Device Find(string? id) => All.FirstOrDefault(d => d.Id == id) ?? All[0];

    /// <summary>The phone app's compact layout: a phone on its side, where the buttons sit over the picture.</summary>
    public bool Compact => Phone && Height <= 519;

    /// <summary>
    /// Room the reader keeps clear above and below the picture on this screen, following ReaderScreen:
    /// the top bar and the arrows, plus room for the words when there is a caption and the screen is tall
    /// enough to move the picture up for them.
    /// </summary>
    public (double Top, double Bottom) Insets(bool caption, int captionLines = 2)
    {
        if (Compact) return (8 + Safe.Top, 8 + Safe.Bottom);
        double top = 64 + Safe.Top, bottom = 104 + Safe.Bottom;
        if (caption && Height >= 520)
        {
            bool narrow = Width <= 520;
            double font = narrow ? 18 : 22;
            double words = captionLines * font * 1.35 + 24 + 18;
            if (Height - top - bottom - words >= Height * 0.45) bottom += words;
        }
        return (top, bottom);
    }

    /// <summary>How many characters fit on one line of a caption on this screen, roughly (Medium word size).</summary>
    public double CaptionCharsPerLine
    {
        get
        {
            bool narrow = Width <= 520, low = Height <= 519;
            double across = Width - Safe.Left - Safe.Right;
            double width = Math.Max(120, low ? across - 210 : narrow ? across - 24 : Math.Min(720, across - 150));
            double font = low ? 17 : narrow ? 18 : 22;
            double padding = low ? 28 : 40;
            return (width - padding) / (font * 0.5);
        }
    }

    /// <summary>The most caption lines that still leave the picture enough room on this screen.</summary>
    public int MostCaptionLines => Height <= 519 ? 2 : Width <= 520 ? 4 : 3;
}

/// <summary>What part of a page a screen shows, worked out the way the reader does it.</summary>
public static class Framing
{
    /// <summary>
    /// The part of a page (in fractions of the page, like a stop's box) that a screen shows when the camera
    /// rests on <paramref name="box"/>: the box with the reader's space around it, widened or heightened
    /// to the screen's shape. The result can reach past the page's edges.
    /// </summary>
    public static RectD Visible(RectD box, double pageAspect, Device device, double padding = 0.025, bool caption = true, int captionLines = 2)
    {
        var (top, bottom) = device.Insets(caption, captionLines);
        double viewW = device.Width - device.Safe.Left - device.Safe.Right;
        double viewH = Math.Max(1, device.Height - top - bottom);
        // World units: the page is pageAspect wide and 1 high, as in the engine.
        var world = new RectD(box.X * pageAspect, box.Y, box.W * pageAspect, box.H);
        RectD cam = Geo.FitToAspect(Geo.Inflate(world, padding), viewW / viewH);
        return new RectD(cam.X / pageAspect, cam.Y, cam.W / pageAspect, cam.H);
    }

    /// <summary>
    /// Which page of a spread a stop is read on when a phone is held upright and the page is seen whole:
    /// 1 the left-hand page, 2 the right-hand page (as the reader decides it, by the box's middle).
    /// </summary>
    public static int HalfOf(RectD box, double pageAspect) => pageAspect < 1 ? 2 : box.CenterX < 0.5 ? 1 : 2;

    /// <summary>True when a box lies across the crease of a spread, which an upright phone can't show whole.</summary>
    public static bool CrossesCrease(RectD box, double pageAspect) => pageAspect >= 1 && box.X < 0.49 && box.Right > 0.51;

    /// <summary>
    /// Rough count of the lines a caption takes on a screen, wrapping whole words like the reader does.
    /// Good enough to warn about words that will cover too much of the picture.
    /// </summary>
    public static int CaptionLines(string? caption, Device device)
    {
        if (string.IsNullOrWhiteSpace(caption)) return 0;
        double per = Math.Max(8, device.CaptionCharsPerLine);
        int lines = 0;
        foreach (string paragraph in caption.Split('\n'))
        {
            lines++;
            double used = 0;
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                double len = word.Length;
                if (used > 0 && used + 1 + len > per) { lines++; used = len; }
                else used += (used > 0 ? 1 : 0) + len;
            }
        }
        return lines;
    }
}
