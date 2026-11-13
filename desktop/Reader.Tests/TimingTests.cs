using Reader.Core;

namespace Reader.Tests;

/// <summary>Stops with their own travel time, stops that go on by themselves, and watch mode.</summary>
public class TimingTests
{
    const string Puddle = "biggest-puddle";
    const int CountingPage = 8;      // page 9: "One... two... three..." then "JUMP!"
    const int SplashPage = 9;        // page 10: "SPLASH!" cuts in with a shake and a sound

    [Fact]
    public void The_sample_book_is_set_up_the_way_these_tests_expect()
    {
        Book book = Kit.Load(Puddle);
        Stop count = book.Pages[CountingPage].Stops[0], jump = book.Pages[CountingPage].Stops[1], splash = book.Pages[SplashPage].Stops[0];
        Assert.True(count.Auto);
        Assert.Equal(2.5, count.HoldSeconds);
        Assert.Equal(0.7, jump.MoveSeconds);
        Assert.Equal(0, splash.MoveSeconds);
        Assert.Equal("splash.wav", splash.Sound);
    }

    [Fact]
    public void A_stop_marked_auto_goes_on_by_itself_after_its_hold_and_the_next_stop_arrives_in_its_own_time()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Jump(CountingPage, Step.WholePage);
        rig.NextAndArrive();                                     // tap once: "One... two... three..."
        Assert.Equal(new Step(CountingPage, 0), e.CurrentStep);
        double arrived = rig.Now;

        rig.Run(2400);
        Assert.Equal(new Step(CountingPage, 0), e.CurrentStep); // still holding
        rig.Run(200);
        Assert.Equal(new Step(CountingPage, 1), e.CurrentStep); // went on with no tap
        Assert.InRange(rig.Now - arrived, 2500, 2600);

        // "JUMP!" sets moveSeconds 0.7, so the camera takes 700 ms whatever the distance.
        double took = rig.RunUntilStill();
        Assert.InRange(took + (rig.Now - took - arrived - 2500), 690, 740);
        Assert.Equal("JUMP!", e.State.Caption);

        // "JUMP!" is not marked auto: it waits for a tap.
        rig.Run(10_000);
        Assert.Equal(new Step(CountingPage, 1), e.CurrentStep);
    }

    [Fact]
    public void Going_back_to_an_auto_stop_does_not_get_pushed_forward_again()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Jump(CountingPage, 1);
        rig.Prev();
        rig.RunUntilStill();
        Assert.Equal(new Step(CountingPage, 0), e.CurrentStep);
        rig.Run(15_000);
        Assert.Equal(new Step(CountingPage, 0), e.CurrentStep);

        // Jumping straight to it (the editor's preview does this) doesn't push on either.
        rig.Jump(CountingPage, 0);
        rig.Run(15_000);
        Assert.Equal(new Step(CountingPage, 0), e.CurrentStep);
    }

    [Fact]
    public void Move_seconds_zero_cuts_straight_to_the_stop()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Jump(CountingPage, 1);
        int whooshes = rig.Sound.Count("whoosh");
        rig.Next();
        Assert.Equal(new Step(SplashPage, 0), e.CurrentStep);
        Assert.False(e.Animating);                               // there already
        Assert.Equal(e.Target().Cam, e.Shown.Cam);
        Assert.Equal(whooshes, rig.Sound.Count("whoosh"));       // a cut makes no rush of air
        Assert.Equal("splash.wav", rig.Sound.Clip);              // its sound plays on arriving
    }

    [Fact]
    public void A_stops_own_travel_time_only_applies_when_arriving_from_the_stop_before()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Jump(SplashPage, 1);
        rig.Prev();                                              // back to "SPLASH!", which cuts when going forward
        Assert.Equal(new Step(SplashPage, 0), e.CurrentStep);
        Assert.True(e.Animating);                                // going back is an ordinary move
        Assert.InRange(rig.RunUntilStill(), 500, 3400);
    }

    [Fact]
    public void An_auto_stop_waits_for_its_sound_to_finish()
    {
        Book book = Kit.OnePage(
            new Stop { Box = new RectD(0.1, 0.1, 0.3, 0.3), Auto = true, HoldSeconds = 1, Sound = "line1.mp3" },
            Kit.StopAt(0.5, 0.5));
        Rig rig = Kit.Open(book);
        ReaderEngine e = rig.Engine;
        rig.Next();
        rig.RunUntilStill();
        Assert.Equal("line1.mp3", rig.Sound.Clip);
        rig.Sound.ClipPlaying = true;                            // the narration runs for five seconds
        rig.Run(5000);
        Assert.Equal(new Step(0, 0), e.CurrentStep);
        rig.Sound.ClipPlaying = false;
        rig.Run(900);
        Assert.Equal(new Step(0, 0), e.CurrentStep);            // the hold starts once the sound has ended
        rig.Run(200);
        Assert.Equal(new Step(0, 1), e.CurrentStep);
    }

    [Fact]
    public void An_auto_stop_with_no_hold_rests_for_a_second_and_a_half()
    {
        Book book = Kit.OnePage(new Stop { Box = new RectD(0.1, 0.1, 0.3, 0.3), Auto = true }, Kit.StopAt(0.5, 0.5));
        Rig rig = Kit.Open(book);
        rig.NextAndArrive();
        rig.Run(1450);
        Assert.Equal(1, rig.Engine.StepIndex);
        rig.Run(100);
        Assert.Equal(2, rig.Engine.StepIndex);
    }

    [Fact]
    public void Watch_mode_moves_on_by_itself_everywhere_and_stops_at_the_end()
    {
        Rig rig = Kit.Open("captain-megawatt", s => { s.AutoPlay = true; s.AutoSeconds = 2; });
        ReaderEngine e = rig.Engine;
        rig.Run(1900);
        Assert.Equal(0, e.StepIndex);
        rig.Run(200);
        Assert.Equal(1, e.StepIndex);
        rig.Run(120_000);
        Assert.True(e.State.Finished);
    }

    [Fact]
    public void Nothing_moves_on_by_itself_while_the_reader_is_hidden()
    {
        Rig rig = Kit.Open("captain-megawatt", s => { s.AutoPlay = true; s.AutoSeconds = 2; });
        rig.Engine.Visible = false;
        rig.Run(10_000);
        Assert.Equal(0, rig.Engine.StepIndex);
    }

    // ------------------------------------------------------------------ arriving: shake and lightning

    [Fact]
    public void The_splash_shakes_the_camera_silently_a_moment_after_arriving()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Jump(CountingPage, 1);
        rig.Next();
        Assert.Equal(default, e.ShakeOffset());                  // starts 120 ms after arriving
        rig.Run(200);
        PointD shake = e.ShakeOffset();
        Assert.True(Math.Abs(shake.X) + Math.Abs(shake.Y) > 0.5, "the view should be shaking");
        Assert.True(Math.Abs(shake.X) < 12 && Math.Abs(shake.Y) < 12, "but gently");
        ViewTransform with = e.GetView(), without = e.GetView(withShake: false);
        Assert.Equal(shake.X, with.OffsetX - without.OffsetX, 9);
        rig.Run(700);
        Assert.Equal(default, e.ShakeOffset());
        Assert.DoesNotContain(rig.Sound.Calls, c => c.StartsWith("thunder"));
    }

    [Fact]
    public void Lightning_flashes_softly_and_the_thunder_comes_after_a_beat()
    {
        Rig rig = Kit.Open(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Jump(6, Step.WholePage);
        rig.NextAndArrive();                                     // page 7, first stop: lightning
        Assert.Contains("thunder 0.85", rig.Sound.Calls);
        Assert.Equal(0, e.FlashAlpha);                           // the flash waits 700 ms
        rig.Run(650);
        Assert.Equal(0, e.FlashAlpha);
        rig.Run(80);
        Assert.InRange(e.FlashAlpha, 0.2, 0.6);                  // never a full white-out
        rig.Run(900);
        Assert.Equal(0, e.FlashAlpha);
    }

    [Fact]
    public void Reduced_motion_keeps_the_thunder_but_drops_the_flash_and_the_shake()
    {
        Rig rig = Kit.Open(Puddle, s => s.ReduceMotion = "on");
        ReaderEngine e = rig.Engine;
        rig.Jump(6, Step.WholePage);
        rig.NextAndArrive();
        Assert.Contains("thunder 0.85", rig.Sound.Calls);
        for (int i = 0; i < 100; i++) { rig.Run(16); Assert.Equal(0, e.FlashAlpha); }

        rig.Jump(CountingPage, 1);
        rig.NextAndArrive();
        Assert.Equal(new Step(SplashPage, 0), e.CurrentStep);
        for (int i = 0; i < 60; i++) { rig.Run(16); Assert.Equal(default, e.ShakeOffset()); }
    }

    [Fact]
    public void With_sound_switched_off_nothing_is_asked_of_the_sound_output()
    {
        Rig rig = Kit.Open(Puddle, s => s.Sound = false);
        rig.Jump(6, Step.WholePage);
        rig.NextAndArrive();
        rig.Jump(CountingPage, 1);
        rig.NextAndArrive();
        rig.Run(2000);
        Assert.Empty(rig.Sound.Calls);
        Assert.DoesNotContain(rig.Sound.RainLevels, l => l > 0);
    }
}
