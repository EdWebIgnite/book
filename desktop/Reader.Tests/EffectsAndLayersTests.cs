using Reader.Core;

namespace Reader.Tests;

/// <summary>Weather that runs between stops, pictures on top that move, and the tiles of a big scene.</summary>
public class EffectsAndLayersTests
{
    const string Puddle = "biggest-puddle";
    const int RainPage = 6, ButterflyPage = 7, SplashPage = 9;

    static PageEffect Falling(string type, int? from = null, int? to = null) => new() { Type = type, Amount = 0.6, From = from, To = to };

    static Book ThreeStops(params PageEffect[] effects)
    {
        Book book = Kit.OnePage(Kit.StopAt(0.1, 0.1), Kit.StopAt(0.4, 0.4), Kit.StopAt(0.6, 0.6));
        book.Pages[0].Effects.AddRange(effects);
        book.Pages.Add(new Page { Image = "page2.jpg", Explore = true, Effects = { Falling("snow", to: 2), Falling("leaves") } });
        return book;
    }

    static int Wanted(Rig rig, int page, int effect) => rig.Engine.EffectWanted(page, rig.Engine.Book!.Pages[page].Effects[effect]);

    [Fact]
    public void An_effect_with_no_range_runs_for_the_whole_page_and_only_on_its_own_page()
    {
        Rig rig = Kit.Open(ThreeStops(Falling("rain")));
        foreach (int stop in new[] { Step.WholePage, 0, 1, 2 })
        {
            rig.Jump(0, stop);
            Assert.Equal(1, Wanted(rig, 0, 0));
        }
        rig.Jump(1, Step.Exploring);
        Assert.Equal(0, Wanted(rig, 0, 0));
        Assert.Equal(1, Wanted(rig, 1, 1));      // leaves on the explore page keep falling while exploring
    }

    [Theory]
    [InlineData(2, null, new[] { 0, 0, 1, 1 })]      // from stop 2: off on the whole page and stop 1
    [InlineData(null, 2, new[] { 1, 1, 1, 0 })]      // to stop 2: on from the start, off at stop 3
    [InlineData(2, 2, new[] { 0, 0, 1, 0 })]         // only at stop 2
    [InlineData(1, 3, new[] { 1, 1, 1, 1 })]         // "from 1" is the same as no start
    public void From_and_to_pick_the_stops_an_effect_runs_between(int? from, int? to, int[] expected)
    {
        Rig rig = Kit.Open(ThreeStops(Falling("snow", from, to)));
        int[] got = new[] { Step.WholePage, 0, 1, 2 }.Select(stop => { rig.Jump(0, stop); return Wanted(rig, 0, 0); }).ToArray();
        Assert.Equal(expected, got);
    }

    [Fact]
    public void An_effect_with_an_end_stop_is_over_by_the_time_the_child_explores()
    {
        Rig rig = Kit.Open(ThreeStops());
        rig.Jump(1, Step.Exploring);
        Assert.Equal(0, Wanted(rig, 1, 0));
    }

    [Fact]
    public void The_sample_books_sparkles_start_at_the_second_stop_of_the_splash_page()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        PageEffect sparkles = e.Book!.Pages[SplashPage].Effects[1];
        Assert.Equal("sparkles", sparkles.Type);
        Assert.Equal(2, sparkles.From);

        rig.Jump(SplashPage, 0);
        rig.Run(1500);
        Assert.Equal(0, e.EffectLevel(SplashPage, 1));

        rig.Next();
        rig.Run(200);
        Assert.InRange(e.EffectLevel(SplashPage, 1), 0.3, 0.999);    // fading in
        rig.Run(1500);
        Assert.Equal(1, e.EffectLevel(SplashPage, 1));

        rig.Prev();
        rig.RunUntilStill();
        rig.Run(2000);
        Assert.Equal(0, e.EffectLevel(SplashPage, 1));               // and out again
    }

    [Fact]
    public void Effects_fade_at_the_same_pace_whatever_the_frame_rate()
    {
        double level(double frameMs)
        {
            Rig rig = Kit.Open(ThreeStops(Falling("snow")));
            rig.Run(300, frameMs);
            return rig.Engine.EffectLevel(0, 0);
        }
        Assert.Equal(level(4), level(50), 6);
        Assert.InRange(level(16), 0.6, 0.8);
    }

    [Fact]
    public void Rain_is_heard_while_it_falls_and_stops_when_the_page_is_left()
    {
        Rig rig = Kit.Open(Puddle);
        rig.Jump(RainPage, 0);
        rig.Run(2000);
        double loud = rig.Sound.RainLevels[^1];
        Assert.InRange(loud, 0.4 + 0.6 * 0.7 - 0.011, 0.4 + 0.6 * 0.7);   // amount 0.7 on this page (changes under 1% aren't passed on)
        rig.Jump(RainPage + 1, 0);
        rig.Run(2500);
        Assert.Equal(0, rig.Sound.RainLevels[^1]);
    }

    [Fact]
    public void Reduced_motion_switches_the_weather_off()
    {
        Rig rig = Kit.Open(Puddle, s => s.ReduceMotion = "on");
        rig.Jump(RainPage, 0);
        rig.Run(2000);
        Assert.Equal(0, rig.Engine.EffectLevel(RainPage, 0));
        Assert.DoesNotContain(rig.Sound.RainLevels, l => l > 0);
    }

    // ------------------------------------------------------------------ weather shapes

    [Fact]
    public void Weather_is_worked_out_from_the_clock_alone()
    {
        Assert.Equal(Weather.Rain(12.5, 1.6, 0.7, 1)[1].Streaks, Weather.Rain(12.5, 1.6, 0.7, 1)[1].Streaks);
        Assert.Equal(Weather.Sparkles(3, 1.6, 0.5, 1), Weather.Sparkles(3, 1.6, 0.5, 1));
        Assert.NotEqual(Weather.Snow(3, 1.6, 0.5, 1), Weather.Snow(3.1, 1.6, 0.5, 1));
    }

    [Fact]
    public void There_is_more_weather_for_a_bigger_amount_and_a_wider_page()
    {
        Assert.Equal(3, Weather.Rain(0, 1.6, 1, 1).Length);
        Assert.Equal(110, Weather.Rain(0, 1.6, 1, 1)[0].Streaks.Length);
        Assert.Equal(55, Weather.Rain(0, 1.6, 0.5, 1)[0].Streaks.Length);
        Assert.Equal(220, Weather.Rain(0, 3.2, 1, 1)[0].Streaks.Length);
        Assert.Equal(170, Weather.Snow(0, 1.6, 1, 1).Length);
        Assert.Equal(28, Weather.Leaves(0, 1.6, 1, 1).Length);
        Assert.Equal(38, Weather.Sparkles(0, 1.6, 1, 1).Length);
        Assert.Equal(46, Weather.Splashes(0, 1.6, 1, 0.6, 1).Length);
    }

    [Fact]
    public void Weather_stays_on_its_page_and_fades_with_its_level()
    {
        for (double t = 0; t < 20; t += 0.37)
        {
            foreach (SplashRing ring in Weather.Splashes(t, 1.6, 0.7, 0.62, 1))
            {
                Assert.InRange(ring.Y, 0.62, 1);                     // only on the ground
                Assert.InRange(ring.X, 0, 1.6);
            }
            foreach (Snowflake flake in Weather.Snow(t, 1.6, 0.7, 1)) Assert.InRange(flake.Y, -0.1, 1.1);
            foreach (Glint star in Weather.Sparkles(t, 1.6, 0.7, 1)) { Assert.InRange(star.X, 0, 1.6); Assert.InRange(star.Y, 0, 1); }
            foreach (RainSheet sheet in Weather.Rain(t, 1.6, 0.7, 1))
                foreach (Streak s in sheet.Streaks) Assert.InRange(s.Y1, -0.15, 1.15);
        }
        Assert.Equal(Weather.Rain(1, 1.6, 0.7, 1)[2].Alpha / 2, Weather.Rain(1, 1.6, 0.7, 0.5)[2].Alpha, 9);
        Assert.All(Weather.Leaves(1, 1.6, 0.7, 0), leaf => Assert.Equal(0, leaf.Alpha));
    }

    // ------------------------------------------------------------------ layers

    [Fact]
    public void The_butterfly_waits_then_flies_across_when_its_stop_arrives()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        Layer butterfly = e.Book!.Pages[ButterflyPage].Layers[0];
        Assert.Equal(2, butterfly.MoveAt);
        Assert.Equal(5, butterfly.MoveSeconds);

        rig.Jump(ButterflyPage, Step.WholePage);
        rig.Run(500);
        Assert.Equal(0, e.LayerProgress(ButterflyPage, 0));
        Assert.Equal(1, e.LayerOpacity(ButterflyPage, 0));
        LayerDraw waiting = Assert.Single(e.LayersFor(ButterflyPage));
        Assert.Equal(butterfly.Box.X, waiting.Box.X, 9);
        Assert.Equal("butterfly.png", waiting.Image);

        rig.NextAndArrive();                                         // stop 1: still waiting
        Assert.Equal(0, e.LayerProgress(ButterflyPage, 0));

        rig.Next();                                                  // stop 2 arrives: off it goes, taking 5 seconds
        double left = rig.Now;
        rig.Run(2500);
        Assert.Equal(0.5, e.LayerProgress(ButterflyPage, 0), 2);
        LayerDraw flying = Assert.Single(e.LayersFor(ButterflyPage));
        Assert.InRange(flying.Box.X - (butterfly.Box.X + butterfly.MoveTo!.Value.X) / 2, -0.01, 0.01);
        Assert.InRange(flying.Box.Y - (butterfly.Box.Y + butterfly.MoveTo.Value.Y) / 2, -0.001, 0.001);   // no bobbing in flight
        rig.Run(2600);
        Assert.Equal(1, e.LayerProgress(ButterflyPage, 0));
        Assert.Equal(butterfly.MoveTo.Value.X, e.LayersFor(ButterflyPage)[0].Box.X, 9);
        Assert.True(rig.Now - left >= 5000);

        rig.Prev();                                                  // back a stop: it flies home again
        rig.Run(5100);
        Assert.Equal(0, e.LayerProgress(ButterflyPage, 0));
    }

    [Fact]
    public void A_layer_is_at_its_start_before_its_page_and_at_its_end_after()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Jump(ButterflyPage - 1, 0);
        Assert.Equal(0, e.LayerProgress(ButterflyPage, 0));
        rig.Jump(ButterflyPage + 1, 0);
        Assert.Equal(1, e.LayerProgress(ButterflyPage, 0));         // seen from the next page, it has already flown
        Assert.Equal((true, 1), e.LayerWanted(ButterflyPage, e.Book!.Pages[ButterflyPage].Layers[0]));
    }

    [Fact]
    public void A_layer_bobs_while_it_waits_unless_motion_is_reduced()
    {
        Rig rig = Kit.Open(Puddle);
        rig.Jump(ButterflyPage, 0);
        var ys = new HashSet<double>();
        for (int i = 0; i < 20; i++) { rig.Run(100); ys.Add(Math.Round(rig.Engine.LayersFor(ButterflyPage)[0].Box.Y, 6)); }
        Assert.True(ys.Count > 5, "the butterfly should be bobbing");

        Rig calm = Kit.Open(Puddle, s => s.ReduceMotion = "on");
        calm.Jump(ButterflyPage, 0);
        double y = calm.Engine.LayersFor(ButterflyPage)[0].Box.Y;
        for (int i = 0; i < 20; i++) { calm.Run(100); Assert.Equal(y, calm.Engine.LayersFor(ButterflyPage)[0].Box.Y); }
    }

    [Fact]
    public void A_layer_can_appear_and_go_away_at_chosen_stops_and_hop_to_its_new_place()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.1, 0.1), Kit.StopAt(0.4, 0.4), Kit.StopAt(0.6, 0.6));
        book.Pages[0].Layers.Add(new Layer
        {
            Image = "frog.png", Box = new RectD(0.1, 0.5, 0.1, 0.1), From = 2, To = 2,
            MoveTo = new RectD(0.5, 0.5, 0.1, 0.1), MoveAt = 2, MoveSeconds = 1, Path = "hop",
        });
        Rig rig = Kit.Open(book);
        ReaderEngine e = rig.Engine;
        Assert.Empty(e.LayersFor(0));                                // not yet: the whole page comes before stop 2
        rig.NextAndArrive();
        Assert.Empty(e.LayersFor(0));
        rig.Next();                                                  // stop 2: appears and hops
        rig.Run(500);
        LayerDraw mid = Assert.Single(e.LayersFor(0));
        Assert.Equal(0.3, mid.Box.X, 2);
        Assert.True(mid.Box.Y < 0.45, "half way through a hop it should be up in the air");
        rig.Run(3000);
        Assert.Equal(0.5, e.LayersFor(0)[0].Box.Y, 6);              // landed
        rig.NextAndArrive();                                         // stop 3: gone
        rig.Run(400);
        Assert.Empty(e.LayersFor(0));
    }

    // ------------------------------------------------------------------ tiles

    [Fact]
    public void A_big_scene_asks_for_sharp_tiles_only_where_the_child_is_looking()
    {
        Rig rig = Kit.Open("deep-sea");
        ReaderEngine e = rig.Engine;
        TileInfo info = e.Book!.Pages[0].Tiles!;
        Assert.Equal((8192, 6144, 1024, 2), (info.Width, info.Height, info.Size, info.Levels));

        rig.Jump(0, Step.Exploring);
        e.ToggleOverview();                                          // right out: the small overview picture is enough
        rig.RunUntilStill();
        Assert.Empty(e.TilesNeeded(0, e.GetView()));

        for (int i = 0; i < 6; i++) { e.ZoomStep(1.7); rig.RunUntilStill(); rig.Run(20); }
        Assert.False(e.State.CanZoomIn);
        ViewTransform v = e.GetView();
        List<TileDraw> tiles = e.TilesNeeded(0, v);
        Assert.NotEmpty(tiles);
        Assert.All(tiles, t => Assert.Equal(0, t.Level));            // full size when zoomed right in
        Assert.InRange(tiles.Count, 1, 12);                          // a screenful, not all 48
        RectD screen = new(0, 0, v.Width, v.Height);
        foreach (TileDraw t in tiles)
        {
            RectD r = v.ToScreen(t.World);
            Assert.True(r.Right >= 0 && r.X <= screen.W && r.Bottom >= 0 && r.Y <= screen.H, "every tile asked for is on screen");
            Assert.True(t.HasParent);
            Assert.Equal((t.X / 2, t.Y / 2), (t.ParentX, t.ParentY));
            Assert.Equal(512, t.ParentSource.W);
            Assert.True(File.Exists(Path.Combine(Kit.BookFolder("deep-sea"), info.PathFor(t.Level, t.X, t.Y))), info.PathFor(t.Level, t.X, t.Y));
        }
        // Together the tiles cover the page area on screen.
        RectD page = v.ToScreen(e.PageRects[0]);
        double covered = tiles.Sum(t => Overlap(v.ToScreen(t.World), screen));
        Assert.Equal(Overlap(page, screen), covered, 0);

        // On a sharper display the same view needs the same tiles; half-size tiles do when zoomed part of the way.
        rig.Jump(0, 0);
        List<TileDraw> intro = e.TilesNeeded(0, e.GetView());
        Assert.NotEmpty(intro);
        Assert.All(intro, t => Assert.Equal(1, t.Level));
        Assert.All(intro, t => Assert.False(t.HasParent));           // level 1 is the last level
        Assert.All(e.TilesNeeded(0, e.GetView(), pixelsPerUnit: 2), t => Assert.Equal(0, t.Level));
    }

    static double Overlap(RectD a, RectD b)
    {
        double w = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X), h = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
        return w > 0 && h > 0 ? w * h : 0;
    }
}
