namespace Reader.Core;

/// <summary>
/// Turns the times at which frames are asked for into the times the reader should draw them at.
///
/// A screen shows frames at a perfectly even beat, but the moment a program is told "draw the next
/// one" wobbles by a few milliseconds either way. Moving the camera by those wobbly times makes
/// motion judder even when no frame is late. So when frames are arriving at a steady beat this clock
/// steps by exactly one beat (or two, when a frame was missed) and only follows the raw times when
/// the beat really is uneven. It never runs backwards and never drifts away from the real time.
/// </summary>
public sealed class FrameClock
{
    const int Remembered = 31;
    readonly double[] _gaps = new double[Remembered];
    readonly double[] _sorted = new double[Remembered];
    int _count, _next;
    double _lastRaw, _time;
    bool _started;

    /// <summary>The time of the latest frame, evened out, in milliseconds.</summary>
    public double Time => _time;

    /// <summary>The usual gap between frames lately, in milliseconds. 0 until a few frames have been seen.</summary>
    public double Beat { get; private set; }

    /// <summary>Start again, for example after the reader has been hidden.</summary>
    public void Reset()
    {
        _started = false;
        _count = _next = 0;
        Beat = 0;
    }

    /// <summary>Give the raw time of a new frame; get back the time to draw it at.</summary>
    public double Next(double rawMs)
    {
        if (!_started)
        {
            _started = true;
            _lastRaw = _time = rawMs;
            return _time;
        }
        double gap = rawMs - _lastRaw;
        _lastRaw = rawMs;
        if (gap <= 0) return _time;
        if (gap > 250)
        {
            // A long stop (the window was hidden or the computer was busy): pick up from the real time.
            _time = rawMs;
            return _time;
        }

        _gaps[_next] = gap;
        _next = (_next + 1) % Remembered;
        if (_count < Remembered) _count++;
        if (_count >= 5)
        {
            // The beat is the average of the ordinary gaps: those near the middle one. Averaging evens out
            // the wobble (an early frame is followed by a long gap), and leaving out the odd ones keeps a
            // missed frame from stretching it.
            Array.Copy(_gaps, _sorted, _count);
            Array.Sort(_sorted, 0, _count);
            double middle = _sorted[_count / 2], sum = 0;
            int ordinary = 0;
            for (int i = 0; i < _count; i++)
            {
                if (Math.Abs(_sorted[i] - middle) > middle * 0.3) continue;
                sum += _sorted[i];
                ordinary++;
            }
            Beat = ordinary > 0 ? sum / ordinary : middle;
        }

        double before = _time;
        double step = gap;
        if (Beat > 0)
        {
            // Close to a whole number of beats: call it exactly that many.
            double beats = Math.Round(gap / Beat);
            if (beats >= 1 && Math.Abs(gap - beats * Beat) < Beat * 0.3) step = beats * Beat;
        }
        _time += step;

        // Stay with the real time: ease back towards it, and give up evening out if it has got away.
        double behind = rawMs - _time;
        double most = Beat > 0 ? Beat : 16;
        if (Math.Abs(behind) > most) _time = rawMs;
        else _time += behind * 0.02;
        if (_time < before) _time = before;
        return _time;
    }
}
