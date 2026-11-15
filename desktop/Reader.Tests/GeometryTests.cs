using Reader.Core;

namespace Reader.Tests;

public class GeometryTests
{
    static void Near(double expected, double actual, double within = 1e-9) =>
        Assert.True(Math.Abs(expected - actual) <= within, $"expected {expected} but got {actual}");

    [Fact]
    public void Straight_blend_is_halfway_on_every_number()
    {
        RectD mid = Geo.MixRect(new RectD(0, 0, 1, 1), new RectD(2, 4, 3, 5), 0.5, smooth: false);
        Assert.Equal(new RectD(1, 2, 2, 3), mid);
    }

    [Fact]
    public void Smooth_blend_moves_the_centre_evenly_and_the_size_on_a_log_scale()
    {
        var a = new RectD(0, 0, 4, 2);
        var b = new RectD(10, 10, 1, 0.5);
        RectD mid = Geo.MixRect(a, b, 0.5, smooth: true);
        Near(2, mid.W);                 // halfway between 4 and 1 on a log scale is their geometric mean
        Near(1, mid.H);
        Near((a.CenterX + b.CenterX) / 2, mid.CenterX);
        Near((a.CenterY + b.CenterY) / 2, mid.CenterY);
        // The ends are exact.
        RectD start = Geo.MixRect(a, b, 0, true), end = Geo.MixRect(a, b, 1, true);
        Near(a.X, start.X); Near(a.W, start.W); Near(b.X, end.X); Near(b.H, end.H);
    }

    [Fact]
    public void Inflate_grows_every_side_and_union_holds_both()
    {
        Assert.Equal(new RectD(-1, -1, 4, 5), Geo.Inflate(new RectD(0, 0, 2, 3), 1));
        Assert.Equal(new RectD(0, 0, 5, 6), Geo.Union(new RectD(0, 0, 1, 1), new RectD(3, 2, 2, 4)));
    }

    [Fact]
    public void Fit_to_aspect_only_ever_shows_more_and_keeps_the_centre()
    {
        var cam = new RectD(1, 1, 2, 2);
        RectD wide = Geo.FitToAspect(cam, 2);
        Assert.Equal(new RectD(0, 1, 4, 2), wide);
        RectD tall = Geo.FitToAspect(cam, 0.5);
        Assert.Equal(new RectD(1, 0, 2, 4), tall);
    }

    [Fact]
    public void Pages_sit_side_by_side_one_unit_tall_with_a_gap()
    {
        List<RectD> pages = Geo.LayoutPages(new[] { 1.6, 0.75, 3.2 });
        Assert.Equal(new RectD(0, 0, 1.6, 1), pages[0]);
        Near(1.72, pages[1].X);
        Near(0.75, pages[1].W);
        Near(1.72 + 0.75 + 0.12, pages[2].X);
        Assert.All(pages, p => Assert.Equal(1, p.H));
    }

    [Theory]
    [InlineData("linear")]
    [InlineData("easeInOutCubic")]
    [InlineData("easeOutCubic")]
    [InlineData("easeInOutSine")]
    [InlineData("easeOutBack")]
    [InlineData("spring")]
    public void Every_easing_starts_at_zero_and_ends_at_one(string name)
    {
        Near(0, Easings.Apply(name, 0), 1e-9);
        Near(1, Easings.Apply(name, 1), 1e-9);
    }

    [Fact]
    public void The_page_turns_landing_starts_like_the_sine_and_creeps_to_rest()
    {
        Near(0, Easings.SoftLanding(0), 1e-12);
        Near(1, Easings.SoftLanding(1), 1e-12);
        Near(Easings.SoftLanding(0.5), 0.578, 1e-3);
        // It only ever goes forward.
        for (double t = 0; t < 1; t += 0.01) Assert.True(Easings.SoftLanding(t + 0.01) >= Easings.SoftLanding(t));
        // The start is as gentle as the sine's (both lift with a speed growing from nothing) ...
        Assert.InRange(Easings.SoftLanding(0.1) / Easings.InOutSine(0.1), 0.8, 1.4);
        // ... and the end much gentler: speed and slowing down both reach nothing, so the step from one
        // frame to the next shrinks smoothly to nothing instead of stopping short.
        const double frame = 1.0 / 54;                           // a 900 ms turn at 60 frames a second
        double lastSine = 1 - Easings.InOutSine(1 - frame), lastSoft = 1 - Easings.SoftLanding(1 - frame);
        Assert.True(lastSoft < lastSine / 10, $"last frame moves {lastSoft} against the sine's {lastSine}");
        double[] steps = Enumerable.Range(44, 11).Select(i => Easings.SoftLanding(i * frame) - Easings.SoftLanding((i - 1) * frame)).ToArray();
        for (int i = 1; i < steps.Length; i++) Assert.True(steps[i] < steps[i - 1], "each of the last frames moves less than the one before");
        for (int i = 2; i < steps.Length; i++)
            Assert.True(steps[i - 1] - steps[i] <= steps[i - 2] - steps[i - 1] + 1e-12, "and the slowing down eases off too");
    }

    [Fact]
    public void The_symmetric_easings_are_halfway_at_half_time_and_an_unknown_name_is_the_cubic()
    {
        Near(0.5, Easings.Apply("easeInOutSine", 0.5));
        Near(0.5, Easings.Apply("easeInOutCubic", 0.5));
        Near(Easings.InOutCubic(0.3), Easings.Apply("no such feel", 0.3));
    }
}
