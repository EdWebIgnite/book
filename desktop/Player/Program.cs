using Avalonia;

namespace Player;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Options options = Options.Parse(args);
        if (options.Help) { Console.WriteLine(Options.Usage); return 0; }
        if (options.Problem is not null)
        {
            Console.Error.WriteLine(options.Problem);
            Console.Error.WriteLine("Run with --help to see what can be asked for.");
            return 2;
        }
        if (options.Shot is not null) return Screenshot.Take(options);

        App.Options = options;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Also used by the previewer in an editor, which is why it is its own method.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
