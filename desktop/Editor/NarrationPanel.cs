using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Editor.Core;
using Reader.Core;

namespace BookEditor;

/// <summary>A recording read once, as one channel at a low rate: enough to draw its shape and know how long it is.</summary>
static class SoundShapes
{
    public const int Rate = 8000;
    static readonly Dictionary<string, (DateTime Stamp, float[]? Samples)> Known = new();
    static readonly HashSet<string> Loading = new();
    public static event Action? Arrived;
    public static bool Synchronous { get; set; }

    public static float[]? Get(string path)
    {
        if (!File.Exists(path)) return null;
        DateTime stamp = File.GetLastWriteTimeUtc(path);
        if (Known.TryGetValue(path, out var k) && k.Stamp == stamp) return k.Samples;
        if (Synchronous) { var s = Read(path); Known[path] = (stamp, s); return s; }
        if (!Loading.Add(path)) return null;
        Task.Run(() =>
        {
            float[]? s = Read(path);
            Dispatcher.UIThread.Post(() => { Known[path] = (stamp, s); Loading.Remove(path); Arrived?.Invoke(); });
        });
        return null;
    }

    static float[]? Read(string path)
    {
        // A plain WAV is read directly; anything else (mp3, ogg) through the reader's sound library.
        if (Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase) && Editor.Core.Wav.Read(path) is var (samples, rate) && rate > 0)
        {
            if (rate == Rate) return samples;
            var down = new float[(int)((long)samples.Length * Rate / rate)];
            for (int i = 0; i < down.Length; i++)
            {
                int from = (int)((long)i * rate / Rate), to = Math.Min(samples.Length, (int)((long)(i + 1) * rate / Rate));
                float peak = 0;
                for (int j = from; j < to; j++) if (Math.Abs(samples[j]) > Math.Abs(peak)) peak = samples[j];
                down[i] = peak;
            }
            return down;
        }
        return Reader.Audio.DesktopSound.ReadSoundFile(path, Rate, 1);
    }

    public static double Seconds(float[] s) => s.Length / (double)Rate;
}

/// <summary>
/// The recording for one stop: add a sound file or record one, trim the start and end, and time the
/// words by tapping along. Words light up in the apps as they are read.
/// </summary>
sealed class NarrationPanel : UserControl
{
    readonly EditorState _state;
    readonly MainWindow _window;
    readonly Stop _stop;
    readonly int _page, _index;
    readonly StackPanel _body = new();
    readonly DispatcherTimer _timer;
    Waveform? _wave;
    TapAlong? _tapping;
    WrapPanel? _words;
    TextBlock? _status;
    int _selectedWord = -1;
    double _playStarted;
    bool _playing;
    readonly System.Diagnostics.Stopwatch _clock = new();

    public NarrationPanel(EditorState state, MainWindow window, Stop stop, int page, int index)
    {
        _state = state;
        _window = window;
        _stop = stop;
        _page = page;
        _index = index;
        Content = _body;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Render, (_, _) => Tick());
        SoundShapes.Arrived += Build;
        Build();
    }

    Voice? Voice => _stop.Voice;

    public void Detach()
    {
        SoundShapes.Arrived -= Build;
        if (_tapping is not null) FinishTapping(save: true);
        StopPlaying();
        // Something else was clicked while recording: keep what was recorded rather than lose it.
        if (_window.Recorder.Recording) Dispatcher.UIThread.Post(StopRecording);
    }

    string? FullPath => Voice is null ? null : _state.Project!.PathOf(Voice.File);

    void Build()
    {
        _body.Children.Clear();
        _wave = null;
        _words = null;
        if (_window.Recorder.Recording) { BuildRecording(); return; }
        if (Voice is not Voice v)
        {
            _body.Children.Add(Ui.Help("No recording yet. With a recording, the apps read the words aloud when Read to me is on, and each word lights up as it is read."));
            var record = Ui.Button("● Record", StartRecording, "Record from the microphone. Read the words, then press Stop.", small: true);
            record.IsEnabled = _window.MicrophoneProblem.Length == 0;
            _body.Children.Add(Ui.Row(Ui.Button("Add a recording...", PickFile, "A wav, mp3 or ogg file of the words being read", small: true), record));
            if (_window.MicrophoneProblem.Length > 0) _body.Children.Add(Ui.Help(_window.MicrophoneProblem + " You can still add a recording made elsewhere."));
            return;
        }

        float[]? samples = FullPath is string p ? SoundShapes.Get(p) : null;
        bool missing = FullPath is null || !File.Exists(FullPath);
        double length = samples is null ? 0 : SoundShapes.Seconds(samples);
        string info = missing ? "The recording is missing from the book's folder." : samples is null ? "Reading the recording..." : $"{length:0.0} seconds";
        _body.Children.Add(Ui.Label($"{v.File}  ·  {info}", 12.5, missing ? Ui.Bad : Ui.Text));

        if (samples is not null)
        {
            _wave = new Waveform(samples, v, ReadAlong.Words(_stop.Caption).Select(w => _stop.Caption!.Substring(w.Start, w.Length)).ToList())
            {
                Height = 96, Margin = new Thickness(0, 6, 0, 2),
            };
            _wave.TrimChanged += (start, end) => _state.Change("Trim the recording", () => { v.Start = start > 0.005 ? Math.Round(start, 3) : null; v.End = Math.Round(end, 3); }, mergeKey: "trim", source: this);
            _wave.WordMoved += (i, t) => _state.Change("Move a word", () => { var list = v.Words; if (i < list.Count) { TapAlong.Nudge(list, i, t - list[i], v.StartOrZero, v.End ?? length); } }, mergeKey: "word" + i, source: this);
            _wave.WordClicked += i => { _selectedWord = i; RefreshWords(); };
            _wave.Seek += t => Play(t);
            _body.Children.Add(_wave);
            _body.Children.Add(Ui.Help("Drag the orange edges to trim quiet or mistakes off the start and end. Click the shape to play from there. Drag a word's line to move it."));
        }

        if (_tapping is not null)
        {
            _body.Children.Add(new Border
            {
                Background = Ui.Brush("#3a2a14"), BorderBrush = Ui.Accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 6, 0, 4),
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        Ui.Label("Tapping along", 13.5, Ui.Text, FontWeight.Bold),
                        Ui.Label("Press Space (or the Tap button) as each word starts. Backspace takes back the last tap. Esc stops.", 12.5, Ui.Text),
                        Ui.Row(Ui.Button("Tap", () => Tap(), "Space", primary: true), Ui.Button("Take back", () => { _tapping?.Back(); RefreshWords(); }, "Backspace", small: true), Ui.Button("Stop", () => FinishTapping(save: true), "Esc", small: true)),
                    },
                },
            });
        }
        else
        {
            int count = ReadAlong.WordCount(_stop.Caption);
            var play = Ui.Button(_playing ? "■ Stop" : "▶ Play", () => { if (_playing) StopPlaying(); else Play(v.StartOrZero); }, "Play the recording (the trimmed part)", small: true);
            var tap = Ui.Button("Tap along", StartTapping, "Play the recording and press Space as each word starts", small: true);
            tap.IsEnabled = count > 0 && samples is not null;
            var even = Ui.Button("Space words evenly", () => _state.Change("Space words evenly", () => v.Words = TapAlong.Spread(_stop.Caption, v.StartOrZero + 0.05, (v.End ?? length) - 0.3), structure: true),
                "A first guess to fix up: the words spread across the recording", small: true);
            even.IsEnabled = count > 0 && samples is not null;
            _body.Children.Add(Ui.Row(play, tap, even));
            var record = Ui.Button("Record again", StartRecording, small: true);
            record.IsEnabled = _window.MicrophoneProblem.Length == 0;
            _body.Children.Add(Ui.Row(Ui.Button("Replace...", PickFile, small: true), record,
                Ui.Button("Remove", () => _state.Change("Remove the recording", () => _stop.Voice = null, structure: true), "The file stays in the folder; Check book lists it as unused", small: true)));
            if (count == 0) _body.Children.Add(Ui.Help("Type the words above first, then they can be timed to the recording."));
        }

        _status = Ui.Help("");
        _words = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        _body.Children.Add(_words);
        _body.Children.Add(_status);
        RefreshWords();
        if (!_window.SoundAvailable) _body.Children.Add(Ui.Help("No sound device is working, so the recording can't be heard here. Timing by tapping needs sound."));
    }

    void BuildRecording()
    {
        var meter = new Border { Height = 8, Background = Ui.Bad, CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left, Width = 2 };
        var time = Ui.Label("0.0 seconds", 13, Ui.Text);
        _body.Children.Add(new Border
        {
            Background = Ui.Brush("#3a1a1a"), BorderBrush = Ui.Bad, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    Ui.Label("● Recording. Read the words, then press Stop.", 13.5, Ui.Text, FontWeight.Bold),
                    new Border { Background = Ui.Field, CornerRadius = new CornerRadius(4), Child = meter },
                    time,
                    Ui.Row(Ui.Button("■ Stop", StopRecording, primary: true), Ui.Button("Cancel", () => { _window.Recorder.Stop(); Build(); }, small: true)),
                },
            },
        });
        var t = new DispatcherTimer(TimeSpan.FromMilliseconds(60), DispatcherPriority.Render, (_, _) => { });
        t.Tick += (_, _) =>
        {
            if (!_window.Recorder.Recording) { t.Stop(); return; }
            meter.Width = Math.Max(2, Math.Min(1, _window.Recorder.Level * 1.6) * 300);
            time.Text = $"{_window.Recorder.Seconds:0.0} seconds";
        };
        t.Start();
    }

    // ---------------------------------------------------------------- the word strip

    void RefreshWords()
    {
        if (_words is null || Voice is not Voice v) return;
        _words.Children.Clear();
        var words = ReadAlong.Words(_stop.Caption);
        IReadOnlyList<double> times = _tapping is not null ? _tapping.Times : v.Words;
        int lit = _tapping is not null ? _tapping.NextWord : _playing ? ReadAlong.WordAt(v.Words, words.Count, Position()) : -1;
        for (int i = 0; i < words.Count; i++)
        {
            string text = _stop.Caption!.Substring(words[i].Start, words[i].Length);
            string time = i < times.Count ? times[i].ToString("0.00", CultureInfo.InvariantCulture) : "--";
            bool sel = i == _selectedWord && _tapping is null;
            var chip = new Border
            {
                Background = i == lit ? Ui.Brush("#ffe066") : sel ? Ui.Brush("#2f3441") : Ui.Raised, CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 3), Margin = new Thickness(0, 0, 4, 4),
                BorderBrush = sel ? Ui.Accent : Brushes.Transparent, BorderThickness = new Thickness(1), Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = i == lit ? Ui.AccentInk : Ui.Text },
                        new TextBlock { Text = time, FontSize = 10.5, Foreground = i == lit ? Ui.AccentInk : Ui.Muted },
                    },
                },
            };
            int index = i;
            chip.PointerPressed += (_, _) => { _selectedWord = index; RefreshWords(); if (index < v.Words.Count) Play(Math.Max(v.StartOrZero, v.Words[index] - 0.15)); };
            _words.Children.Add(chip);
        }
        if (_status is not null)
        {
            _status.Text = _tapping is not null ? $"Word {Math.Min(_tapping.NextWord + 1, words.Count)} of {words.Count} next."
                : words.Count == 0 ? ""
                : v.Words.Count == 0 ? "The words aren't timed yet. Press Tap along."
                : v.Words.Count != words.Count ? "The words have changed since they were timed. Press Tap along again."
                : _selectedWord >= 0 ? "Nudge the chosen word with , (earlier) and . (later), or with the buttons below."
                : "Click a word to hear it and to nudge its time.";
        }
        if (_selectedWord >= 0 && _selectedWord < v.Words.Count && _tapping is null)
        {
            _words.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 2, 0, 0),
                Children = { Ui.Button("◀ Earlier", () => Nudge(-0.05), ", (comma)", small: true), Ui.Button("Later ▶", () => Nudge(0.05), ". (full stop)", small: true) },
            });
        }
        _wave?.Update(times, lit, _playing ? Position() : -1);
    }

    void Nudge(double by)
    {
        if (Voice is not Voice v || _selectedWord < 0 || _selectedWord >= v.Words.Count) return;
        double end = v.End ?? 600;
        int w = _selectedWord;
        _state.Change("Nudge a word", () => TapAlong.Nudge(v.Words, w, by, v.StartOrZero, end), mergeKey: "nudge" + w, source: this);
        RefreshWords();
        Play(Math.Max(v.StartOrZero, v.Words[w] - 0.15));
    }

    /// <summary>Keys for this panel: Space taps, Backspace takes back, Esc stops, comma and full stop nudge. True if used.</summary>
    public bool HandleKey(Key key)
    {
        if (_tapping is not null)
        {
            switch (key)
            {
                case Key.Space or Key.T or Key.Enter: Tap(); return true;
                case Key.Back: _tapping.Back(); RefreshWords(); return true;
                case Key.Escape: FinishTapping(save: true); return true;
            }
            return false;
        }
        switch (key)
        {
            case Key.OemComma: Nudge(-0.05); return _selectedWord >= 0;
            case Key.OemPeriod: Nudge(0.05); return _selectedWord >= 0;
        }
        return false;
    }

    // ---------------------------------------------------------------- playing

    double Position()
    {
        double p = _window.Sound.VoicePosition;
        if (p >= 0) return p;
        return _playStarted + _clock.Elapsed.TotalSeconds;     // no sound device: keep time anyway
    }

    void Play(double from)
    {
        if (Voice is not Voice v || FullPath is not string path) return;
        _window.Sound.PlayVoice(path, from, v.End ?? 0);
        _playStarted = from;
        _clock.Restart();
        bool was = _playing;
        _playing = true;
        _timer.Start();
        if (!was) Build();
    }

    void StopPlaying()
    {
        _timer.Stop();
        _window.Sound.StopVoice();
        _clock.Reset();
        if (_playing) { _playing = false; Build(); }
    }

    void Tick()
    {
        if (Voice is not Voice v) { StopPlaying(); return; }
        double pos = Position();
        double end = v.End ?? (_wave?.Length ?? 0);
        bool finished = _window.SoundAvailable ? !_window.Sound.VoicePlaying && _clock.Elapsed.TotalSeconds > 0.4 : pos >= end;
        if (finished)
        {
            if (_tapping is not null) { FinishTapping(save: true); return; }
            StopPlaying();
            return;
        }
        RefreshWords();
    }

    // ---------------------------------------------------------------- tapping along

    void StartTapping()
    {
        if (Voice is not Voice v) return;
        _tapping = new TapAlong(ReadAlong.WordCount(_stop.Caption), v.StartOrZero);
        _selectedWord = -1;
        _window.FocusForKeys();
        Play(v.StartOrZero);
        Build();
    }

    void Tap()
    {
        if (_tapping is null) return;
        _tapping.Tap(Position());
        if (_tapping.Done) { FinishTapping(save: true); return; }
        RefreshWords();
    }

    void FinishTapping(bool save)
    {
        TapAlong? t = _tapping;
        _tapping = null;
        if (t is not null && save && t.Times.Count > 0 && Voice is Voice v)
        {
            List<double> result = t.Result(v.Words);
            _state.Change("Time the words", () => v.Words = result, source: this);
        }
        StopPlaying();
        Build();
    }

    // ---------------------------------------------------------------- adding and recording

    async void PickFile()
    {
        string? file = await _window.PickOneFile("Choose a recording", new[] { "*.wav", "*.mp3", "*.ogg", "*.flac" }, "Recordings");
        if (file is null) return;
        string name = _state.Project!.Import(file, "voice", $"p{_page + 1}-s{_index + 1}{Path.GetExtension(file)}");
        _state.Change("Add a recording", () =>
        {
            _stop.Voice = new Voice { File = name };
        }, structure: true);
        _window.Say("Added the recording. Trim it if needed, then press Tap along to time the words.");
    }

    void StartRecording()
    {
        StopPlaying();
        if (!_window.Recorder.Start())
        {
            _window.Say(_window.Recorder.Problem);
            return;
        }
        Build();
    }

    void StopRecording()
    {
        float[] samples = _window.Recorder.Stop();
        if (samples.Length < Microphone.SampleRate / 5)
        {
            _window.Say("That recording was too short to keep.");
            Build();
            return;
        }
        string name = _state.Project!.UniqueName($"p{_page + 1}-s{_index + 1}.wav", "voice");
        Editor.Core.Wav.Write(_state.Project.PathOf(name)!, samples, Microphone.SampleRate);
        var (start, end) = Editor.Core.Wav.Sounding(samples, Microphone.SampleRate);
        _state.Change("Record the words", () => _stop.Voice = new Voice { File = name, Start = start > 0.005 ? Math.Round(start, 3) : null, End = Math.Round(end, 3) }, structure: true);
        _window.Say("Recorded. The quiet at each end was trimmed off. Press Tap along to time the words.");
    }
}

/// <summary>
/// The shape of a recording, with the trimmed part, the playhead and a line where each word starts.
/// Drag the orange edges to trim, click to play from a point, drag a word's line to move it.
/// </summary>
sealed class Waveform : Control
{
    readonly float[] _peaks;
    readonly Voice _voice;
    readonly List<string> _wordText;
    IReadOnlyList<double> _times;
    int _lit = -1;
    double _play = -1;
    enum Grab { None, Start, End, Word }
    Grab _grab;
    int _grabWord;
    double _start, _end;

    public Waveform(float[] samples, Voice voice, List<string> words)
    {
        Length = SoundShapes.Seconds(samples);
        _peaks = Editor.Core.Wav.Peaks(samples, 600);
        _voice = voice;
        _wordText = words;
        _times = voice.Words;
        _start = voice.StartOrZero;
        _end = voice.End is double e && e > 0 ? Math.Min(e, Length) : Length;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public double Length { get; }

    public event Action<double, double>? TrimChanged;
    public event Action<int, double>? WordMoved;
    public event Action<int>? WordClicked;
    public event Action<double>? Seek;

    public void Update(IReadOnlyList<double> times, int lit, double play)
    {
        _times = times;
        _lit = lit;
        _play = play;
        InvalidateVisual();
    }

    double X(double t) => Length <= 0 ? 0 : t / Length * Bounds.Width;
    double T(double x) => Math.Clamp(x / Math.Max(1, Bounds.Width) * Length, 0, Length);

    static readonly Typeface Face = new(FontFamily.Default);

    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width, h = Bounds.Height, mid = (h - 16) / 2;
        ctx.DrawRectangle(Ui.Field, null, new Rect(0, 0, w, h), 6, 6);
        var on = new SolidColorBrush(Color.Parse("#7fb8ff")); var off = new SolidColorBrush(Color.Parse("#3a4558"));
        for (int i = 0; i < _peaks.Length; i++)
        {
            double x = i * w / _peaks.Length, t = (i + 0.5) / _peaks.Length * Length;
            double a = Math.Max(1, _peaks[i] * mid * 0.95);
            ctx.FillRectangle(t >= _start && t <= _end ? on : off, new Rect(x, mid - a, Math.Max(1, w / _peaks.Length - 0.4), a * 2));
        }
        // Trimmed-off parts are shaded, with an orange handle at each edge.
        var shade = new SolidColorBrush(Colors.Black, 0.45);
        ctx.FillRectangle(shade, new Rect(0, 0, X(_start), h));
        ctx.FillRectangle(shade, new Rect(X(_end), 0, w - X(_end), h));
        foreach (double t in new[] { _start, _end })
        {
            ctx.FillRectangle(Ui.Accent, new Rect(X(t) - 2, 0, 4, h));
            ctx.FillRectangle(Ui.Accent, new Rect(X(t) - 6, 0, 12, 12));
        }
        // Word lines.
        for (int i = 0; i < _times.Count; i++)
        {
            double x = X(_times[i]);
            var brush = i == _lit ? Ui.Gold : Ui.Brush("#ffe9a8");
            ctx.FillRectangle(brush, new Rect(x - 0.75, 0, 1.5, h - 16));
            if (i < _wordText.Count)
            {
                var ft = new FormattedText(_wordText[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, 10.5, i == _lit ? Ui.Gold : Ui.Muted);
                ctx.DrawText(ft, new Point(x + 2, h - 15));
            }
        }
        if (_play >= 0) ctx.FillRectangle(Brushes.White, new Rect(X(_play) - 1, 0, 2, h));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Point p = e.GetPosition(this);
        e.Pointer.Capture(this);
        if (Math.Abs(p.X - X(_start)) < 8) { _grab = Grab.Start; return; }
        if (Math.Abs(p.X - X(_end)) < 8) { _grab = Grab.End; return; }
        for (int i = 0; i < _times.Count; i++)
        {
            if (Math.Abs(p.X - X(_times[i])) < 5 && p.Y < Bounds.Height - 16) { _grab = Grab.Word; _grabWord = i; WordClicked?.Invoke(i); return; }
        }
        _grab = Grab.None;
        Seek?.Invoke(Math.Clamp(T(p.X), _start, _end));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_grab == Grab.None) return;
        double t = T(e.GetPosition(this).X);
        switch (_grab)
        {
            case Grab.Start: _start = Math.Min(t, _end - 0.2); TrimChanged?.Invoke(_start, _end); break;
            case Grab.End: _end = Math.Max(t, _start + 0.2); TrimChanged?.Invoke(_start, _end); break;
            case Grab.Word: WordMoved?.Invoke(_grabWord, t); break;
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _grab = Grab.None;
        e.Pointer.Capture(null);
    }
}
