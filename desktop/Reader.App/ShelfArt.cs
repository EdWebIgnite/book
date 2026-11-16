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
/// small pictures from a seeded noise (so they are the same every run), and the fern in its pot and the
/// clothbound books lying on their sides are little vector shapes.
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
    /// Cherry wood, the grain running along the plank: long wavy stripes bent by a slow noise, with
    /// fine fibres. 1024 x 96, stretched along each plank.
    /// </summary>
    public static Bitmap Wood => _wood ??= Make(1024, 96, (x, y) =>
    {
        double warp = Fbm(x * 0.003, y * 0.02, 7) * 9 + Fbm(x * 0.012, y * 0.05, 11) * 1.6;
        double ring = 0.5 + 0.5 * Math.Sin((y * 0.16 + warp) * 2.6);
        ring = Math.Pow(ring, 3);
        double fibre = Fbm(x * 0.02, y * 0.9, 23);
        double t = Math.Clamp(0.1 + 0.6 * ring + 0.35 * (fibre - 0.5) + 0.25 * (Fbm(x * 0.002, y * 0.01, 3) - 0.5), 0, 1);
        Color a = Palette.Wood, b = Palette.WoodDark, deep = Palette.WoodDeep, light = Palette.WoodLight;
        double r = Mix(a.R, b.R, t), g = Mix(a.G, b.G, t), bl = Mix(a.B, b.B, t);
        // The darkest lines of the figure, and here and there a soft glow where the grain catches the light.
        double line = Math.Pow(ring, 4) * 0.45, glow = Math.Max(0, 0.5 - ring) * 0.5 * Fbm(x * 0.006, y * 0.04, 17);
        r = Mix(Mix(r, deep.R, line), light.R, glow); g = Mix(Mix(g, deep.G, line), light.G, glow); bl = Mix(Mix(bl, deep.B, line), light.B, glow);
        return Bgra(r, g, bl);
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
    /// A Boston fern in a glazed cream jardinière with a bottle green band and a brass rim, standing on a
    /// shelf: the pot's bottom middle at <paramref name="foot"/>, about <paramref name="size"/> tall.
    /// </summary>
    public static void Plant(DrawingContext ctx, Point foot, double size)
    {
        double potH = size * 0.34, potTop = size * 0.40, potBottom = size * 0.28;
        double x = foot.X, y = foot.Y;
        Point base_ = new(x, y - potH * 0.92);
        Color[] greens = { Color.Parse("#4f6e47"), Color.Parse("#5e7f55"), Color.Parse("#6f8f62"), Color.Parse("#84a076") };
        // Fronds arching up and out, the outer ones drooping over the rim; drawn back to front.
        var fronds = new (double Angle, double Length, int Colour)[]
        {
            (-82, 0.62, 0), (82, 0.6, 0), (-60, 0.72, 1), (60, 0.7, 1), (-38, 0.78, 2), (38, 0.76, 2),
            (-16, 0.8, 1), (16, 0.82, 3), (0, 0.74, 2), (-70, 0.5, 3), (72, 0.52, 3),
        };
        foreach (var (angle, length, colour) in fronds) Frond(ctx, base_, angle, size * length * 0.62, greens[colour]);

        // The jardinière: a rounded bowl shape, glazed cream, shaded on the right.
        var pot = new StreamGeometry();
        using (StreamGeometryContext g = pot.Open())
        {
            g.BeginFigure(new Point(x - potTop / 2, y - potH), true);
            g.LineTo(new Point(x + potTop / 2, y - potH));
            g.CubicBezierTo(new Point(x + potTop / 2, y - potH * 0.35), new Point(x + potBottom / 2 + size * 0.04, y - potH * 0.08), new Point(x + potBottom / 2, y));
            g.LineTo(new Point(x - potBottom / 2, y));
            g.CubicBezierTo(new Point(x - potBottom / 2 - size * 0.04, y - potH * 0.08), new Point(x - potTop / 2, y - potH * 0.35), new Point(x - potTop / 2, y - potH));
            g.EndFigure(true);
        }
        var glaze = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#fbf6e8"), 0), new GradientStop(Color.Parse("#efe5cf"), 0.6), new GradientStop(Color.Parse("#cfc3a6"), 1) },
        };
        ctx.DrawGeometry(glaze, null, pot);
        using (ctx.PushClip(pot.Bounds))
        using (ctx.PushGeometryClip(pot))
        {
            double bandY = y - potH * 0.52;
            ctx.FillRectangle(new ImmutableSolidColorBrush(Palette.Leaf), new Rect(x - potTop, bandY - potH * 0.1, potTop * 2, potH * 0.2));
            ctx.FillRectangle(new ImmutableSolidColorBrush(Palette.Sun, 0.9), new Rect(x - potTop, bandY - potH * 0.1 - 1.2, potTop * 2, 1.2));
            ctx.FillRectangle(new ImmutableSolidColorBrush(Palette.Sun, 0.9), new Rect(x - potTop, bandY + potH * 0.1, potTop * 2, 1.2));
            ctx.FillRectangle(new ImmutableSolidColorBrush(Colors.White, 0.35), new Rect(x - potTop * 0.36, y - potH, potTop * 0.08, potH));
        }
        // A brass rim.
        var rim = new Rect(x - potTop / 2 - size * 0.015, y - potH - size * 0.04, potTop + size * 0.03, size * 0.05);
        ctx.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Palette.SunLight, 0), new GradientStop(Palette.SunDeep, 1) },
        }, null, rim, size * 0.02, size * 0.02);
    }

    /// <summary>One fern frond: a curving stem with little leaflets either side, smaller towards the tip.</summary>
    static void Frond(DrawingContext ctx, Point from, double angle, double length, Color colour)
    {
        double a = angle * Math.PI / 180;
        // Outer fronds bend down under their own weight.
        double droop = Math.Abs(angle) / 90.0;
        Point Along(double t)
        {
            double dx = 0, dy = 0;
            int steps = 12;
            for (int i = 0; i < steps; i++)
            {
                double u = t * i / steps;
                double angU = a + Math.Sign(angle == 0 ? 1 : angle) * droop * u * u * 1.4;
                dx += Math.Sin(angU) * length * t / steps;
                dy -= Math.Cos(angU) * length * t / steps;
            }
            return new Point(from.X + dx, from.Y + dy);
        }
        var stemGeo = new StreamGeometry();
        using (StreamGeometryContext g = stemGeo.Open())
        {
            g.BeginFigure(from, false);
            for (int i = 1; i <= 16; i++) g.LineTo(Along(i / 16.0));
            g.EndFigure(false);
        }
        var brush = new ImmutableSolidColorBrush(colour);
        ctx.DrawGeometry(null, new ImmutablePen(brush, Math.Max(0.8, length * 0.018), lineCap: PenLineCap.Round), stemGeo);
        int leaflets = 11;
        for (int i = 1; i <= leaflets; i++)
        {
            double t = 0.12 + 0.86 * i / leaflets;
            Point p = Along(t), q = Along(Math.Min(1, t + 0.02));
            double dir = Math.Atan2(q.Y - p.Y, q.X - p.X) * 180 / Math.PI;   // the stem's heading
            double size = length * 0.2 * (1.05 - t * 0.8);
            Ornaments.Leaf(ctx, p, size, dir + 90 - 62, colour);
            Ornaments.Leaf(ctx, p, size, dir + 90 + 62, colour);
        }
    }

    static readonly (Color Cloth, Color Gilt)[] LyingBooks =
    {
        (Color.Parse("#2f5642"), Color.Parse("#d8b45c")),
        (Color.Parse("#7b302b"), Color.Parse("#d8b45c")),
        (Color.Parse("#2e3f5a"), Color.Parse("#d8b45c")),
    };

    /// <summary>
    /// A little pile of clothbound books lying on their sides, in bottle green, oxblood and navy with gilt
    /// bands on their spines, the bottom one's bottom middle at <paramref name="foot"/>.
    /// </summary>
    public static void BookPile(DrawingContext ctx, Point foot, double width)
    {
        double h = width * 0.15, y = foot.Y;
        var page = new ImmutableSolidColorBrush(Color.Parse("#efe4c8"));
        for (int i = 0; i < LyingBooks.Length; i++)
        {
            double w = width * (1 - i * 0.08), off = (i % 2 == 0 ? -1 : 1) * width * 0.03;
            double bh = h * (i == 1 ? 1.15 : 1);
            var r = new Rect(foot.X - w / 2 + off, y - bh, w, bh);
            var (cloth, gilt) = LyingBooks[i];
            // The pages show at the far end; the spine faces us.
            ctx.DrawRectangle(page, null, new Rect(r.X + w * 0.04, r.Y + bh * 0.12, w * 0.92, bh * 0.76));
            ctx.DrawRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Lighter(cloth, 0.18), 0), new GradientStop(cloth, 0.5), new GradientStop(Lighter(cloth, -0.25), 1) },
            }, null, new Rect(r.X, r.Y, w * 0.93, bh), bh * 0.22, bh * 0.22);
            var giltBrush = new ImmutableSolidColorBrush(gilt, 0.9);
            foreach (double f in new[] { 0.1, 0.16, 0.77, 0.83 })
                ctx.FillRectangle(giltBrush, new Rect(r.X + w * f, r.Y + bh * 0.15, Math.Max(1, w * 0.012), bh * 0.7));
            ctx.FillRectangle(new ImmutableSolidColorBrush(gilt, 0.75), new Rect(r.X + w * 0.36, r.Y + bh * 0.42, w * 0.2, Math.Max(1, bh * 0.14)));
            y -= bh;
        }
    }

    static Color Lighter(Color c, double by)
    {
        byte L(byte v) => (byte)Math.Clamp(by >= 0 ? v + (255 - v) * by : v * (1 + by), 0, 255);
        return Color.FromRgb(L(c.R), L(c.G), L(c.B));
    }
}
