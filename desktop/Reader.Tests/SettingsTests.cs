using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Reader.Core;

namespace Reader.Tests;

public class SettingsTests
{
    /// <summary>Pull a `const NAME = { ... };` block out of web/reader.js, without its comments.</summary>
    static string JsBlock(string name)
    {
        string js = File.ReadAllText(Path.Combine(Kit.RepoRoot, "web", "reader.js"));
        Match m = Regex.Match(js, @"const " + name + @" = \{(.*?)\n  \};", RegexOptions.Singleline);
        Assert.True(m.Success, name + " not found in web/reader.js");
        return Regex.Replace(m.Groups[1].Value, @"//[^\n]*", "");
    }

    static JsonNode JsValue(string text)
    {
        text = text.Trim();
        if (text is "true" or "false") return JsonValue.Create(text == "true");
        if (text.StartsWith('"')) return JsonValue.Create(text.Trim('"'));
        return JsonValue.Create(double.Parse(text, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Defaults_match_DEFAULT_SETTINGS_in_the_browser_reader()
    {
        JsonObject ours = new ReaderSettings().ToJson();
        var theirs = Regex.Matches(JsBlock("DEFAULT_SETTINGS"), @"(\w+):\s*(""[^""]*""|[\w.\-]+)\s*,");
        Assert.Equal(28, theirs.Count);
        Assert.Equal(theirs.Select(m => m.Groups[1].Value), ours.Select(p => p.Key));
        foreach (Match m in theirs)
        {
            string key = m.Groups[1].Value;
            Assert.True(JsValue(m.Groups[2].Value).ToJsonString() == ours[key]!.ToJsonString(),
                $"{key}: reader.js has {m.Groups[2].Value}, the desktop has {ours[key]!.ToJsonString()}");
        }
    }

    [Fact]
    public void Age_presets_match_AGE_PRESETS_in_the_browser_reader()
    {
        var bands = Regex.Matches(JsBlock("AGE_PRESETS"), @"""([^""]+)"":\s*\{([^}]*)\}");
        Assert.Equal(3, bands.Count);
        foreach (Match band in bands)
        {
            var s = new ReaderSettings { MoveMs = -1, HintSeconds = -1, MinTargetCm = -1, DoubleTapZoom = band.Groups[1].Value != "9+", Easing = "?" };
            s.ApplyAgePreset(band.Groups[1].Value);
            JsonObject ours = s.ToJson();
            Assert.Equal(band.Groups[1].Value, s.Age);
            var fields = Regex.Matches(band.Groups[2].Value, @"(\w+):\s*(""[^""]*""|[\w.\-]+)");
            Assert.Equal(5, fields.Count);
            foreach (Match f in fields)
                Assert.True(JsValue(f.Groups[2].Value).ToJsonString() == ours[f.Groups[1].Value]!.ToJsonString(), band.Groups[1].Value + " " + f.Groups[1].Value);
        }
    }

    [Fact]
    public void The_defaults_named_in_the_plan_are_what_a_fresh_install_gets()
    {
        var s = new ReaderSettings();
        Assert.Equal(1400, s.MoveMs);
        Assert.Equal("easeInOutSine", s.Easing);
        Assert.Equal("3-5", s.Age);
        Assert.Equal(4, s.HintSeconds);
        Assert.Equal("afterStory", s.Extras);
        Assert.False(s.Prompts);
        Assert.Equal(2.4, s.MinTargetCm);
        Assert.Equal(0.97, s.Spotlight);
    }

    [Fact]
    public void Settings_are_kept_between_runs()
    {
        string path = Path.Combine(Path.GetTempPath(), "reader-tests-" + Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            var s = new ReaderSettings { MoveMs = 900, Prompts = true, Extras = "on", CaptionSize = "large" };
            Assert.True(SettingsStore.Save(s, path));
            ReaderSettings back = SettingsStore.Load(path);
            Assert.Equal(s.ToJsonString(), back.ToJsonString());
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void A_missing_or_damaged_file_gives_the_defaults()
    {
        string defaults = new ReaderSettings().ToJsonString();
        Assert.Equal(defaults, SettingsStore.Load(Path.Combine(Path.GetTempPath(), "no-such-folder-" + Guid.NewGuid().ToString("N"), "x.json")).ToJsonString());
        Assert.Equal(defaults, ReaderSettings.FromJsonString("{ not json").ToJsonString());
        Assert.Equal(defaults, ReaderSettings.FromJsonString("[1, 2]").ToJsonString());
        // A value of the wrong kind is skipped; the rest still load.
        ReaderSettings s = ReaderSettings.FromJsonString("""{ "moveMs": "fast", "sound": 3, "glide": 0.9, "somethingNew": 1 }""");
        Assert.Equal(1400, s.MoveMs);
        Assert.True(s.Sound);
        Assert.Equal(0.9, s.Glide);
    }

    [Fact]
    public void The_old_name_for_padding_still_loads()
    {
        Assert.Equal(0.05, ReaderSettings.FromJsonString("""{ "panelPadding": 0.05 }""").Padding);
        Assert.Equal(0.01, ReaderSettings.FromJsonString("""{ "panelPadding": 0.05, "padding": 0.01 }""").Padding);
    }

    [Fact]
    public void Reset_goes_back_to_the_defaults_in_place()
    {
        var s = new ReaderSettings { MoveMs = 200, Age = "9+", Gate = true };
        ReaderSettings same = s;
        s.Reset();
        Assert.Same(same, s);
        Assert.Equal(new ReaderSettings().ToJsonString(), s.ToJsonString());
    }
}
