using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Reader.Core;

/// <summary>
/// Reads and writes book.json. This is the same format the browser reader uses, and it has to stay
/// that way: <see cref="Parse(string)"/> follows normalizeBook in web/reader.js and
/// <see cref="Serialize"/> follows serializeBook, key for key.
/// </summary>
public static class BookJson
{
    /// <summary>Read a book from JSON text. Throws <see cref="JsonException"/> when the text isn't a book.</summary>
    public static Book Parse(string json)
    {
        JsonNode? root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        if (root is not JsonObject o) throw new JsonException("book.json should hold one object.");
        return Parse(o);
    }

    public static Book Parse(JsonObject raw)
    {
        var book = new Book
        {
            Id = Text(raw["id"]) ?? "",
            Title = Text(raw["title"]) ?? "",
            Author = Text(raw["author"]) ?? "",
            Cover = Text(raw["cover"]) ?? "",
            FocusStyle = NonEmpty(Text(raw["focusStyle"])) ?? FocusStyles.Dim,
        };
        foreach (JsonNode? p in Items(raw["pages"]))
        {
            if (p is JsonObject page) book.Pages.Add(ParsePage(page));
        }
        return book;
    }

    /// <summary>Read book.json from a book's folder.</summary>
    public static Book Load(string bookFolder) => Parse(File.ReadAllText(Path.Combine(bookFolder, "book.json")));

    static Page ParsePage(JsonObject p)
    {
        var page = new Page
        {
            Image = Text(p["image"]) ?? "",
            Explore = Text(p["mode"]) == "explore",
            OpenWhole = !IsFalse(p["openWhole"]),
        };

        if (p["stops"] is JsonArray stops)
        {
            foreach (JsonNode? s in stops) if (s is JsonObject stop) page.Stops.Add(ParseStop(stop));
        }
        else
        {
            // Older files list "panels" (plain boxes) instead of stops.
            foreach (JsonNode? b in Items(p["panels"])) page.Stops.Add(new Stop { Box = Box(b) });
        }

        foreach (JsonNode? h in Items(p["hotspots"]))
        {
            if (h is not JsonObject hot) continue;
            page.Hotspots.Add(new Hotspot
            {
                Box = Box(hot["box"]),
                Name = NonEmpty(Text(hot["name"])),
                Story = Whole(hot["story"]) ?? 0,
                Caption = NonEmpty(Text(hot["caption"])),
                Ask = NonEmpty(Text(hot["ask"])),
                Sound = NonEmpty(Text(hot["sound"])),
                Effect = NonEmpty(Text(hot["effect"])),
            });
        }

        if (p["guidance"] is JsonObject g)
        {
            page.Guidance = new Guidance { Hints = !IsFalse(g["hints"]), ShowMe = !IsFalse(g["showMe"]), Map = Truthy(g["map"]) };
        }

        foreach (JsonNode? e in Items(p["effects"]))
        {
            if (e is not JsonObject fx) continue;
            var effect = new PageEffect
            {
                Type = Text(fx["type"]) ?? "",
                Amount = Num(fx["amount"]),
                From = Whole(fx["from"]),
                To = Whole(fx["to"]),
                Ground = Num(fx["ground"]),
                Stop = Whole(fx["stop"]),
                Strength = Num(fx["strength"]),
            };
            foreach (JsonNode? b in Items(fx["dry"])) effect.Dry.Add(Box(b));
            page.Effects.Add(effect);
        }

        foreach (JsonNode? l in Items(p["layers"]))
        {
            if (l is not JsonObject layer) continue;
            page.Layers.Add(new Layer
            {
                Image = Text(layer["image"]) ?? "",
                Box = Box(layer["box"]),
                From = Whole(layer["from"]),
                To = Whole(layer["to"]),
                MoveTo = layer["moveTo"] is JsonObject ? Box(layer["moveTo"]) : null,
                MoveAt = Whole(layer["moveAt"]),
                MoveSeconds = Num(layer["moveSeconds"]),
                Path = NonEmpty(Text(layer["path"])),
                Idle = NonEmpty(Text(layer["idle"])),
            });
        }

        if (p["tiles"] is JsonObject t)
        {
            page.Tiles = new TileInfo
            {
                Width = Whole(t["width"]) ?? 0,
                Height = Whole(t["height"]) ?? 0,
                Size = Whole(t["size"]) ?? 0,
                Levels = Whole(t["levels"]) ?? 0,
                Path = Text(t["path"]) ?? "",
            };
        }
        return page;
    }

    static Stop ParseStop(JsonObject s) => new()
    {
        Box = Box(s["box"]),
        To = s["to"] is JsonObject ? Box(s["to"]) : null,
        PanSeconds = Num(s["panSeconds"]),
        Style = NonEmpty(Text(s["style"])),
        MoveSeconds = Num(s["moveSeconds"]),
        Auto = Truthy(s["auto"]),
        HoldSeconds = Num(s["holdSeconds"]),
        Caption = NonEmpty(Text(s["caption"])),
        Ask = NonEmpty(Text(s["ask"])),
        Sound = NonEmpty(Text(s["sound"])),
    };

    // ---------------------------------------------------------------- writing

    /// <summary>Clean JSON for saving: only known fields, rounded numbers, in the order the browser editor writes them.</summary>
    public static JsonObject Serialize(Book book)
    {
        var pages = new JsonArray();
        foreach (Page p in book.Pages) pages.Add((JsonNode)SerializePage(p));
        return new JsonObject
        {
            ["id"] = book.Id,
            ["title"] = book.Title,
            ["author"] = book.Author ?? "",
            ["cover"] = book.CoverOrFirstPage,
            ["focusStyle"] = string.IsNullOrEmpty(book.FocusStyle) ? FocusStyles.Dim : book.FocusStyle,
            ["pages"] = pages,
        };
    }

    /// <summary>The text to put in book.json: two-space indents, accents and curly quotes left as they are.</summary>
    public static string ToJsonString(Book book) => Serialize(book).ToJsonString(ReaderSettings.JsonOut);

    static JsonObject SerializePage(Page p)
    {
        var stops = new JsonArray();
        foreach (Stop s in p.Stops)
        {
            var o = new JsonObject { ["box"] = BoxJson(s.Box) };
            if (s.To is RectD to) { o["to"] = BoxJson(to); o["panSeconds"] = s.PanSeconds is > 0 or < 0 ? s.PanSeconds.Value : 4; }
            if (!string.IsNullOrEmpty(s.Style)) o["style"] = s.Style;
            if (s.MoveSeconds is double move) o["moveSeconds"] = move;
            if (s.Auto) o["auto"] = true;
            if (s.HoldSeconds is double hold && hold != 0) o["holdSeconds"] = hold;
            if (!string.IsNullOrEmpty(s.Caption)) o["caption"] = s.Caption;
            if (!string.IsNullOrEmpty(s.Ask)) o["ask"] = s.Ask;
            if (!string.IsNullOrEmpty(s.Sound)) o["sound"] = s.Sound;
            stops.Add((JsonNode)o);
        }
        var page = new JsonObject { ["image"] = p.Image, ["stops"] = stops };
        if (!p.OpenWhole) page["openWhole"] = false;

        if (p.Effects.Count > 0)
        {
            var effects = new JsonArray();
            foreach (PageEffect e in p.Effects)
            {
                if (e.Type == PageEffect.Lightning)
                {
                    effects.Add((JsonNode)new JsonObject { ["type"] = e.Type, ["stop"] = e.StopOrFirst });
                    continue;
                }
                if (e.Type == PageEffect.Shake)
                {
                    effects.Add((JsonNode)new JsonObject { ["type"] = e.Type, ["stop"] = e.StopOrFirst, ["strength"] = Round4(e.StrengthOrDefault) });
                    continue;
                }
                var o = new JsonObject { ["type"] = e.Type, ["amount"] = Round4(e.AmountOrDefault) };
                if (e.From is > 1) o["from"] = e.From.Value;
                if (e.To is >= 1) o["to"] = e.To.Value;
                if (e.Ground is double ground && ground != 0 && e.Type == PageEffect.Rain) o["ground"] = Round4(ground);
                if (e.Dry.Count > 0)
                {
                    var dry = new JsonArray();
                    foreach (RectD b in e.Dry) dry.Add((JsonNode)BoxJson(b));
                    o["dry"] = dry;
                }
                effects.Add((JsonNode)o);
            }
            page["effects"] = effects;
        }

        if (p.Layers.Count > 0)
        {
            var layers = new JsonArray();
            foreach (Layer l in p.Layers)
            {
                var o = new JsonObject { ["image"] = l.Image, ["box"] = BoxJson(l.Box) };
                if (l.From is > 1) o["from"] = l.From.Value;
                if (l.To is >= 1) o["to"] = l.To.Value;
                if (l.MoveTo is RectD moveTo)
                {
                    o["moveTo"] = BoxJson(moveTo);
                    o["moveAt"] = l.MoveAt is > 0 or < 0 ? l.MoveAt.Value : 1;
                    o["moveSeconds"] = l.MoveSeconds is > 0 or < 0 ? l.MoveSeconds.Value : 1.2;
                    if (l.Path == "hop") o["path"] = "hop";
                }
                if (l.Idle is "bob" or "sway") o["idle"] = l.Idle;
                layers.Add((JsonNode)o);
            }
            page["layers"] = layers;
        }

        if (p.Tiles is TileInfo t)
        {
            page["tiles"] = new JsonObject
            {
                ["width"] = t.Width, ["height"] = t.Height, ["size"] = t.Size, ["levels"] = t.Levels, ["path"] = t.Path,
            };
        }

        if (p.Explore)
        {
            page["mode"] = "explore";
            var hotspots = new JsonArray();
            foreach (Hotspot h in p.Hotspots)
            {
                var o = new JsonObject { ["box"] = BoxJson(h.Box) };
                if (!string.IsNullOrEmpty(h.Name)) o["name"] = h.Name;
                if (h.Story != 0) o["story"] = h.Story;
                if (!string.IsNullOrEmpty(h.Caption)) o["caption"] = h.Caption;
                if (!string.IsNullOrEmpty(h.Ask)) o["ask"] = h.Ask;
                if (!string.IsNullOrEmpty(h.Sound)) o["sound"] = h.Sound;
                if (!string.IsNullOrEmpty(h.Effect) && h.Effect != "sparkle") o["effect"] = h.Effect;
                hotspots.Add((JsonNode)o);
            }
            page["hotspots"] = hotspots;
            page["guidance"] = new JsonObject { ["hints"] = p.Guidance.Hints, ["showMe"] = p.Guidance.ShowMe, ["map"] = p.Guidance.Map };
        }
        return page;
    }

    /// <summary>Four decimal places, rounding halves up the way the browser does.</summary>
    public static double Round4(double v) => Math.Floor(v * 10000 + 0.5) / 10000;

    static JsonObject BoxJson(RectD b) => new() { ["x"] = Round4(b.X), ["y"] = Round4(b.Y), ["w"] = Round4(b.W), ["h"] = Round4(b.H) };

    // ---------------------------------------------------------------- small helpers for forgiving reads

    static IEnumerable<JsonNode?> Items(JsonNode? n) => n as JsonArray ?? Enumerable.Empty<JsonNode?>();

    static RectD Box(JsonNode? n) =>
        n is JsonObject b ? new RectD(Num(b["x"]) ?? 0, Num(b["y"]) ?? 0, Num(b["w"]) ?? 0, Num(b["h"]) ?? 0) : default;

    /// <summary>A number, or a number written as text (hand-edited files do that). Empty text counts as not set.</summary>
    static double? Num(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue(out double d)) return double.IsFinite(d) ? d : null;
        if (v.TryGetValue(out string? s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && double.IsFinite(d)) return d;
        return null;
    }

    static int? Whole(JsonNode? n) => Num(n) is double d ? (int)Math.Round(d) : null;

    static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    static string? NonEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    static bool IsFalse(JsonNode? n) => n is JsonValue v && v.TryGetValue(out bool b) && !b;

    /// <summary>True, a number that isn't zero, or text that isn't empty.</summary>
    static bool Truthy(JsonNode? n)
    {
        if (n is not JsonValue v) return n is not null;
        if (v.TryGetValue(out bool b)) return b;
        if (v.TryGetValue(out double d)) return d != 0;
        if (v.TryGetValue(out string? s)) return !string.IsNullOrEmpty(s);
        return true;
    }
}
