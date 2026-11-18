namespace Reader.Core;

/// <summary>
/// The grown-ups' gate: three numbers written as words ("seven, two, nine"), which a grown-up taps on
/// a number pad. A child who can't read yet can't follow it, and holding a button down (the old gate)
/// is something small children do by accident. New numbers every time; after three wrong tries the
/// pad rests for half a minute. Time is passed in, as everywhere in the reader, so it can be tested.
/// </summary>
public sealed class GateChallenge
{
    public const int Length = 3, MissesAllowed = 3;
    public const double RestMs = 30_000;

    readonly Random _random;
    readonly List<int> _typed = new();
    int _misses;
    double _restUntil = double.NegativeInfinity;

    public GateChallenge(Random? random = null)
    {
        _random = random ?? new Random();
        Digits = Fresh(null);
    }

    /// <summary>The numbers to tap, in order.</summary>
    public int[] Digits { get; private set; }

    /// <summary>What has been tapped so far.</summary>
    public IReadOnlyList<int> Typed => _typed;

    /// <summary>True straight after a wrong try, until the next tap: time to say "try again".</summary>
    public bool JustMissed { get; private set; }

    /// <summary>Three different numbers, never the same three as last time.</summary>
    int[] Fresh(int[]? last)
    {
        while (true)
        {
            int[] d = Enumerable.Range(0, 10).OrderBy(_ => _random.Next()).Take(Length).ToArray();
            if (last is null || !d.SequenceEqual(last)) return d;
        }
    }

    /// <summary>The numbers as words in the app's language, for example "seven, two, nine".</summary>
    public string Words(string language) => string.Join(", ", Digits.Select(d => Strings.For(language, "number." + d)));

    public bool Resting(double nowMs) => nowMs < _restUntil;

    /// <summary>Whole seconds of rest left (0 when the pad can be used).</summary>
    public int RestSeconds(double nowMs) => Resting(nowMs) ? (int)Math.Ceiling((_restUntil - nowMs) / 1000) : 0;

    public enum Outcome { More, Opened, Missed, Resting }

    /// <summary>A number was tapped.</summary>
    public Outcome Tap(int digit, double nowMs)
    {
        if (Resting(nowMs)) return Outcome.Resting;
        JustMissed = false;
        _typed.Add(digit);
        if (_typed.Count < Length) return Outcome.More;
        bool right = _typed.SequenceEqual(Digits);
        _typed.Clear();
        if (right) { _misses = 0; return Outcome.Opened; }
        _misses++;
        JustMissed = true;
        Digits = Fresh(Digits);
        if (_misses >= MissesAllowed)
        {
            _misses = 0;
            _restUntil = nowMs + RestMs;
            return Outcome.Resting;
        }
        return Outcome.Missed;
    }

    /// <summary>Take back the last number tapped.</summary>
    public void Delete()
    {
        if (_typed.Count > 0) _typed.RemoveAt(_typed.Count - 1);
    }
}
