using System.Text.Json.Nodes;
using Reader.Core;

namespace Reader.Tests;

public class BookJsonTests
{
    public static IEnumerable<object[]> Books() => Kit.BookIds().Select(id => new object[] { id });

    [Fact]
    public void The_library_lists_the_four_sample_books()
    {
        Assert.Equal(new[] { "biggest-puddle", "captain-megawatt", "deep-sea", "waterhole" }, Kit.BookIds().OrderBy(x => x));
    }

    [Theory]
    [MemberData(nameof(Books))]
    public void Saving_a_sample_book_writes_what_the_browser_reader_writes(string id)
    {
        JsonNode browser = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", id + ".browser.json")))!;
        JsonNode desktop = BookJson.Serialize(Kit.Load(id));
        AssertSameJson(browser, desktop, "$");
    }

    [Theory]
    [MemberData(nameof(Books))]
    public void A_saved_book_loads_back_the_same(string id)
    {
        Book first = Kit.Load(id);
        string text = BookJson.ToJsonString(first);
        Book second = BookJson.Parse(text);
        Assert.Equal(text, BookJson.ToJsonString(second));
        Assert.Equal(first.Pages.Count, second.Pages.Count);
        Assert.Equal(first.Title, second.Title);
    }

    [Theory]
    [MemberData(nameof(Books))]
    public void Every_page_picture_named_in_a_sample_book_exists(string id)
    {
        Book book = Kit.Load(id);
        Assert.NotEmpty(book.Pages);
        foreach (Page p in book.Pages) Assert.True(File.Exists(Path.Combine(Kit.BookFolder(id), p.Image)), p.Image);
        Assert.True(File.Exists(Path.Combine(Kit.BookFolder(id), book.CoverOrFirstPage)));
    }

    [Fact]
    public void Older_files_with_panels_still_load_and_each_panel_becomes_a_stop()
    {
        Book book = BookJson.Parse("""
            { "id": "old", "title": "Old", "pages": [
              { "image": "p1.jpg", "panels": [ { "x": 0.1, "y": 0.2, "w": 0.3, "h": 0.4 }, { "x": 0.5, "y": 0.5, "w": 0.4, "h": 0.4 } ] } ] }
            """);
        Assert.Equal(2, book.Pages[0].Stops.Count);
        Assert.Equal(new RectD(0.1, 0.2, 0.3, 0.4), book.Pages[0].Stops[0].Box);
        // It is saved in today's format.
        JsonNode saved = BookJson.Serialize(book);
        Assert.Null(saved["pages"]![0]!["panels"]);
        Assert.Equal(2, saved["pages"]![0]!["stops"]!.AsArray().Count);
    }

    [Fact]
    public void Missing_parts_get_the_same_defaults_as_the_browser()
    {
        Book book = BookJson.Parse("""{ "id": "b", "title": "B", "pages": [ { "image": "p1.jpg" }, { "image": "p2.jpg", "mode": "explore" } ] }""");
        Assert.Equal("dim", book.FocusStyle);
        Assert.False(book.Pages[0].Explore);
        Assert.True(book.Pages[0].OpenWhole);
        Assert.Empty(book.Pages[0].Stops);
        Assert.True(book.Pages[1].Explore);
        Assert.True(book.Pages[1].Guidance.Hints);
        Assert.True(book.Pages[1].Guidance.ShowMe);
        Assert.False(book.Pages[1].Guidance.Map);
        Assert.Null(book.Pages[1].Tiles);

        JsonNode saved = BookJson.Serialize(book);
        Assert.Equal("p1.jpg", saved["cover"]!.GetValue<string>());       // no cover: the first page stands in
        Assert.Equal("", saved["author"]!.GetValue<string>());
        Assert.Null(saved["pages"]![0]!["mode"]);
        Assert.Equal("explore", saved["pages"]![1]!["mode"]!.GetValue<string>());
    }

    [Fact]
    public void Every_field_of_the_format_survives_a_round_trip()
    {
        const string json = """
            { "id": "all", "title": "All", "author": "A", "cover": "c.jpg", "focusStyle": "spotlight", "pages": [
              { "image": "p1.jpg",
                "stops": [
                  { "box": { "x": 0.1, "y": 0.2, "w": 0.3, "h": 0.4 }, "to": { "x": 0.2, "y": 0.2, "w": 0.3, "h": 0.4 }, "panSeconds": 3,
                    "style": "none", "moveSeconds": 0, "auto": true, "holdSeconds": 1.5, "caption": "Hello", "ask": "Why?", "sound": "a.mp3" } ],
                "openWhole": false,
                "effects": [
                  { "type": "rain", "amount": 0.7, "from": 2, "to": 3, "ground": 0.62, "dry": [ { "x": 0, "y": 0, "w": 0.5, "h": 0.5 } ] },
                  { "type": "snow", "amount": 0.3 },
                  { "type": "lightning", "stop": 1 },
                  { "type": "shake", "stop": 2, "strength": 0.4 } ],
                "layers": [
                  { "image": "boat.png", "box": { "x": 0.1, "y": 0.1, "w": 0.2, "h": 0.2 }, "from": 2, "to": 3,
                    "moveTo": { "x": 0.6, "y": 0.1, "w": 0.2, "h": 0.2 }, "moveAt": 2, "moveSeconds": 1.5, "path": "hop", "idle": "sway" } ] },
              { "image": "p2.jpg",
                "stops": [],
                "tiles": { "width": 8192, "height": 6144, "size": 1024, "levels": 2, "path": "t/{z}/{x}_{y}.jpg" },
                "mode": "explore",
                "hotspots": [
                  { "box": { "x": 0.5, "y": 0.5, "w": 0.1, "h": 0.1 }, "name": "Old Turtle", "story": 1, "caption": "Hi", "ask": "Who?", "sound": "t.wav", "effect": "none" } ],
                "guidance": { "hints": false, "showMe": true, "map": true } } ] }
            """;
        Book book = BookJson.Parse(json);
        Stop stop = book.Pages[0].Stops[0];
        Assert.Equal(0, stop.MoveSeconds);
        Assert.True(stop.Auto);
        Assert.Equal("hop", book.Pages[0].Layers[0].Path);
        Assert.Equal(1, book.Pages[1].Hotspots[0].Story);
        Assert.False(book.Pages[1].Guidance.Hints);
        Assert.Equal("t/1/2_3.jpg", book.Pages[1].Tiles!.PathFor(1, 2, 3));
        AssertSameJson(JsonNode.Parse(json)!, BookJson.Serialize(book), "$");
    }

    [Fact]
    public void Boxes_are_rounded_to_four_places_when_saved()
    {
        Book book = Kit.OnePage(new Stop { Box = new RectD(0.123456, 0.00005, 1.0 / 3, 0.99995) });
        JsonNode box = BookJson.Serialize(book)["pages"]![0]!["stops"]![0]!["box"]!;
        Assert.Equal("""{"x":0.1235,"y":0.0001,"w":0.3333,"h":1}""", box.ToJsonString());
    }

    static double Number(JsonValue v) => double.Parse(v.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The same keys in the same order, the same kinds of value, and numbers equal to within a hair.</summary>
    static void AssertSameJson(JsonNode? expected, JsonNode? actual, string path)
    {
        switch (expected)
        {
            case null:
                Assert.True(actual is null, path + ": expected null");
                break;
            case JsonObject eo:
                var ao = Assert.IsType<JsonObject>(actual);
                Assert.True(eo.Select(p => p.Key).SequenceEqual(ao.Select(p => p.Key)),
                    $"{path}: keys differ. Expected [{string.Join(", ", eo.Select(p => p.Key))}] but got [{string.Join(", ", ao.Select(p => p.Key))}]");
                foreach (var (key, value) in eo) AssertSameJson(value, ao[key], path + "." + key);
                break;
            case JsonArray ea:
                var aa = Assert.IsType<JsonArray>(actual);
                Assert.True(ea.Count == aa.Count, $"{path}: expected {ea.Count} items but got {aa.Count}");
                for (int i = 0; i < ea.Count; i++) AssertSameJson(ea[i], aa[i], $"{path}[{i}]");
                break;
            default:
                var ev = expected.AsValue();
                Assert.True(actual is JsonValue, path + ": expected a plain value");
                var av = actual!.AsValue();
                Assert.True(ev.GetValueKind() == av.GetValueKind(), $"{path}: expected {ev.GetValueKind()} but got {av.GetValueKind()}");
                if (ev.GetValueKind() == System.Text.Json.JsonValueKind.Number)
                    Assert.True(Math.Abs(Number(ev) - Number(av)) < 1e-9, $"{path}: expected {ev} but got {av}");
                else
                    Assert.True(ev.ToJsonString() == av.ToJsonString(), $"{path}: expected {ev.ToJsonString()} but got {av.ToJsonString()}");
                break;
        }
    }
}
