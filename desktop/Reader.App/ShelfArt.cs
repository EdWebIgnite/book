using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Reader.Ui;

namespace Reader.App;

/// <summary>
/// The shelf's hand-made art, all drawn in code: the wood grain and the paper texture are made once as
/// small pictures from a seeded noise (so they are the same every run), and the potted plant and the
/// books lying on their sides are little vector shapes.
/// </summary>
static class ShelfArt
{
    // ---------------------------------------------------------------- noise

    static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }

    static double Rand(int x, int y, int seed) => (Hash(x, y, seed) & 0xffffff) / (double)0xffffff;

    /// <summary>Smooth value noise, wrapping every <paramref name="period"/> cells across so a texture tiles.</summary>
    static double Noise(double x, double y, int seed, int period = 1 << 20)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        double sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
        int xa = ((x0 % period) + period) % period, xb = (xa + 1) % period;
        double a = Rand(xa, y0, seed), b = Rand(xb, y0, seed), c = Rand(xa, y0 + 1, seed), d = Rand(xb, y0 + 1, seed);
        return a + (b - a) * sx + (c - a) * sy + (a - b - c + d) * sx * sy;
    }

    static double Fbm(double x, double y, int seed, int period = 1 << 20)
    {
        double sum = 0, amp = 0.5, total = 0;
        for (int o = 0; o < 4; o++)
        {
            sum += amp * Noise(x, y, seed + o * 31, period);
            total += amp;
            x *= 2; y *= 2; period *= 2; amp *= 0.5;
        }
        return sum / total;
    }

    static Bitmap Make(int w, int h, Func<int, int, uint> pixel)
    {
        var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        var data = new int[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) data[y * w + x] = unchecked((int)pixel(x, y));
        using (ILockedFramebuffer fb = bmp.Lock())
        {
            for (int y = 0; y < h; y++) Marshal.Copy(data, y * w, fb.Address + y * fb.RowBytes, w);
        }
        return bmp;
    }

    static uint Bgra(double r, double g, double b, double a = 1)
    {
        static uint C(double v) => (uint)Math.Clamp(Math.Round(v), 0, 255);
        return (C(a * 255) << 24) | (C(r * a) << 16) | (C(g * a) << 8) | C(b * a);
    }

    static double Mix(double a, double b, double t) => a + (b - a) * t;

    // ---------------------------------------------------------------- textures

    static Bitmap? _wood, _grain;

    /// <summary>
    /// Light honey wood, the grain running along the plank: long wavy stripes bent by a slow noise, with
    /// fine fibres. 1024 x 96, stretched along each plank.
    /// </summary>
    public static Bitmap Wood => _wood ??= Make(1024, 96, (x, y) =>
    {
        double warp = Fbm(x * 0.003, y * 0.02, 7) * 9 + Fbm(x * 0.012, y * 0.05, 11) * 1.6;
        double ring = 0.5 + 0.5 * Math.Sin((y * 0.16 + warp) * 2.6);
        ring = Math.Pow(ring, 3);
        double fibre = Fbm(x * 0.02, y * 0.9, 23);
        double t = 0.15 + 0.55 * ring + 0.3 * (fibre - 0.5) + 0.2 * Fbm(x * 0.002, y * 0.01, 3);
        Color a = Palette.Wood, b = Palette.WoodDark;
        return Bgra(Mix(a.R, b.R, t), Mix(a.G, b.G, t), Mix(a.B, b.B, t));
    });

    /// <summary>A fine see-through speckle, laid over painted and papered surfaces so they don't look flat. 256 x 256, wraps.</summary>
    public static Bitmap Grain => _grain ??= Make(256, 256, (x, y) =>
    {
        double n = 0.5 * Rand(x, y, 5) + 0.5 * Rand(x / 2, y / 2, 9);   // fine speckle, so the tile has no edges to show
        double light = n > 0.5 ? 255 : 60;
        double a = Math.Abs(n - 0.5) * 0.35;
        return Bgra(light, light, light * 0.95, a);
    });

    /// <summary>
    /// Lay the grain over a rectangle, tile by tile. (Drawn as whole pictures rather than with a tiling
    /// brush, which left faint seams between the tiles at some screen scales.)
    /// </summary>
    public static void Speckle(DrawingContext ctx, Rect area, double opacity)
    {
        Bitmap g = Grain;
        double size = g.PixelSize.Width;
        var source = new Rect(0, 0, size, size);
        using (ctx.PushClip(area))
        using (ctx.PushOpacity(opacity))
        {
            for (double y = Math.Floor(area.Y / size) * size; y < area.Bottom; y += size)
                for (double x = Math.Floor(area.X / size) * size; x < area.Right; x += size)
                    ctx.DrawImage(g, source, new Rect(x, y, size, size));
        }
    }

    // ---------------------------------------------------------------- leaves and things

    /// <summary>
    /// A potted plant standing on a shelf, its pot's bottom middle at <paramref name="foot"/>, about
    /// <paramref name="size"/> tall. A cream pot with a green band, and a fan of leaves.
    /// </summary>
    public static void Plant(DrawingContext ctx, Point foot, double size)
    {
        double potH = size * 0.36, potTop = size * 0.42, potBottom = size * 0.32;
        double x = foot.X, y = foot.Y;
        // Leaves first, behind the pot's rim.
        var leaves = new (double Angle, double Length, int Colour)[]
        {
            (-62, 0.50, 2), (-38, 0.62, 0), (-16, 0.70, 1), (6, 0.74, 0), (26, 0.66, 3), (48, 0.58, 2), (68, 0.46, 1), (-4, 0.48, 3), (-30, 0.44, 1),
        };
        var stem = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#4a8a35")), Math.Max(1, size * 0.018), lineCap: PenLineCap.Round);
        Point base_ = new(x, y - potH * 0.9);
        foreach (var (angle, length, colour) in leaves)
        {
            double a = angle * Math.PI / 180;
            double reach = size * length * 0.55;
            var tip = new Point(base_.X + Math.Sin(a) * reach, base_.Y - Math.Cos(a) * reach);
            ctx.DrawLine(stem, base_, tip);
            Ornaments.Leaf(ctx, tip, size * length * 0.48, angle * 1.1, Ornaments.LeafColours[colour]);
        }
        // The pot: slightly tapered, with a rim and a band.
        var pot = new StreamGeometry();
        using (StreamGeometryContext g = pot.Open())
        {
            g.BeginFigure(new Point(x - potTop / 2, y - potH), true);
            g.LineTo(new Point(x + potTop / 2, y - potH));
            g.LineTo(new Point(x + potBottom / 2, y));
            g.LineTo(new Point(x - potBottom / 2, y));
            g.EndFigure(true);
        }
        var potFill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#fffdf6"), 0), new GradientStop(Color.Parse("#f3ead7"), 0.7), new GradientStop(Color.Parse("#e2d6bd"), 1) },
        };
        ctx.DrawGeometry(potFill, null, pot);
        double bandY = y - potH * 0.55;
        double bandHalf(double yy) => (potBottom + (potTop - potBottom) * (y - yy) / potH) / 2;
        ctx.DrawRectangle(new ImmutableSolidColorBrush(Palette.LeafBright), null,
            new Rect(x - bandHalf(bandY), bandY - potH * 0.08, bandHalf(bandY) * 2, potH * 0.16));
        ctx.DrawRectangle(new ImmutableSolidColorBrush(Color.Parse("#fffdf6")), new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#e2d6bd")), 1),
            new Rect(x - potTop / 2 - size * 0.02, y - potH - size * 0.05, potTop + size * 0.04, size * 0.07), size * 0.02, size * 0.02);
    }

    static readonly (Color Cover, Color Band)[] LyingBooks =
    {
        (Color.Parse("#9fcb86"), Color.Parse("#f6efd9")),
        (Color.Parse("#ffd66b"), Color.Parse("#e9a917")),
        (Color.Parse("#f2b49b"), Color.Parse("#fff3e6")),
    };

    /// <summary>A little pile of books lying on their sides, the bottom one's bottom middle at <paramref name="foot"/>.</summary>
    public static void BookPile(DrawingContext ctx, Point foot, double width)
    {
        double h = width * 0.16, y = foot.Y;
        var page = new ImmutableSolidColorBrush(Color.Parse("#fbf6ea"));
        for (int i = 0; i < LyingBooks.Length; i++)
        {
            double w = width * (1 - i * 0.09), off = (i % 2 == 0 ? -1 : 1) * width * 0.03;
            var r = new Rect(foot.X - w / 2 + off, y - h, w, h);
            ctx.DrawRectangle(new ImmutableSolidColorBrush(LyingBooks[i].Cover), null, r, h * 0.18, h * 0.18);
            ctx.DrawRectangle(page, null, new Rect(r.Right - w * 0.06, r.Y + h * 0.18, w * 0.05, h * 0.64));
            ctx.DrawRectangle(new ImmutableSolidColorBrush(LyingBooks[i].Band), null, new Rect(r.X + w * 0.14, r.Y, w * 0.06, h));
            ctx.DrawRectangle(new ImmutableSolidColorBrush(Colors.White, 0.22), null, new Rect(r.X + h * 0.2, r.Y + h * 0.12, w - h * 0.4, h * 0.16), h * 0.08, h * 0.08);
            y -= h;
        }
    }
}
