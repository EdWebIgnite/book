namespace Reader.Core;

/*
 * Rain, snow, leaves and sparkles as plain shapes for a renderer to draw.
 *
 * Everything here is in "page units": the page is 1.0 tall and `aspect` wide, with (0, 0) at its
 * top-left corner. A renderer multiplies by the page's height on screen and adds the page's corner,
 * which is what keeps the weather fixed to the picture while the camera moves.
 *
 * Every piece's place comes from the clock and its number, so nothing is kept between frames and
 * the same time always gives the same picture.
 */

/// <summary>One raindrop: a short slanted line.</summary>
public readonly record struct Streak(double X1, double Y1, double X2, double Y2);

/// <summary>Rain at one depth. Far rain is thin, slow and faint; near rain is thick, fast and bright.</summary>
public sealed record RainSheet(double Alpha, double Width, Streak[] Streaks);

/// <summary>A splash ring where a drop landed: a flat oval that grows and fades.</summary>
public readonly record struct SplashRing(double X, double Y, double RadiusX, double RadiusY, double Alpha);

public readonly record struct Snowflake(double X, double Y, double Radius, double Alpha);

/// <summary>A tumbling leaf: a pointed oval, turned by Rotation and squashed top to bottom by Squash.</summary>
public readonly record struct Leaf(double X, double Y, double Size, double Rotation, double Squash, int Tint, double Alpha);

/// <summary>A four-pointed star that glints and fades.</summary>
public readonly record struct Glint(double X, double Y, double Size, double Alpha);

public static class Weather
{
    /// <summary>Leaf colours, by <see cref="Leaf.Tint"/>.</summary>
    public static readonly string[] LeafTints = { "#d9822b", "#e3b23c", "#b5532a", "#c96a2b", "#93a13d" };

    /// <summary>The thinnest a streak or ring is drawn, in screen pixels, however small the page is on screen.</summary>
    public const double MinLinePixels = 0.8;

    /// <summary>Line width of the splash rings, in page units.</summary>
    public const double RingWidth = 0.002;

    /// <summary>The same number in always gives the same "random" fraction out.</summary>
    static double Rnd(double n)
    {
        double s = Math.Sin(n * 127.1 + 311.7) * 43758.5453;
        return s - Math.Floor(s);
    }

    /// <summary>How many pieces: more for a bigger amount and for a wider page.</summary>
    static double Many(double amount, double aspect) => amount * aspect / 1.6;

    static int Count(double perPage, double amount, double aspect) => (int)Math.Floor(perPage * Many(amount, aspect) + 0.5);

    /// <summary>Three sheets of rain, far to near. <paramref name="level"/> (0 to 1) fades the whole effect in and out.</summary>
    public static RainSheet[] Rain(double seconds, double aspect, double amount, double level)
    {
        const double slant = 0.2;
        int perSheet = Count(110, amount, aspect);
        var sheets = new RainSheet[3];
        for (int sheet = 0; sheet < 3; sheet++)
        {
            double depth = 0.5 + sheet * 0.25, len = 0.035 + 0.045 * depth;
            var streaks = new Streak[perSheet];
            for (int n = 0; n < perSheet; n++)
            {
                int id = sheet * 1000 + n;
                double fall = seconds * (1.1 + 0.5 * Rnd(id + 0.5)) * depth * 1.25 + Rnd(id) * 9;
                double y = fall % 1.3 - 0.15;
                double x = Rnd(id * 1.37 + Math.Floor(fall / 1.3) * 7.9) * (aspect + slant) - slant * y;
                streaks[n] = new Streak(x, y, x + slant * len, y - len);
            }
            sheets[sheet] = new RainSheet((0.2 + 0.4 * depth) * level, 0.0024 * depth, streaks);
        }
        return sheets;
    }

    /// <summary>Splash rings where drops land, bigger towards the bottom of the picture (nearer to us).</summary>
    public static SplashRing[] Splashes(double seconds, double aspect, double amount, double ground, double level)
    {
        int count = Count(46, amount, aspect);
        var rings = new SplashRing[count];
        for (int n = 0; n < count; n++)
        {
            double beat = seconds / (0.7 + 0.6 * Rnd(n + 40)) + Rnd(n + 80) * 5;
            double c = Math.Floor(beat), u = beat - c;
            double gy = Rnd(n * 3.1 + c * 2.9), y = ground + gy * (1 - ground), x = Rnd(n * 7.3 + c * 1.7) * aspect;
            double size = (0.004 + 0.022 * u) * (0.45 + gy);
            rings[n] = new SplashRing(x, y, size, size * 0.36, (1 - u) * 0.55 * level);
        }
        return rings;
    }

    /// <summary>Slow flakes that drift from side to side as they fall.</summary>
    public static Snowflake[] Snow(double seconds, double aspect, double amount, double level)
    {
        int count = Count(170, amount, aspect);
        var flakes = new Snowflake[count];
        for (int n = 0; n < count; n++)
        {
            double depth = 0.4 + 0.6 * Rnd(n + 0.3);
            double fall = seconds * (0.06 + 0.07 * Rnd(n + 0.5)) * (0.6 + depth) + Rnd(n) * 9, y = fall % 1.2 - 0.1;
            double x = Rnd(n * 1.37 + Math.Floor(fall / 1.2) * 7.9) * aspect + 0.03 * depth * Math.Sin(seconds * (0.5 + Rnd(n + 0.7)) + n);
            flakes[n] = new Snowflake(x, y, 0.0022 + 0.0042 * depth, (0.4 + 0.55 * depth) * level);
        }
        return flakes;
    }

    /// <summary>A few leaves that tumble and flutter down.</summary>
    public static Leaf[] Leaves(double seconds, double aspect, double amount, double level)
    {
        int count = Count(28, amount, aspect);
        var leaves = new Leaf[count];
        for (int n = 0; n < count; n++)
        {
            double fall = seconds * (0.07 + 0.08 * Rnd(n + 0.5)) + Rnd(n) * 9, y = fall % 1.2 - 0.1;
            double x = Rnd(n * 1.37 + Math.Floor(fall / 1.2) * 7.9) * aspect + 0.07 * Math.Sin(seconds * (0.7 + Rnd(n + 0.7)) + n);
            double size = 0.011 + 0.012 * Rnd(n + 0.9);
            double rotation = seconds * (0.8 + Rnd(n + 0.2)) + n;
            double squash = 0.45 + 0.55 * Math.Abs(Math.Sin(seconds * 1.7 + n));
            leaves[n] = new Leaf(x, y, size, rotation, squash, n % LeafTints.Length, 0.92 * level);
        }
        return leaves;
    }

    /// <summary>Little stars that glint and fade, each time somewhere new.</summary>
    public static Glint[] Sparkles(double seconds, double aspect, double amount, double level)
    {
        int count = Count(38, amount, aspect);
        var stars = new Glint[count];
        for (int n = 0; n < count; n++)
        {
            double beat = seconds / (1.5 + 2 * Rnd(n + 40)) + Rnd(n + 80) * 5;
            double c = Math.Floor(beat), u = beat - c;
            double glow = Math.Pow(Math.Sin(Math.PI * u), 2), x = Rnd(n * 7.3 + c * 1.7) * aspect, y = Rnd(n * 3.1 + c * 2.9);
            double size = (0.007 + 0.013 * Rnd(n + 9)) * (0.5 + 0.5 * glow);
            stars[n] = new Glint(x, y, size, glow * 0.95 * level);
        }
        return stars;
    }
}
