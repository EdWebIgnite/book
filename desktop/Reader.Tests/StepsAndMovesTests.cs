using Reader.Core;

namespace Reader.Tests;

/// <summary>How a book becomes a list of steps, and how the camera gets from one to the next.</summary>
public class StepsAndMovesTests
{
    const string Puddle = "biggest-puddle";

    [Fact]
    public void Each_guided_page_opens_whole_first_then_goes_through_its_stops()
    {
        Rig rig = Kit.Open(Puddle);
        var steps = rig.Engine.Steps;
        Assert.Equal(new Step(0, Step.WholePage), steps[0]);
        Assert.Equal(new Step(0, 0), steps[1]);
        Assert.Equal(new Step(0, 1), steps[2]);
        Assert.Equal(new Step(1, Step.WholePage), steps[3]);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    public void Pages_with_openWhole_false_open_on_their_first_stop(int pageNumber)
    {
        Rig rig = Kit.Open(Puddle);
        int page = pageNumber - 1;
        Assert.False(rig.Engine.Book!.Pages[page].OpenWhole);
        Step first = rig.Engine.Steps.First(s => s.Page == page);
        Assert.Equal(0, first.Stop);
        Assert.DoesNotContain(rig.Engine.Steps, s => s.Page == page && s.Stop == Step.WholePage);
    }

    [Fact]
    public void The_explore_page_plays_its_intro_and_ends_in_an_explore_step()
    {
        Rig rig = Kit.Open(Puddle);
        var steps = rig.Engine.Steps;
        int page = rig.Engine.Book!.Pages.Count - 1;
        Assert.True(rig.Engine.Book.Pages[page].Explore);
        Assert.Equal(new[] { new Step(page, Step.WholePage), new Step(page, 0), new Step(page, Step.Exploring, true) },
            steps.Where(s => s.Page == page));
        Assert.Equal(steps[^1], new Step(page, Step.Exploring, true));
        // Only the last page hands over the camera.
        Assert.Single(steps, s => s.Explore);
    }

    [Fact]
    public void Switching_off_whole_page_first_leaves_only_the_stops()
    {
        Rig rig = Kit.Open(Puddle, s => s.WholePageFirst = false);
        Assert.DoesNotContain(rig.Engine.Steps, s => s.Stop == Step.WholePage);
        Assert.Equal(new Step(0, 0), rig.Engine.Steps[0]);
    }

    [Fact]
    public void Whole_page_last_adds_a_closing_look_at_each_guided_page()
    {
        Rig rig = Kit.Open(Puddle, s => s.WholePageLast = true);
        var page1 = rig.Engine.Steps.Where(s => s.Page == 0).ToList();
        Assert.Equal(new[] { -1, 0, 1, -1 }, page1.Select(s => s.Stop));
    }

    [Fact]
    public void A_page_with_no_stops_is_shown_whole_and_an_explore_page_with_none_goes_straight_to_exploring()
    {
        var book = new Book { Id = "b", Title = "B", Pages = { new Page { Image = "a.jpg" }, new Page { Image = "b.jpg", Explore = true } } };
        Rig rig = Kit.Open(book);
        Assert.Equal(new[] { new Step(0, Step.WholePage), new Step(1, Step.Exploring, true) }, rig.Engine.Steps);
    }

    [Fact]
    public void The_whole_page_is_framed_with_a_little_air_and_a_stop_with_its_padding()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        Assert.True(e.State.WholePage);
        Assert.Equal(Geo.Inflate(e.PageRects[0], 0.03), e.Shown.Cam);
        // Soft edges at 60% strength on the whole page of a "vignette" book.
        Assert.Equal(0.75 * 0.6, e.Shown.Vig, 9);

        rig.NextAndArrive();
        Stop stop = e.Book!.Pages[0].Stops[0];
        RectD box = e.BoxRect(0, stop.Box);
        Assert.Equal(Geo.Inflate(box, e.Settings.Padding), e.Shown.Cam);
        Assert.Equal(box, e.Shown.Focus);
        Assert.Equal(0.75, e.Shown.Vig, 9);
        Assert.Equal("Moss had new boots.", e.State.Caption);
    }

    [Fact]
    public void A_spotlight_stop_goes_dark_outside_the_light_and_the_raindrop_page_never_shows_whole()
    {
        Rig rig = Kit.Open(Puddle);
        rig.Jump(5, 0);
        ReaderEngine e = rig.Engine;
        Assert.Equal("spotlight", e.CurrentStop!.Style);
        Assert.Equal(0.97, e.Shown.Spot, 9);
        Assert.Equal(0, e.Shown.Vig);
        Assert.Equal(0, e.Shown.Dim);
        Assert.False(e.State.WholePage);
    }

    [Fact]
    public void The_comic_uses_the_dim_style_on_every_panel()
    {
        Rig rig = Kit.Open("captain-megawatt");
        rig.Jump(0, 0);
        Assert.Equal(0.72, rig.Engine.Shown.Dim, 9);
        Assert.Equal(0, rig.Engine.Shown.Vig);
    }

    // ------------------------------------------------------------------ move time

    [Fact]
    public void A_short_hop_takes_about_half_the_set_time_and_a_long_one_up_to_two_and_a_half_times()
    {
        var e = new ReaderEngine(new ReaderSettings());
        var a = new RectD(0, 0, 1, 1);
        Assert.Equal(1400 * 0.55, e.MoveTime(a, a, 1400).Ms, 6);                          // nowhere to go: the shortest
        Assert.Equal(1400 * (0.55 + 0.45 * 1), e.MoveTime(a, new RectD(1, 0, 1, 1), 1400).Ms, 6);   // one camera-width sideways
        Assert.Equal(1400 * 2.4, e.MoveTime(a, new RectD(30, 0, 1, 1), 1400).Ms, 6);      // very far: capped
        Assert.False(e.MoveTime(a, new RectD(2.9, 0, 1, 1), 1400).Far);
        Assert.True(e.MoveTime(a, new RectD(3.1, 0, 1, 1), 1400).Far);                    // far enough to pull back mid-flight
    }

    [Fact]
    public void Zooming_counts_as_distance_too()
    {
        var e = new ReaderEngine(new ReaderSettings());
        var a = new RectD(0, 0, 1, 1);
        RectD half = Geo.ScaleAround(a, 0.5);
        Assert.Equal(1400 * (0.55 + 0.45 * Math.Log(2)), e.MoveTime(a, half, 1400).Ms, 6);
        // The same either way.
        Assert.Equal(e.MoveTime(a, half, 1400).Ms, e.MoveTime(half, a, 1400).Ms, 6);
    }

    [Fact]
    public void With_scaling_off_every_move_takes_the_set_time_but_zoom_still_has_a_speed_limit()
    {
        var e = new ReaderEngine(new ReaderSettings { ScaleTime = false });
        var a = new RectD(0, 0, 1, 1);
        Assert.Equal(400, e.MoveTime(a, new RectD(9, 0, 1, 1), 400).Ms, 6);
        // Zooming by a factor of 100 is never quicker than 330 ms per e-fold.
        Assert.Equal(Math.Log(100) * 330, e.MoveTime(a, Geo.ScaleAround(a, 0.01), 400).Ms, 6);
    }

    [Fact]
    public void An_ordinary_move_takes_the_scaled_time_and_follows_the_clock_not_the_frame_rate()
    {
        double arrive(double frameMs)
        {
            Rig rig = Kit.Open(Puddle);
            rig.Next();
            Assert.True(rig.Engine.Animating);
            double expected = rig.Engine.MoveTime(Geo.Inflate(rig.Engine.PageRects[0], 0.03), rig.Engine.Target().Cam, 1400).Ms;
            double took = rig.RunUntilStill(frameMs: frameMs);
            Assert.InRange(took, expected, expected + frameMs);
            return expected;
        }
        Assert.Equal(arrive(5), arrive(40));

        // Half way through, the camera is in the same place however many frames it took to get there.
        RectD at(double frameMs)
        {
            Rig rig = Kit.Open(Puddle);
            rig.Next();
            rig.Run(400, frameMs);
            return rig.Engine.Shown.Cam;
        }
        RectD fine = at(4), coarse = at(50);
        Assert.Equal(fine.X, coarse.X, 9);
        Assert.Equal(fine.W, coarse.W, 9);
    }

    [Fact]
    public void A_pan_stop_glides_to_its_second_box_after_arriving()
    {
        Rig rig = Kit.Open(Puddle);
        rig.Jump(0, 1);
        ReaderEngine e = rig.Engine;
        Stop stop = e.CurrentStop!;
        Assert.NotNull(stop.To);
        Assert.Equal(Geo.Inflate(e.BoxRect(0, stop.Box), 0.025), e.Shown.Cam);
        rig.Run(stop.PanSeconds!.Value * 1000 - 100);
        Assert.True(e.Animating);
        rig.Run(150);
        Assert.False(e.Animating);
        RectD end = Geo.Inflate(e.BoxRect(0, stop.To!.Value), 0.025);
        Assert.Equal(end.X, e.Shown.Cam.X, 9);
        Assert.Equal(end.W, e.Shown.Cam.W, 9);
    }

    // ------------------------------------------------------------------ taps

    [Fact]
    public void A_second_tap_straight_away_is_ignored()
    {
        Rig rig = Kit.Open(Puddle);
        rig.Engine.Next();
        Assert.Equal(1, rig.Engine.StepIndex);
        rig.Run(200);
        rig.Engine.Next();                      // 200 ms later: an accident
        Assert.Equal(1, rig.Engine.StepIndex);
        Assert.True(rig.Engine.Animating);
    }

    [Fact]
    public void An_impatient_tap_finishes_the_move_and_the_next_tap_goes_on()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        e.Next();
        rig.Run(400);
        Assert.True(e.Animating);
        e.Next();                               // still travelling: just get there
        Assert.Equal(1, e.StepIndex);
        Assert.False(e.Animating);
        Assert.Equal(e.Target().Cam, e.Shown.Cam);
        rig.Run(400);
        e.Next();
        Assert.Equal(2, e.StepIndex);
    }

    [Fact]
    public void Going_back_from_the_first_step_does_nothing_and_going_on_from_the_last_finishes_the_book()
    {
        Rig rig = Kit.Open("captain-megawatt");
        ReaderEngine e = rig.Engine;
        Assert.False(e.State.CanGoBack);
        Assert.True(e.State.FirstStep);
        rig.Prev();
        Assert.Equal(0, e.StepIndex);

        rig.Jump(2, 3);
        rig.Next();
        Assert.True(e.State.Finished);
        rig.Next();                              // nothing more happens
        Assert.True(e.State.Finished);

        rig.Engine.Restart();
        Assert.False(e.State.Finished);
        Assert.Equal(0, e.StepIndex);
    }

    [Fact]
    public void Whole_page_and_back_to_the_close_up()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Jump(0, 0);
        Assert.False(e.State.WholePage);
        e.ToggleOverview();
        rig.RunUntilStill();
        Assert.True(e.State.WholePage);
        Assert.True(e.State.Overview);
        Assert.Equal("", e.State.Caption);                       // no words over the whole page
        Assert.Equal(Geo.Inflate(e.PageRects[0], 0.03), e.Shown.Cam);
        e.ToggleOverview();
        rig.RunUntilStill();
        Assert.False(e.State.WholePage);
        Assert.Equal(1, e.StepIndex);
    }

    [Fact]
    public void Questions_for_the_grown_up_only_show_when_switched_on()
    {
        Rig off = Kit.Open(Puddle);
        off.Jump(0, 1);
        Assert.Equal("", off.Engine.State.Ask);
        Rig on = Kit.Open(Puddle, s => s.Prompts = true);
        on.Jump(0, 1);
        Assert.Equal("What do you love to wear?", on.Engine.State.Ask);
    }

    [Fact]
    public void Changing_a_setting_keeps_the_reader_on_the_same_page_and_stop()
    {
        Rig rig = Kit.Open(Puddle);
        rig.Jump(2, 1);
        rig.Engine.Settings.WholePageFirst = false;
        rig.Engine.Refresh();
        Assert.Equal(new Step(2, 1), rig.Engine.CurrentStep);
    }

    // ------------------------------------------------------------------ reduced motion

    [Fact]
    public void Reduced_motion_swaps_the_flight_for_a_dip_to_dark()
    {
        Rig rig = Kit.Open(Puddle, s => s.ReduceMotion = "on");
        ReaderEngine e = rig.Engine;
        RectD from = e.Shown.Cam;
        rig.Next();
        RectD to = e.Target().Cam;
        rig.Run(100);
        Assert.Equal(from, e.Shown.Cam);                         // no travelling: still the old picture
        Assert.InRange(e.Shown.Fade, 0.4, 0.8);                  // ...going dark
        rig.Run(120);
        Assert.Equal(to, e.Shown.Cam);                           // switched at the darkest point
        Assert.True(e.Shown.Fade > 0);
        rig.Run(140);
        Assert.False(e.Animating);
        Assert.Equal(0, e.Shown.Fade);
    }

    [Fact]
    public void Auto_follows_the_device_setting()
    {
        var e = new ReaderEngine(new ReaderSettings { ReduceMotion = "auto" });
        Assert.False(e.Reduced);
        e.DeviceReducedMotion = true;
        Assert.True(e.Reduced);
        e.Settings.ReduceMotion = "off";
        Assert.False(e.Reduced);
    }

    [Fact]
    public void The_picture_eases_up_to_make_room_for_words()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        e.SetInsets(64, 104);
        e.InsetGoal.Bottom = 180;
        rig.Run(60);
        Assert.InRange(e.Insets.Bottom, 105, 179);
        rig.Run(1000);
        Assert.Equal(180, e.Insets.Bottom);
    }
}

/// <summary>When the picture is still, the engine says so, and a renderer can stop redrawing.</summary>
public class RestlessTests
{
    [Fact]
    public void A_still_page_is_at_rest_and_a_move_is_not()
    {
        Rig rig = Kit.Open("captain-megawatt");
        rig.Run(500);
        Assert.False(rig.Engine.Restless);
        rig.Next();
        rig.Run(100);
        Assert.True(rig.Engine.Restless);
        rig.RunUntilStill();
        rig.Run(500);
        Assert.False(rig.Engine.Restless);
    }

    [Fact]
    public void Weather_bobbing_layers_pans_and_touch_ripples_keep_it_restless()
    {
        Rig rig = Kit.Open("biggest-puddle");
        rig.Jump(6, 0);                                  // rain
        rig.Run(1000);
        Assert.True(rig.Engine.Restless);
        rig.Jump(7, 0);                                  // sparkles and a bobbing butterfly
        rig.Run(3000);
        Assert.True(rig.Engine.Restless);

        rig.Jump(0, 1);                                  // a pan stop: moving for three and a half seconds, then still
        rig.Run(1000);
        Assert.True(rig.Engine.Restless);
        rig.Run(4000);
        Assert.False(rig.Engine.Restless);

        rig.Engine.Ripple(100, 100);
        rig.Run(16);
        Assert.True(rig.Engine.Restless);
        rig.Run(600);
        Assert.False(rig.Engine.Restless);
    }

    [Fact]
    public void Reduced_motion_lets_the_rain_page_rest()
    {
        Rig rig = Kit.Open("biggest-puddle", s => s.ReduceMotion = "on");
        rig.Jump(6, 0);
        rig.Run(1000);
        Assert.False(rig.Engine.Restless);
    }

    [Fact]
    public void A_scene_with_a_story_beacon_or_a_hint_keeps_pulsing_and_one_with_everything_found_rests()
    {
        Rig rig = Kit.Open("biggest-puddle", s => s.Extras = "off");
        rig.Jump(11, Step.Exploring);
        rig.Run(1000);
        Assert.True(rig.Engine.Restless);                // the first duckling's beacon
        rig.Engine.React(0); rig.Engine.React(1); rig.Engine.React(2);
        rig.Run(20_000);                                 // party over, words cleared
        Assert.False(rig.Engine.Restless);
    }
}
