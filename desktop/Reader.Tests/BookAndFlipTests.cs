using System.Text.Json.Nodes;
using Reader.Core;

namespace Reader.Tests;

/// <summary>The book behaviour: the whole page again after its stops, and pages that turn over like a book's.</summary>
public class ClosingStepTests
{
    const string Puddle = "biggest-puddle";

    [Fact]
    public void The_whole_page_after_the_stops_is_marked_as_closing()
    {
        Rig rig = Kit.Open(Puddle);
        var page1 = rig.Engine.Steps.Where(s => s.Page == 0).ToList();
        Assert.Equal(new[] { -1, 0, 1, -1 }, page1.Select(s => s.Stop));
        Assert.Equal(new[] { false, false, false, true }, page1.Select(s => s.Closing));
        // The first ten pages of this book end that way. Page 11's last stop is the whole page already, so it
        // needs no closing step, and the explore page ends in exploring instead.
        for (int p = 0; p < 10; p++) Assert.True(rig.Engine.Steps.Last(s => s.Page == p).Closing, "page " + (p + 1));
        Assert.DoesNotContain(rig.Engine.Steps, s => s.Page >= 10 && s.Closing);
        Assert.Equal(new Step(10, 2), rig.Engine.Steps.Last(s => s.Page == 10));
    }

    [Fact]
    public void There_is_no_closing_step_when_the_last_stop_already_shows_the_whole_page()
    {
        Book full = Kit.OnePage(Kit.StopAt(0.1, 0.1), new Stop { Box = new RectD(0.01, 0.02, 0.98, 0.96) });
        Assert.DoesNotContain(Kit.Open(full).Engine.Steps, s => s.Closing);

        // Nearly the whole page is not enough, and neither is a stop that pans somewhere.
        Book nearly = Kit.OnePage(Kit.StopAt(0.1, 0.1), new Stop { Box = new RectD(0, 0, 0.95, 1) });
        Assert.Contains(Kit.Open(nearly).Engine.Steps, s => s.Closing);
        Book panning = Kit.OnePage(new Stop { Box = new RectD(0, 0, 1, 1), To = new RectD(0.2, 0.2, 0.5, 0.5) });
        Assert.Contains(Kit.Open(panning).Engine.Steps, s => s.Closing);

        // With the setting off there is none anywhere, and a page with no stops never had one.
        Assert.DoesNotContain(Kit.Open(nearly, s => s.WholePageLast = false).Engine.Steps, s => s.Closing);
        var empty = new Book { Id = "b", Title = "B", Pages = { new Page { Image = "a.jpg" } } };
        Assert.Equal(new[] { new Step(0, Step.WholePage) }, Kit.Open(empty).Engine.Steps);
    }

    [Fact]
    public void On_the_closing_step_the_butterfly_stays_where_it_flew_to()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        const int page = 7;
        Layer butterfly = e.Book!.Pages[page].Layers[0];
        rig.Jump(page, 1);
        rig.Run(6000);                                           // it has flown across
        Assert.Equal(1, e.LayerProgress(page, 0));
        rig.NextAndArrive();
        Assert.True(e.CurrentStep.Closing);
        rig.Run(6000);
        Assert.Equal(1, e.LayerProgress(page, 0));               // still there, not back at its start
        Assert.Equal((true, 1), e.LayerWanted(page, butterfly));
        Assert.Equal(butterfly.MoveTo!.Value.X, e.LayersFor(page)[0].Box.X, 9);

        // The whole page as it opens is before every stop, so there it has not moved yet.
        rig.Jump(page, Step.WholePage);
        Assert.False(e.CurrentStep.Closing);
        Assert.Equal((true, 0), e.LayerWanted(page, butterfly));
    }

    [Fact]
    public void On_the_closing_step_weather_that_started_during_the_page_keeps_going()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        const int splashPage = 9;
        PageEffect sparkles = e.Book!.Pages[splashPage].Effects[1];
        Assert.Equal(2, sparkles.From);
        Assert.True(rig.JumpToClosing(splashPage));
        Assert.Equal(1, e.EffectWanted(splashPage, sparkles));
        rig.Run(2000);
        Assert.Equal(1, e.EffectLevel(splashPage, 1));
    }

    [Fact]
    public void On_the_closing_step_things_that_ended_at_a_stop_are_over()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.1, 0.1), Kit.StopAt(0.4, 0.4), Kit.StopAt(0.6, 0.6));
        book.Pages[0].Effects.Add(new PageEffect { Type = "snow", To = 2 });
        book.Pages[0].Effects.Add(new PageEffect { Type = "leaves", From = 3 });
        book.Pages[0].Layers.Add(new Layer { Image = "a.png", Box = new RectD(0, 0, 0.1, 0.1), To = 2 });
        book.Pages[0].Layers.Add(new Layer { Image = "b.png", Box = new RectD(0, 0, 0.1, 0.1), From = 3 });
        Rig rig = Kit.Open(book);
        ReaderEngine e = rig.Engine;
        Assert.True(rig.JumpToClosing(0));
        Assert.Equal(0, e.EffectWanted(0, book.Pages[0].Effects[0]));
        Assert.Equal(1, e.EffectWanted(0, book.Pages[0].Effects[1]));
        Assert.False(e.LayerWanted(0, book.Pages[0].Layers[0]).Show);
        Assert.True(e.LayerWanted(0, book.Pages[0].Layers[1]).Show);
    }
}

public class SettingsVersionTests
{
    [Fact]
    public void Settings_saved_by_the_first_build_pick_up_the_book_behaviour()
    {
        // What the first desktop build wrote: no version, the old page change, no whole page at the end.
        var old = new JsonObject { ["moveMs"] = 700, ["pageTurn"] = "fly", ["wholePageLast"] = false, ["prompts"] = true, ["age"] = "6-8" };
        ReaderSettings s = ReaderSettings.FromJsonString(old.ToJsonString());
        Assert.True(s.WholePageLast);
        Assert.Equal("flip", s.PageTurn);
        Assert.True(s.BookLook);
        Assert.Equal(2, s.SettingsVersion);
        Assert.Equal(700, s.MoveMs);                             // everything else is kept
        Assert.True(s.Prompts);
        Assert.Equal("6-8", s.Age);
    }

    [Fact]
    public void Settings_saved_at_version_two_are_left_as_the_user_set_them()
    {
        var mine = new ReaderSettings { PageTurn = "zoomOut", WholePageLast = false, BookLook = false };
        ReaderSettings back = ReaderSettings.FromJsonString(mine.ToJsonString());
        Assert.Equal("zoomOut", back.PageTurn);
        Assert.False(back.WholePageLast);
        Assert.False(back.BookLook);
        Assert.Equal(2, back.SettingsVersion);
    }

    [Fact]
    public void The_change_happens_once_and_survives_being_saved_and_loaded_through_the_store()
    {
        string path = Path.Combine(Path.GetTempPath(), "reader-tests-" + Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, """{ "pageTurn": "cut", "wholePageLast": false, "glide": 0.9 }""");
            ReaderSettings first = SettingsStore.Load(path);
            Assert.Equal("flip", first.PageTurn);
            Assert.Equal(0.9, first.Glide);

            first.PageTurn = "cut";                              // the user changes it back on purpose
            SettingsStore.Save(first, path);
            ReaderSettings second = SettingsStore.Load(path);
            Assert.Equal("cut", second.PageTurn);                // and this time it stays
            Assert.True(second.WholePageLast);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void An_empty_file_gives_the_defaults_and_an_old_version_number_counts_as_old()
    {
        Assert.Equal(new ReaderSettings().ToJsonString(), ReaderSettings.FromJsonString("{}").ToJsonString());
        ReaderSettings s = ReaderSettings.FromJsonString("""{ "settingsVersion": 1, "bookLook": false }""");
        Assert.True(s.BookLook);
        Assert.Equal(2, s.SettingsVersion);
    }
}

public class FlipTests
{
    const string Puddle = "biggest-puddle";

    /// <summary>On the closing whole view of page 1, ready to turn to page 2.</summary>
    static Rig AtEndOfPage1(Action<ReaderSettings>? tweak = null)
    {
        Rig rig = Kit.Open(Puddle, tweak);
        Assert.True(rig.JumpToClosing(0));
        rig.Sound.Calls.Clear();
        return rig;
    }

    [Fact]
    public void Going_on_from_a_whole_page_to_the_next_whole_page_turns_the_page()
    {
        Rig rig = AtEndOfPage1();
        ReaderEngine e = rig.Engine;
        Assert.Null(e.GetFlip());
        rig.Next();
        Assert.Equal(new Step(1, Step.WholePage), e.CurrentStep);
        FlipDraw start = Assert.IsType<FlipDraw>(e.GetFlip());
        Assert.True(start.Forward);
        Assert.Equal(0, start.TurningPage);                      // page 1's leaf lifts...
        Assert.Equal(1, start.UnderPage);                        // ...and page 2 lies underneath
        Assert.Equal(0, start.Fold, 9);
        Assert.Equal(e.WholeCam(0), start.TurningCam);
        Assert.Equal(e.WholeCam(1), start.UnderCam);
        Assert.Equal(new[] { "page turn" }, rig.Sound.Calls);    // a swish, not the rush of air

        double last = 0;
        for (int i = 0; i < 50; i++)
        {
            rig.Run(16);
            if (e.GetFlip() is not FlipDraw f) break;
            Assert.True(f.Fold >= last, "the fold only ever travels one way");
            last = f.Fold;
            Assert.Equal(e.WholeCam(1), e.Shown.Cam);            // the camera holds still while the leaf turns
            Assert.True(e.Restless);
        }
        Assert.True(last > 0.9);
    }

    [Theory]
    [InlineData(1400, 900)]
    [InlineData(700, 450)]
    [InlineData(2100, 1350)]
    public void A_flip_takes_900_ms_at_the_standard_speed_and_scales_with_the_move_speed(double moveMs, double expected)
    {
        Rig rig = AtEndOfPage1(s => s.MoveMs = moveMs);
        rig.Next();
        double took = rig.RunUntilStill(frameMs: 4);
        Assert.InRange(took, expected, expected + 4);
        Assert.Equal(new Step(1, Step.WholePage), rig.Engine.CurrentStep);
        Assert.Null(rig.Engine.GetFlip());
    }

    [Fact]
    public void The_fold_starts_slowly_and_finishes_slowly()
    {
        Rig rig = AtEndOfPage1();
        rig.Next();
        rig.Run(90, 2);
        double early = rig.Engine.GetFlip()!.Value.Fold;
        rig.Run(360, 2);
        double half = rig.Engine.GetFlip()!.Value.Fold;
        rig.Run(360, 2);
        double late = rig.Engine.GetFlip()!.Value.Fold;
        Assert.Equal(0.5, half, 2);                              // half way at half time
        Assert.True(early < 0.04, "a tenth of the time in, it has barely lifted");
        Assert.True(late > 0.96, "and it settles gently");
    }

    [Fact]
    public void Going_back_plays_it_in_reverse_with_the_previous_pages_leaf_coming_back_over()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Jump(1, Step.WholePage);
        rig.Prev();
        Assert.Equal(new Step(0, Step.WholePage, Closing: true), e.CurrentStep);
        FlipDraw f = Assert.IsType<FlipDraw>(e.GetFlip());
        Assert.False(f.Forward);
        Assert.Equal(0, f.TurningPage);                          // page 1's leaf again, coming back from the spine
        Assert.Equal(1, f.UnderPage);
        Assert.Equal(1, f.Fold, 9);
        rig.Run(450, 2);
        Assert.Equal(0.5, e.GetFlip()!.Value.Fold, 2);
        rig.RunUntilStill();
        Assert.Null(e.GetFlip());
        Assert.Equal(e.WholeCam(0), e.Shown.Cam);
    }

    [Fact]
    public void From_a_close_up_the_camera_first_eases_out_to_the_whole_page_then_the_page_turns()
    {
        Rig rig = Kit.Open(Puddle, s => s.WholePageLast = false);
        ReaderEngine e = rig.Engine;
        rig.Jump(0, 1);
        rig.RunUntilStill();                                     // let the stop's own pan finish
        RectD closeUp = e.Shown.Cam;
        double easeOut = e.MoveTime(closeUp, e.WholeCam(0), 1400).Ms;
        rig.Sound.Calls.Clear();
        rig.Next();
        Assert.Equal(new Step(1, Step.WholePage), e.CurrentStep);
        Assert.Null(e.GetFlip());                                // not yet: pulling back first
        Assert.Equal(new[] { 0 }, e.BookPages());                // still looking at page 1
        rig.Run(easeOut - 50, 2);
        Assert.Null(e.GetFlip());
        Assert.DoesNotContain("page turn", rig.Sound.Calls);
        rig.Run(100, 2);
        Assert.NotNull(e.GetFlip());                             // now it turns
        Assert.Contains("page turn", rig.Sound.Calls);
        double rest = rig.RunUntilStill(frameMs: 2);
        Assert.InRange(rest, 900 - 52, 900 - 46);                // the two parts join without a pause
        Assert.Equal(e.WholeCam(1), e.Shown.Cam);
    }

    [Fact]
    public void A_last_stop_that_covers_the_page_turns_straight_away_with_no_pause_to_pull_back()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.1, 0.1), new Stop { Box = new RectD(0, 0, 1, 1) });
        var second = new Page { Image = "page2.jpg" };
        second.Stops.Add(Kit.StopAt(0.3, 0.3));
        book.Pages.Add(second);
        Rig rig = Kit.Open(book);
        ReaderEngine e = rig.Engine;
        rig.Jump(0, 1);
        Assert.DoesNotContain(e.Steps, s => s.Page == 0 && s.Closing);
        double strength = e.Shown.Vig;
        rig.Next();
        Assert.NotNull(e.GetFlip());                             // at once
        rig.Run(450, 2);
        Assert.InRange(e.Shown.Vig, strength * 0.6 + 0.01, strength - 0.01);   // the soft edges ease to the whole page's
        Assert.InRange(rig.RunUntilStill(frameMs: 2), 448, 452);
    }

    [Fact]
    public void When_the_new_page_opens_on_a_stop_the_page_turns_and_then_the_camera_goes_in()
    {
        Rig rig = Kit.Open(Puddle, s => s.WholePageFirst = false);
        ReaderEngine e = rig.Engine;
        Assert.True(rig.JumpToClosing(0));
        rig.Next();
        Assert.Equal(new Step(1, 0), e.CurrentStep);
        Assert.NotNull(e.GetFlip());
        rig.Run(950, 2);
        Assert.Null(e.GetFlip());
        Assert.True(e.Animating);                                // going in to the stop
        Assert.Equal(new[] { 1 }, e.BookPages());
        rig.RunUntilStill();
        Assert.Equal(Geo.Inflate(e.BoxRect(1, e.CurrentStop!.Box), 0.025), e.Shown.Cam);
        Assert.Equal(e.CurrentStop.Caption, e.State.Caption);
    }

    [Fact]
    public void A_page_that_opens_on_a_close_up_is_never_flipped_to_because_that_would_give_it_away()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        const int secretPage = 5;                                // page 6 hides a raindrop until its third stop
        Assert.False(e.Book!.Pages[secretPage].OpenWhole);
        Assert.True(rig.JumpToClosing(secretPage - 1));
        RectD before = e.Shown.Cam;
        RectD target = Geo.Inflate(e.BoxRect(secretPage, e.Book.Pages[secretPage].Stops[0].Box), 0.025);
        rig.Sound.Calls.Clear();
        rig.Next();
        Assert.Equal(new Step(secretPage, 0), e.CurrentStep);
        Assert.Null(e.GetFlip());
        Assert.DoesNotContain("page turn", rig.Sound.Calls);

        // A dip to dark instead. At no moment is the whole of the new page on screen.
        bool sawDark = false;
        for (int i = 0; i < 200 && e.Animating; i++)
        {
            rig.Run(4, 4);
            Assert.Null(e.GetFlip());
            RectD cam = e.Shown.Cam;
            Assert.True(cam == before || cam == target, "the camera is on the old view or on the new stop, never in between");
            Assert.Equal(cam == before ? new[] { secretPage - 1 } : new[] { secretPage }, e.BookPages());
            if (e.Shown.Fade > 0.9) sawDark = true;
        }
        Assert.True(sawDark);
        Assert.Equal(0, e.Shown.Fade);
        Assert.Equal(0.97, e.Shown.Spot, 9);                     // arrived: the spotlight on Moss
    }

    [Fact]
    public void Leaving_such_a_page_backwards_from_its_close_up_does_not_pull_back_to_show_it_whole()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Jump(5, 0);
        RectD closeUp = e.Shown.Cam;
        rig.Prev();
        Assert.Equal(4, e.CurrentStep.Page);
        Assert.Null(e.GetFlip());
        for (int i = 0; i < 200 && e.Animating; i++)
        {
            rig.Run(4, 4);
            Assert.Null(e.GetFlip());
            Assert.True(e.Shown.Cam == closeUp || e.Shown.Cam == e.WholeCam(4));
        }
    }

    [Fact]
    public void Once_such_a_page_has_been_read_to_the_end_it_turns_like_any_other()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        Assert.True(rig.JumpToClosing(5));                       // page 6, whole, after its last stop
        rig.Next();
        Assert.NotNull(e.GetFlip());                             // on to page 7
        rig.RunUntilStill();
        rig.Prev();
        FlipDraw back = Assert.IsType<FlipDraw>(e.GetFlip());    // and back to page 6's closing whole view
        Assert.Equal(5, back.TurningPage);
        Assert.True(e.CurrentStep.Closing);
    }

    [Fact]
    public void A_stop_that_cuts_still_cuts()
    {
        Rig rig = Kit.Open(Puddle);
        Assert.True(rig.JumpToClosing(8));
        rig.Next();                                              // "SPLASH!" has moveSeconds 0
        Assert.Equal(new Step(9, 0), rig.Engine.CurrentStep);
        Assert.False(rig.Engine.Animating);
        Assert.Null(rig.Engine.GetFlip());
    }

    [Fact]
    public void Reduced_motion_fades_instead_of_flipping()
    {
        Rig rig = AtEndOfPage1(s => s.ReduceMotion = "on");
        rig.Next();
        Assert.Null(rig.Engine.GetFlip());
        rig.Run(170);
        Assert.True(rig.Engine.Shown.Fade > 0.8);
        Assert.InRange(rig.RunUntilStill(), 150, 190);
        Assert.DoesNotContain("page turn", rig.Sound.Calls);
    }

    [Fact]
    public void Without_the_book_look_flip_behaves_like_flying_across()
    {
        Rig rig = AtEndOfPage1(s => s.BookLook = false);
        ReaderEngine e = rig.Engine;
        Assert.False(e.BookMode);
        rig.Next();
        Assert.Null(e.GetFlip());
        Assert.Equal(new[] { "whoosh" }, rig.Sound.Calls);
        rig.Run(300);
        Assert.True(e.Shown.Cam.X > e.WholeCam(0).X && e.Shown.Cam.X < e.WholeCam(1).X, "the camera is on its way across");
    }

    [Theory]
    [InlineData("fly")]
    [InlineData("zoomOut")]
    public void The_other_page_changes_still_fly_with_the_book_look_on_and_both_pages_are_drawn_on_the_way(string pageTurn)
    {
        Rig rig = AtEndOfPage1(s => s.PageTurn = pageTurn);
        ReaderEngine e = rig.Engine;
        Assert.True(e.BookMode);
        Assert.Equal(new[] { 0 }, e.BookPages());
        rig.Next();
        Assert.Null(e.GetFlip());
        rig.Run(300);
        Assert.Equal(new[] { 0, 1 }, e.BookPages());
        rig.RunUntilStill();
        Assert.Equal(new[] { 1 }, e.BookPages());                // at rest only the page being read
    }

    [Fact]
    public void Pages_of_different_shapes_fade_across_because_one_cannot_lie_on_the_other()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Jump(10, 2);                                         // page 11's last stop; page 12 is the wide garden
        rig.Sound.Calls.Clear();
        rig.Next();
        Assert.Equal(new Step(11, Step.WholePage), e.CurrentStep);
        Assert.Null(e.GetFlip());
        rig.Run(170);
        Assert.True(e.Shown.Fade > 0.8);
    }

    [Fact]
    public void Exploring_and_tiled_scenes_are_not_shown_as_a_book()
    {
        Rig rig = Kit.Open(Puddle);
        rig.Jump(11, 0);
        Assert.True(rig.Engine.BookMode);                        // the guided intro of the garden page still is
        rig.Jump(11, Step.Exploring);
        Assert.False(rig.Engine.BookMode);

        Rig sea = Kit.Open("deep-sea");
        Assert.False(sea.Engine.BookMode);                       // a tiled scene, even during its intro
        Assert.False(Kit.Open(Puddle, s => s.BookLook = false).Engine.BookMode);
    }

    [Fact]
    public void The_tap_guard_holds_during_a_flip_and_an_impatient_tap_finishes_it()
    {
        Rig rig = AtEndOfPage1();
        ReaderEngine e = rig.Engine;
        rig.Next();
        int step = e.StepIndex;
        rig.Run(200);
        e.Next();                                                // an accidental second tap: ignored
        Assert.NotNull(e.GetFlip());
        Assert.Equal(step, e.StepIndex);
        rig.Run(200);
        e.Next();                                                // a real, impatient one: the page lands
        Assert.Null(e.GetFlip());
        Assert.False(e.Animating);
        Assert.Equal(step, e.StepIndex);
        Assert.Equal(e.WholeCam(1), e.Shown.Cam);
        rig.Run(400);
        e.Next();                                                // and the next tap goes on
        Assert.Equal(step + 1, e.StepIndex);
    }

    [Fact]
    public void An_impatient_tap_skips_every_part_of_a_longer_journey_and_still_arrives_properly()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.1, 0.1));
        var second = new Page { Image = "page2.jpg" };
        second.Stops.Add(new Stop { Box = new RectD(0.5, 0.5, 0.3, 0.3), Sound = "hello.mp3", Caption = "Hello" });
        book.Pages.Add(second);
        Rig rig = Kit.Open(book, s => { s.WholePageFirst = false; s.WholePageLast = false; });
        ReaderEngine e = rig.Engine;
        rig.Next();                                              // pull back, flip, go in: three parts
        Assert.Equal(new Step(1, 0), e.CurrentStep);
        rig.Run(400);
        Assert.True(e.Animating);
        Assert.Null(rig.Sound.Clip);
        e.Next();
        Assert.False(e.Animating);
        Assert.Equal(e.Target().Cam, e.Shown.Cam);
        Assert.Equal("hello.mp3", rig.Sound.Clip);               // arriving at the stop plays its sound
        Assert.Equal("Hello", e.State.Caption);
    }

    [Fact]
    public void Read_again_turns_back_to_the_first_page()
    {
        Rig rig = Kit.Open("captain-megawatt");
        ReaderEngine e = rig.Engine;
        Assert.True(rig.JumpToClosing(2));
        rig.Next();
        Assert.True(e.Finished);
        e.Restart();
        FlipDraw f = Assert.IsType<FlipDraw>(e.GetFlip());
        Assert.False(f.Forward);
        Assert.Equal(0, f.TurningPage);
        rig.RunUntilStill();
        Assert.Equal(0, e.StepIndex);
    }
}

public class MoveTimeParityTests
{
    sealed record Case(bool ScaleTime, double Base, RectD From, RectD To, double Ms, bool Far);

    static IEnumerable<Case> Cases()
    {
        JsonArray list = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "movetime.browser.json")))!.AsArray();
        static RectD R(JsonNode n) => new(n["x"]!.GetValue<double>(), n["y"]!.GetValue<double>(), n["w"]!.GetValue<double>(), n["h"]!.GetValue<double>());
        foreach (JsonNode? c in list)
            yield return new Case(c!["scaleTime"]!.GetValue<bool>(), c["base"]!.GetValue<double>(), R(c["from"]!), R(c["to"]!), c["ms"]!.GetValue<double>(), c["far"]!.GetValue<bool>());
    }

    [Fact]
    public void Move_times_are_the_browser_readers_to_the_millisecond()
    {
        int checkedCases = 0;
        foreach (Case c in Cases())
        {
            var e = new ReaderEngine(new ReaderSettings { ScaleTime = c.ScaleTime });
            var (ms, far) = e.MoveTime(c.From, c.To, c.Base);
            Assert.True(Math.Abs(ms - c.Ms) < 1e-6, $"from {c.From} to {c.To} at {c.Base}: the browser takes {c.Ms} ms, the desktop {ms}");
            Assert.Equal(c.Far, far);
            checkedCases++;
        }
        Assert.Equal(32, checkedCases);
    }

    [Fact]
    public void A_real_move_lasts_exactly_as_long_as_the_browser_says_and_never_less()
    {
        // The first case is the whole of a page in to its first stop; the second is from that stop to the next.
        Case wholeToStop = Cases().First(c => c.ScaleTime && c.Base == 1400);
        Case stopToStop = Cases().Where(c => c.ScaleTime && c.Base == 1400).ElementAt(1);

        // A page 1.6 wide whose stops are padded out to exactly those cameras.
        static RectD Unpad(RectD cam) => Geo.Inflate(cam, -0.025);
        static RectD AsBox(RectD world) => new(world.X / 1.6, world.Y, world.W / 1.6, world.H);
        Book book = Kit.OnePage(new Stop { Box = AsBox(Unpad(wholeToStop.To)) }, new Stop { Box = AsBox(Unpad(stopToStop.To)) });
        Rig rig = Kit.Open(book);
        ReaderEngine e = rig.Engine;
        Assert.Equal(1400, e.Settings.MoveMs);

        foreach (Case c in new[] { wholeToStop, stopToStop })
        {
            // The whole-page camera here is a touch bigger than the fixture's, so ask the engine for this move's own time too.
            RectD from = e.Shown.Cam;
            rig.Next();
            RectD to = e.Target().Cam;
            double expected = e.MoveTime(from, to, 1400).Ms;
            if (c == stopToStop) Assert.Equal(c.Ms, expected, 3);
            double start = rig.Now;
            // Step in quarter milliseconds so the moment of arrival is known closely.
            while (e.Animating) rig.Run(0.25, 0.25);
            double took = rig.Now - start;
            Assert.InRange(took, expected, expected + 0.26);
        }
    }
}

public class FrameClockTests
{
    [Fact]
    public void A_steady_beat_with_wobbly_timing_comes_out_even()
    {
        var clock = new FrameClock();
        var random = new Random(7);
        double raw = 1000, last = clock.Next(raw);
        var steps = new List<double>();
        for (int i = 0; i < 300; i++)
        {
            raw = 1000 + (i + 1) * (1000.0 / 60) + (random.NextDouble() - 0.5) * 5;    // up to 2.5 ms early or late
            double t = clock.Next(raw);
            if (i > 30) steps.Add(t - last);
            last = t;
        }
        Assert.InRange(clock.Beat, 15.5, 17.8);
        Assert.InRange(steps.Min(), clock.Beat - 1.5, clock.Beat + 1.5);
        Assert.InRange(steps.Max(), clock.Beat - 1.5, clock.Beat + 1.5);
        double spread = steps.Max() - steps.Min();
        Assert.True(spread < 1.6, $"the steps should be far more even than the 5 ms wobble going in, but spread over {spread} ms");
        Assert.InRange(last - raw, -17, 17);                     // and it has stayed with the real time
    }

    [Fact]
    public void A_missed_frame_is_two_beats_and_a_long_stop_is_taken_as_it_is()
    {
        var clock = new FrameClock();
        double raw = 0;
        clock.Next(raw);
        for (int i = 0; i < 40; i++) clock.Next(raw += 16.67);
        double before = clock.Time;
        double after = clock.Next(raw += 33.9);                  // one frame was missed
        Assert.InRange(after - before, 32.5, 34.5);
        clock.Next(raw += 3000);                                 // the window was hidden for three seconds
        Assert.Equal(raw, clock.Time, 6);
    }

    [Fact]
    public void An_uneven_beat_is_followed_as_it_comes_and_time_never_runs_backwards()
    {
        var clock = new FrameClock();
        var random = new Random(3);
        double raw = 500, last = clock.Next(raw);
        for (int i = 0; i < 500; i++)
        {
            raw += 5 + random.NextDouble() * 60;                 // anything from 5 to 65 ms: no beat at all
            double t = clock.Next(raw);
            Assert.True(t >= last);
            Assert.InRange(t - raw, -40, 40);
            last = t;
        }
        Assert.Equal(last, clock.Next(raw - 10));                // an out-of-order time changes nothing
    }

    [Fact]
    public void Other_refresh_rates_are_picked_up_too()
    {
        foreach (double hz in new[] { 30.0, 60, 75, 120, 144 })
        {
            var clock = new FrameClock();
            double raw = 0;
            clock.Next(raw);
            for (int i = 0; i < 60; i++) clock.Next(raw += 1000 / hz + (i % 2 == 0 ? 0.4 : -0.4));
            Assert.InRange(clock.Beat, 1000 / hz - 0.5, 1000 / hz + 0.5);
        }
    }
}
