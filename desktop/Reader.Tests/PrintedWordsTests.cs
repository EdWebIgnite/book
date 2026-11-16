using System.Text.Json.Nodes;
using Editor.Core;
using Reader.Core;

namespace Reader.Tests;

/// <summary>The words printed on the page: the book format, what is printed, the sizes, the switches and Check book.</summary>
public class PrintedWordsTests
{
    const string WithWords = """
        { "id": "w", "title": "W", "pages": [
          { "image": "p1.jpg",
            "stops": [ { "box": { "x": 0.1, "y": 0.5, "w": 0.3, "h": 0.3 }, "caption": "Moss had new boots." },
                       { "box": { "x": 0.5, "y": 0.5, "w": 0.3, "h": 0.3 } },
                       { "box": { "x": 0.6, "y": 0.1, "w": 0.3, "h": 0.3 }, "caption": "  He loved them. " } ],
            "text": { "x": 0.05, "y": 0.04, "w": 0.4, "h": 0.2, "align": "center", "size": 0.035, "color": "#112233", "shadow": "soft" } },
          { "image": "p2.jpg", "stops": [] } ] }
        """;

    [Fact]
    public void A_words_box_loads_and_saves_with_every_field_and_keeps_fields_it_does_not_know()
    {
        Book book = BookJson.Parse(WithWords);
        PageText t = book.Pages[0].Text!;
        Assert.Equal(new RectD(0.05, 0.04, 0.4, 0.2), t.Box);
        Assert.Equal(PageText.Center, t.Align);
        Assert.Equal(0.035, t.Size);
        Assert.Equal("#112233", t.Color);
        Assert.Null(t.Content);
        Assert.Null(book.Pages[1].Text);

        JsonNode saved = BookJson.Serialize(book);
        JsonObject text = saved["pages"]![0]!["text"]!.AsObject();
        Assert.Equal(new[] { "x", "y", "w", "h", "size", "align", "color", "shadow" }, text.Select(p => p.Key));
        Assert.Equal("soft", text["shadow"]!.GetValue<string>());
        Assert.Null(saved["pages"]![1]!["text"]);                      // a page without words stays as it was

        string once = BookJson.ToJsonString(book);
        Assert.Equal(once, BookJson.ToJsonString(BookJson.Parse(once)));
    }

    [Fact]
    public void Missing_or_odd_values_fall_back_to_the_usual_look()
    {
        Book book = BookJson.Parse("""
            { "id": "w", "title": "W", "pages": [ { "image": "p1.jpg", "stops": [],
              "text": { "x": "0.1", "y": 0.2, "w": 0.3, "h": 0.1, "align": "middle", "color": "red", "size": -1, "content": "" } } ] }
            """);
        PageText t = book.Pages[0].Text!;
        Assert.Equal(new RectD(0.1, 0.2, 0.3, 0.1), t.Box);           // a number written as text still reads
        Assert.Equal(PageText.Left, t.Align);
        Assert.Null(t.Color);
        Assert.Null(t.Size);
        Assert.Null(t.Content);
        Assert.Equal(PageText.DefaultSize, t.SizeOrDefault);
        Assert.Equal(PageText.DefaultColor, t.ColorOrDefault);

        JsonObject saved = BookJson.Serialize(book)["pages"]![0]!["text"]!.AsObject();
        Assert.Equal(new[] { "x", "y", "w", "h", "size" }, saved.Select(p => p.Key));   // only what isn't the usual
        Assert.Equal(PageText.DefaultSize, saved["size"]!.GetValue<double>());
    }

    [Fact]
    public void A_page_prints_its_stops_words_in_order_unless_it_has_words_of_its_own()
    {
        Book book = BookJson.Parse(WithWords);
        Page p = book.Pages[0];
        Assert.Equal("Moss had new boots. He loved them.", PrintedWords.Content(p));   // the stop with no words is skipped
        Assert.True(PrintedWords.Prints(p));

        p.Text!.Content = "   ";
        Assert.Equal("Moss had new boots. He loved them.", PrintedWords.Content(p));
        p.Text.Content = "Moss had boots.\nRed ones.";
        Assert.Equal("Moss had boots.\nRed ones.", PrintedWords.Content(p));
        Assert.Contains("\"content\": \"Moss had boots.\\nRed ones.\"", BookJson.ToJsonString(book));

        // No box, or nothing to print: nothing is printed.
        Assert.False(PrintedWords.Prints(book.Pages[1]));
        book.Pages[1].Text = new PageText { Box = new RectD(0.1, 0.1, 0.3, 0.2) };
        Assert.False(PrintedWords.Prints(book.Pages[1]));
    }

    [Fact]
    public void The_words_scale_with_the_picture()
    {
        var t = new PageText { Box = new RectD(0.1, 0.2, 0.4, 0.25), Size = 0.04 };
        Assert.Equal(40, PrintedWords.FontPixels(t, 1000), 9);
        Assert.Equal(80, PrintedWords.FontPixels(t, 2000), 9);

        // The box is a share of the page wherever and however big the page is drawn.
        RectD small = PrintedWords.BoxOn(t, new RectD(0, 0, 1600, 1000));
        RectD big = PrintedWords.BoxOn(t, new RectD(100, 50, 3200, 2000));
        Assert.Equal(new RectD(160, 200, 640, 250), small);
        Assert.Equal(new RectD(100 + 320, 50 + 400, 1280, 500), big);
        Assert.Equal(big.W / small.W, PrintedWords.FontPixels(t, 2000) / PrintedWords.FontPixels(t, 1000), 9);

        // More words, or a narrower box, need more height; the estimate is in page heights whatever the size.
        string words = string.Join(" ", Enumerable.Repeat("puddle", 30));
        double tall = PrintedWords.EstimateHeight(t, words, 1.6);
        Assert.True(PrintedWords.EstimateHeight(t, words + " " + words, 1.6) > tall);
        Assert.True(PrintedWords.EstimateHeight(Narrow(0.2), words, 1.6) > tall);
        Assert.True(tall > 0.04 * PrintedWords.LineHeight * 2);

        Assert.Equal(1, PrintedWords.Coverage(new RectD(1, 1, 2, 2), new RectD(0, 0, 10, 10)), 9);
        Assert.Equal(0.5, PrintedWords.Coverage(new RectD(0, 0, 2, 2), new RectD(1, 0, 10, 10)), 9);
        Assert.Equal(0, PrintedWords.Coverage(new RectD(0, 0, 2, 2), new RectD(5, 5, 1, 1)), 9);
    }

    static PageText Narrow(double w) => new() { Box = new RectD(0.1, 0.2, w, 0.25), Size = 0.04 };

    [Fact]
    public void Colours_are_read_as_hash_and_three_or_six_hex_digits()
    {
        Assert.Equal(((byte)0x11, (byte)0x22, (byte)0x33), PrintedWords.ParseColor("#123"));
        Assert.Equal(((byte)0xff, (byte)0xfa, (byte)0xf0), PrintedWords.ParseColor("#FFFAF0"));
        Assert.Null(PrintedWords.ParseColor("red"));
        Assert.Null(PrintedWords.ParseColor("#12345"));
        Assert.Null(PrintedWords.ParseColor(null));
    }

    [Fact]
    public void A_suggested_box_sits_at_the_top_of_one_page_of_a_spread()
    {
        var page = new Page { Image = "p.jpg", Stops = { Kit.StopAt(0.6, 0.5) } };
        PageText t = PrintedWords.Suggest(page, 1.6);
        Assert.True(t.Box.Right <= 0.5);                              // the first stop is on the right, so the words go left
        page.Stops[0].Box = new RectD(0.1, 0.5, 0.2, 0.2);
        Assert.True(PrintedWords.Suggest(page, 1.6).Box.X >= 0.5);
        Assert.Equal(0.9, PrintedWords.Suggest(page, 0.7).Box.W, 9);  // a single portrait page: right across
    }

    // ---------------------------------------------------------------- the switches

    [Fact]
    public void Both_switches_start_on_and_are_saved_under_their_own_names()
    {
        var s = new ReaderSettings();
        Assert.True(s.PrintedWords);
        Assert.True(s.PopUpWords);
        Assert.Contains("printedWords", ReaderSettings.DesktopOnlyKeys);
        Assert.Contains("popUpWords", ReaderSettings.DesktopOnlyKeys);

        ReaderSettings off = ReaderSettings.FromJsonString("""{ "settingsVersion": 2, "printedWords": false, "popUpWords": false }""");
        Assert.False(off.PrintedWords);
        Assert.False(off.PopUpWords);
        string json = off.ToJsonString();
        Assert.Contains("\"printedWords\": false", json);
        Assert.Contains("\"popUpWords\": false", json);
        Assert.False(ReaderSettings.FromJsonString(json).PrintedWords);
        // An older file without them gets them switched on.
        Assert.True(ReaderSettings.FromJsonString("""{ "settingsVersion": 2, "sound": false }""").PrintedWords);
        off.Reset();
        Assert.True(off.PopUpWords);
    }

    /// <summary>A spread whose words sit top left, with a stop far from them and a stop over the whole page.</summary>
    static Book Spread() => new()
    {
        Id = "s", Title = "S", FocusStyle = FocusStyles.Vignette,
        Pages =
        {
            new Page
            {
                Image = "p.jpg",
                Stops = { new Stop { Box = new RectD(0.6, 0.55, 0.3, 0.35), Caption = "Far from the words." }, new Stop { Box = new RectD(0, 0, 1, 1), Caption = "All of it." } },
                Text = new PageText { Box = new RectD(0.05, 0.05, 0.38, 0.2) },
            },
        },
    };

    [Fact]
    public void The_printed_words_count_as_seen_only_where_the_camera_shows_them()
    {
        Rig rig = Kit.Open(Spread());
        ReaderEngine e = rig.Engine;
        rig.Jump(0, Step.WholePage);
        Assert.True(e.State.PrintedWordsInView);                      // the whole page
        rig.Jump(0, 0);
        Assert.False(e.State.PrintedWordsInView);                     // zoomed in on the other corner
        rig.Jump(0, 1);
        Assert.True(e.State.PrintedWordsInView);                      // a stop over the whole page

        Rig off = Kit.Open(Spread(), s => s.PrintedWords = false);
        off.Jump(0, 1);
        Assert.False(off.Engine.State.PrintedWordsInView);            // switched off: nothing is printed

        Book plain = Spread();
        plain.Pages[0].Text = null;
        Rig none = Kit.Open(plain);
        none.Jump(0, 1);
        Assert.False(none.Engine.State.PrintedWordsInView);           // an old book prints nothing
    }

    [Fact]
    public void One_page_at_a_time_the_words_on_the_hidden_page_are_not_seen()
    {
        Rig rig = Kit.Open(Spread());
        rig.Engine.OnePage = true;
        rig.Engine.Refresh();
        rig.Jump(0, Step.WholePage);
        rig.RunUntilStill();
        // The words are on the left page: seen exactly when the left page is the one showing.
        int shown = rig.Engine.VisibleHalf();
        Assert.NotEqual(Step.Both, shown);
        Assert.Equal(shown == Step.LeftPage, rig.Engine.State.PrintedWordsInView);
    }

    // ---------------------------------------------------------------- Check book

    static BookProject Project(TempFolder temp, Page page)
    {
        string folder = temp["book"];
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "p.jpg"), File.ReadAllBytes(Path.Combine(Kit.BookFolder("biggest-puddle"), "page1.jpg")));
        page.Image = "p.jpg";
        var book = new Book { Id = "b", Title = "B", Pages = { page } };
        File.WriteAllText(Path.Combine(folder, "book.json"), BookJson.ToJsonString(book));
        return BookProject.Open(folder);
    }

    [Fact]
    public void Check_book_warns_when_the_words_overflow_their_box_run_off_the_picture_or_cross_the_crease()
    {
        using var temp = new TempFolder();
        var page = new Page { Stops = { new Stop { Box = new RectD(0.6, 0.5, 0.3, 0.3), Caption = "Moss had new boots." } }, Text = new PageText { Box = new RectD(0.05, 0.05, 0.4, 0.2) } };
        BookProject p = Project(temp, page);
        Page pg = p.Book.Pages[0];
        List<Issue> Words(Func<PageText, string, double, (double, double)> measure) => BookChecker.Check(p, measureWords: measure).Where(i => i.Kind == Spot.Words).ToList();

        Assert.Empty(Words((_, _, _) => (0.3, 0.1)));
        Assert.Contains(Words((_, _, _) => (0.3, 0.3)), i => i.Level == Severity.Warning && i.Text.Contains("don't fit their box") && i.Page == 0);
        Assert.Contains(Words((_, _, _) => (0.3, 0.99)), i => i.Text.Contains("run off the edge"));
        string seen = "";
        Words((t, content, aspect) => { seen = content; return (0.1, 0.1); });
        Assert.Equal("Moss had new boots.", seen);                    // measured with what will really be printed

        pg.Text!.Box = new RectD(0.3, 0.05, 0.4, 0.2);
        Assert.Contains(Words((_, _, _) => (0.3, 0.1)), i => i.Text.Contains("across the middle"));
        pg.Text.Box = new RectD(0.05, 0.05, 0.4, 0.2);
        pg.Text.Size = 0.02;
        Assert.Contains(Words((_, _, _) => (0.3, 0.1)), i => i.Text.Contains("small"));
        pg.Text.Size = null;
        pg.Stops[0].Caption = null;
        Assert.Contains(Words((_, _, _) => (0, 0)), i => i.Level == Severity.Tip && i.Text.Contains("nothing to print"));

        // Without the real letters, a rough estimate still catches a box far too small.
        pg.Text.Content = string.Join(" ", Enumerable.Repeat("Splash!", 60));
        Assert.Contains(BookChecker.Check(p), i => i.Kind == Spot.Words && i.Text.Contains("don't fit"));
    }

    [Fact]
    public void Export_keeps_the_words_box()
    {
        using var temp = new TempFolder();
        var page = new Page { Stops = { new Stop { Box = new RectD(0.6, 0.5, 0.3, 0.3), Caption = "Hi." } },
            Text = new PageText { Box = new RectD(0.05, 0.05, 0.4, 0.2), Align = PageText.Right, Size = 0.05, Color = "#334455", Content = "Hello." } };
        BookProject p = Project(temp, page);
        ExportResult r = Exporter.Export(p, temp["out"], ExportOptions.Originals with { BookFile = false });
        PageText t = BookJson.Load(r.Folder).Pages[0].Text!;
        Assert.Equal(new RectD(0.05, 0.05, 0.4, 0.2), t.Box);
        Assert.Equal((PageText.Right, 0.05, "#334455", "Hello."), (t.Align, t.Size!.Value, t.Color!, t.Content!));
    }

    [Fact]
    public void The_moss_story_keeps_every_words_box_on_one_page_of_its_spread_and_on_the_picture()
    {
        Book book = Kit.Load("biggest-puddle");
        Assert.True(book.Pages.Count(p => p.Text is not null) >= 6);
        foreach (Page p in book.Pages.Where(p => p.Text is not null))
        {
            RectD b = p.Text!.Box;
            Assert.True(b.X >= 0 && b.Y >= 0 && b.Right <= 1 && b.Bottom <= 1, p.Image);
            if (!p.Explore) Assert.True(b.Right <= 0.5 || b.X >= 0.5, p.Image + " crosses the crease");
            Assert.True(p.Text.SizeOrDefault >= BookChecker.SmallWords, p.Image);
            Assert.True(PrintedWords.Prints(p), p.Image);
        }
        // The page that hides a raindrop prints nothing, so its words can't give the surprise away.
        Assert.Null(book.Pages.First(p => !p.OpenWhole).Text);
    }
}
