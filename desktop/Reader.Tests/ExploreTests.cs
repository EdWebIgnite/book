using Reader.Core;

namespace Reader.Tests;

/// <summary>Explore pages: the child's camera, things to find, the story hidden in a scene, hints and the map.</summary>
public class ExploreTests
{
    const string Puddle = "biggest-puddle";
    const int Garden = 11;           // page 12: the whole garden, with Dot's three ducklings to find

    static Rig OpenGarden(Action<ReaderSettings>? tweak = null)
    {
        Rig rig = Kit.Open(Puddle, tweak);
        rig.Jump(Garden, Step.Exploring);
        return rig;
    }

    /// <summary>Tap the middle of a thing to find, as a finger would.</summary>
    static void Tap(Rig rig, int hotspot)
    {
        ReaderEngine e = rig.Engine;
        RectD box = e.BoxRect(e.CurrentStep.Page, e.CurrentPage.Hotspots[hotspot].Box);
        if (!Geo.Inflate(e.VisibleCam(), -0.02).Contains(box.CenterX, box.CenterY))
        {
            // Bring it on screen first, the way a child would drag to it.
            RectD cam = e.TakeCamera();
            e.SetCam(new RectD(box.CenterX - cam.W / 2, box.CenterY - cam.H / 2, cam.W, cam.H));
        }
        RectD r = e.GetView().ToScreen(box);
        rig.Run(400);
        e.TapAt(r.CenterX, r.CenterY);
        rig.Run(16);
    }

    // ------------------------------------------------------------------ the story

    [Fact]
    public void The_last_page_has_a_three_step_story_and_ten_extras()
    {
        Rig rig = OpenGarden();
        ReaderState st = rig.Engine.State;
        Assert.True(st.Exploring);
        Assert.Equal(13, st.HotspotCount);
        Assert.Equal(3, st.StoryTotal);
        Assert.Equal(10, st.ExtraTotal);
        Assert.Equal(new[] { 0, 1, 2 }, rig.Engine.StoryOrder());
        Assert.Equal(0, rig.Engine.NextStory());
    }

    [Fact]
    public void A_later_story_step_tapped_too_early_says_not_yet_and_names_the_one_to_find()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        Tap(rig, 1);                                             // the second duckling, before the first
        Assert.Equal("Not yet! First find the duckling at the watering can.", e.State.Caption);
        Assert.Equal(0, e.State.StoryFound);
        Assert.False(e.IsFound(1));
        Assert.Equal(1, rig.Sound.Count("locked"));
        Assert.Equal(0, rig.Sound.Count("find"));
        BurstDraw ring = Assert.Single(e.Bursts());
        Assert.True(ring.Locked);

        Tap(rig, 2);                                             // the third is locked too, and the first is still the one named
        Assert.Equal("Not yet! First find the duckling at the watering can.", e.State.Caption);
    }

    [Fact]
    public void Story_steps_open_in_order_and_the_last_one_starts_the_party()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        Tap(rig, 0);
        Assert.True(e.IsFound(0));
        Assert.StartsWith("One!", e.State.Caption);
        Assert.Equal(1, e.NextStory());
        Assert.Null(e.GetParty());

        Tap(rig, 1);
        Tap(rig, 2);
        Assert.Equal(3, e.State.StoryFound);
        Assert.Equal(-1, e.NextStory());
        Assert.Equal(2, rig.Sound.Count("find"));
        Assert.Equal(1, rig.Sound.Count("party"));
        PartyDraw party = Assert.IsType<PartyDraw>(e.GetParty());
        Assert.Equal(70, party.Stars.Length);
        rig.Run(4300);
        Assert.Null(e.GetParty());                               // stars rain for about four seconds

        Tap(rig, 2);                                             // tapping it again is fine, and is no second party
        Assert.Equal(1, rig.Sound.Count("party"));
    }

    [Fact]
    public void A_story_step_with_no_name_is_called_the_next_part_of_the_story()
    {
        var page = new Page { Image = "p.jpg", Explore = true };
        page.Hotspots.Add(new Hotspot { Box = new RectD(0.2, 0.2, 0.1, 0.1), Story = 1, Caption = "First" });
        page.Hotspots.Add(new Hotspot { Box = new RectD(0.7, 0.7, 0.1, 0.1), Story = 2, Caption = "Second" });
        Rig rig = Kit.Open(new Book { Id = "b", Title = "B", Pages = { page } });
        rig.Engine.React(1);
        Assert.Equal("Not yet! First find the next part of the story.", rig.Engine.State.Caption);
    }

    [Fact]
    public void A_found_things_words_show_for_a_while_then_clear_themselves()
    {
        Rig rig = OpenGarden(s => s.Extras = "on");
        ReaderEngine e = rig.Engine;
        int changes = 0;
        e.Changed += _ => changes++;
        Tap(rig, 4);
        Assert.Equal("A ladybird! Can you count her spots?", e.State.Caption);
        rig.Run(4300);
        Assert.NotEqual("", e.State.Caption);                    // at least four and a half seconds
        int before = changes;
        rig.Run(3000);                                           // longer words stay longer, but not this long
        Assert.Equal("", e.State.Caption);
        Assert.True(changes > before, "the screen should be told the words have gone");
    }

    // ------------------------------------------------------------------ extras

    [Fact]
    public void Extras_wait_until_the_story_is_finished_by_default()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        Assert.Equal("afterStory", e.Settings.Extras);
        Assert.False(e.ExtrasOpen());
        Assert.Equal(new[] { 0 }, e.UnfoundHotspots());          // hints lead only to the story

        Tap(rig, 12);                                            // the dog: an extra, not open yet
        Assert.False(e.IsFound(12));
        Assert.Equal(1, rig.Sound.Count("tap"));                 // answered like any touch on empty space

        e.React(0); e.React(1); e.React(2);
        Assert.True(e.ExtrasOpen());
        Assert.Equal(Enumerable.Range(3, 10), e.UnfoundHotspots());
        Tap(rig, 12);
        Assert.True(e.IsFound(12));
        Assert.Equal(1, e.State.ExtraFound);
    }

    [Fact]
    public void Extras_can_be_open_all_the_time_or_switched_off()
    {
        Rig always = OpenGarden(s => s.Extras = "on");
        Assert.True(always.Engine.ExtrasOpen());
        Assert.Equal(11, always.Engine.UnfoundHotspots().Count); // the next story step first, then the ten extras
        Assert.Equal(0, always.Engine.UnfoundHotspots()[0]);
        Tap(always, 12);
        Assert.True(always.Engine.IsFound(12));

        Rig never = OpenGarden(s => s.Extras = "off");
        never.Engine.React(0); never.Engine.React(1); never.Engine.React(2);
        Assert.False(never.Engine.ExtrasOpen());
        Assert.Equal(0, never.Engine.State.ExtraTotal);
        Assert.False(never.Engine.State.CanShowMe);
        Tap(never, 12);
        Assert.False(never.Engine.IsFound(12));
    }

    [Fact]
    public void A_page_with_no_story_has_its_extras_open_from_the_start()
    {
        Rig rig = Kit.Open("waterhole");
        rig.Jump(7, Step.Exploring);
        Assert.True(rig.Engine.ExtrasOpen());
        Assert.Equal(10, rig.Engine.UnfoundHotspots().Count);
        Assert.Equal(10, rig.Engine.State.ExtraTotal);
    }

    // ------------------------------------------------------------------ touch targets

    /// <summary>One explore page with a tiny thing to find in the middle, shown whole on a 1000 x 700 screen.</summary>
    static Rig Tiny(double minTargetCm, params Hotspot[] more)
    {
        var page = new Page { Image = "p.jpg", Explore = true };
        page.Hotspots.Add(new Hotspot { Box = new RectD(0.495, 0.495, 0.01, 0.01), Caption = "A tiny beetle" });
        page.Hotspots.AddRange(more);
        Rig rig = Kit.Open(new Book { Id = "b", Title = "B", Pages = { page } }, s => { s.MinTargetCm = minTargetCm; s.Extras = "on"; });
        ZoomRightOut(rig);
        return rig;
    }

    static void ZoomRightOut(Rig rig)
    {
        rig.Engine.ToggleOverview();
        rig.RunUntilStill();
    }

    static PointD Centre(Rig rig, int hotspot)
    {
        ReaderEngine e = rig.Engine;
        RectD r = e.GetView().ToScreen(e.BoxRect(0, e.CurrentPage.Hotspots[hotspot].Box));
        return new PointD(r.CenterX, r.CenterY);
    }

    [Fact]
    public void Small_things_get_a_touch_area_of_at_least_the_set_size()
    {
        Rig rig = Tiny(2.4);
        ReaderEngine e = rig.Engine;
        PointD c = Centre(rig, 0);
        RectD onScreen = e.GetView().ToScreen(e.BoxRect(0, e.CurrentPage.Hotspots[0].Box));
        Assert.True(onScreen.W < 12 && onScreen.H < 12, "the beetle itself is only a few pixels across");

        double half = 2.4 * ReaderEngine.DefaultUnitsPerCm / 2;  // 2.4 cm across, so about 45 pixels either side
        Assert.Equal(45.36, half, 2);
        Assert.Equal(0, e.HotspotAt(c.X, c.Y));
        Assert.Equal(0, e.HotspotAt(c.X + half - 1, c.Y));
        Assert.Equal(0, e.HotspotAt(c.X, c.Y - half + 1));
        Assert.Equal(0, e.HotspotAt(c.X - half + 1, c.Y + half - 1));
        Assert.Equal(-1, e.HotspotAt(c.X + half + 1, c.Y));
        Assert.Equal(-1, e.HotspotAt(c.X, c.Y + half + 1));
    }

    [Fact]
    public void The_touch_area_follows_the_setting_and_the_screens_pixels_per_centimetre()
    {
        Rig small = Tiny(1.4);                                   // the 9+ preset
        PointD c = Centre(small, 0);
        Assert.Equal(0, small.Engine.HotspotAt(c.X + 25, c.Y));
        Assert.Equal(-1, small.Engine.HotspotAt(c.X + 28, c.Y));

        Rig dense = Tiny(2.4);
        dense.Engine.UnitsPerCm = 60;                            // a screen that packs more units into a centimetre
        Assert.Equal(0, dense.Engine.HotspotAt(c.X + 70, c.Y));
        Assert.Equal(-1, dense.Engine.HotspotAt(c.X + 73, c.Y));
    }

    [Fact]
    public void A_big_thing_is_only_hit_inside_its_own_box()
    {
        Rig rig = Tiny(2.4, new Hotspot { Box = new RectD(0.1, 0.1, 0.2, 0.3), Caption = "A big tree" });
        ReaderEngine e = rig.Engine;
        RectD tree = e.GetView().ToScreen(e.BoxRect(0, e.CurrentPage.Hotspots[1].Box));
        Assert.True(tree.W > 100);
        Assert.Equal(1, e.HotspotAt(tree.X + 2, tree.Y + 2));
        Assert.Equal(-1, e.HotspotAt(tree.X - 3, tree.Y + 2));
    }

    [Fact]
    public void Under_the_finger_the_smallest_box_wins_and_nearby_the_nearest_one_does()
    {
        Rig rig = Tiny(2.4,
            new Hotspot { Box = new RectD(0.4, 0.4, 0.2, 0.2), Caption = "The bush the beetle sits on" },
            new Hotspot { Box = new RectD(0.53, 0.495, 0.01, 0.01), Caption = "Another beetle, close by" });
        ReaderEngine e = rig.Engine;
        PointD beetle = Centre(rig, 0), other = Centre(rig, 2);
        Assert.Equal(0, e.HotspotAt(beetle.X, beetle.Y));        // inside both the beetle and the bush: the beetle
        Assert.Equal(1, e.HotspotAt(beetle.X - 30, beetle.Y));   // inside the bush itself: the bush, even near the beetle

        // Away from the bush, between two small things, the nearer one is chosen.
        var page = new Page { Image = "p.jpg", Explore = true };
        page.Hotspots.Add(new Hotspot { Box = new RectD(0.495, 0.495, 0.01, 0.01) });
        page.Hotspots.Add(new Hotspot { Box = new RectD(0.535, 0.495, 0.01, 0.01) });
        Rig pair = Kit.Open(new Book { Id = "b", Title = "B", Pages = { page } }, s => s.Extras = "on");
        ZoomRightOut(pair);
        PointD a = Centre(pair, 0), b = Centre(pair, 1);
        double gap = b.X - a.X;
        Assert.InRange(gap, 10, 80);
        Assert.Equal(0, pair.Engine.HotspotAt(a.X + gap * 0.4, a.Y));
        Assert.Equal(1, pair.Engine.HotspotAt(a.X + gap * 0.6, a.Y));
    }

    [Fact]
    public void The_story_step_being_looked_for_beats_a_nearer_extra()
    {
        var page = new Page { Image = "p.jpg", Explore = true };
        page.Hotspots.Add(new Hotspot { Box = new RectD(0.495, 0.495, 0.01, 0.01), Story = 1, Name = "the key" });
        page.Hotspots.Add(new Hotspot { Box = new RectD(0.525, 0.495, 0.01, 0.01), Caption = "A pebble" });
        Rig rig = Kit.Open(new Book { Id = "b", Title = "B", Pages = { page } }, s => s.Extras = "on");
        ZoomRightOut(rig);
        PointD key = Centre(rig, 0), pebble = Centre(rig, 1);
        double nearPebble = key.X + (pebble.X - key.X) * 0.7;
        Assert.Equal(0, rig.Engine.HotspotAt(nearPebble, key.Y)); // nearer the pebble, but within reach of the key
        rig.Engine.React(0);
        Assert.Equal(1, rig.Engine.HotspotAt(nearPebble, key.Y)); // with the key found, the pebble gets it
    }

    // ------------------------------------------------------------------ the child's camera

    [Fact]
    public void Exploring_carries_on_from_where_the_intro_ended()
    {
        Rig rig = Kit.Open(Puddle);
        rig.Jump(Garden, 0);
        RectD intro = rig.Engine.Shown.Cam;
        rig.Next();
        rig.RunUntilStill();
        Assert.True(rig.Engine.Exploring);
        Assert.Equal(intro, rig.Engine.ExploreStart());
        Assert.Equal(0, rig.Engine.Shown.Vig);
    }

    [Fact]
    public void A_wide_scene_with_no_intro_starts_at_its_left_end()
    {
        var page = new Page { Image = "p.jpg", Explore = true };
        var book = new Book { Id = "b", Title = "B", Pages = { page } };
        var e = new ReaderEngine(new ReaderSettings());
        e.SetViewSize(1000, 700);
        e.SetBook(book, new[] { 4.0 });
        Assert.Equal(new RectD(0, 0, 1.4, 1), e.ExploreStart());
    }

    [Fact]
    public void The_camera_stays_on_the_page_and_within_the_zoom_limits()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        RectD page = e.PageRects[Garden];

        RectD farLeft = e.ClampCam(new RectD(page.X - 5, 0.2, 0.8, 0.4));
        Assert.Equal(page.X - 0.03, farLeft.X, 9);
        RectD farRight = e.ClampCam(new RectD(page.Right + 5, 0.2, 0.8, 0.4));
        Assert.Equal(page.Right + 0.03, farRight.Right, 9);

        RectD tooClose = e.ClampCam(new RectD(page.X + 1, 0.5, 0.02, 0.01));
        Assert.Equal(ReaderEngine.ClosestZoom, tooClose.H, 9);

        RectD tooFar = e.ClampCam(new RectD(page.X - 10, -10, 40, 20));
        Assert.Equal(e.MaxCam().W, tooFar.W, 9);
        Assert.Equal(page.CenterX, tooFar.CenterX, 9);           // bigger than the page: centred on it
    }

    [Fact]
    public void The_zoom_buttons_step_in_and_out_and_grey_out_at_the_limits()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        double before = e.VisibleCam().W;
        e.ZoomStep(1.7);
        rig.RunUntilStill();
        Assert.Equal(before / 1.7, e.VisibleCam().W, 6);
        Assert.Equal(1, rig.Sound.Count("zoom in"));

        for (int i = 0; i < 8; i++) { e.ZoomStep(1.7); rig.RunUntilStill(); }
        Assert.False(e.State.CanZoomIn);
        Assert.True(e.State.CanZoomOut);
        for (int i = 0; i < 12; i++) { e.ZoomStep(1 / 1.7); rig.RunUntilStill(); }
        Assert.False(e.State.CanZoomOut);
        Assert.True(e.State.CanZoomIn);
        Assert.Equal(e.MaxCam().W, e.VisibleCam().W, 6);
    }

    [Fact]
    public void Zooming_at_a_point_keeps_that_point_under_the_finger()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        RectD cam = e.TakeCamera();
        PointD before = e.ToWorld(cam, 700, 300);
        RectD zoomed = e.ZoomAt(cam, 2, 700, 300);
        PointD after = e.ToWorld(zoomed, 700, 300);
        Assert.Equal(before.X, after.X, 9);
        Assert.Equal(before.Y, after.Y, 9);
        Assert.Equal(cam.W / 2, zoomed.W, 9);
    }

    [Fact]
    public void Whole_page_zooms_right_out_and_pressing_it_again_goes_back_to_where_exploring_started()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        e.ToggleOverview();
        rig.RunUntilStill();
        Assert.Equal(e.MaxCam().W, e.VisibleCam().W, 6);
        e.ToggleOverview();
        rig.RunUntilStill();
        Assert.Equal(e.ClampCam(Geo.FitToAspect(e.ExploreStart(), 1000.0 / (700 - 64 - 88))).W, e.VisibleCam().W, 6);
    }

    [Fact]
    public void A_drag_past_the_edge_gives_a_little_then_eases_back()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        RectD page = e.PageRects[Garden];
        RectD cam = e.TakeCamera();
        e.SetCam(cam with { X = page.X - 1 }, soft: true);
        Assert.True(e.Shown.Cam.X < page.X - 0.03, "the picture should give a little past its edge");
        Assert.True(e.Shown.Cam.X > page.X - 0.03 - cam.W * 0.13, "but only a little");
        Assert.True(e.Settle());
        rig.RunUntilStill();
        Assert.Equal(page.X - 0.03, e.Shown.Cam.X, 6);
        Assert.False(e.Settle());
    }

    [Fact]
    public void A_flick_glides_on_and_slows_to_a_stop_the_same_way_at_any_frame_rate()
    {
        RectD glide(double frameMs, double glideSetting = 0.5)
        {
            Rig rig = OpenGarden(s => s.Glide = glideSetting);
            ReaderEngine e = rig.Engine;
            for (int i = 0; i < 4; i++) { e.ZoomStep(1.7); rig.RunUntilStill(); }
            RectD cam = e.TakeCamera();
            e.SetCam(cam with { X = e.PageRects[Garden].X + 0.2 });
            e.StartFling(0.0015, 0);
            Assert.True(e.Animating);
            rig.Run(6000, frameMs);
            Assert.False(e.Animating);
            return e.Shown.Cam;
        }
        Rig still = OpenGarden();
        double start = still.Engine.PageRects[Garden].X + 0.2;
        RectD smooth = glide(4), choppy = glide(48);
        Assert.True(smooth.X > start + 0.1, "it should have glided on");
        Assert.Equal(smooth.X, choppy.X, 3);
        Assert.True(glide(16, 1).X > glide(16, 0).X, "more glide goes further");
    }

    [Fact]
    public void A_flick_into_the_edge_stops_there()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        for (int i = 0; i < 3; i++) { e.ZoomStep(1.7); rig.RunUntilStill(); }
        e.TakeCamera();
        e.StartFling(-0.02, 0);
        rig.Run(4000);
        Assert.False(e.Animating);
        Assert.Equal(e.PageRects[Garden].X - 0.03, e.Shown.Cam.X, 6);
    }

    // ------------------------------------------------------------------ hints, Show me and the map

    [Fact]
    public void After_an_idle_spell_a_hint_points_at_the_next_thing_to_find()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        e.TakeCamera();                                          // a touch
        rig.Run(3900);
        HintDraw? early = e.GetHint(e.GetView());
        Assert.True(early is null || early.Kind == HintKind.None);
        rig.Run(200);                                            // four seconds idle
        HintDraw hint = Assert.IsType<HintDraw>(e.GetHint(e.GetView()));
        Assert.NotEqual(HintKind.None, hint.Kind);

        // The first duckling is off to the left of where exploring starts: an arrow at the left edge shows the way.
        Assert.Equal(HintKind.Arrow, hint.Kind);
        Assert.Equal(46, hint.ArrowX);
        Assert.InRange(Math.Abs(hint.ArrowAngle), Math.PI * 0.75, Math.PI);

        e.ShowMe();                                              // fly there
        rig.RunUntilStill();
        Assert.True(e.IsFound(0));                               // ...and it reacts as if tapped
        e.ToggleOverview();                                      // right out, so the next duckling is on screen
        rig.RunUntilStill();
        HintDraw next = Assert.IsType<HintDraw>(e.GetHint(e.GetView()));
        Assert.True(next.Beacon);                                // the next story step always carries its beacon
    }

    [Fact]
    public void A_thing_on_screen_gets_a_glow_and_hints_can_be_switched_off_for_a_page()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        e.ToggleOverview();                                      // right out: everything is on screen
        rig.RunUntilStill();
        rig.Run(4100);
        HintDraw hint = Assert.IsType<HintDraw>(e.GetHint(e.GetView()));
        Assert.Equal(HintKind.Glow, hint.Kind);
        Assert.True(hint.Beacon);
        Assert.Equal(hint.BeaconBox, hint.GlowBox);

        e.CurrentPage.Guidance.Hints = false;
        HintDraw quiet = Assert.IsType<HintDraw>(e.GetHint(e.GetView()));
        Assert.Equal(HintKind.None, quiet.Kind);                 // the story beacon stays
        Assert.True(quiet.Beacon);
    }

    [Fact]
    public void Show_me_goes_to_the_next_story_step_first_and_goes_away_when_everything_is_found()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        Assert.True(e.State.CanShowMe);
        for (int i = 0; i < 13; i++)
        {
            Assert.True(e.State.CanShowMe);
            e.ShowMe();
            rig.RunUntilStill();
            rig.Run(100);
        }
        Assert.Equal(13, e.State.Found);
        Assert.False(e.State.CanShowMe);
        Assert.Equal(1, rig.Sound.Count("party"));
        Assert.Equal(12, rig.Sound.Count("find"));
    }

    [Fact]
    public void The_corner_map_shows_where_the_child_is_and_touching_it_moves_the_view()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        MapDraw map = Assert.IsType<MapDraw>(e.GetMap());
        Assert.Equal(210, map.Rect.W, 6);
        Assert.Equal(1000 - 210 - 12, map.Rect.X, 6);
        Assert.Equal(64 + 6, map.Rect.Y, 6);
        Assert.True(map.HasDot);                                 // the next story step blinks
        Assert.True(map.ViewRect.W < map.Rect.W);

        RectD page = e.PageRects[Garden];
        e.MapGoTo(map.Rect.X + map.Rect.W * 0.25, map.Rect.CenterY);
        Assert.Equal(page.X + page.W * 0.25, e.Shown.Cam.CenterX, 6);

        // Pages without a map have none, and neither does a guided page.
        Rig waterhole = Kit.Open("waterhole");
        waterhole.Jump(7, Step.Exploring);
        Assert.Null(waterhole.Engine.GetMap());
        rig.Jump(0, 0);
        Assert.Null(e.GetMap());
    }

    // ------------------------------------------------------------------ gestures

    [Fact]
    public void On_a_guided_page_a_tap_on_the_left_goes_back_and_anywhere_else_goes_on()
    {
        Rig rig = Kit.Open(Puddle);
        var touch = new GestureInput(rig.Engine);
        touch.PointerDown(1, 700, 300, rig.Now);
        touch.PointerUp(1, 702, 301, rig.Now + 80);
        Assert.Equal(1, rig.Engine.StepIndex);
        Assert.Single(rig.Engine.Ripples());

        rig.RunUntilStill();
        rig.Run(400);
        touch.PointerDown(1, 200, 300, rig.Now);                 // the left third
        touch.PointerUp(1, 200, 300, rig.Now + 80);
        Assert.Equal(0, rig.Engine.StepIndex);
    }

    [Fact]
    public void A_swipe_left_goes_on_and_a_swipe_right_goes_back()
    {
        Rig rig = Kit.Open(Puddle);
        var touch = new GestureInput(rig.Engine);
        touch.PointerDown(1, 600, 300, rig.Now);
        touch.PointerMove(1, 500, 305, rig.Now + 50);
        touch.PointerUp(1, 450, 310, rig.Now + 100);
        Assert.Equal(1, rig.Engine.StepIndex);
        rig.RunUntilStill();
        rig.Run(400);
        touch.PointerDown(1, 300, 300, rig.Now);
        touch.PointerUp(1, 420, 300, rig.Now + 100);
        Assert.Equal(0, rig.Engine.StepIndex);
    }

    [Fact]
    public void Pinching_in_on_a_guided_page_shows_the_whole_page()
    {
        Rig rig = Kit.Open(Puddle);
        rig.Jump(0, 0);
        var touch = new GestureInput(rig.Engine);
        touch.PointerDown(1, 300, 300, rig.Now);
        touch.PointerDown(2, 700, 300, rig.Now);
        touch.PointerMove(1, 450, 300, rig.Now + 100);
        touch.PointerMove(2, 550, 300, rig.Now + 100);
        touch.PointerUp(1, 450, 300, rig.Now + 150);
        touch.PointerUp(2, 550, 300, rig.Now + 150);
        Assert.True(rig.Engine.Overview);
    }

    [Fact]
    public void Dragging_an_explore_page_moves_it_with_the_finger_and_a_quick_flick_glides()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        e.ZoomStep(1.7);
        rig.RunUntilStill();
        e.SetCam(e.TakeCamera() with { X = e.PageRects[Garden].X + 1 });
        RectD before = e.Shown.Cam;
        var touch = new GestureInput(e);
        double t = rig.Now;
        touch.PointerDown(1, 600, 300, t);
        for (int i = 1; i <= 10; i++) touch.PointerMove(1, 600 - i * 20, 300, t + i * 10);
        double scale = e.ScreenScale(before);
        Assert.Equal(before.X + 200 / scale, e.Shown.Cam.X, 6); // 200 pixels to the left shows 200 pixels more of the right
        touch.PointerUp(1, 400, 300, t + 105);
        Assert.True(e.Animating, "a fast drag should carry on as a glide");
        rig.Run(5000);
        Assert.True(e.Shown.Cam.X > before.X + 200 / scale + 0.05);
    }

    [Fact]
    public void A_second_tap_in_the_same_spot_only_zooms_when_double_tap_is_switched_on()
    {
        foreach (bool on in new[] { false, true })
        {
            Rig rig = OpenGarden(s => s.DoubleTapZoom = on);
            ReaderEngine e = rig.Engine;
            double width = e.VisibleCam().W;
            var touch = new GestureInput(e);
            touch.PointerDown(1, 500, 300, rig.Now); touch.PointerUp(1, 500, 300, rig.Now + 40);
            touch.PointerDown(1, 502, 301, rig.Now + 150); touch.PointerUp(1, 502, 301, rig.Now + 190);
            rig.Run(200);
            rig.RunUntilStill();
            if (on) Assert.Equal(width / 2.2, e.VisibleCam().W, 6);
            else Assert.Equal(width, e.VisibleCam().W, 9);
            Assert.Equal(1, rig.Sound.Count("tap"));             // only the first tap counts as a tap
        }
    }

    [Fact]
    public void The_wheel_steps_through_a_guided_page_and_zooms_an_explore_page_at_the_pointer()
    {
        Rig rig = Kit.Open(Puddle);
        var input = new GestureInput(rig.Engine);
        input.Wheel(500, 300, 0, 100, zoomKey: false, smooth: false, rig.Now);
        Assert.Equal(1, rig.Engine.StepIndex);
        input.Wheel(500, 300, 0, 100, zoomKey: false, smooth: false, rig.Now + 100);   // too soon after the last one
        Assert.Equal(1, rig.Engine.StepIndex);

        Rig garden = OpenGarden();
        ReaderEngine e = garden.Engine;
        var wheel = new GestureInput(e);
        RectD cam = e.VisibleCam();
        PointD under = e.ToWorld(cam, 300, 400);
        wheel.Wheel(300, 400, 0, -100, zoomKey: false, smooth: false, garden.Now);     // away from you: closer
        Assert.Equal(cam.W / Math.Exp(0.15), e.Shown.Cam.W, 6);
        PointD still = e.ToWorld(e.Shown.Cam, 300, 400);
        Assert.Equal(under.X, still.X, 6);
    }

    [Fact]
    public void Touching_the_corner_map_moves_the_view_instead_of_dragging_the_page()
    {
        Rig rig = OpenGarden();
        ReaderEngine e = rig.Engine;
        MapDraw map = e.GetMap()!;
        var touch = new GestureInput(e);
        touch.PointerDown(1, map.Rect.X + map.Rect.W * 0.8, map.Rect.CenterY, rig.Now);
        RectD page = e.PageRects[Garden];
        Assert.Equal(page.X + page.W * 0.8, e.Shown.Cam.CenterX, 6);
        touch.PointerMove(1, map.Rect.X + map.Rect.W * 0.5, map.Rect.CenterY, rig.Now + 50);
        Assert.Equal(page.X + page.W * 0.5, e.Shown.Cam.CenterX, 6);
        touch.PointerUp(1, map.Rect.X + map.Rect.W * 0.5, map.Rect.CenterY, rig.Now + 90);
        Assert.Equal(0, rig.Sound.Count("tap"));
    }

    [Fact]
    public void Keys_do_what_the_buttons_do()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        e.HandleKey(ReaderKey.Next);
        Assert.Equal(1, e.StepIndex);
        rig.RunUntilStill(); rig.Run(400);
        e.HandleKey(ReaderKey.ToggleWholePage);
        Assert.True(e.Overview);
        rig.RunUntilStill(); rig.Run(400);
        e.HandleKey(ReaderKey.CloseUp);
        Assert.False(e.Overview);
        rig.RunUntilStill(); rig.Run(400);
        e.HandleKey(ReaderKey.Previous);
        Assert.Equal(0, e.StepIndex);
    }
}
