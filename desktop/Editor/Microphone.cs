using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace BookEditor;

/// <summary>
/// Recording from the computer's microphone, through the same sound library the reader plays with.
/// It fails soft: with no microphone, no permission or no sound library, <see cref="Start"/> returns
/// false and <see cref="Problem"/> says why in plain words, and the editor carries on.
/// </summary>
sealed class Microphone : IDisposable
{
    public const int SampleRate = 48000;

    readonly object _gate = new();
    readonly List<float> _samples = new();
    MiniAudioEngine? _engine;
    AudioCaptureDevice? _device;
    volatile float _level;

    public bool Recording { get; private set; }
    public string Problem { get; private set; } = "";

    /// <summary>How loud the last bit of sound was, 0 to 1, for the meter.</summary>
    public float Level => _level;

    public double Seconds { get { lock (_gate) return _samples.Count / (double)SampleRate; } }

    /// <summary>True when the computer has at least one microphone the sound library can see.</summary>
    public static bool Available(out string problem)
    {
        try
        {
            using var engine = new MiniAudioEngine();
            engine.UpdateAudioDevicesInfo();
            if (engine.CaptureDevices.Length == 0) { problem = "No microphone was found on this computer."; return false; }
            problem = "";
            return true;
        }
        catch (Exception e)
        {
            problem = "The microphone can't be used here (" + e.Message + ").";
            return false;
        }
    }

    public bool Start()
    {
        Stop();
        lock (_gate) _samples.Clear();
        try
        {
            _engine = new MiniAudioEngine();
            var format = new AudioFormat { Format = SampleFormat.F32, Channels = 1, SampleRate = SampleRate };
            _device = _engine.InitializeCaptureDevice(null, format, new MiniAudioDeviceConfig());
            _device.OnAudioProcessed += Captured;
            _device.Start();
            Recording = true;
            Problem = "";
            return true;
        }
        catch (Exception e)
        {
            Problem = "Recording didn't start. Check that a microphone is plugged in and that Windows lets apps use it (Settings, Privacy, Microphone). " + e.Message;
            Release();
            return false;
        }
    }

    void Captured(Span<float> samples, Capability capability)
    {
        float peak = 0;
        foreach (float s in samples) peak = Math.Max(peak, Math.Abs(s));
        _level = peak;
        lock (_gate)
        {
            // Ten minutes at most: a recording left running by mistake can't fill the memory.
            if (_samples.Count < SampleRate * 600) _samples.AddRange(samples.ToArray());
        }
    }

    /// <summary>Stop and hand back what was recorded, one channel at <see cref="SampleRate"/>.</summary>
    public float[] Stop()
    {
        if (Recording)
        {
            try { _device?.Stop(); } catch (Exception) { /* already stopped */ }
        }
        Recording = false;
        Release();
        _level = 0;
        lock (_gate) return _samples.ToArray();
    }

    void Release()
    {
        if (_device is not null) _device.OnAudioProcessed -= Captured;
        try { _device?.Dispose(); } catch (Exception) { }
        try { _engine?.Dispose(); } catch (Exception) { }
        _device = null;
        _engine = null;
    }

    public void Dispose() => Stop();
}
