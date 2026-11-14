namespace Reader.Core;

/// <summary>
/// Turns raw touches, mouse moves and wheel turns into reader actions. It knows nothing about any
/// particular screen toolkit: the view passes on what happened and where, in screen units measured
/// from the top-left of the picture area, with the time in milliseconds.
///
/// Guided pages: tap the left third to go back and anywhere else to go on, swipe, two-finger pinch
/// for the whole page, wheel to move through the stops.
/// Explore pages: drag to move, pinch or wheel to zoom, flick to glide, double-tap to zoom (if that
/// is switched on), tap to find things, touch the corner map to jump there.
/// </summary>
public sealed class GestureInput
{
    sealed class Gesture
    {
        public PointD Start;
        public bool Multi, Explore, Map;
        public double StartDist, LastDist;
        public RectD Cam;
        public PointD Mid;
        public readonly List<(double X, double Y, double T)> Samples = new();
    }

    readonly ReaderEngine _reader;
    readonly Dictionary<long, PointD> _pointers = new();
    Gesture? _gesture;
    double _wheelAcc, _wheelLock, _magnify = 1;
    (double X, double Y, double T)? _lastTap;

    public GestureInput(ReaderEngine reader) => _reader = reader;

    /// <summary>
    /// A tap anywhere goes on, as on a phone, where small hands tap wherever they like; going back is a
    /// swipe to the right. When false (a computer), a tap on the left edge goes back. (A second tap that
    /// comes straight after the first is ignored either way: see <see cref="ReaderEngine.Go"/>.)
    /// </summary>
    public bool TapAnywhereGoesOn { get; set; }


    /// <summary>True while a finger or the mouse button is down on the picture.</summary>
    public bool Active => _gesture is not null;

    public void PointerDown(long id, double x, double y, double nowMs)
    {
        _reader.SetTime(nowMs);
        var p = new PointD(x, y);
        _pointers[id] = p;
        if (_pointers.Count == 1)
        {
            _gesture = new Gesture { Start = p, Explore = _reader.Exploring };
            if (_gesture.Explore && _reader.GetMap() is MapDraw map && map.Rect.Contains(x, y))
            {
                // Touching the corner map moves the view there.
                _gesture.Map = true;
                _reader.MapGoTo(x, y);
                return;
            }
            if (_gesture.Explore) _gesture.Cam = _reader.TakeCamera();
        }
        if (_pointers.Count == 2 && _gesture is { Map: false } g)
        {
            var (a, b) = TwoPointers();
            g.Multi = true;
            g.StartDist = g.LastDist = Distance(a, b);
            if (g.Explore)
            {
                g.Cam = _reader.TakeCamera();
                g.Mid = new PointD((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            }
        }
    }

    public void PointerMove(long id, double x, double y, double nowMs)
    {
        if (!_pointers.ContainsKey(id) || _gesture is not Gesture g) return;
        _reader.SetTime(nowMs);
        var p = new PointD(x, y);
        _pointers[id] = p;
        if (g.Map) { _reader.MapGoTo(x, y); return; }
        if (_pointers.Count == 2)
        {
            var (a, b) = TwoPointers();
            g.LastDist = Distance(a, b);
            if (g.Explore)
            {
                // Zoom around the fingers' midpoint and follow it as it moves.
                double f = g.LastDist / (g.StartDist > 0 ? g.StartDist : 1);
                var mid = new PointD((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                RectD zoomed = _reader.ZoomAt(g.Cam, f, g.Mid.X, g.Mid.Y);
                double sc = _reader.ScreenScale(zoomed);
                _reader.SetCam(new RectD(zoomed.X - (mid.X - g.Mid.X) / sc, zoomed.Y - (mid.Y - g.Mid.Y) / sc, zoomed.W, zoomed.H));
            }
            return;
        }
        if (g.Explore && !g.Multi)
        {
            double sc = _reader.ScreenScale(g.Cam);
            double dx = x - g.Start.X, dy = y - g.Start.Y;
            _reader.SetCam(new RectD(g.Cam.X - dx / sc, g.Cam.Y - dy / sc, g.Cam.W, g.Cam.H), soft: true);
            // Remember the last tenth of a second of movement, to tell a flick from a careful drag.
            g.Samples.Add((x, y, nowMs));
            while (g.Samples.Count > 0 && nowMs - g.Samples[0].T > 90) g.Samples.RemoveAt(0);
        }
    }

    public void PointerUp(long id, double x, double y, double nowMs)
    {
        if (!_pointers.Remove(id)) return;
        if (_pointers.Count > 0 || _gesture is not Gesture g) return;
        _reader.SetTime(nowMs);
        _gesture = null;
        if (g.Map) return;
        double dx = x - g.Start.X, dy = y - g.Start.Y;
        bool isTap = !g.Multi && Math.Sqrt(dx * dx + dy * dy) < 10;

        if (g.Explore)
        {
            if (g.Multi) { _reader.SetCam(_reader.Shown.Cam); return; }
            if (isTap)
            {
                _reader.Ripple(x, y);
                if (_lastTap is var (lx, ly, lt) && nowMs - lt < 320 && Math.Sqrt(Math.Pow(x - lx, 2) + Math.Pow(y - ly, 2)) < 30)
                {
                    // A second tap in the same spot straight away: zoom if that's switched on, otherwise ignore it.
                    _lastTap = null;
                    if (_reader.Settings.DoubleTapZoom) _reader.DoubleTapAt(x, y);
                }
                else
                {
                    _lastTap = (x, y, nowMs);
                    _reader.TapAt(x, y);
                }
                return;
            }
            // Pulled past the edge: ease back. Otherwise a flick keeps gliding the way it was going.
            if (_reader.Settle()) return;
            if (g.Samples.Count >= 2 && !_reader.Reduced)
            {
                var a = g.Samples[0];
                var b = g.Samples[^1];
                double dt = Math.Max(1, b.T - a.T);
                double sc = _reader.ScreenScale(_reader.Shown.Cam);
                double vx = -(b.X - a.X) / dt / sc, vy = -(b.Y - a.Y) / dt / sc;
                if (Math.Sqrt(vx * vx + vy * vy) * sc > 0.25) _reader.StartFling(vx, vy);
            }
            return;
        }

        if (g.Multi)
        {
            double zoom = g.LastDist / (g.StartDist > 0 ? g.StartDist : 1);
            if (zoom < 0.85) _reader.SetOverview(true);
            else if (zoom > 1.15) _reader.SetOverview(false);
            return;
        }
        if (Math.Abs(dx) > 48 && Math.Abs(dx) > Math.Abs(dy)) _reader.Go(dx < 0 ? 1 : -1);
        else if (isTap)
        {
            _reader.Ripple(x, y);
            _reader.Go(!TapAnywhereGoesOn && g.Start.X < _reader.ViewWidth * 0.3 ? -1 : 1);
        }
    }

    /// <summary>The system took the touch away (a phone call, a palm on the screen): forget the gesture.</summary>
    public void PointerCancel(long id)
    {
        _pointers.Remove(id);
        _gesture = null;
    }

    /// <summary>
    /// A wheel turn or a two-finger scroll. Deltas are in screen units, positive down and to the right
    /// (one notch of an ordinary mouse wheel is about 100). <paramref name="zoomKey"/> is Ctrl, which is
    /// also how a trackpad pinch arrives on most systems. <paramref name="smooth"/> is true for a
    /// trackpad that scrolls in both directions.
    /// </summary>
    public void Wheel(double x, double y, double deltaX, double deltaY, bool zoomKey, bool smooth, double nowMs)
    {
        _reader.SetTime(nowMs);
        if (_reader.Exploring)
        {
            RectD cam = _reader.TakeCamera();
            if (zoomKey || !smooth || Math.Abs(deltaX) < 1)
            {
                // Pinch on a trackpad, or a mouse wheel: zoom at the pointer.
                _reader.SetCam(_reader.ZoomAt(cam, Math.Exp(-deltaY * (zoomKey ? 0.01 : 0.0015)), x, y));
            }
            else
            {
                double sc = _reader.ScreenScale(cam);
                _reader.SetCam(new RectD(cam.X + deltaX / sc, cam.Y + deltaY / sc, cam.W, cam.H));
            }
            return;
        }
        if (nowMs < _wheelLock) return;
        if (zoomKey)
        {
            if (deltaY > 4) { _reader.SetOverview(true); _wheelLock = nowMs + 400; }
            else if (deltaY < -4) { _reader.SetOverview(false); _wheelLock = nowMs + 400; }
            return;
        }
        _wheelAcc += deltaY + deltaX;
        if (Math.Abs(_wheelAcc) > 60)
        {
            _reader.Go(_wheelAcc > 0 ? 1 : -1);
            _wheelAcc = 0;
            _wheelLock = nowMs + 350;
        }
    }

    /// <summary>
    /// A trackpad pinch on systems that report it as its own gesture. <paramref name="scaleChange"/> is
    /// how much the pinch grew since the last report (0.05 = five percent bigger).
    /// </summary>
    public void Magnify(double x, double y, double scaleChange, double nowMs)
    {
        _reader.SetTime(nowMs);
        if (_reader.Exploring)
        {
            RectD cam = _reader.TakeCamera();
            _reader.SetCam(_reader.ZoomAt(cam, Math.Max(0.2, 1 + scaleChange), x, y));
            return;
        }
        if (nowMs < _wheelLock) { _magnify = 1; return; }
        _magnify *= Math.Max(0.2, 1 + scaleChange);
        if (_magnify < 0.85) { _reader.SetOverview(true); _magnify = 1; _wheelLock = nowMs + 400; }
        else if (_magnify > 1.15) { _reader.SetOverview(false); _magnify = 1; _wheelLock = nowMs + 400; }
    }

    (PointD A, PointD B) TwoPointers()
    {
        using var e = _pointers.Values.GetEnumerator();
        e.MoveNext();
        PointD a = e.Current;
        e.MoveNext();
        return (a, e.Current);
    }

    static double Distance(PointD a, PointD b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
