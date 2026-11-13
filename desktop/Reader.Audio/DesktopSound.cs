using System.Diagnostics;
using Reader.Core;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;

namespace Reader.Audio;

/// <summary>
/// The reader's sounds on a desktop computer. The interface sounds come from <see cref="Synth"/>;
/// the SoundFlow library opens the sound device and reads sound files (wav, mp3, flac).
///
/// It fails soft. A computer with no speakers, no sound driver, or a missing native library simply
/// stays silent: nothing here ever throws, <see cref="Available"/> is false and <see cref="Status"/>
/// says why.
/// </summary>
public sealed class DesktopSound : ISoundOutput, IDisposable
{
    /// <summary>The longest sound file that will be played, in seconds. Longer ones are cut off rather than filling memory.</summary>
    const double LongestClipSeconds = 600;

    /// <summary>Feeds the synth's samples to the device.</summary>
    sealed class SynthSource : SoundComponent
    {
        readonly Synth _synth;

        public SynthSource(AudioEngine engine, AudioFormat format, Synth synth) : base(engine, format) => _synth = synth;

        public override string Name { get; set; } = "Reader sounds";

        protected override void GenerateAudio(Span<float> buffer, int channels) => _synth.Render(buffer, channels);
    }

    readonly object _gate = new();
    readonly Stopwatch _clipClock = new();
    Synth _synth = new();
    MiniAudioEngine? _engine;
    AudioPlaybackDevice? _device;
    SynthSource? _source;
    AudioFormat _format;
    int _clipToken;
    volatile bool _clipLoading;
    double _clipSeconds;
    float _volume = 1;
    bool _disposed;

    /// <summary>Opens the default sound device. Never throws: check <see cref="Available"/>.</summary>
    public DesktopSound() => Start();

    /// <summary>True when a sound device is open and playing.</summary>
    public bool Available { get; private set; }

    /// <summary>A line for a log or an "about" box: what is playing the sound, or why there is none.</summary>
    public string Status { get; private set; } = "Sound has not started.";

    /// <summary>Overall loudness, 0 to 1.</summary>
    public float Volume
    {
        get => _volume;
        set { _volume = Math.Clamp(value, 0, 1); _synth.Volume = _volume; }
    }

    void Start()
    {
        try
        {
            _engine = new MiniAudioEngine();
            var wanted = new AudioFormat { Format = SampleFormat.F32, Channels = 2, SampleRate = 48000 };
            _device = _engine.InitializePlaybackDevice(null, wanted, new MiniAudioDeviceConfig());
            _format = _device.Format;
            _synth = new Synth(_format.SampleRate) { Volume = _volume };
            _source = new SynthSource(_engine, _format, _synth);
            _device.MasterMixer.AddComponent(_source);
            _device.Start();
            Available = true;
            Status = $"Sound is playing through {_device.Info?.Name ?? "the default device"} at {_format.SampleRate} Hz.";
        }
        catch (Exception e)
        {
            // No sound card, no driver, the native library missing or the wrong kind: carry on in silence.
            Available = false;
            Status = "No sound: " + e.Message;
            Shutdown();
        }
    }

    void Shutdown()
    {
        try { _device?.Stop(); } catch (Exception) { /* already stopped */ }
        try { _source?.Dispose(); } catch (Exception) { /* nothing to free */ }
        try { _device?.Dispose(); } catch (Exception) { /* nothing to free */ }
        try { _engine?.Dispose(); } catch (Exception) { /* nothing to free */ }
        _source = null;
        _device = null;
        _engine = null;
    }

    public void Tap() { if (Available) _synth.Tap(); }
    public void Zoom(bool zoomIn) { if (Available) _synth.Zoom(zoomIn); }
    public void Find() { if (Available) _synth.Find(); }
    public void Locked() { if (Available) _synth.Locked(); }
    public void Party() { if (Available) _synth.Party(); }
    public void Whoosh(double milliseconds) { if (Available) _synth.PlayWhoosh(milliseconds); }
    public void Rain(double level) { if (Available) _synth.SetRain(level); }
    public void Thunder(double delaySeconds) { if (Available) _synth.PlayThunder(delaySeconds); }

    /// <summary>
    /// Play a sound file. The file is read in the background so a long narration never holds up the
    /// picture, and <see cref="ClipPlaying"/> is true from this call until the sound has finished.
    /// </summary>
    public void PlayClip(string fileName)
    {
        StopClip();
        if (!Available || _disposed || string.IsNullOrEmpty(fileName)) return;
        MiniAudioEngine? engine = _engine;
        AudioFormat format = _format;
        if (engine is null) return;
        int token;
        lock (_gate) token = ++_clipToken;
        _clipLoading = true;
        Task.Run(() =>
        {
            float[]? samples = Decode(engine, format, fileName);
            lock (_gate)
            {
                if (token != _clipToken || _disposed) return;      // stopped, or another sound asked for, meanwhile
                _clipLoading = false;
                if (samples is null || samples.Length == 0) return;
                _clipSeconds = samples.Length / (double)Math.Max(1, format.Channels) / format.SampleRate;
                _clipClock.Restart();
                _synth.PlayClip(samples, format.Channels);
            }
        });
    }

    /// <summary>
    /// Read a sound file with no device open, as interleaved samples at the given rate. Null if the file
    /// or the sound library can't be used. This is how the file reading can be checked on a machine with
    /// no speakers.
    /// </summary>
    internal static float[]? ReadSoundFile(string path, int sampleRate = 48000, int channels = 2)
    {
        try
        {
            using var engine = new MiniAudioEngine();
            return Decode(engine, new AudioFormat { Format = SampleFormat.F32, Channels = channels, SampleRate = sampleRate }, path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Read a whole sound file as samples in the device's own format. Null if it can't be read.</summary>
    static float[]? Decode(MiniAudioEngine engine, AudioFormat format, string path)
    {
        try
        {
            using FileStream file = File.OpenRead(path);
            using var provider = new StreamDataProvider(engine, format, file);
            int most = (int)(LongestClipSeconds * format.SampleRate) * Math.Max(1, format.Channels);
            var all = new List<float>(provider.Length > 0 ? Math.Min(provider.Length, most) : 1 << 16);
            var block = new float[8192];
            int read;
            while (all.Count < most && (read = provider.ReadBytes(block)) > 0) all.AddRange(block.AsSpan(0, read));
            return all.ToArray();
        }
        catch (Exception)
        {
            return null;   // missing, damaged or of a kind the library can't read: no sound for this one
        }
    }

    public void StopClip()
    {
        lock (_gate)
        {
            _clipToken++;
            _clipLoading = false;
            _clipClock.Reset();
            _clipSeconds = 0;
        }
        _synth.StopClip();
    }

    /// <summary>
    /// Judged by the clock rather than by asking the device, so a device that stalls can never leave a
    /// stop waiting for ever for its narration to end.
    /// </summary>
    public bool ClipPlaying
    {
        get
        {
            if (!Available) return false;
            if (_clipLoading) return true;
            lock (_gate) return _clipClock.IsRunning && _clipClock.Elapsed.TotalSeconds < _clipSeconds;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopClip();
        Available = false;
        Shutdown();
    }
}
