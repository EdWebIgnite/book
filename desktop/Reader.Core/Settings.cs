using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Reader.Core;

/// <summary>
/// Everything a parent or a tester can change about how the reader moves and behaves.
/// The names and starting values match DEFAULT_SETTINGS in web/reader.js, and the saved JSON
/// uses the same keys, so settings copied from the browser playground can be pasted here.
/// </summary>
public sealed class ReaderSettings
{
    public double MoveMs { get; set; } = 1400;
    public string Easing { get; set; } = Easings.EaseInOutSine;
    public string ZoomPath { get; set; } = "smooth";          // smooth | straight
    public string PageTurn { get; set; } = "fly";             // fly | zoomOut | cut
    public double ArcAmount { get; set; } = 0.6;
    public double Padding { get; set; } = 0.025;
    public double Dim { get; set; } = 0.72;
    public double Vignette { get; set; } = 0.75;
    /// <summary>How dark it gets outside the light on a "spotlight" stop.</summary>
    public double Spotlight { get; set; } = 0.97;
    public bool WholePageFirst { get; set; } = true;
    public bool WholePageLast { get; set; }
    public string Drift { get; set; } = "off";                // off | zoomIn | pan
    public double DriftAmount { get; set; } = 0.06;
    public double DriftSeconds { get; set; } = 6;
    public bool AutoPlay { get; set; }
    public double AutoSeconds { get; set; } = 4;
    public double HintSeconds { get; set; } = 4;
    public double Glide { get; set; } = 0.5;

    // For parents
    /// <summary>Which age preset was last applied.</summary>
    public string Age { get; set; } = "3-5";
    /// <summary>auto (follow the device) | on | off</summary>
    public string ReduceMotion { get; set; } = "auto";
    public bool Sound { get; set; } = true;
    /// <summary>Things to find that aren't part of the story: on | afterStory | off</summary>
    public string Extras { get; set; } = "afterStory";
    /// <summary>Show "ask" questions for the adult.</summary>
    public bool Prompts { get; set; }
    public string CaptionSize { get; set; } = "medium";       // small | medium | large
    /// <summary>The gear needs a two-second hold.</summary>
    public bool Gate { get; set; }

    // Fine tuning
    /// <summary>Longer moves take longer.</summary>
    public bool ScaleTime { get; set; } = true;
    /// <summary>Smallest touch target for things to find.</summary>
    public double MinTargetCm { get; set; } = 2.4;
    /// <summary>Young children double-tap by accident, so this starts off.</summary>
    public bool DoubleTapZoom { get; set; }

    public ReaderSettings Clone()
    {
        var copy = new ReaderSettings();
        copy.CopyFrom(this);
        return copy;
    }

    /// <summary>Take every value from another set, keeping this object (the reader holds on to it).</summary>
    public void CopyFrom(ReaderSettings other) => ApplyJson(other.ToJson());

    /// <summary>Back to how a fresh install starts.</summary>
    public void Reset() => CopyFrom(new ReaderSettings());

    /// <summary>What each age band changes. Research basis: docs/movement-ideas.md.</summary>
    public static readonly IReadOnlyDictionary<string, AgePreset> AgePresets = new Dictionary<string, AgePreset>
    {
        ["3-5"] = new(1400, 4, 2.4, false, Easings.EaseInOutSine),
        ["6-8"] = new(700, 6, 2.0, false, Easings.EaseInOutCubic),
        ["9+"] = new(550, 9, 1.4, true, Easings.EaseInOutCubic),
    };

    /// <summary>An age band sets several things at once. An unknown band changes nothing but the label.</summary>
    public void ApplyAgePreset(string age)
    {
        Age = age;
        if (!AgePresets.TryGetValue(age, out var p)) return;
        MoveMs = p.MoveMs;
        HintSeconds = p.HintSeconds;
        MinTargetCm = p.MinTargetCm;
        DoubleTapZoom = p.DoubleTapZoom;
        Easing = p.Easing;
    }

    /// <summary>The settings as JSON, with the same keys and order as the browser playground's "Copy settings".</summary>
    public JsonObject ToJson() => new()
    {
        ["moveMs"] = MoveMs,
        ["easing"] = Easing,
        ["zoomPath"] = ZoomPath,
        ["pageTurn"] = PageTurn,
        ["arcAmount"] = ArcAmount,
        ["padding"] = Padding,
        ["dim"] = Dim,
        ["vignette"] = Vignette,
        ["spotlight"] = Spotlight,
        ["wholePageFirst"] = WholePageFirst,
        ["wholePageLast"] = WholePageLast,
        ["drift"] = Drift,
        ["driftAmount"] = DriftAmount,
        ["driftSeconds"] = DriftSeconds,
        ["autoPlay"] = AutoPlay,
        ["autoSeconds"] = AutoSeconds,
        ["hintSeconds"] = HintSeconds,
        ["glide"] = Glide,
        ["age"] = Age,
        ["reduceMotion"] = ReduceMotion,
        ["sound"] = Sound,
        ["extras"] = Extras,
        ["prompts"] = Prompts,
        ["captionSize"] = CaptionSize,
        ["gate"] = Gate,
        ["scaleTime"] = ScaleTime,
        ["minTargetCm"] = MinTargetCm,
        ["doubleTapZoom"] = DoubleTapZoom,
    };

    public string ToJsonString() => ToJson().ToJsonString(JsonOut);

    internal static readonly JsonSerializerOptions JsonOut = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // keep curly quotes and accents readable
    };

    /// <summary>
    /// Take the values present in <paramref name="saved"/>. Anything missing or of the wrong kind keeps
    /// its current value, so an old or hand-edited file can never break the reader.
    /// </summary>
    public void ApplyJson(JsonObject saved)
    {
        // The focus padding had a longer name in early versions.
        if (saved["padding"] is null && saved["panelPadding"] is JsonNode old) Padding = Num(old, Padding);

        MoveMs = Num(saved["moveMs"], MoveMs);
        Easing = Text(saved["easing"], Easing);
        ZoomPath = Text(saved["zoomPath"], ZoomPath);
        PageTurn = Text(saved["pageTurn"], PageTurn);
        ArcAmount = Num(saved["arcAmount"], ArcAmount);
        Padding = Num(saved["padding"], Padding);
        Dim = Num(saved["dim"], Dim);
        Vignette = Num(saved["vignette"], Vignette);
        Spotlight = Num(saved["spotlight"], Spotlight);
        WholePageFirst = Flag(saved["wholePageFirst"], WholePageFirst);
        WholePageLast = Flag(saved["wholePageLast"], WholePageLast);
        Drift = Text(saved["drift"], Drift);
        DriftAmount = Num(saved["driftAmount"], DriftAmount);
        DriftSeconds = Num(saved["driftSeconds"], DriftSeconds);
        AutoPlay = Flag(saved["autoPlay"], AutoPlay);
        AutoSeconds = Num(saved["autoSeconds"], AutoSeconds);
        HintSeconds = Num(saved["hintSeconds"], HintSeconds);
        Glide = Num(saved["glide"], Glide);
        Age = Text(saved["age"], Age);
        ReduceMotion = Text(saved["reduceMotion"], ReduceMotion);
        Sound = Flag(saved["sound"], Sound);
        Extras = Text(saved["extras"], Extras);
        Prompts = Flag(saved["prompts"], Prompts);
        CaptionSize = Text(saved["captionSize"], CaptionSize);
        Gate = Flag(saved["gate"], Gate);
        ScaleTime = Flag(saved["scaleTime"], ScaleTime);
        MinTargetCm = Num(saved["minTargetCm"], MinTargetCm);
        DoubleTapZoom = Flag(saved["doubleTapZoom"], DoubleTapZoom);
    }

    /// <summary>Defaults with the values from a JSON text laid over them. Text that isn't JSON gives the defaults.</summary>
    public static ReaderSettings FromJsonString(string? json)
    {
        var s = new ReaderSettings();
        if (string.IsNullOrWhiteSpace(json)) return s;
        try
        {
            if (JsonNode.Parse(json) is JsonObject o) s.ApplyJson(o);
        }
        catch (JsonException) { /* damaged file: use defaults */ }
        return s;
    }

    static double Num(JsonNode? n, double fallback)
    {
        if (n is JsonValue v)
        {
            if (v.TryGetValue(out double d) && double.IsFinite(d)) return d;
            if (v.TryGetValue(out string? s) && double.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
        }
        return fallback;
    }

    static string Text(JsonNode? n, string fallback) =>
        n is JsonValue v && v.TryGetValue(out string? s) && s is not null ? s : fallback;

    static bool Flag(JsonNode? n, bool fallback) =>
        n is JsonValue v && v.TryGetValue(out bool b) ? b : fallback;
}

/// <summary>The handful of settings an age band decides.</summary>
public sealed record AgePreset(double MoveMs, double HintSeconds, double MinTargetCm, bool DoubleTapZoom, string Easing);

/// <summary>Keeps the settings between runs, as a small JSON file in the user's own app-data folder.</summary>
public static class SettingsStore
{
    /// <summary>
    /// Where the settings live: %APPDATA%\BookReader\settings.json on Windows,
    /// ~/.config/BookReader/settings.json on Linux, ~/Library/Application Support on a Mac.
    /// </summary>
    public static string DefaultPath
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            if (string.IsNullOrEmpty(root)) root = Path.GetTempPath();
            return Path.Combine(root, "BookReader", "settings.json");
        }
    }

    public static ReaderSettings Load(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            return File.Exists(path) ? ReaderSettings.FromJsonString(File.ReadAllText(path)) : new ReaderSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new ReaderSettings();   // storage blocked: use defaults
        }
    }

    /// <summary>Returns false when the file could not be written. The reader carries on either way.</summary>
    public static bool Save(ReaderSettings settings, string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, settings.ToJsonString());
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
