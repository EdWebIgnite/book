using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Editor.Core;
using Reader.Core;
using Whisper.net;

namespace BookEditor;

/// <summary>
/// Which recording, found again by where it is in the book: a stop or a thing to find. (Undo and redo
/// swap in fresh copies of the book, so the objects themselves can't be held on to while the computer
/// listens.)
/// </summary>
readonly record struct VoiceTarget(int Page, bool Hotspot, int Index)
{
    public Voice? Voice(Book b) => Find(b) is var (voice, _) ? voice : null;
    public string? Caption(Book b) => Find(b) is var (_, caption) ? caption : null;

    (Voice? Voice, string? Caption)? Find(Book b)
    {
        if (Page < 0 || Page >= b.Pages.Count) return null;
        Page p = b.Pages[Page];
        if (Hotspot) return Index >= 0 && Index < p.Hotspots.Count ? (p.Hotspots[Index].Voice, p.Hotspots[Index].Caption) : null;
        return Index >= 0 && Index < p.Stops.Count ? (p.Stops[Index].Voice, p.Stops[Index].Caption) : null;
    }

    public string Name => Hotspot ? $"page {Page + 1}, thing to find {Index + 1}" : $"page {Page + 1}, stop {Index + 1}";
}

/// <summary>
/// The speech recogniser: listens to a recording and says which words it heard, and when. It runs on this
/// computer with no internet (see <see cref="SpeechModel"/>). Loading it takes a few seconds and a few
/// hundred MB of memory, so it is loaded once, the first time it's needed.
/// </summary>
static class SpeechRecognizer
{
    static WhisperFactory? _factory;
    static string? _loadedFrom;
    static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Thrown with a message for the author when the recogniser can't run on this computer.</summary>
    public sealed class Unavailable(string message, Exception? inner = null, bool perRecording = false) : Exception(message, inner)
    {
        /// <summary>A problem with one recording (missing, unreadable), not with the recogniser itself.</summary>
        public bool PerRecording { get; } = perRecording;
    }

    /// <summary>
    /// Listen to one channel of samples at 16000 a second. <paramref name="hint"/> (the caption) helps it
    /// expect the right words. <paramref name="language"/> is "auto", "en", "af" and so on.
    /// </summary>
    public static async Task<List<HeardWord>> Listen(string modelPath, float[] samples, string? hint, string language, IProgress<int>? progress, CancellationToken cancel)
    {
        await Gate.WaitAsync(cancel);
        try
        {
            WhisperFactory factory = Load(modelPath);
            cancel.ThrowIfCancellationRequested();
            var builder = factory.CreateBuilder()
                .WithLanguage(string.IsNullOrEmpty(language) ? "auto" : language)
                .WithTokenTimestamps()
                .WithThreads(Math.Clamp(Environment.ProcessorCount - 1, 1, 8));
            if (!string.IsNullOrWhiteSpace(hint)) builder = builder.WithPrompt(hint);
            if (progress is not null) builder = builder.WithProgressHandler(p => progress.Report(p));
            using WhisperProcessor processor = builder.Build();
            var pieces = new List<HeardPieces.Piece>();
            await foreach (SegmentData seg in processor.ProcessAsync(samples, cancel))
            {
                foreach (WhisperToken t in seg.Tokens) pieces.Add(new HeardPieces.Piece(t.Text ?? "", t.Start / 100.0, t.End / 100.0, t.DtwTimestamp >= 0 ? t.DtwTimestamp / 100.0 : -1));
            }
            return Onsets.Snap(samples, 16000, HeardPieces.ToWords(pieces));
        }
        catch (Exception e) when (e is not OperationCanceledException and not Unavailable)
        {
            throw new Unavailable("The speech recogniser stopped with a problem, so nothing was changed. Tap along still works.", e);
        }
        finally { Gate.Release(); }
    }

    static WhisperFactory Load(string modelPath)
    {
        if (_factory is not null && _loadedFrom == modelPath) return _factory;
        try
        {
            _factory?.Dispose();
            // The attention-based timing points need to know which size of recogniser file this is (the one SpeechModel downloads).
            _factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseDtwTimeStamps = true, HeadsPreset = WhisperAlignmentHeadsPreset.Base });
            _loadedFrom = modelPath;
            return _factory;
        }
        catch (Exception e)
        {
            _factory = null;
            bool missingCode = e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or TypeInitializationException
                || e.Message.Contains("native", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("library", StringComparison.OrdinalIgnoreCase);
            throw new Unavailable(missingCode
                ? "Automatic timing can't start on this computer (a part of it that runs on the processor wouldn't load). Tap along still works."
                : "The speech recogniser's file couldn't be opened. It may be damaged: it will be downloaded again next time. Tap along still works.", e);
        }
    }

    /// <summary>Let go of the recogniser (and its memory).</summary>
    public static void Unload()
    {
        _factory?.Dispose();
        _factory = null;
        _loadedFrom = null;
    }

    /// <summary>
    /// A recording as one channel at 16000 samples a second, cut to the trimmed part, which is what the
    /// recogniser listens to. Null if the file can't be read.
    /// </summary>
    public static float[]? ReadForListening(string path, double from, double to)
    {
        const int rate = 16000;
        float[]? all = null;
        if (Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase) && Editor.Core.Wav.Read(path) is var (samples, srcRate) && srcRate > 0)
            all = Resample(samples, srcRate, rate);
        all ??= Reader.Audio.DesktopSound.ReadSoundFile(path, rate, 1);
        if (all is null || all.Length == 0) return null;
        int a = Math.Clamp((int)(from * rate), 0, all.Length);
        int b = to > from ? Math.Clamp((int)(to * rate), a, all.Length) : all.Length;
        var cut = all[a..b];
        // A little quiet at each end helps it hear the first and last words.
        var padded = new float[cut.Length + rate / 2];
        cut.CopyTo(padded, rate / 4);
        return padded;
    }

    /// <summary>The time the padding at the start adds, in seconds, to take off what it hears.</summary>
    public const double Lead = 0.25;

    static float[] Resample(float[] s, int from, int to)
    {
        if (from == to) return s;
        int n = (int)((long)s.Length * to / from);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            double x = i * (double)from / to;
            int j = (int)x;
            double f = x - j;
            float a = s[Math.Min(j, s.Length - 1)], b = s[Math.Min(j + 1, s.Length - 1)];
            o[i] = (float)(a * (1 - f) + b * f);
        }
        return o;
    }
}

/// <summary>
/// "Time words automatically" and "Time all stops": the computer listens to a recording and works out
/// when each word of the caption starts, so the author only has to check it and nudge a word or two.
/// The first time, it downloads the speech recogniser's file (with a progress bar and a way to stop).
/// Anything that goes wrong ends in a plain message, and tapping along always still works.
/// </summary>
sealed class AutoTiming
{
    readonly MainWindow _window;
    readonly EditorState _state;
    readonly Border _panel;
    readonly TextBlock _title, _text;
    readonly ProgressBar _bar;
    CancellationTokenSource? _cancel;

    public AutoTiming(MainWindow window, EditorState state)
    {
        _window = window;
        _state = state;
        _title = Ui.Label("", 14, Ui.Text, FontWeight.Bold);
        _text = Ui.Label("", 12.5, Ui.Muted);
        _bar = new ProgressBar { Minimum = 0, Maximum = 1, Height = 8, Margin = new Thickness(0, 8, 0, 8), Foreground = Ui.Accent };
        var stop = Ui.Button("Stop", () => _cancel?.Cancel(), "Stop now. Nothing is changed.", small: true);
        _panel = new Border
        {
            IsVisible = false, Width = 380, Background = Ui.Panel, BorderBrush = Ui.Accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12), Margin = new Thickness(0, 0, 400, 110), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 8, Blur = 30, Color = Color.FromArgb(140, 0, 0, 0) }),
            Child = new StackPanel { Children = { _title, _text, _bar, new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { stop } } } },
        };
        Model = new SpeechModel();
    }

    /// <summary>The progress card, shown over the window while it works.</summary>
    public Control Panel => _panel;

    public SpeechModel Model { get; set; }
    public bool Busy => _cancel is not null;

    /// <summary>Raised when timing starts or finishes, so buttons can grey out.</summary>
    public event Action? BusyChanged;

    // ---------------------------------------------------------------- the language setting

    static string SettingsFile => Path.Combine(EditorPaths.AppData, "word-timing.json");

    /// <summary>The language of the recordings: "auto" (worked out from the sound), "en" or "af".</summary>
    public static string Language
    {
        get
        {
            try { return File.Exists(SettingsFile) && JsonNode.Parse(File.ReadAllText(SettingsFile))?["language"]?.GetValue<string>() is { Length: > 0 } l ? l : "auto"; }
            catch (Exception) { return "auto"; }
        }
        set
        {
            try
            {
                Directory.CreateDirectory(EditorPaths.AppData);
                File.WriteAllText(SettingsFile, new JsonObject { ["language"] = value }.ToJsonString());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    public static readonly (string Value, string Text)[] Languages = { ("auto", "Work it out from the recording"), ("en", "English"), ("af", "Afrikaans") };

    // ---------------------------------------------------------------- the progress card

    void Show(string title, string text, double? progress)
    {
        _title.Text = title;
        _text.Text = text;
        _bar.IsIndeterminate = progress is null;
        if (progress is double p) _bar.Value = Math.Clamp(p, 0, 1);
        _panel.IsVisible = true;
    }

    void Begin()
    {
        _cancel = new CancellationTokenSource();
        BusyChanged?.Invoke();
    }

    void End()
    {
        _cancel?.Dispose();
        _cancel = null;
        _panel.IsVisible = false;
        BusyChanged?.Invoke();
    }

    // ---------------------------------------------------------------- getting the recogniser

    /// <summary>Make sure the recogniser's file is here, asking first and downloading it if not. False if it isn't.</summary>
    async Task<bool> EnsureModel(CancellationToken cancel, bool ask = true)
    {
        if (Model.Ready) return true;
        if (ask)
        {
            int a = await _window.Dialogs.Ask("Time words automatically",
                $"The first time, the editor needs to download its speech recogniser (about {Model.Megabytes} MB). It is kept on this computer, so after this it works without the internet, and your recordings never leave the computer.",
                new[] { "Download it", "Not now" }, 0, 1);
            if (a != 0) return false;
        }
        return await Download(cancel);
    }

    /// <summary>Download the recogniser's file, showing how far it has got. False if it didn't arrive.</summary>
    public async Task<bool> Download(CancellationToken cancel)
    {
        double mb = Model.From.Size / 1024.0 / 1024.0;
        Show("Downloading the speech recogniser", "Starting...", 0);
        var progress = new Progress<(long Done, long Total)>(p =>
            Show("Downloading the speech recogniser", $"{p.Done / 1024.0 / 1024.0:0} of {mb:0} MB. This happens only once.", (double)p.Done / Math.Max(1, p.Total)));
        try
        {
            await Task.Run(() => Model.Download(progress, cancel), cancel);
            return true;
        }
        catch (OperationCanceledException)
        {
            _window.Say("Stopped the download. Next time it carries on from where it got to.", 6);
        }
        catch (Exception e)
        {
            string why = e is IOException ? e.Message : "Something went wrong.";
            _window.Say($"The speech recogniser couldn't be downloaded. {why} Check the internet connection and try again. Tap along still works.", 10);
        }
        return false;
    }

    /// <summary>Start a download with no question (for the screenshot hook). Returns its stopper.</summary>
    public CancellationTokenSource StartDownload()
    {
        Begin();
        CancellationTokenSource c = _cancel!;
        _ = Download(c.Token).ContinueWith(_ => Dispatcher.UIThread.Post(End));
        return c;
    }

    // ---------------------------------------------------------------- listening

    /// <summary>The words of one recording, timed. Null with a message said, when it can't be done.</summary>
    async Task<(List<double> Times, WordAligner.Result Result)?> Listen(Voice v, string caption, CancellationToken cancel, string title, double from = 0, double share = 1)
    {
        string? path = _state.Project!.PathOf(v.File);
        if (path is null || !File.Exists(path)) throw new SpeechRecognizer.Unavailable("The recording is missing from the book's folder.", perRecording: true);
        double start = v.StartOrZero;
        float[]? samples = await Task.Run(() => SpeechRecognizer.ReadForListening(path, start, v.End ?? 0), cancel);
        if (samples is null) throw new SpeechRecognizer.Unavailable("The recording couldn't be read. Try saving it again as a wav or mp3 file.", perRecording: true);
        double end = v.End is double e && e > start ? e : start + samples.Length / 16000.0 - 2 * SpeechRecognizer.Lead;
        Show(title, "Listening to the recording...", from);
        var progress = new Progress<int>(p => Show(title, "Listening to the recording...", from + share * Math.Clamp(p, 0, 100) / 100.0));
        string language = Language;
        string modelPath = Model.FilePath;
        List<HeardWord> heard = await Task.Run(() => SpeechRecognizer.Listen(modelPath, samples, caption, language, progress, cancel), cancel);
        // What it heard is timed from the start of the cut (with the bit of quiet added in front).
        heard = heard.Select(w => w with { Start = w.Start - SpeechRecognizer.Lead + start, End = w.End - SpeechRecognizer.Lead + start }).ToList();
        WordAligner.Result? r = WordAligner.Align(caption, heard, start, end);
        return r is null ? null : (r.Times, r);
    }

    /// <summary>Time the words of one recording.</summary>
    public async Task TimeOne(VoiceTarget target)
    {
        if (Busy) { _window.Say("The words are already being timed. Wait for that to finish, or press Stop."); return; }
        if (!_state.HasBook || target.Voice(_state.Book) is not Voice v) return;
        string caption = target.Caption(_state.Book) ?? "";
        if (ReadAlong.WordCount(caption) == 0) { _window.Say("Type the words first, then they can be timed."); return; }
        Begin();
        try
        {
            if (!await EnsureModel(_cancel!.Token)) return;
            string file = v.File;
            var got = await Listen(v, caption, _cancel.Token, "Timing the words");
            if (got is not var (times, result))
            {
                _window.Say("The words couldn't be made out in this recording, so nothing was changed. Check the trim covers the reading, or use Tap along.", 10);
                return;
            }
            // Only if the same recording and words are still there (the author may have changed them meanwhile).
            if (target.Voice(_state.Book) is not Voice now || now.File != file || target.Caption(_state.Book) != caption)
            {
                _window.Say("The recording or its words changed while it was listening, so nothing was changed. Try again.", 8);
                return;
            }
            _state.Change("Time the words automatically", () => now.Words = times, structure: true);
            _window.Say(result.Guessed == 0
                ? $"Timed all {result.Total} words. Play it to check, and nudge any word that's a little off."
                : $"Timed {result.Total} words. {result.Guessed} of them weren't heard clearly and were placed between the others, so play it to check those.", 9);
        }
        catch (OperationCanceledException) { _window.Say("Stopped. Nothing was changed.", 4); }
        catch (SpeechRecognizer.Unavailable e) { _window.Say(e.Message, 10); }
        finally { End(); }
    }

    /// <summary>Time every recording in the book (stops and things to find), as one step that can be undone.</summary>
    public async Task TimeAll()
    {
        if (Busy) { _window.Say("The words are already being timed. Wait for that to finish, or press Stop."); return; }
        if (!_state.HasBook) return;
        Book book = _state.Book;
        var all = new List<VoiceTarget>();
        for (int p = 0; p < book.Pages.Count; p++)
        {
            Page page = book.Pages[p];
            for (int i = 0; i < page.Stops.Count; i++) if (page.Stops[i].Voice is not null && ReadAlong.WordCount(page.Stops[i].Caption) > 0) all.Add(new VoiceTarget(p, false, i));
            for (int i = 0; i < page.Hotspots.Count; i++) if (page.Hotspots[i].Voice is not null && ReadAlong.WordCount(page.Hotspots[i].Caption) > 0) all.Add(new VoiceTarget(p, true, i));
        }
        all.RemoveAll(t => !_state.Project!.Has(t.Voice(book)!.File));
        if (all.Count == 0) { _window.Say("No stop has both words and a recording yet, so there's nothing to time."); return; }
        var untimed = all.Where(t => !ReadAlong.TimesFit(t.Voice(book)!, t.Caption(book)) || t.Voice(book)!.Words.Count == 0).ToList();
        List<VoiceTarget> todo = untimed;
        if (untimed.Count < all.Count)
        {
            int a = await _window.Dialogs.Ask("Time all stops",
                untimed.Count == 0 ? $"All {all.Count} recordings already have their words timed. Time them all again? Any nudging you did by hand would be replaced."
                                   : $"{all.Count - untimed.Count} of the {all.Count} recordings already have their words timed. Time only the other {untimed.Count}, or all of them again?",
                untimed.Count == 0 ? new[] { "Time them all again", "Cancel" } : new[] { $"Only the {untimed.Count} not timed", "All of them again", "Cancel" },
                0, untimed.Count == 0 ? 1 : 2);
            if (untimed.Count == 0) { if (a != 0) return; todo = all; }
            else if (a == 1) todo = all;
            else if (a != 0) return;
        }

        Begin();
        try { await TimeThese(todo); }
        finally { End(); }
    }

    async Task TimeThese(List<VoiceTarget> todo)
    {
        var done = new List<(VoiceTarget Target, string File, string Caption, List<double> Times, WordAligner.Result Result)>();
        var failed = new List<string>();
        bool stopped = false;
        try
        {
            if (!await EnsureModel(_cancel!.Token)) return;
            for (int k = 0; k < todo.Count; k++)
            {
                VoiceTarget t = todo[k];
                if (t.Voice(_state.Book) is not Voice v) continue;
                string caption = t.Caption(_state.Book) ?? "";
                string title = $"Timing the words: {k + 1} of {todo.Count}";
                try
                {
                    var got = await Listen(v, caption, _cancel.Token, title, (double)k / todo.Count, 1.0 / todo.Count);
                    if (got is var (times, result)) done.Add((t, v.File, caption, times, result));
                    else failed.Add(t.Name);
                }
                catch (SpeechRecognizer.Unavailable e) when (e.PerRecording)
                {
                    failed.Add(t.Name);
                }
            }
        }
        catch (OperationCanceledException) { stopped = true; }
        catch (SpeechRecognizer.Unavailable e) { _window.Say(e.Message, 10); return; }

        // Keep what was finished, as one step, for the recordings that are still as they were.
        var apply = done.Where(d => d.Target.Voice(_state.Book) is Voice now && now.File == d.File && d.Target.Caption(_state.Book) == d.Caption).ToList();
        if (apply.Count > 0)
        {
            _state.Change(apply.Count == 1 ? "Time the words automatically" : "Time all stops", () =>
            {
                foreach (var d in apply) d.Target.Voice(_state.Book)!.Words = d.Times;
            }, structure: true);
        }
        int guessed = apply.Sum(d => d.Result.Guessed);
        string msg = stopped ? $"Stopped. {apply.Count} of {todo.Count} recordings were timed and kept." : $"Timed the words of {apply.Count} recording{(apply.Count == 1 ? "" : "s")}.";
        if (failed.Count > 0) msg += $" The words couldn't be made out in {failed.Count}: {string.Join(", ", failed.Take(4))}{(failed.Count > 4 ? "..." : "")}. Use Tap along for those.";
        else if (guessed > 0) msg += $" {guessed} word{(guessed == 1 ? " wasn't" : "s weren't")} heard clearly and {(guessed == 1 ? "was" : "were")} placed between the others. Play the book to check.";
        else if (apply.Count > 0) msg += " Play the book to check them.";
        _window.Say(msg, 12);
    }
}
