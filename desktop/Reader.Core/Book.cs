namespace Reader.Core;

/*
 * A book in memory. The file format (book.json) is described at the top of web/reader.js and in the
 * repo README; BookJson reads and writes it. Boxes are fractions of the page image: X and Y are the
 * top-left corner, W and H the size, so 0.5 means halfway across or down.
 *
 * These are plain classes that can be changed freely, because the book editor sits on top of them.
 *
 * Fields this program does not know about are kept in Extra when a book is read and written back
 * when it is saved, so a book made by a newer editor (or edited by hand) loses nothing here.
 */

public static class FocusStyles
{
    public const string Dim = "dim";
    public const string Vignette = "vignette";
    public const string Spotlight = "spotlight";
    public const string None = "none";
}

public sealed class Book
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Cover { get; set; } = "";
    /// <summary>What happens around a stop unless the stop says otherwise: dim, vignette, spotlight or none.</summary>
    public string FocusStyle { get; set; } = FocusStyles.Dim;
    public List<Page> Pages { get; set; } = new();
    /// <summary>Fields in book.json this program doesn't know, kept as they were.</summary>
    public System.Text.Json.Nodes.JsonObject? Extra { get; set; }

    /// <summary>The picture for the shelf: the cover, or the first page when there isn't one.</summary>
    public string CoverOrFirstPage => !string.IsNullOrEmpty(Cover) ? Cover : Pages.Count > 0 ? Pages[0].Image : "";
}

public sealed class Page
{
    public string Image { get; set; } = "";
    /// <summary>Explore pages hand the camera to the child. Guided pages go from stop to stop.</summary>
    public bool Explore { get; set; }
    /// <summary>False goes straight to the first stop instead of showing the whole page first (keeps a surprise hidden).</summary>
    public bool OpenWhole { get; set; } = true;
    /// <summary>On explore pages these play first as a guided intro.</summary>
    public List<Stop> Stops { get; set; } = new();
    /// <summary>Explore pages: things to find and tap.</summary>
    public List<Hotspot> Hotspots { get; set; } = new();
    public Guidance Guidance { get; set; } = new();
    public List<PageEffect> Effects { get; set; } = new();
    public List<Layer> Layers { get; set; } = new();
    /// <summary>For very big scenes: sharp tiles that load only where the child is looking.</summary>
    public TileInfo? Tiles { get; set; }
    /// <summary>
    /// Editor only: the separate left-hand and right-hand page pictures this page's spread was joined
    /// from. The readers never look at it; they show <see cref="Image"/>, which is the joined picture.
    /// </summary>
    public SpreadSource? Spread { get; set; }
    public System.Text.Json.Nodes.JsonObject? Extra { get; set; }
}

/// <summary>The two page pictures a spread was made from, in the book's folder (see <see cref="Page.Spread"/>).</summary>
public sealed class SpreadSource
{
    public string Left { get; set; } = "";
    public string Right { get; set; } = "";
}

/// <summary>
/// A recorded voice reading a stop's words aloud, with when each word starts so the words can light up
/// as they are read. Times are seconds from the start of the sound file.
/// </summary>
public sealed class Voice
{
    /// <summary>A sound file in the book's folder (wav, mp3 or ogg).</summary>
    public string File { get; set; } = "";
    /// <summary>Play from here (a trim at the start). 0 when not set.</summary>
    public double? Start { get; set; }
    /// <summary>Stop playing here (a trim at the end). The end of the file when not set.</summary>
    public double? End { get; set; }
    /// <summary>When each word of the caption starts, in order. Fewer times than words lights up only those words.</summary>
    public List<double> Words { get; set; } = new();

    public double StartOrZero => Start is > 0 ? Start.Value : 0;
}

public sealed class Stop
{
    public RectD Box { get; set; }
    /// <summary>Optional: glide from Box to this box while resting.</summary>
    public RectD? To { get; set; }
    /// <summary>How long that glide takes. Four seconds when not set.</summary>
    public double? PanSeconds { get; set; }
    /// <summary>Optional: overrides the book's focus style for this stop.</summary>
    public string? Style { get; set; }
    /// <summary>Optional: how long the camera takes to get to this stop (0 = cut).</summary>
    public double? MoveSeconds { get; set; }
    /// <summary>Go on to the next stop without a tap...</summary>
    public bool Auto { get; set; }
    /// <summary>...after resting here this long (also the rest time in watch mode).</summary>
    public double? HoldSeconds { get; set; }
    public string? Caption { get; set; }
    /// <summary>A question for the adult reading along.</summary>
    public string? Ask { get; set; }
    /// <summary>A sound file in the book folder, played on arriving.</summary>
    public string? Sound { get; set; }
    /// <summary>Optional: the caption read aloud, played on arriving when "Read to me" is on.</summary>
    public Voice? Voice { get; set; }
    public System.Text.Json.Nodes.JsonObject? Extra { get; set; }
}

public sealed class Hotspot
{
    public RectD Box { get; set; }
    /// <summary>Used in "First find Old Turtle" when a later story step is tapped too early.</summary>
    public string? Name { get; set; }
    /// <summary>0 for an extra. Otherwise this is that step of the story hidden in the scene.</summary>
    public int Story { get; set; }
    public string? Caption { get; set; }
    public string? Ask { get; set; }
    public string? Sound { get; set; }
    /// <summary>"none" for words without the sparkle. Anything else sparkles.</summary>
    public string? Effect { get; set; }
    public System.Text.Json.Nodes.JsonObject? Extra { get; set; }
}

/// <summary>Help for the child on an explore page.</summary>
public sealed class Guidance
{
    public bool Hints { get; set; } = true;
    public bool ShowMe { get; set; } = true;
    public bool Map { get; set; }
    public System.Text.Json.Nodes.JsonObject? Extra { get; set; }
}

/// <summary>Something the reader draws moving over the picture, or does on arriving at a stop.</summary>
public sealed class PageEffect
{
    public const string Rain = "rain", Snow = "snow", Leaves = "leaves", Sparkles = "sparkles";
    public const string Lightning = "lightning", Shake = "shake";

    public string Type { get; set; } = "";
    /// <summary>How much, 0 to 1. 0.6 when not set.</summary>
    public double? Amount { get; set; }
    /// <summary>Runs from this stop (1 = first)...</summary>
    public int? From { get; set; }
    /// <summary>...to this one.</summary>
    public int? To { get; set; }
    /// <summary>Rain only: splash rings below this line (fraction of the page height).</summary>
    public double? Ground { get; set; }
    /// <summary>Areas it misses (under a roof).</summary>
    public List<RectD> Dry { get; set; } = new();
    /// <summary>Lightning and shake: the stop they happen at (1 = first).</summary>
    public int? Stop { get; set; }
    /// <summary>Shake only. 0.6 when not set.</summary>
    public double? Strength { get; set; }
    public System.Text.Json.Nodes.JsonObject? Extra { get; set; }

    /// <summary>Effects made of many small moving pieces.</summary>
    public bool IsFalling => Type is Rain or Snow or Leaves or Sparkles;

    public double AmountOrDefault => Amount ?? 0.6;
    public double StrengthOrDefault => Strength ?? 0.6;
    public int StopOrFirst => Stop is > 0 ? Stop.Value : 1;
}

/// <summary>A picture with a see-through background placed on top of the page.</summary>
public sealed class Layer
{
    public string Image { get; set; } = "";
    public RectD Box { get; set; }
    /// <summary>Only shown between these stops (1 = first).</summary>
    public int? From { get; set; }
    public int? To { get; set; }
    /// <summary>Moves to this box when stop MoveAt arrives...</summary>
    public RectD? MoveTo { get; set; }
    public int? MoveAt { get; set; }
    /// <summary>...taking this long (1.2 seconds when not set), straight or in a "hop".</summary>
    public double? MoveSeconds { get; set; }
    public string? Path { get; set; }
    /// <summary>A small movement while it waits: "bob" or "sway".</summary>
    public string? Idle { get; set; }
    public System.Text.Json.Nodes.JsonObject? Extra { get; set; }

    public int MoveAtOrFirst => MoveAt is > 0 ? MoveAt.Value : 1;
    public double MoveSecondsOrDefault => MoveSeconds is > 0 ? MoveSeconds.Value : 1.2;
}

/// <summary>
/// A big scene cut into tiles. The page's own image is then a small overview of the whole scene.
/// Level 0 is full size and each level after is half the one before.
/// </summary>
public sealed class TileInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Size { get; set; }
    public int Levels { get; set; }
    /// <summary>For example "page1_tiles/{z}/{x}_{y}.jpg".</summary>
    public string Path { get; set; } = "";

    /// <summary>The file for one tile, relative to the book's folder.</summary>
    public string PathFor(int level, int x, int y) =>
        Path.Replace("{z}", level.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{x}", x.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{y}", y.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
