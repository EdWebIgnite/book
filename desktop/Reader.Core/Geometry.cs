namespace Reader.Core;

/// <summary>A rectangle: top-left corner and size. Used for page boxes, the camera and screen areas alike.</summary>
public readonly record struct RectD(double X, double Y, double W, double H)
{
    public double Right => X + W;
    public double Bottom => Y + H;
    public double CenterX => X + W / 2;
    public double CenterY => Y + H / 2;

    public bool Contains(double x, double y) => x >= X && x <= X + W && y >= Y && y <= Y + H;
}

/// <summary>A point on the screen or in the world.</summary>
public readonly record struct PointD(double X, double Y);

/// <summary>The small maths the camera is built from.</summary>
public static class Geo
{
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

    /// <summary>Grow a rectangle by <paramref name="d"/> on every side (shrink it with a negative number).</summary>
    public static RectD Inflate(RectD r, double d) => new(r.X - d, r.Y - d, r.W + 2 * d, r.H + 2 * d);

    /// <summary>The smallest rectangle that holds both.</summary>
    public static RectD Union(RectD a, RectD b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        return new RectD(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }

    /// <summary>Blend two rectangles. "Smooth" blends sizes on a log scale so big zooms feel even.</summary>
    public static RectD MixRect(RectD a, RectD b, double t, bool smooth)
    {
        if (!smooth) return new RectD(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t), Lerp(a.W, b.W, t), Lerp(a.H, b.H, t));
        double cx = Lerp(a.CenterX, b.CenterX, t);
        double cy = Lerp(a.CenterY, b.CenterY, t);
        double w = Math.Exp(Lerp(Math.Log(a.W), Math.Log(b.W), t));
        double h = Math.Exp(Lerp(Math.Log(a.H), Math.Log(b.H), t));
        return new RectD(cx - w / 2, cy - h / 2, w, h);
    }

    /// <summary>Scale a rectangle about its centre, optionally sliding the centre sideways by <paramref name="dx"/>.</summary>
    public static RectD ScaleAround(RectD r, double f, double dx = 0)
    {
        double cx = r.CenterX + dx, cy = r.CenterY;
        return new RectD(cx - r.W * f / 2, cy - r.H * f / 2, r.W * f, r.H * f);
    }

    /// <summary>What part of the world a screen of the given aspect ratio shows when fitted around <paramref name="cam"/>.</summary>
    public static RectD FitToAspect(RectD cam, double aspect)
    {
        double w = cam.W, h = cam.H;
        if (w / h < aspect) w = h * aspect; else h = w / aspect;
        return new RectD(cam.CenterX - w / 2, cam.CenterY - h / 2, w, h);
    }

    /// <summary>
    /// Pages side by side in "world" space: each page is 1.0 tall and as wide as its aspect ratio
    /// (width divided by height), with a small gap between neighbours.
    /// </summary>
    public static List<RectD> LayoutPages(IEnumerable<double> aspects)
    {
        const double gap = 0.12;
        double x = 0;
        var rects = new List<RectD>();
        foreach (double aspect in aspects)
        {
            var r = new RectD(x, 0, aspect, 1);
            rects.Add(r);
            x += r.W + gap;
        }
        return rects;
    }
}

/// <summary>The "feel" curves a camera move can follow. Each takes 0..1 and returns 0..1 (some overshoot a little).</summary>
public static class Easings
{
    public const string Linear = "linear";
    public const string EaseInOutCubic = "easeInOutCubic";
    public const string EaseOutCubic = "easeOutCubic";
    public const string EaseInOutSine = "easeInOutSine";
    public const string EaseOutBack = "easeOutBack";
    public const string Spring = "spring";

    public static double InOutCubic(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    public static double OutCubic(double t) => 1 - Math.Pow(1 - t, 3);
    public static double InOutSine(double t) => -(Math.Cos(Math.PI * t) - 1) / 2;

    public static double OutBack(double t)
    {
        const double c1 = 1.4, c3 = c1 + 1;
        return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
    }

    public static double SpringCurve(double t) => t >= 1 ? 1 : 1 - Math.Exp(-6 * t) * Math.Cos(11 * t);

    /// <summary>Apply the named curve. An unknown name falls back to the smooth cubic, as the browser version does.</summary>
    public static double Apply(string? name, double t) => name switch
    {
        Linear => t,
        EaseInOutCubic => InOutCubic(t),
        EaseOutCubic => OutCubic(t),
        EaseInOutSine => InOutSine(t),
        EaseOutBack => OutBack(t),
        Spring => SpringCurve(t),
        _ => InOutCubic(t),
    };
}
