using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Reader.Ui;

/// <summary>The skin's little hand-made shapes: leaves and small yellow flowers, drawn in code.</summary>
public static class Ornaments
{
    /// <summary>A leaf one unit long, its stalk at (0, 0) and its tip at (0, -1).</summary>
    static readonly Geometry LeafShape = Geometry.Parse("M0,0 C0.42,-0.18 0.46,-0.7 0,-1 C-0.46,-0.7 -0.42,-0.18 0,0 Z");
    static readonly Geometry LeafHalf = Geometry.Parse("M0,0 C0.42,-0.18 0.46,-0.7 0,-1 Z");
    static readonly Geometry LeafVein = Geometry.Parse("M0,-0.04 L0,-0.86");

    public static readonly Color[] LeafColours =
    {
        Color.Parse("#5e7f55"), Color.Parse("#7a9a6c"), Color.Parse("#4c6c46"), Color.Parse("#93ad85"),
    };

    /// <summary>Draw a leaf <paramref name="length"/> long at <paramref name="at"/>, turned by <paramref name="degrees"/> from pointing up.</summary>
    public static void Leaf(DrawingContext ctx, Point at, double length, double degrees, Color colour)
    {
        Matrix m = Matrix.CreateScale(length, length) * Matrix.CreateRotation(degrees * Math.PI / 180) * Matrix.CreateTranslation(at.X, at.Y);
        using (ctx.PushTransform(m))
        {
            ctx.DrawGeometry(new ImmutableSolidColorBrush(colour), null, LeafShape);
            // One half a little lighter, as if the light falls across the leaf.
            ctx.DrawGeometry(new ImmutableSolidColorBrush(Colors.White, 0.16), null, LeafHalf);
            ctx.DrawGeometry(null, new ImmutablePen(new ImmutableSolidColorBrush(Palette.LeafDeep, 0.35), 0.045, lineCap: PenLineCap.Round), LeafVein);
        }
    }

    /// <summary>A small five-petalled flower: cream petals round a brass middle.</summary>
    public static void Flower(DrawingContext ctx, Point at, double r)
    {
        var petal = new ImmutableSolidColorBrush(Color.Parse("#f6ecd2"));
        for (int i = 0; i < 5; i++)
        {
            double a = i * Math.PI * 2 / 5 - Math.PI / 2;
            ctx.DrawEllipse(petal, null, new Point(at.X + Math.Cos(a) * r * 0.62, at.Y + Math.Sin(a) * r * 0.62), r * 0.5, r * 0.5);
        }
        ctx.DrawEllipse(new ImmutableSolidColorBrush(Palette.Sun), null, at, r * 0.42, r * 0.42);
    }
}

/// <summary>Three leaves and a flower: the skin's small mark, beside "My Books" and on The End box.</summary>
public sealed class Sprig : Control
{
    public Sprig() => IsHitTestVisible = false;

    public override void Render(DrawingContext ctx)
    {
        double s = Math.Min(Bounds.Width, Bounds.Height);
        var foot = new Point(s * 0.42, s * 0.95);
        Ornaments.Leaf(ctx, foot, s * 0.62, -38, Ornaments.LeafColours[0]);
        Ornaments.Leaf(ctx, foot, s * 0.74, 6, Ornaments.LeafColours[2]);
        Ornaments.Leaf(ctx, foot, s * 0.56, 48, Ornaments.LeafColours[1]);
        Ornaments.Flower(ctx, new Point(s * 0.78, s * 0.3), s * 0.16);
    }
}
