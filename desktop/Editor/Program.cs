using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace BookEditor;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ShotOptions? shot;
        try { shot = ShotOptions.Parse(args); }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(ShotOptions.Usage);
            return 2;
        }
        if (args.Contains("--help") || args.Contains("-h")) { Console.WriteLine(ShotOptions.Usage); return 0; }
        if (shot is not null) return Screenshot.Take(shot);
        if (args.Contains("--time-words")) return TimeWordsCommand.Run(args);
        if (Array.IndexOf(args, "--make-icons") is int mi and >= 0) return mi + 1 < args.Length ? IconMaker.Run(args[mi + 1]) : 2;
        EditorApp.OpenAtStart = args.FirstOrDefault(a => !a.StartsWith('-') && Directory.Exists(a));
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<EditorApp>()
        .UsePlatformDetect()
        .With(new SkiaOptions { MaxGpuResourceSizeBytes = 512L * 1024 * 1024 });
}

sealed class EditorApp : Application
{
    /// <summary>A book folder given on the command line (or by dropping a folder on BookEditor.exe).</summary>
    public static string? OpenAtStart { get; set; }

    public override void Initialize() => EditorSkin.Install(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            bool offer = Walkthrough.ShouldOffer;
            if (OpenAtStart is string folder) window.Opened += async (_, _) =>
            {
                await window.OpenBook(folder);
                if (offer) await window.AskAboutTour();
            };
            else if (offer) window.OfferTour();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>
/// For trying automatic word timing from the command line, with no window:
///   BookEditor --time-words recording.wav --words "The words that are read" [--from s] [--to s] [--language en|af|auto] [--models folder]
/// It prints what was heard and the time worked out for each word. The speech recogniser's file must
/// already be downloaded (the editor does that the first time "Time words automatically" is pressed), or
/// be in the --models folder.
/// </summary>
static class TimeWordsCommand
{
    public static int Run(string[] args)
    {
        string? Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        string? sound = Arg("--time-words"), words = Arg("--words");
        if (sound is null || words is null) { Console.Error.WriteLine("Use: BookEditor --time-words recording.wav --words \"the words\" [--from s] [--to s] [--language en|af|auto] [--models folder]"); return 2; }
        double from = double.TryParse(Arg("--from"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double f) ? f : 0;
        double to = double.TryParse(Arg("--to"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double t) ? t : 0;
        var model = new Editor.Core.SpeechModel(Arg("--models"));
        if (!model.Ready) { Console.Error.WriteLine("The speech recogniser hasn't been downloaded yet: " + model.FilePath); return 1; }
        float[]? samples = SpeechRecognizer.ReadForListening(sound, from, to);
        if (samples is null) { Console.Error.WriteLine("Couldn't read " + sound); return 1; }
        double end = to > from ? to : from + samples.Length / 16000.0 - 2 * SpeechRecognizer.Lead;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var heard = SpeechRecognizer.Listen(model.FilePath, samples, words, Arg("--language") ?? "auto", null, CancellationToken.None).GetAwaiter().GetResult()
                .Select(w => w with { Start = w.Start - SpeechRecognizer.Lead + from, End = w.End - SpeechRecognizer.Lead + from }).ToList();
            Console.WriteLine($"Heard ({clock.ElapsedMilliseconds} ms): " + string.Join(" ", heard.Select(w => $"{w.Text}@{w.Start:0.00}-{w.End:0.00}")));
            var r = Editor.Core.WordAligner.Align(words, heard, from, end);
            if (r is null) { Console.WriteLine("The words couldn't be made out."); return 3; }
            var list = Editor.Core.WordAligner.CaptionWords(words);
            for (int i = 0; i < list.Count; i++) Console.WriteLine($"{r.Times[i],7:0.000}  {list[i]}");
            Console.WriteLine($"{r.Clear} of {r.Total} heard clearly.");
            return 0;
        }
        catch (SpeechRecognizer.Unavailable e)
        {
            Console.Error.WriteLine(e.Message + (e.InnerException is null ? "" : " (" + e.InnerException.Message + ")"));
            return 1;
        }
    }
}
