using System.Collections.Concurrent;

namespace Reader.Audio;

/// <summary>
/// Small synthesised sounds, so every touch can be answered without any audio files. This is the
/// desktop's version of class Sounds in web/reader.js: the same notes, lengths and loudness, made by
/// adding up samples instead of wiring Web Audio nodes together.
///
/// It needs no sound device. Something asks it to <see cref="Render"/> a block of samples whenever
/// the device wants more, on the device's own thread; the other methods are called from the screen's
/// thread and only leave a note for the next block. That also means it can be run with no device at
/// all, to check what it produces.
/// </summary>
public sealed class Synth
{
    public enum Wave { Sine, Triangle }

    abstract class Voice
    {
        /// <summary>Seconds still to wait before this voice starts.</summary>
        public double Delay;
        /// <summary>Seconds played so far.</summary>
        public double Age;
        /// <summary>The next sample, or null when the voice has finished.</summary>
        public abstract float? Next(Synth synth);
    }

    /// <summary>One note: a quick rise, then dying away, like a soft bell.</summary>
    sealed class Tone : Voice
    {
        public double Frequency, Length, Gain, Phase;
        public Wave Shape;

        public override float? Next(Synth s)
        {
            if (Age > Length + 0.02) return null;
            const double attack = 0.012, floor = 0.0001;
            double gain = Age < attack ? Gain * Age / attack
                        : Age < Length ? Gain * Math.Pow(floor / Gain, (Age - attack) / (Length - attack))
                        : floor;
            double sample = Shape == Wave.Sine ? Math.Sin(Phase * 2 * Math.PI) : 4 * Math.Abs(Phase - Math.Floor(Phase + 0.5)) - 1;
            Phase += Frequency / s.SampleRate;
            if (Phase > 1024) Phase -= 1024;
            Age += 1.0 / s.SampleRate;
            return (float)(sample * gain);
        }
    }

    /// <summary>Soft rush of air for a camera move: hissing noise through a filter that sweeps up and back down.</summary>
    sealed class Whoosh : Voice
    {
        public double Length;
        Biquad _band;
        int _sinceTuned = 32;      // tune the filter on the very first sample, then every 32nd

        public override float? Next(Synth s)
        {
            if (Age >= Length) return null;
            double u = Age / Length;
            if (++_sinceTuned >= 32)
            {
                double hz = u < 0.5 ? 300 + (900 - 300) * (u / 0.5) : 900 + (350 - 900) * ((u - 0.5) / 0.5);
                _band.SetBandPass(hz, 0.8, s.SampleRate);
                _sinceTuned = 0;
            }
            double gain = u < 0.4 ? 0.035 * (u / 0.4) : 0.035 * (1 - (u - 0.4) / 0.6);
            Age += 1.0 / s.SampleRate;
            return (float)(_band.Run(s.Noise()) * gain);
        }
    }

    /// <summary>A low rumble, not a crack: this is for small children.</summary>
    sealed class Thunder : Voice
    {
        Biquad _low;
        int _sinceTuned = 32;      // tune the filter on the very first sample, then every 32nd

        public override float? Next(Synth s)
        {
            if (Age >= 2.4) return null;
            if (++_sinceTuned >= 32)
            {
                // The rumble starts at 260 Hz and sinks to 70 Hz.
                _low.SetLowPass(260 * Math.Pow(70.0 / 260, Math.Min(1, Age / 2.2)), s.SampleRate);
                _sinceTuned = 0;
            }
            double gain = Age < 0.12 ? 0.5 * Age / 0.12 : 0.5 * Math.Pow(0.001 / 0.5, Math.Min(1, (Age - 0.12) / (2.3 - 0.12)));
            Age += 1.0 / s.SampleRate;
            return (float)(_low.Run(s.Noise()) * gain);
        }
    }

    /// <summary>A two-pole filter (the "biquad" every audio toolkit has), with the usual cookbook recipes.</summary>
    struct Biquad
    {
        double _b0, _b1, _b2, _a1, _a2, _x1, _x2, _y1, _y2;

        /// <summary>Web Audio gives its low-pass and high-pass filters a small bump at the cut-off (1 dB) by default; so does this.</summary>
        const double Bump = 1.1220184543;

        public void SetLowPass(double hz, int rate)
        {
            (double cos, double alpha) = Angle(hz, Bump, rate);
            Set((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public void SetHighPass(double hz, int rate)
        {
            (double cos, double alpha) = Angle(hz, Bump, rate);
            Set((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public void SetBandPass(double hz, double q, int rate)
        {
            (double cos, double alpha) = Angle(hz, q, rate);
            Set(alpha, 0, -alpha, 1 + alpha, -2 * cos, 1 - alpha);
        }

        static (double Cos, double Alpha) Angle(double hz, double q, int rate)
        {
            double w = 2 * Math.PI * Math.Clamp(hz, 10, rate * 0.45) / rate;
            return (Math.Cos(w), Math.Sin(w) / (2 * q));
        }

        void Set(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            _b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0; _a1 = a1 / a0; _a2 = a2 / a0;
        }

        public double Run(double x)
        {
            double y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1; _x1 = x;
            _y2 = _y1; _y1 = y;
            return y;
        }
    }

    sealed class Clip
    {
        public required float[] Samples;      // interleaved
        public required int Channels;
        public int Position;
    }

    readonly ConcurrentQueue<Voice> _incoming = new();
    readonly List<Voice> _voices = new();
    uint _random = 0x9E3779B9;
    volatile float _rainTarget;
    double _rainGain;
    Biquad _rainHigh, _rainLow;
    volatile Clip? _clip;

    public Synth(int sampleRate = 48000)
    {
        SampleRate = Math.Max(8000, sampleRate);
        _rainHigh.SetHighPass(1100, SampleRate);
        _rainLow.SetLowPass(7000, SampleRate);
    }

    public int SampleRate { get; }

    /// <summary>Overall loudness, 0 to 1. Zero keeps tests quiet on a machine that does have speakers.</summary>
    public float Volume { get; set; } = 1;

    /// <summary>The most voices kept at once. A child hammering the screen shouldn't pile up thousands.</summary>
    const int MostVoices = 48;

    /// <summary>White noise between -1 and 1 (a quick shift-register, good enough for hiss).</summary>
    double Noise()
    {
        _random ^= _random << 13;
        _random ^= _random >> 17;
        _random ^= _random << 5;
        return _random / 2147483648.0 - 1;
    }

    // ---------------------------------------------------------------- what can be asked for

    public void PlayTone(double hz, double delaySeconds, double seconds, double gain = 0.06, Wave shape = Wave.Sine) =>
        Add(new Tone { Frequency = hz, Delay = delaySeconds, Length = seconds, Gain = gain, Shape = shape });

    public void Tap() => PlayTone(720, 0, 0.09, 0.045);

    public void Zoom(bool zoomIn)
    {
        PlayTone(zoomIn ? 620 : 520, 0, 0.08, 0.04);
        PlayTone(zoomIn ? 820 : 400, 0.05, 0.1, 0.04);
    }

    public void Find()
    {
        double[] notes = { 1047, 1319, 1568 };
        for (int i = 0; i < notes.Length; i++) PlayTone(notes[i], i * 0.07, 0.3, 0.055, Wave.Triangle);
    }

    public void Locked()
    {
        PlayTone(330, 0, 0.12, 0.05, Wave.Triangle);
        PlayTone(262, 0.1, 0.18, 0.05, Wave.Triangle);
    }

    public void Party()
    {
        double[] notes = { 784, 988, 1175, 1568, 1976 };
        for (int i = 0; i < notes.Length; i++) PlayTone(notes[i], i * 0.09, 0.45, 0.06, Wave.Triangle);
    }

    /// <summary>Longer for longer moves, up to about a second. Very short moves make no sound.</summary>
    public void PlayWhoosh(double milliseconds)
    {
        if (milliseconds < 120) return;
        Add(new Whoosh { Length = Math.Min(1.2, milliseconds / 1000) * 0.9 });
    }

    public void PlayThunder(double delaySeconds) => Add(new Thunder { Delay = Math.Max(0, delaySeconds) });

    /// <summary>Steady rain, as loud as <paramref name="level"/> (0 to 1). It eases to the new loudness over about half a second.</summary>
    public void SetRain(double level) => _rainTarget = (float)(Math.Clamp(level, 0, 1) * 0.045);

    /// <summary>Start a decoded sound file. The samples must already be at this synth's sample rate.</summary>
    public void PlayClip(float[] interleaved, int channels) => _clip = new Clip { Samples = interleaved, Channels = Math.Max(1, channels) };

    public void StopClip() => _clip = null;

    void Add(Voice voice)
    {
        if (_incoming.Count < MostVoices) _incoming.Enqueue(voice);
    }

    // ---------------------------------------------------------------- making the samples

    /// <summary>
    /// Fill a block of interleaved samples (frame after frame, <paramref name="channels"/> samples each).
    /// Whatever was in the block is replaced.
    /// </summary>
    public void Render(Span<float> block, int channels)
    {
        channels = Math.Max(1, channels);
        while (_incoming.TryDequeue(out Voice? voice))
        {
            if (_voices.Count < MostVoices) _voices.Add(voice);
        }
        Clip? clip = _clip;
        float volume = Volume;
        double step = 1.0 / SampleRate;
        double rainEase = 1 - Math.Exp(-step / 0.2);
        int frames = block.Length / channels;

        for (int f = 0; f < frames; f++)
        {
            double mix = 0;
            for (int v = _voices.Count - 1; v >= 0; v--)
            {
                Voice voice = _voices[v];
                if (voice.Delay > 0) { voice.Delay -= step; continue; }
                if (voice.Next(this) is float sample) mix += sample;
                else _voices.RemoveAt(v);
            }

            _rainGain += (_rainTarget - _rainGain) * rainEase;
            if (_rainGain > 1e-5) mix += _rainLow.Run(_rainHigh.Run(Noise())) * _rainGain;

            int at = f * channels;
            for (int c = 0; c < channels; c++)
            {
                double sample = mix;
                if (clip is not null && clip.Position < clip.Samples.Length)
                {
                    // A one-channel file plays on every speaker; a stereo file keeps its sides.
                    int source = clip.Position + Math.Min(c, clip.Channels - 1);
                    if (source < clip.Samples.Length) sample += clip.Samples[source];
                }
                block[at + c] = (float)Math.Clamp(sample * volume, -1, 1);
            }
            if (clip is not null) clip.Position += clip.Channels;
        }
        if (clip is not null && clip.Position >= clip.Samples.Length && ReferenceEquals(_clip, clip)) _clip = null;
    }

    /// <summary>True when nothing is sounding: no notes, no rain, no sound file.</summary>
    public bool Silent => _voices.Count == 0 && _incoming.IsEmpty && _rainGain <= 1e-5 && _rainTarget <= 0 && _clip is null;
}
