using Reader.Core;

namespace Reader.Tests;

/// <summary>
/// One page at a time, as on a phone held upright: the open spread is read a page (half the picture)
/// at a time when seen whole, close-ups still move over the whole picture, and turning the phone keeps
/// the place.
/// </summary>
public class OnePageTests
{
    /// <summary>
    /// Two landscape pictures: the first with a stop on its left-hand page and one on its right-hand page,
    /// the second with nothing to stop at.
    /// </summary>
    static Book TwoPictures()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.1, 0.3, 0.25, 0.3), Kit.StopAt(0.6, 0.3, 0.25, 0.3));
        book.Pages.Add(new Page { Image = "page2.jpg" });
        return book;
    }

    /// <summary>An engine on a phone-shaped screen held upright, one page at a time.</summary>
    static Rig Upright(Book book, Action<ReaderSettings>? tweak = null)
    {
        Rig rig = Kit.Open(book, tweak);
        Turn(rig, upright: true);
        return rig;
    }

    /// <summary>Turn the phone: the screen changes shape, and one page at a time goes on or off with it.</summary>
    static void Turn(Rig rig, bool upright)
    {
        ReaderEngine e = rig.Engine;
        e.SetTime(rig.Now);
        if (upright) e.SetViewSize(400, 860); else e.SetViewSize(860, 400);
        e.OnePage = upright;
        e.Refresh();
        rig.RunUntilStill();
    }

    [Fact]
    public void Upright_the_whole_page_is_the_page_holding_the_first_stop_as_it_opens_and_the_last_stop_at_the_end()
    {
        Rig rig = Upright(TwoPictures());
        List<Step> steps = rig.Engine.Steps.Where(s => s.Page == 0).ToList();
        Assert.Equal(new Step(0, Step.WholePage, Half: Step.LeftPage), steps[0]);
        Assert.Equal(0, steps[1].Stop);
        Assert.Equal(1, steps[2].Stop);
        Assert.Equal(new Step(0, Step.WholePage, Closing: true, Half: Step.RightPage), steps[3]);
    }

    [Fact]
    public void Upright_a_picture_with_nothing_to_stop_at_shows_its_left_page_then_its_right_page()
    {
        Rig rig = Upright(TwoPictures());
        Assert.Equal(new[] { new Step(1, Step.WholePage, Half: Step.LeftPage), new Step(1, Step.WholePage, Half: Step.RightPage) },
            rig.Engine.Steps.Where(s => s.Page == 1));
    }

    [Fact]
    public void Sideways_nothing_changes_the_spread_is_seen_whole()
    {
        Rig rig = Kit.Open(TwoPictures());
        Assert.All(rig.Engine.Steps, s => Assert.Equal(Step.Both, s.Half));
        Assert.Single(rig.Engine.Steps, s => s.Page == 1);
    }

    [Fact]
    public void A_portrait_page_is_always_the_right_hand_page()
    {
        var book = new Book { Id = "tall", Title = "Tall", Pages = { new Page { Image = "1.jpg" }, new Page { Image = "2.jpg" } } };
        Rig rig = Kit.Open(book);
        ReaderEngine e = rig.Engine;
        e.SetBook(book, new[] { 0.667, 0.667 }, 0, hasCover: false);
        Turn(rig, upright: true);
        Assert.All(e.Steps, s => Assert.Equal(Step.RightPage, s.Half));
        Assert.Equal(2, e.Steps.Count);
    }

    [Fact]
    public void The_whole_page_upright_frames_one_page_with_its_board_and_page_edges_round_it()
    {
        Rig rig = Upright(TwoPictures());
        ReaderEngine e = rig.Engine;
        RectD left = e.HalfRect(0, Step.LeftPage), right = e.HalfRect(0, Step.RightPage);
        Assert.Equal(e.PageRects[0].W / 2, left.W, 9);
        Assert.Equal(left.Right, right.X, 9);
        RectD cam = e.WholeCam(0, Step.LeftPage);
        Assert.True(cam.X < left.X && cam.Right > left.Right && cam.Right < right.CenterX, "one page and a little round it");
        Assert.Equal(Step.LeftPage, e.VisibleHalf());
        Assert.Equal(1, e.CreaseStrength(), 2);                       // seen whole, so the other page is fully hidden
    }

    [Fact]
    public void Close_ups_move_over_the_whole_picture_and_the_other_page_shows_again()
    {
        Rig rig = Upright(TwoPictures());
        rig.NextAndArrive();
        ReaderEngine e = rig.Engine;
        Assert.Equal(0, e.CurrentStep.Stop);
        Assert.True(e.CreaseStrength() < 0.05, "in close, the hidden page fades back in");
        RectD cam = e.Shown.Cam;
        Assert.True(cam.W < e.HalfRect(0, Step.LeftPage).W, "a close-up is closer than the page seen whole");
    }

    [Fact]
    public void Going_from_one_page_of_a_picture_to_its_other_page_is_a_single_page_turn()
    {
        Rig rig = Upright(TwoPictures(), s => s.PageTurn = "flip");
        ReaderEngine e = rig.Engine;
        rig.Jump(1, Step.WholePage);
        rig.RunUntilStill();
        Assert.Equal(new Step(1, Step.WholePage, Half: Step.LeftPage), e.CurrentStep);
        rig.Next();
        rig.Run(200);
        FlipDraw? flip = e.GetFlip();
        Assert.NotNull(flip);
        Assert.True(flip.Value.OnePage);
        Assert.Equal((1, Step.LeftPage, 1, Step.RightPage), (flip.Value.TurningPage, flip.Value.TurningHalf, flip.Value.UnderPage, flip.Value.UnderHalf));
        Assert.True(flip.Value.Forward);
        rig.RunUntilStill();
        Assert.Null(e.GetFlip());
        Assert.Equal(Step.RightPage, e.VisibleHalf());
    }

    [Fact]
    public void Going_on_to_the_next_picture_turns_one_page_from_the_last_stops_page_to_the_first_stops_page()
    {
        Rig rig = Upright(TwoPictures(), s => s.PageTurn = "flip");
        ReaderEngine e = rig.Engine;
        Assert.True(rig.JumpToClosing(0));
        rig.Next();
        rig.Run(200);
        FlipDraw flip = e.GetFlip() ?? throw new Xunit.Sdk.XunitException("no page turn");
        Assert.Equal((0, Step.RightPage, 1, Step.LeftPage), (flip.TurningPage, flip.TurningHalf, flip.UnderPage, flip.UnderHalf));
        Assert.Equal(e.WholeCam(0, Step.RightPage), flip.TurningCam);
        Assert.Equal(e.WholeCam(1, Step.LeftPage), flip.UnderCam);
    }

    [Fact]
    public void Sideways_page_turns_are_whole_sheets_as_before()
    {
        Rig rig = Kit.Open(TwoPictures(), s => s.PageTurn = "flip");
        Assert.True(rig.JumpToClosing(0));
        rig.Next();
        rig.Run(200);
        FlipDraw flip = rig.Engine.GetFlip() ?? throw new Xunit.Sdk.XunitException("no page turn");
        Assert.False(flip.OnePage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Turning_the_phone_keeps_the_place(int stepOnFirstPicture)
    {
        Rig rig = Upright(TwoPictures());
        ReaderEngine e = rig.Engine;
        for (int i = 0; i < stepOnFirstPicture; i++) rig.NextAndArrive();
        Step before = e.CurrentStep;

        Turn(rig, upright: false);
        Assert.Equal((before.Page, before.Stop, before.Closing), (e.CurrentStep.Page, e.CurrentStep.Stop, e.CurrentStep.Closing));
        Assert.Equal(Step.Both, e.CurrentStep.Half);

        Turn(rig, upright: true);
        Assert.Equal(before, e.CurrentStep);
    }

    [Fact]
    public void Turning_the_phone_glides_to_the_new_framing_rather_than_jumping()
    {
        Rig rig = Upright(TwoPictures());
        ReaderEngine e = rig.Engine;
        e.SetTime(rig.Now);
        e.SetViewSize(860, 400);
        e.OnePage = false;
        e.Refresh();
        rig.Run(16);
        Assert.True(e.Animating, "the camera moves to the spread");
        rig.RunUntilStill();
        Assert.Equal(e.WholeCam(0).W, e.Shown.Cam.W, 6);
    }

    [Fact]
    public void Explore_pages_are_explored_the_same_both_ways()
    {
        Rig rig = Upright(Kit.Load("biggest-puddle"));
        ReaderEngine e = rig.Engine;
        int explore = e.Book!.Pages.FindIndex(p => p.Explore);
        Assert.True(explore >= 0);
        Assert.All(e.Steps.Where(s => s.Explore), s => Assert.Equal(Step.Both, s.Half));
        Assert.Contains(e.Steps, s => s.Page == explore && s.Explore);
    }

    [Fact]
    public void Upright_every_page_of_the_sample_books_is_still_reached_in_order()
    {
        foreach (string id in Kit.BookIds())
        {
            Rig rig = Upright(Kit.Load(id));
            IReadOnlyList<Step> steps = rig.Engine.Steps;
            for (int i = 1; i < steps.Count; i++) Assert.True(steps[i].Page >= steps[i - 1].Page, $"{id}: steps go forwards");
            Assert.Equal(Enumerable.Range(0, rig.Engine.Book!.Pages.Count), steps.Select(s => s.Page).Distinct());
        }
    }

    // ------------------------------------------------------------------ touch

    [Fact]
    public void On_a_phone_a_tap_anywhere_goes_on_even_on_the_left()
    {
        Rig rig = Upright(TwoPictures());
        var touch = new GestureInput(rig.Engine) { TapAnywhereGoesOn = true };
        touch.PointerDown(1, 40, 400, rig.Now);
        touch.PointerUp(1, 41, 401, rig.Now + 80);
        Assert.Equal(1, rig.Engine.StepIndex);
    }

    [Fact]
    public void On_a_phone_a_quick_second_tap_does_not_skip_a_step()
    {
        Rig rig = Upright(TwoPictures());
        var touch = new GestureInput(rig.Engine) { TapAnywhereGoesOn = true };
        touch.PointerDown(1, 200, 400, rig.Now);
        touch.PointerUp(1, 200, 400, rig.Now + 60);
        rig.Run(150);
        touch.PointerDown(1, 205, 402, rig.Now);
        touch.PointerUp(1, 205, 402, rig.Now + 60);
        Assert.Equal(1, rig.Engine.StepIndex);
    }

    [Fact]
    public void On_a_phone_a_swipe_right_still_goes_back()
    {
        Rig rig = Upright(TwoPictures());
        rig.NextAndArrive();
        rig.Run(400);
        var touch = new GestureInput(rig.Engine) { TapAnywhereGoesOn = true };
        touch.PointerDown(1, 100, 400, rig.Now);
        touch.PointerMove(1, 180, 402, rig.Now + 50);
        touch.PointerUp(1, 260, 404, rig.Now + 100);
        Assert.Equal(0, rig.Engine.StepIndex);
    }
}
