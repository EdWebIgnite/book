using System.Text;
using Reader.Core;

namespace Editor.Core;

/// <summary>
/// Timing the words of a caption to its recording by hand. The author plays the recording and taps a
/// key as each word starts; each tap gives the next word its time. Afterwards single words can be
/// nudged earlier or later. (Working the times out from the sound alone is not attempted.)
/// </summary>
public sealed class TapAlong
{
    readonly List<double> _times = new();

    public TapAlong(int wordCount, double startSeconds = 0)
    {
        WordCount = wordCount;
        StartSeconds = startSeconds;
    }

    public int WordCount { get; }
    public double StartSeconds { get; }

    /// <summary>The word the next tap is for (0 = first), or <see cref="WordCount"/> when every word has a time.</summary>
    public int NextWord => _times.Count;

    public bool Done => _times.Count >= WordCount;

    public IReadOnlyList<double> Times => _times;

    /// <summary>
    /// A tap at <paramref name="seconds"/> into the recording. A tap earlier than the word before it (a
    /// double press) is moved just after it, so the times always run in order. Returns the word tapped,
    /// or -1 when every word already has its time.
    /// </summary>
    public int Tap(double seconds)
    {
        if (Done) return -1;
        double t = Math.Max(StartSeconds, seconds);
        if (_times.Count > 0 && t < _times[^1] + 0.05) t = _times[^1] + 0.05;
        _times.Add(t);
        return _times.Count - 1;
    }

    /// <summary>Take back the last tap.</summary>
    public void Back()
    {
        if (_times.Count > 0) _times.RemoveAt(_times.Count - 1);
    }

    /// <summary>
    /// The times to keep when tapping stops: the words tapped, and if it stopped early, the words after them
    /// keep their old times where those still come later (otherwise they are left out).
    /// </summary>
    public List<double> Result(IReadOnlyList<double>? before = null)
    {
        var result = new List<double>(_times);
        if (before is not null)
        {
            for (int i = result.Count; i < Math.Min(WordCount, before.Count); i++)
            {
                if (result.Count > 0 && before[i] <= result[^1]) break;
                result.Add(before[i]);
            }
        }
        return result;
    }

    /// <summary>
    /// Move one word's time by <paramref name="delta"/> seconds, without passing the words either side of it
    /// (a word always keeps at least 0.05 s), and within the recording's trimmed part.
    /// </summary>
    public static void Nudge(List<double> times, int index, double delta, double min = 0, double max = double.MaxValue)
    {
        if (index < 0 || index >= times.Count) return;
        double lo = index > 0 ? times[index - 1] + 0.05 : min;
        double hi = index < times.Count - 1 ? times[index + 1] - 0.05 : max;
        if (hi < lo) return;
        times[index] = Math.Round(Math.Clamp(times[index] + delta, lo, hi), 3);
    }

    /// <summary>
    /// A first guess to fix up by hand: the words spread across the recording, each given time in
    /// proportion to its length (long words take longer to say).
    /// </summary>
    public static List<double> Spread(string? caption, double start, double end)
    {
        var words = ReadAlong.Words(caption);
        var times = new List<double>();
        if (words.Count == 0 || end <= start) return times;
        double total = words.Sum(w => w.Length + 2.0);
        double at = start;
        foreach (var w in words)
        {
            times.Add(Math.Round(at, 3));
            at += (end - start) * (w.Length + 2.0) / total;
        }
        return times;
    }
}

/// <summary>Plain WAV files: what the editor writes for a recording made with the microphone.</summary>
public static class Wav
{
    /// <summary>Write one channel of samples (-1 to 1) as a 16-bit WAV file.</summary>
    public static void Write(string path, ReadOnlySpan<float> samples, int sampleRate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var f = new BinaryWriter(File.Create(path));
        int bytes = samples.Length * 2;
        f.Write(Encoding.ASCII.GetBytes("RIFF"));
        f.Write(36 + bytes);
        f.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        f.Write(16);
        f.Write((short)1);              // plain PCM
        f.Write((short)1);              // one channel
        f.Write(sampleRate);
        f.Write(sampleRate * 2);
        f.Write((short)2);
        f.Write((short)16);
        f.Write(Encoding.ASCII.GetBytes("data"));
        f.Write(bytes);
        foreach (float s in samples) f.Write((short)Math.Round(Math.Clamp(s, -1f, 1f) * 32767));
    }

    /// <summary>Read a 16-bit PCM WAV file as one channel (stereo is mixed down). Null if it isn't one.</summary>
    public static (float[] Samples, int SampleRate)? Read(string path)
    {
        try
        {
            using var r = new BinaryReader(File.OpenRead(path));
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "RIFF") return null;
            r.ReadInt32();
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "WAVE") return null;
            int channels = 1, rate = 0, bits = 0;
            while (r.BaseStream.Position < r.BaseStream.Length - 8)
            {
                string id = Encoding.ASCII.GetString(r.ReadBytes(4));
                int size = r.ReadInt32();
                if (id == "fmt ")
                {
                    long end = r.BaseStream.Position + size;
                    short format = r.ReadInt16();
                    channels = r.ReadInt16();
                    rate = r.ReadInt32();
                    r.ReadInt32();
                    r.ReadInt16();
                    bits = r.ReadInt16();
                    if (format != 1 || bits != 16) return null;
                    r.BaseStream.Position = end;
                }
                else if (id == "data")
                {
                    int frames = size / 2 / Math.Max(1, channels);
                    var samples = new float[frames];
                    for (int i = 0; i < frames; i++)
                    {
                        float sum = 0;
                        for (int c = 0; c < channels; c++) sum += r.ReadInt16() / 32768f;
                        samples[i] = sum / channels;
                    }
                    return (samples, rate);
                }
                else r.BaseStream.Position += size + (size & 1);
            }
            return null;
        }
        catch (Exception e) when (e is IOException or EndOfStreamException) { return null; }
    }

    /// <summary>
    /// Where the sound in a recording starts and ends, leaving out quiet at either end (with a little
    /// room kept), in seconds. For trimming a fresh microphone recording.
    /// </summary>
    public static (double Start, double End) Sounding(ReadOnlySpan<float> mono, int sampleRate, float threshold = 0.02f, double keep = 0.12)
    {
        int window = Math.Max(1, sampleRate / 100);
        int first = -1, last = -1;
        for (int i = 0; i + window <= mono.Length; i += window)
        {
            float peak = 0;
            for (int j = i; j < i + window; j++) peak = Math.Max(peak, Math.Abs(mono[j]));
            if (peak >= threshold)
            {
                if (first < 0) first = i;
                last = i + window;
            }
        }
        double length = mono.Length / (double)sampleRate;
        if (first < 0) return (0, length);
        return (Math.Max(0, first / (double)sampleRate - keep), Math.Min(length, last / (double)sampleRate + keep));
    }

    /// <summary>The loudest point in each of <paramref name="buckets"/> equal slices of a recording, for drawing its shape.</summary>
    public static float[] Peaks(ReadOnlySpan<float> mono, int buckets)
    {
        var peaks = new float[Math.Max(1, buckets)];
        if (mono.Length == 0) return peaks;
        for (int b = 0; b < peaks.Length; b++)
        {
            int from = (int)((long)mono.Length * b / peaks.Length), to = (int)((long)mono.Length * (b + 1) / peaks.Length);
            float p = 0;
            for (int i = from; i < Math.Max(to, from + 1) && i < mono.Length; i++) p = Math.Max(p, Math.Abs(mono[i]));
            peaks[b] = p;
        }
        return peaks;
    }
}
