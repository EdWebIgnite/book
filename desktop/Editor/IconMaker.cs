using Avalonia;
using Avalonia.Headless;
using Editor.Core;
using Reader.Ui;

namespace BookEditor;

/// <summary>
/// BookEditor --make-icons &lt;folder&gt;: draws the reader's and the editor's icons (Reader.Ui.AppIcon) and
/// writes everything the projects use, laid out as they want it:
///   dassie.ico, dassie-editor.ico          Windows icons, 16 to 256 pixels (Player and Editor)
///   android/mipmap-*/ic_launcher*.png      the launcher icons at each density, square and round,
///                                          and the adaptive icon's foreground
///   android/mipmap-anydpi-v26/*.xml        the adaptive icon (foreground picture on a colour)
///   android/values/ic_launcher_background.xml
///   preview.png                            every size side by side, for looking at
/// Run it again after changing the drawing, then copy the files in (see the README).
/// </summary>
static class IconMaker
{
    public static int Run(string folder)
    {
        AppBuilder.Configure<EditorApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        Directory.CreateDirectory(folder);
        foreach (bool editor in new[] { false, true })
        {
            var pictures = IcoFile.Sizes.Select(s => (s, Png(s, editor, AppIcon.Back.Square))).ToList();
            string name = editor ? "dassie-editor.ico" : "dassie.ico";
            using FileStream f = File.Create(Path.Combine(folder, name));
            IcoFile.Write(f, pictures);
            Console.WriteLine("made " + name);
        }

        // Android: the launcher sizes for each screen density (48 units), and the adaptive icon's
        // 108 unit foreground, whose picture must stay inside the middle 72 (the art is scaled to fit).
        string android = Path.Combine(folder, "android");
        foreach (var (dpi, scale) in new[] { ("mdpi", 1.0), ("hdpi", 1.5), ("xhdpi", 2.0), ("xxhdpi", 3.0), ("xxxhdpi", 4.0) })
        {
            string dir = Path.Combine(android, "mipmap-" + dpi);
            Directory.CreateDirectory(dir);
            int launcher = (int)Math.Round(48 * scale), fore = (int)Math.Round(108 * scale);
            File.WriteAllBytes(Path.Combine(dir, "ic_launcher.png"), Png(launcher, false, AppIcon.Back.Square));
            File.WriteAllBytes(Path.Combine(dir, "ic_launcher_round.png"), Png(launcher, false, AppIcon.Back.Circle));
            File.WriteAllBytes(Path.Combine(dir, "ic_launcher_foreground.png"), Png(fore, false, AppIcon.Back.None, 0.62));
        }
        string anydpi = Path.Combine(android, "mipmap-anydpi-v26");
        Directory.CreateDirectory(anydpi);
        const string Adaptive = """
            <?xml version="1.0" encoding="utf-8"?>
            <!-- Made by BookEditor (its make-icons option): the dassie over a book, on bottle green. -->
            <adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
                <background android:drawable="@color/ic_launcher_background" />
                <foreground android:drawable="@mipmap/ic_launcher_foreground" />
            </adaptive-icon>

            """;
        File.WriteAllText(Path.Combine(anydpi, "ic_launcher.xml"), Adaptive);
        File.WriteAllText(Path.Combine(anydpi, "ic_launcher_round.xml"), Adaptive);
        string values = Path.Combine(android, "values");
        Directory.CreateDirectory(values);
        Avalonia.Media.Color c = AppIcon.BackColour(false);
        File.WriteAllText(Path.Combine(values, "ic_launcher_background.xml"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <resources>
                <color name="ic_launcher_background">#{c.R:X2}{c.G:X2}{c.B:X2}</color>
            </resources>

            """);
        File.WriteAllBytes(Path.Combine(folder, "preview.png"), Preview());
        Console.WriteLine("made the Android icons and preview.png in " + folder);
        return 0;
    }

    static byte[] Png(int pixels, bool editor, AppIcon.Back back, double art = 1)
    {
        using var bmp = AppIcon.Render(pixels, editor, back, art);
        using var ms = new MemoryStream();
        bmp.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Both icons at every Windows size, at their real size and enlarged, on light and dark.</summary>
    static byte[] Preview()
    {
        int w = 1100, h = 1160;
        var bmp = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        using (var ctx = bmp.CreateDrawingContext(true))
        {
            ctx.FillRectangle(Avalonia.Media.Brushes.White, new Rect(0, 0, w, h / 2));
            ctx.FillRectangle(new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#202020")), new Rect(0, h / 2, w, h / 2));
            for (int half = 0; half < 2; half++)
            {
                double y0 = half * h / 2 + 20;
                for (int e = 0; e < 2; e++)
                {
                    double x = 20, y = y0 + e * 280;
                    foreach (int s in IcoFile.Sizes)
                    {
                        using (ctx.PushTransform(Matrix.CreateTranslation(x, y))) AppIcon.Draw(ctx, s, e == 1);
                        x += s + 16;
                    }
                    using (ctx.PushTransform(Matrix.CreateTranslation(x, y))) AppIcon.Draw(ctx, 64, e == 1, AppIcon.Back.Circle);
                }
            }
        }
        using var ms = new MemoryStream();
        bmp.Save(ms);
        bmp.Dispose();
        return ms.ToArray();
    }
}
