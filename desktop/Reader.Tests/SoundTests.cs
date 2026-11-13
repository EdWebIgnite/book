using Reader.Audio;
using Reader.Core;

namespace Reader.Tests;

/// <summary>
/// The synthesised sounds, checked by making the samples with no sound device and measuring them,
/// and the promise that the desktop sound output never throws, with or without speakers.
/// </summary>
public class SoundTests
{
    const int Rate = 48000;

    static float[] Render(Synth synth, double seconds, int channels = 1)
    {
        var block = new float[(int)(seconds * synth.SampleRate) * channels];
        // In blocks, the way a sound device asks for them.
        for (int at = 0; at < block.Length; at += 480 * channels)
            synth.Render(block.AsSpan(at, Math.Min(480 * channels, block.Length - at)), channels);
        return block;
    }

    static float Peak(ReadOnlySpan<float> samples)
    {
        float peak = 0;
        foreach (float s in samples) peak = Math.Max(peak, Math.Abs(s));
        return peak;
    }

    static Span<float> Slice(float[] samples, double from, double to) => samples.AsSpan((int)(from * Rate), (int)((to - from) * Rate));

    /// <summary>The pitch, from how often the wave crosses zero going up.</summary>
    static double Pitch(ReadOnlySpan<float> samples)
    {
        int crossings = 0;
        for (int i = 1; i < samples.Length; i++) if (samples[i - 1] < 0 && samples[i] >= 0) crossings++;
        return crossings / (samples.Length / (double)Rate);
    }

    [Fact]
    public void A_fresh_synth_is_silent()
    {
        var synth = new Synth(Rate);
        Assert.Equal(0, Peak(Render(synth, 0.2)));
        Assert.True(synth.Silent);
    }

    [Fact]
    public void A_tap_is_a_short_soft_blip_at_720_hertz()
    {
        var synth = new Synth(Rate);
        synth.Tap();
        float[] sound = Render(synth, 0.3);
        Assert.InRange(Peak(sound), 0.03f, 0.046f);                       // soft: under 5% of full loudness
        Assert.InRange(Pitch(Slice(sound, 0.005, 0.06)), 690, 750);
        Assert.Equal(0, Peak(Slice(sound, 0.12, 0.3)));                   // over in about a tenth of a second
        Assert.True(synth.Silent);
    }

    [Fact]
    public void Zooming_in_rises_and_zooming_out_falls()
    {
        var up = new Synth(Rate);
        up.Zoom(true);
        float[] rising = Render(up, 0.3);
        var down = new Synth(Rate);
        down.Zoom(false);
        float[] falling = Render(down, 0.3);
        Assert.True(Pitch(Slice(rising, 0.1, 0.14)) > Pitch(Slice(rising, 0.0, 0.04)));
        Assert.True(Pitch(Slice(falling, 0.1, 0.14)) < Pitch(Slice(falling, 0.0, 0.04)));
    }

    [Fact]
    public void Finding_something_plays_three_rising_notes_and_the_party_plays_five()
    {
        var synth = new Synth(Rate);
        synth.Find();
        float[] find = Render(synth, 0.6);
        Assert.InRange(Pitch(Slice(find, 0.01, 0.06)), 1000, 1100);       // the first note alone: about 1047 Hz
        Assert.True(Peak(Slice(find, 0.14, 0.2)) > 0.02f);                // the third has started
        Assert.Equal(0, Peak(Slice(find, 0.5, 0.6)));

        synth.Party();
        float[] party = Render(synth, 1.2);
        Assert.True(Peak(Slice(party, 0.36, 0.5)) > 0.02f);               // the fifth note starts at 0.36 s
        Assert.Equal(0, Peak(Slice(party, 0.9, 1.2)));
        Assert.True(Peak(party) < 0.25f);
    }

    [Fact]
    public void A_whoosh_swells_and_fades_and_a_very_short_move_makes_none()
    {
        var synth = new Synth(Rate);
        synth.PlayWhoosh(100);
        Assert.Equal(0, Peak(Render(synth, 0.3)));

        synth.PlayWhoosh(1000);                                           // 0.9 seconds of air
        float[] sound = Render(synth, 1.2);
        float start = Peak(Slice(sound, 0, 0.03)), middle = Peak(Slice(sound, 0.3, 0.45)), end = Peak(Slice(sound, 0.87, 0.9));
        Assert.True(middle > start * 3 && middle > end * 3, "loudest in the middle");
        Assert.InRange(middle, 0.002f, 0.08f);
        Assert.Equal(0, Peak(Slice(sound, 0.95, 1.2)));

        synth.PlayWhoosh(60_000);                                         // however long the move, about a second at most
        Assert.Equal(0, Peak(Render(synth, 2).AsSpan((int)(1.2 * Rate))));
    }

    [Fact]
    public void A_page_turn_is_a_short_quiet_swish_with_nothing_sharp_at_either_end()
    {
        var synth = new Synth(Rate);
        synth.PlayPageTurn();
        float[] sound = Render(synth, 0.6);
        Assert.InRange(Peak(sound), 0.004f, 0.05f);                       // quieter than a tap
        Assert.True(Peak(Slice(sound, 0, 0.004)) < Peak(sound) / 8, "it swells in rather than starting with a click");
        Assert.True(Peak(Slice(sound, 0.31, 0.34)) < Peak(sound) / 8, "and trails off");
        Assert.True(Peak(Slice(sound, 0.05, 0.2)) > Peak(sound) / 2, "loudest early on");
        Assert.Equal(0, Peak(Slice(sound, 0.36, 0.6)));                   // about a third of a second in all
        Assert.True(synth.Silent);
        // Paper, not a whistle or a thump: hiss in the middle of the range.
        Assert.InRange(Pitch(Slice(sound, 0.05, 0.25)), 1200, 4500);
    }

    [Fact]
    public void Thunder_waits_for_its_delay_then_rumbles_low_and_dies_away()
    {
        var synth = new Synth(Rate);
        synth.PlayThunder(0.85);
        float[] sound = Render(synth, 4);
        Assert.Equal(0, Peak(Slice(sound, 0, 0.84)));
        float loud = Peak(Slice(sound, 0.9, 1.4));
        Assert.InRange(loud, 0.02f, 0.5f);
        Assert.True(Peak(Slice(sound, 2.9, 3.2)) < loud / 10, "nearly gone after two seconds");
        Assert.Equal(0, Peak(Slice(sound, 3.3, 4)));
        // A rumble, not a crack: it crosses zero far less often than hiss would.
        Assert.True(Pitch(Slice(sound, 1.0, 2.0)) < 400);
    }

    [Fact]
    public void Rain_fades_in_to_its_level_and_out_again()
    {
        var synth = new Synth(Rate);
        synth.SetRain(1);
        float[] sound = Render(synth, 3);
        Assert.True(Peak(Slice(sound, 0, 0.02)) < Peak(Slice(sound, 2, 2.5)) / 3, "it eases in");
        Assert.InRange(Peak(Slice(sound, 2, 3)), 0.01f, 0.2f);
        synth.SetRain(0);
        float[] after = Render(synth, 4);
        Assert.True(Peak(Slice(after, 3, 4)) < 0.0005f);
    }

    [Fact]
    public void A_sound_file_plays_once_on_every_speaker_and_can_be_stopped()
    {
        var synth = new Synth(Rate);
        var mono = new float[Rate / 10];
        for (int i = 0; i < mono.Length; i++) mono[i] = 0.5f;
        synth.PlayClip(mono, 1);
        float[] stereo = Render(synth, 0.3, channels: 2);
        Assert.Equal(0.5f, stereo[0]);
        Assert.Equal(0.5f, stereo[1]);                                    // one channel in, both speakers out
        Assert.Equal(0.5f, stereo[2 * (mono.Length - 1)]);
        Assert.Equal(0f, stereo[2 * mono.Length + 2]);                    // and it ends
        Assert.True(synth.Silent);

        synth.PlayClip(mono, 1);
        Render(synth, 0.02, 2);
        synth.StopClip();
        Assert.Equal(0, Peak(Render(synth, 0.1, 2)));
    }

    [Fact]
    public void Nothing_is_ever_louder_than_the_speakers_can_take_and_volume_zero_is_silence()
    {
        var synth = new Synth(Rate);
        var loud = new float[Rate];
        Array.Fill(loud, 3f);
        synth.PlayClip(loud, 1);
        for (int i = 0; i < 100; i++) synth.Party();                      // a child hammering the screen
        Assert.InRange(Peak(Render(synth, 0.5)), 0.9f, 1f);

        var quiet = new Synth(Rate) { Volume = 0 };
        quiet.Party();
        quiet.SetRain(1);
        Assert.Equal(0, Peak(Render(quiet, 0.5)));
    }

    [Fact]
    public void The_desktop_sound_output_never_throws_whether_or_not_there_is_a_sound_device()
    {
        using var sound = new DesktopSound { Volume = 0 };                // silent, in case this machine does have speakers
        ISoundOutput output = sound;
        Assert.False(string.IsNullOrEmpty(sound.Status));
        output.Tap();
        output.Zoom(true);
        output.Zoom(false);
        output.Find();
        output.Locked();
        output.Party();
        output.Whoosh(900);
        output.PageTurn();
        output.Rain(0.8);
        output.Thunder(0.85);
        output.Rain(0);
        output.PlayClip(Path.Combine(Kit.BookFolder("biggest-puddle"), "splash.wav"));
        output.PlayClip(Path.Combine(Kit.BookFolder("biggest-puddle"), "no-such-file.mp3"));
        output.PlayClip("");
        output.StopClip();
        Assert.False(output.ClipPlaying);
        if (!sound.Available) Assert.StartsWith("No sound", sound.Status);
        sound.Dispose();
        sound.Dispose();                                                  // twice is fine
        output.Tap();                                                     // and so is using it afterwards
        output.PlayClip(Path.Combine(Kit.BookFolder("biggest-puddle"), "splash.wav"));
        Assert.False(output.ClipPlaying);
    }

    [Fact]
    public void The_sample_books_splash_can_be_read_as_sound()
    {
        float[]? samples = DesktopSound.ReadSoundFile(Path.Combine(Kit.BookFolder("biggest-puddle"), "splash.wav"), Rate, 2);
        if (samples is null) return;                                      // no sound library on this machine: nothing to check
        Assert.InRange(samples.Length / 2.0 / Rate, 0.85, 0.95);          // the file is 0.9 seconds long
        Assert.True(Peak(samples) > 0.2f);
        Assert.Null(DesktopSound.ReadSoundFile(Path.Combine(Kit.BookFolder("biggest-puddle"), "book.json")));
    }

    [Fact]
    public void A_stop_with_a_sound_asks_for_the_file_by_its_name_in_the_book()
    {
        Rig rig = Kit.Open("biggest-puddle");
        Assert.True(rig.JumpToClosing(8));
        rig.Next();
        Assert.Contains("clip splash.wav", rig.Sound.Calls);
        rig.Next();                                                       // moving on stops it
        Assert.Null(rig.Sound.Clip);
    }
}
