using System.Text.Json.Nodes;
using Reader.Core;

namespace Reader.Tests;

/// <summary>Recorded voices on stops, read-along word timings, and how the reader plays them.</summary>
public class NarrationTests
{
    static Stop Spoken(string caption, double? end = 2.0, params double[] words) => new()
    {
        Box = new RectD(0.1, 0.1, 0.3, 0.3),
        Caption = caption,
        Voice = new Voice { File = "voice/p1-s1.wav", Start = 0.2, End = end, Words = words.ToList() },
    };

    [Fact]
    public void A_voice_is_written_and_read_back()
    {
        Book book = Kit.OnePage(Spoken("Drip. Drip. Drip.", 2.5, 0.31234, 0.9, 1.45));
        JsonNode saved = BookJson.Serialize(book);
        JsonNode voice = saved["pages"]![0]!["stops"]![0]!["voice"]!;
        Assert.Equal("""{"file":"voice/p1-s1.wav","start":0.2,"end":2.5,"words":[0.312,0.9,1.45]}""", voice.ToJsonString());

        Book back = BookJson.Parse(saved.ToJsonString());
        Voice v = back.Pages[0].Stops[0].Voice!;
        Assert.Equal("voice/p1-s1.wav", v.File);
        Assert.Equal(0.2, v.Start);
        Assert.Equal(2.5, v.End);
        Assert.Equal(new[] { 0.312, 0.9, 1.45 }, v.Words);
    }

    [Fact]
    public void A_stop_without_a_voice_is_saved_exactly_as_before()
    {
        Book book = Kit.OnePage(new Stop { Box = new RectD(0, 0, 0.5, 0.5), Caption = "Hi." });
        Assert.Null(BookJson.Serialize(book)["pages"]![0]!["stops"]![0]!["voice"]);
        // A voice with no file is not a voice.
        Assert.Null(BookJson.Parse("""{ "pages": [ { "image": "a.jpg", "stops": [ { "box": {"x":0,"y":0,"w":1,"h":1}, "voice": { "words": [1] } } ] } ] }""").Pages[0].Stops[0].Voice);
    }

    [Fact]
    public void Fields_this_reader_does_not_know_are_kept_when_saving()
    {
        const string json = """
            { "id": "x", "title": "X", "madeWith": "something newer", "pages": [
              { "image": "a.jpg", "background": "blue",
                "stops": [ { "box": {"x":0,"y":0,"w":0.5,"h":0.5}, "mood": "happy" } ],
                "layers": [ { "image": "l.png", "box": {"x":0,"y":0,"w":0.1,"h":0.1}, "blend": "soft" } ],
                "effects": [ { "type": "snow", "amount": 0.5, "wind": 2 } ] },
              { "image": "b.jpg", "mode": "explore", "hotspots": [ { "box": {"x":0,"y":0,"w":0.1,"h":0.1}, "wiggle": true } ],
                "guidance": { "hints": true, "showMe": true, "map": false, "arrow": "big" } } ] }
            """;
        JsonNode saved = JsonNode.Parse(BookJson.ToJsonString(BookJson.Parse(json)))!;
        Assert.Equal("something newer", saved["madeWith"]!.GetValue<string>());
        Assert.Equal("blue", saved["pages"]![0]!["background"]!.GetValue<string>());
        Assert.Equal("happy", saved["pages"]![0]!["stops"]![0]!["mood"]!.GetValue<string>());
        Assert.Equal("soft", saved["pages"]![0]!["layers"]![0]!["blend"]!.GetValue<string>());
        Assert.Equal(2, saved["pages"]![0]!["effects"]![0]!["wind"]!.GetValue<double>());
        Assert.True(saved["pages"]![1]!["hotspots"]![0]!["wiggle"]!.GetValue<bool>());
        Assert.Equal("big", saved["pages"]![1]!["guidance"]!["arrow"]!.GetValue<string>());
    }

    [Fact]
    public void The_editors_spread_note_is_kept()
    {
        Book book = BookJson.Parse("""{ "pages": [ { "image": "p1.jpg", "spread": { "left": "p1-left.png", "right": "p1-right.png" } } ] }""");
        Assert.Equal("p1-left.png", book.Pages[0].Spread!.Left);
        Assert.Equal("p1-right.png", book.Pages[0].Spread!.Right);
        Assert.Contains("\"spread\"", BookJson.ToJsonString(book));
    }

    [Fact]
    public void Captions_split_into_words_with_their_punctuation()
    {
        var words = ReadAlong.Words("  Big, red,\nshiny boots. ");
        Assert.Equal(new[] { "Big,", "red,", "shiny", "boots." }, words.Select(w => "  Big, red,\nshiny boots. ".Substring(w.Start, w.Length)));
        Assert.Empty(ReadAlong.Words(""));
        Assert.Empty(ReadAlong.Words(null));
    }

    [Fact]
    public void The_word_being_read_is_the_last_one_started()
    {
        double[] times = { 0.5, 1.0, 1.6 };
        Assert.Equal(-1, ReadAlong.WordAt(times, 3, 0.2));
        Assert.Equal(0, ReadAlong.WordAt(times, 3, 0.5));
        Assert.Equal(1, ReadAlong.WordAt(times, 3, 1.3));
        Assert.Equal(2, ReadAlong.WordAt(times, 3, 9));
        // Never past the words the caption has.
        Assert.Equal(1, ReadAlong.WordAt(times, 2, 9));
    }

    [Fact]
    public void Word_times_fit_only_when_there_is_one_per_word_in_order()
    {
        var v = new Voice { File = "a.wav", Start = 0.2, End = 3, Words = { 0.3, 0.9, 1.5 } };
        Assert.True(ReadAlong.TimesFit(v, "One two three"));
        Assert.False(ReadAlong.TimesFit(v, "One two three four"));
        v.Words[2] = 0.5;
        Assert.False(ReadAlong.TimesFit(v, "One two three"));
    }

    [Fact]
    public void The_voice_starts_when_the_camera_arrives()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.5, 0.5), Spoken("Drip. Drip. Drip.", 2.0, 0.3, 0.8, 1.3));
        Rig rig = Kit.Open(book, s => s.WholePageFirst = false);
        Assert.DoesNotContain(rig.Sound.Calls, c => c.StartsWith("voice "));
        rig.NextAndArrive();
        Assert.Contains("voice voice/p1-s1.wav 0.2-2", rig.Sound.Calls);
        Assert.True(rig.Engine.Reading);
        Assert.True(rig.Engine.State.Reading);
        // The engine keeps time itself (this output can't tell): 0.2 s in at the start.
        Assert.Equal(-1, rig.Engine.ReadAlongWord());
        rig.Run(200);                                  // 0.4 s into the file
        Assert.Equal(0, rig.Engine.ReadAlongWord());
        rig.Run(500);                                  // 0.9 s
        Assert.Equal(1, rig.Engine.ReadAlongWord());
        rig.Run(600);                                  // 1.5 s
        Assert.Equal(2, rig.Engine.ReadAlongWord());
        rig.Run(600);                                  // past the end at 2.0 s
        Assert.False(rig.Engine.Reading);
        Assert.True(rig.Engine.ReadingDone);
        Assert.Equal(-1, rig.Engine.ReadAlongWord());
        Assert.True(rig.Engine.State.ReadingDone);
    }

    [Fact]
    public void The_output_s_own_position_is_followed_when_it_has_one()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.5, 0.5), Spoken("Drip. Drip. Drip.", 2.0, 0.3, 0.8, 1.3));
        Rig rig = Kit.Open(book, s => s.WholePageFirst = false);
        rig.NextAndArrive();
        rig.Sound.VoicePlaying = true;
        rig.Sound.VoicePosition = 0.2;                 // still loading: stays at the start
        rig.Run(1000);
        Assert.Equal(-1, rig.Engine.ReadAlongWord());
        Assert.True(rig.Engine.Reading);
        rig.Sound.VoicePosition = 0.85;
        Assert.Equal(1, rig.Engine.ReadAlongWord());
    }

    [Fact]
    public void A_tap_goes_on_straight_away_and_stops_the_voice()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.5, 0.5), Spoken("Drip. Drip. Drip.", 2.0, 0.3, 0.8, 1.3), Kit.StopAt(0.1, 0.6));
        Rig rig = Kit.Open(book, s => s.WholePageFirst = false);
        rig.NextAndArrive();
        rig.Run(400);
        Assert.True(rig.Engine.Reading);
        rig.Next();
        Assert.Contains("stop voice", rig.Sound.Calls);
        Assert.False(rig.Engine.Reading);
    }

    [Fact]
    public void Without_watch_mode_the_reader_waits_for_a_tap_after_the_voice()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.5, 0.5), Spoken("Drip. Drip. Drip.", 2.0, 0.3, 0.8, 1.3), Kit.StopAt(0.1, 0.6));
        Rig rig = Kit.Open(book, s => s.WholePageFirst = false);
        rig.NextAndArrive();
        int step = rig.Engine.StepIndex;
        rig.Run(20_000);
        Assert.Equal(step, rig.Engine.StepIndex);
    }

    [Fact]
    public void Watch_mode_waits_for_the_voice_then_goes_on_after_a_breath()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.5, 0.5), Spoken("Drip. Drip. Drip.", 4.0, 0.3, 0.8, 1.3), Kit.StopAt(0.1, 0.6));
        Rig rig = Kit.Open(book, s => { s.WholePageFirst = false; s.AutoPlay = true; s.AutoSeconds = 2; });
        rig.Next();
        rig.RunUntilStill();
        int step = rig.Engine.StepIndex;
        Assert.Equal(1, rig.Engine.CurrentStep.Stop);
        rig.Run(3000);                                 // the voice runs 3.8 s: still reading, so no going on yet
        Assert.Equal(step, rig.Engine.StepIndex);
        rig.Run(800 + 1500 + 200);                     // the voice ends, then a breath of 1.5 s
        Assert.Equal(step + 1, rig.Engine.StepIndex);
    }

    [Fact]
    public void Read_to_me_off_plays_no_voice()
    {
        Book book = Kit.OnePage(Kit.StopAt(0.5, 0.5), Spoken("Drip.", 2.0, 0.3));
        Rig rig = Kit.Open(book, s => { s.WholePageFirst = false; s.ReadToMe = false; });
        rig.NextAndArrive();
        Assert.DoesNotContain(rig.Sound.Calls, c => c.StartsWith("voice "));
        Assert.False(rig.Engine.Reading);
        Assert.Equal(-1, rig.Engine.ReadAlongWord());
    }

    [Theory]
    [MemberData(nameof(BookJsonTests.Books), MemberType = typeof(BookJsonTests))]
    public void Books_without_narration_never_ask_for_a_voice(string id)
    {
        Rig rig = Kit.Open(id, s => s.AutoPlay = true);
        for (int i = 0; i < 60 && !rig.Engine.Finished; i++) rig.Run(500);
        Assert.DoesNotContain(rig.Sound.Calls, c => c.StartsWith("voice "));
        Assert.False(rig.Engine.Reading);
        Assert.False(rig.Engine.State.Reading);
        Assert.False(rig.Engine.State.ReadingDone);
    }

    [Fact]
    public void Read_to_me_is_kept_with_the_settings_and_starts_on()
    {
        Assert.True(new ReaderSettings().ReadToMe);
        ReaderSettings s = ReaderSettings.FromJsonString("""{ "readToMe": false }""");
        Assert.False(s.ReadToMe);
        Assert.Contains("\"readToMe\": false", s.ToJsonString());
    }

    [Fact]
    public void A_voice_is_cut_to_its_trim()
    {
        float[] samples = Enumerable.Range(0, 48000 * 2 * 2).Select(i => (float)i).ToArray();   // 2 s of stereo at 48 kHz
        float[] part = Reader.Audio.DesktopSound.Slice(samples, 2, 48000, 0.5, 1.5);
        Assert.Equal(48000 * 2, part.Length);
        Assert.Equal(48000 * 2 / 2, part[0]);          // frame 24000, left
        Assert.Same(samples, Reader.Audio.DesktopSound.Slice(samples, 2, 48000, 0, 0));
        Assert.Equal((96000 - 72000) * 2, Reader.Audio.DesktopSound.Slice(samples, 2, 48000, 1.5, 0).Length);
    }

    [Fact]
    public void The_synth_plays_a_voice_alongside_a_sound_effect_and_tracks_how_far_it_got()
    {
        var synth = new Reader.Audio.Synth(8000) { Volume = 1 };
        synth.PlayClip(Enumerable.Repeat(0.25f, 8000).ToArray(), 1);
        synth.PlayVoice(Enumerable.Repeat(0.5f, 4000).ToArray(), 1);
        var block = new float[1600];
        synth.Render(block, 1);
        Assert.Equal(0.75f, block[0], 3);
        Assert.Equal(0.2, synth.VoiceSeconds, 3);
        Assert.True(synth.VoicePlaying);
        synth.Render(new float[3200], 1);
        Assert.False(synth.VoicePlaying);
        Assert.Equal(0.5, synth.VoiceSeconds, 3);      // stays at the end
    }
}
