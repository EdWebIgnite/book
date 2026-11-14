using Reader.Core;

namespace Reader.Tests;

/// <summary>The open book: the spread's shape, the crease that fades as the camera goes in, the curling page and the cover.</summary>
public class SpreadTests
{
    static void Near(double expected, double actual, double within = 1e-9) =>
        Assert.True(Math.Abs(expected - actual) <= within, $"expected {expected} but got {actual}");

    [Fact]
    public void A_landscape_picture_is_the_whole_spread_and_the_crease_runs_down_its_middle()
    {
        var page = new RectD(2, 0, 1.6, 1);
        Assert.False(BookLayout.IsPortrait(page));
        Assert.Equal(page, BookLayout.Spread(page));
        Near(2.8, BookLayout.SpineX(page));
    }

    [Fact]
    public void A_portrait_page_is_the_right_hand_page_of_a_spread_two_pages_wide()
    {
        var page = new RectD(2, 0, 0.667, 1);
        Assert.True(BookLayout.IsPortrait(page));
        RectD spread = BookLayout.Spread(page);
        Near(2 - 0.667, spread.X);
        Near(1.334, spread.W);
        Near(page.X, BookLayout.SpineX(page));                   // the spine is the page's left edge
    }

    [Fact]
    public void The_book_holds_the_spread_with_the_page_edges_and_board_all_round_and_the_closed_book_is_its_right_half()
    {
        var page = new RectD(0, 0, 1.6, 1);
        RectD book = BookLayout.Book(page);
        double side = BookLayout.StackRoom + BookLayout.Board;
        Assert.Equal(new RectD(-side, -BookLayout.Board, 1.6 + 2 * side, 1 + 2 * BookLayout.Board), book);
        RectD closed = BookLayout.Closed(page);
        Near(0.8, closed.X);                                     // from the spine
        Assert.True(closed.Right > page.Right, "the closed book shows its thickness beyond the page");
        Assert.True(BookLayout.CoverBoard(page).Right <= closed.Right);
    }

    [Fact]
    public void Page_edges_pile_up_on_the_left_as_the_book_is_read()
    {
        Assert.Equal((1, BookLayout.MostStackLines), BookLayout.StackLines(0, 12));
        Assert.Equal((BookLayout.MostStackLines, 1), BookLayout.StackLines(11, 12));
        var middle = BookLayout.StackLines(6, 12);
        Assert.InRange(middle.Left, 2, 4);
        Assert.InRange(middle.Right, 2, 4);
        Assert.Equal((1, BookLayout.MostStackLines), BookLayout.StackLines(0, 1));
    }

    [Fact]
    public void The_whole_page_view_frames_the_whole_open_book()
    {
        Rig rig = Kit.Open("biggest-puddle");
        ReaderEngine e = rig.Engine;
        RectD book = BookLayout.Book(e.PageRects[0]);
        RectD cam = e.WholeCam(0);
        Assert.True(cam.X < book.X && cam.Right > book.Right && cam.Y < book.Y && cam.Bottom > book.Bottom);
        Assert.Equal(cam, e.Shown.Cam);
        Assert.Equal(BookLayout.Spread(e.PageRects[0]), e.Shown.Focus);

        // Without the book look, the page alone with a little air, as in the browser.
        Rig plain = Kit.Open("biggest-puddle", s => s.BookLook = false);
        Assert.Equal(Geo.Inflate(plain.Engine.PageRects[0], 0.03), plain.Engine.Shown.Cam);

        // A portrait book frames both of its pages.
        Rig comic = Kit.Open("captain-megawatt");
        comic.Jump(1, Step.WholePage);
        RectD spread = BookLayout.Spread(comic.Engine.PageRects[1]);
        Assert.True(comic.Engine.Shown.Cam.X < spread.X && comic.Engine.Shown.Cam.Right > spread.Right);
    }

    [Fact]
    public void Explore_pages_and_tiled_scenes_are_framed_as_before()
    {
        Rig sea = Kit.Open("deep-sea");
        Assert.Equal(Geo.Inflate(sea.Engine.PageRects[0], 0.03), sea.Engine.WholeCam(0));
        Rig rig = Kit.Open("biggest-puddle");
        rig.Jump(11, Step.Exploring);
        Assert.False(rig.Engine.BookMode);
        Assert.Equal(0, rig.Engine.CreaseStrength());
    }
}

public class CreaseTests
{
    [Fact]
    public void The_crease_is_full_at_the_whole_book_and_gone_by_one_and_a_third_times_closer()
    {
        Assert.Equal(1, BookLayout.CreaseStrength(1));
        Assert.Equal(1, BookLayout.CreaseStrength(0.6));          // further out still: the closed book opening
        Assert.Equal(0.5, BookLayout.CreaseStrength(1.15), 9);
        Assert.Equal(0, BookLayout.CreaseStrength(1.3), 9);
        Assert.Equal(0, BookLayout.CreaseStrength(4));
    }

    [Fact]
    public void Going_in_to_a_stop_fades_the_crease_out_and_coming_back_brings_it_back()
    {
        Rig rig = Kit.Open("biggest-puddle");
        ReaderEngine e = rig.Engine;
        Assert.Equal(1, e.CreaseStrength(), 9);
        rig.Next();
        double last = 1;
        while (e.Animating)
        {
            rig.Run(16);
            Assert.True(e.CreaseStrength() <= last + 1e-9, "it only fades on the way in");
            last = e.CreaseStrength();
        }
        Assert.Equal(0, e.CreaseStrength());                     // a close-up never has the dark line through it
        e.ToggleOverview();
        rig.RunUntilStill();
        Assert.Equal(1, e.CreaseStrength(), 9);
    }
}

public class CurlTests
{
    [Fact]
    public void The_page_starts_flat_on_one_side_and_ends_flat_on_the_other()
    {
        for (double t = 0; t <= 1; t += 0.125)
        {
            var start = Curl.Sheet(0, t);
            Assert.Equal((t, 0.0, 0.0), start);
            var end = Curl.Sheet(1, t);
            Assert.Equal(-t, end.X, 9);
            Assert.Equal(0, end.Z, 9);
        }
    }

    [Fact]
    public void The_free_edge_travels_evenly_across_and_is_at_the_spine_half_way()
    {
        for (double f = 0.05; f < 1; f += 0.05)
            Assert.Equal(1 - 2 * f, Curl.Sheet(f, 1).X, 9);
        Assert.Equal(0, Curl.Sheet(0.5, 1).X, 9);
    }

    [Fact]
    public void The_paper_never_stretches()
    {
        foreach (double f in new[] { 0.1, 0.3, 0.5, 0.7, 0.9 })
        {
            double length = 0;
            var last = Curl.Sheet(f, 0);
            const int pieces = 4000;
            for (int i = 1; i <= pieces; i++)
            {
                var p = Curl.Sheet(f, (double)i / pieces);
                length += Math.Sqrt(Math.Pow(p.X - last.X, 2) + Math.Pow(p.Z - last.Z, 2));
                last = p;
            }
            Assert.Equal(1, length, 3);
        }
    }

    [Fact]
    public void Mid_turn_the_page_bends_over_a_roll_and_its_back_lies_face_up()
    {
        var bend = Enumerable.Range(0, 101).Select(i => Curl.Sheet(0.5, i / 100.0)).ToList();
        Assert.Contains(bend, p => p.Angle > 0.3 && p.Angle < Math.PI - 0.3);     // a real bend, not a fold
        Assert.Equal(2 * Curl.MostRadius, bend.Max(p => p.Z), 9);              // lifted off the page
        Assert.Equal(Math.PI, bend[^1].Angle, 9);                              // the far end lies back over, face down
        Assert.Equal(1, Curl.Lift(0.5), 9);
        Assert.Equal(0, Curl.Lift(0), 9);
        // Near the ends the roll is small: the page lifts gently and settles gently.
        Assert.True(Curl.Sheet(0.05, 1).Z < 0.1);
        Assert.True(Curl.Sheet(0.95, 1).Z < 0.1);
    }

    [Fact]
    public void The_fold_leans_with_the_top_corner_leading_and_both_ends_agree()
    {
        Assert.True(Curl.Leaning(0.5, 0) > 0.5 && Curl.Leaning(0.5, 1) < 0.5);
        Assert.Equal(0, Curl.Leaning(0, 0), 9);
        Assert.Equal(1, Curl.Leaning(1, 1), 9);
        Assert.Equal(0.3, Curl.Leaning(0.3, 0.5), 9);
    }

    [Fact]
    public void The_cover_is_a_stiff_board_that_swings_round_the_spine()
    {
        Assert.Equal(1, Curl.Board(0, 1).X, 9);
        Assert.Equal(0, Curl.Board(0.5, 1).X, 9);
        Assert.Equal(1, Curl.Board(0.5, 1).Z, 9);
        Assert.Equal(-1, Curl.Board(1, 1).X, 9);
        Assert.Equal(-0.5, Curl.Board(1, 0.5).X, 9);
        // Stiff: every point is at its own distance from the spine, whatever the angle.
        foreach (double open in new[] { 0.2, 0.6 })
            foreach (double t in new[] { 0.25, 1.0 })
            {
                var p = Curl.Board(open, t);
                Assert.Equal(t, Math.Sqrt(p.X * p.X + p.Z * p.Z), 9);
            }
    }

    [Fact]
    public void A_flip_reports_its_progress_through_the_curl_from_start_to_landing()
    {
        Rig rig = Kit.Open("biggest-puddle");
        Assert.True(rig.JumpToClosing(1));
        rig.Next();
        var folds = new List<double>();
        while (rig.Engine.GetFlip() is FlipDraw f) { folds.Add(f.Fold); rig.Run(16); }
        Assert.True(folds.Count > 40);
        Assert.True(folds.Zip(folds.Skip(1)).All(p => p.Second >= p.First));
        Assert.True(folds[0] < 0.01 && folds[^1] > 0.99);
        Assert.Equal(new Step(2, Step.WholePage), rig.Engine.CurrentStep);
    }
}

public class CoverTests
{
    const string Puddle = "biggest-puddle";

    [Fact]
    public void A_book_with_a_cover_starts_closed_on_it()
    {
        Rig rig = Kit.OpenWithCover(Puddle);
        ReaderEngine e = rig.Engine;
        Assert.Equal(new Step(0, Step.Cover), e.Steps[0]);
        Assert.Equal(new Step(0, Step.WholePage), e.Steps[1]);
        Assert.Equal(0, e.StepIndex);
        Assert.Equal(new CoverDraw(0, 0, false), e.GetCover());
        Assert.Equal(e.ClosedCam(0), e.Shown.Cam);
        Assert.Equal(BookLayout.Closed(e.PageRects[0]).CenterX, e.Shown.Cam.CenterX, 9);   // centred on the closed book
        Assert.Equal(0, e.Shown.Vig);
        Assert.True(e.State.FirstStep);
        Assert.Equal("", e.State.Caption);
        Assert.False(e.State.CanGoBack);
    }

    [Fact]
    public void Without_a_cover_picture_or_without_the_book_look_the_book_starts_open()
    {
        Assert.DoesNotContain(Kit.Open(Puddle).Engine.Steps, s => s.Stop == Step.Cover);
        Assert.DoesNotContain(Kit.OpenWithCover(Puddle, s => s.BookLook = false).Engine.Steps, s => s.Stop == Step.Cover);
        Assert.DoesNotContain(Kit.OpenWithCover("deep-sea").Engine.Steps, s => s.Stop == Step.Cover);   // its only page is explored
    }

    [Fact]
    public void The_first_tap_swings_the_cover_open_while_the_camera_eases_to_the_open_book()
    {
        Rig rig = Kit.OpenWithCover(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Next();
        Assert.Equal(new Step(0, Step.WholePage), e.CurrentStep);
        Assert.Contains("page turn", rig.Sound.Calls);
        double last = 0;
        RectD closed = e.ClosedCam(0), open = e.WholeCam(0);
        rig.Run(550);
        CoverDraw half = Assert.IsType<CoverDraw>(e.GetCover());
        Assert.True(half.Opening);
        Assert.InRange(half.Open, 0.4, 0.6);
        Assert.InRange(e.Shown.Cam.CenterX, open.CenterX + 0.01, closed.CenterX - 0.01);   // on its way to the middle
        while (e.GetCover() is CoverDraw c) { Assert.True(c.Open >= last); last = c.Open; rig.Run(16); }
        Assert.False(e.Animating);
        Assert.Equal(open, e.Shown.Cam);
        Assert.Null(e.GetCover());
        Assert.Equal(1, e.CreaseStrength(), 9);
    }

    [Theory]
    [InlineData(1400, 1100)]
    [InlineData(700, 550)]
    public void The_cover_takes_its_time_scaled_with_the_move_speed(double moveMs, double expected)
    {
        Rig rig = Kit.OpenWithCover(Puddle, s => s.MoveMs = moveMs);
        rig.Next();
        Assert.InRange(rig.RunUntilStill(frameMs: 4), expected, expected + 4);
    }

    [Fact]
    public void Going_back_from_the_first_page_closes_the_book_again()
    {
        Rig rig = Kit.OpenWithCover(Puddle);
        ReaderEngine e = rig.Engine;
        rig.NextAndArrive();
        rig.Prev();
        Assert.Equal(new Step(0, Step.Cover), e.CurrentStep);
        CoverDraw shutting = Assert.IsType<CoverDraw>(e.GetCover());
        Assert.False(shutting.Opening);
        Assert.True(shutting.Open > 0.99);
        rig.RunUntilStill();
        Assert.Equal(new CoverDraw(0, 0, false), e.GetCover());
        Assert.Equal(e.ClosedCam(0), e.Shown.Cam);
    }

    [Fact]
    public void From_a_close_up_the_camera_pulls_back_before_the_book_closes()
    {
        Rig rig = Kit.OpenWithCover(Puddle, s => s.WholePageFirst = false);
        ReaderEngine e = rig.Engine;
        Assert.Equal(new Step(0, 0), e.Steps[1]);
        rig.NextAndArrive();                                     // opens and goes in to the first stop
        Assert.Equal(new Step(0, 0), e.CurrentStep);
        rig.Prev();
        Assert.Null(e.GetCover());                               // pulling back first
        rig.Run(e.MoveTime(e.Shown.Cam, e.WholeCam(0), 1400).Ms + 50);
        Assert.NotNull(e.GetCover());
        rig.RunUntilStill();
        Assert.Equal(e.ClosedCam(0), e.Shown.Cam);
    }

    [Fact]
    public void Reduced_motion_fades_from_the_cover_and_the_cover_shows_until_the_darkest_moment()
    {
        Rig rig = Kit.OpenWithCover(Puddle, s => s.ReduceMotion = "on");
        ReaderEngine e = rig.Engine;
        rig.Next();
        rig.Run(100);
        Assert.Equal(new CoverDraw(0, 0, false), e.GetCover());
        rig.Run(150);
        Assert.Null(e.GetCover());
        Assert.InRange(rig.RunUntilStill(), 60, 120);
        Assert.DoesNotContain("page turn", rig.Sound.Calls);
    }

    [Fact]
    public void The_tap_guard_and_an_impatient_tap_hold_while_the_cover_swings()
    {
        Rig rig = Kit.OpenWithCover(Puddle);
        ReaderEngine e = rig.Engine;
        rig.Next();
        rig.Run(200);
        e.Next();                                                // too soon: ignored
        Assert.NotNull(e.GetCover());
        rig.Run(200);
        e.Next();                                                // impatient: the cover lands
        Assert.Null(e.GetCover());
        Assert.False(e.Animating);
        Assert.Equal(1, e.StepIndex);
    }

    [Fact]
    public void Asking_for_a_page_skips_the_cover()
    {
        Rig rig = Kit.OpenWithCover(Puddle);
        Assert.Equal(1, rig.Engine.FindStep(0, 99));             // no such stop: the first step of the page, past the cover
        rig.Jump(0, Step.WholePage);
        Assert.Null(rig.Engine.GetCover());
        rig.Jump(3, 0);
        Assert.Equal(new Step(3, 0), rig.Engine.CurrentStep);
    }

    [Fact]
    public void Read_again_goes_back_to_the_closed_book()
    {
        Rig rig = Kit.OpenWithCover("captain-megawatt");
        ReaderEngine e = rig.Engine;
        Assert.True(rig.JumpToClosing(2));
        rig.Next();
        Assert.True(e.Finished);
        e.Restart();
        rig.RunUntilStill();
        Assert.Equal(new Step(0, Step.Cover), e.CurrentStep);
        Assert.NotNull(e.GetCover());
    }

    [Fact]
    public void Switching_the_book_look_off_on_the_cover_opens_the_book()
    {
        Rig rig = Kit.OpenWithCover(Puddle);
        rig.Engine.Settings.BookLook = false;
        rig.Engine.Refresh();
        Assert.DoesNotContain(rig.Engine.Steps, s => s.Stop == Step.Cover);
        Assert.Equal(new Step(0, Step.WholePage), rig.Engine.CurrentStep);
    }
}
