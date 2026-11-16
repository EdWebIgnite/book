using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Reader.Ui;

namespace Reader.App;

/// <summary>
/// Puts the skin on the standard controls: the Fluent theme with leaf green as its accent, rounded
/// buttons in cream and green, and the app's font everywhere. The colours themselves are in
/// <see cref="Palette"/>. Called once by the Windows program and by the phone app as they start.
/// </summary>
public static class AppSkin
{
    /// <param name="phone">The phone app. The same look for now; the phone gets its own tuning later.</param>
    public static void Install(Application app, bool phone = false)
    {
        var fluent = new FluentTheme();
        fluent.Palettes[ThemeVariant.Light] = new ColorPaletteResources
        {
            Accent = Palette.Leaf,
            RegionColor = Palette.Cream,
            BaseHigh = Palette.Ink,
            ChromeAltLow = Palette.Ink,
            ErrorText = Palette.Clay,
        };
        app.Styles.Add(fluent);
        // The shelf and the settings are light whatever the computer's or phone's own theme is.
        app.RequestedThemeVariant = ThemeVariant.Light;

        var r = app.Resources;
        r["ContentControlThemeFontFamily"] = Palette.Font;
        r["ControlCornerRadius"] = new CornerRadius(12);
        r["OverlayCornerRadius"] = new CornerRadius(14);

        // Buttons: cream with a soft green edge, a mint wash under the pointer, deeper when pressed.
        r["ButtonBackground"] = Palette.PaperBrush;
        r["ButtonBackgroundPointerOver"] = Palette.MintBrush;
        r["ButtonBackgroundPressed"] = Palette.LeafPaleBrush;
        r["ButtonBackgroundDisabled"] = Palette.Brush(Palette.Mint, 0.6);
        r["ButtonForeground"] = Palette.LeafDeepBrush;
        r["ButtonForegroundPointerOver"] = Palette.LeafDeepBrush;
        r["ButtonForegroundPressed"] = Palette.LeafDeepBrush;
        r["ButtonForegroundDisabled"] = Palette.InkFaintBrush;
        r["ButtonBorderBrush"] = Palette.LeafPaleBrush;
        r["ButtonBorderBrushPointerOver"] = Palette.Brush(Palette.LeafBright);
        r["ButtonBorderBrushPressed"] = Palette.LeafBrush;
        r["ButtonBorderBrushDisabled"] = Palette.LeafPaleBrush;
        r["ButtonBorderThemeThickness"] = new Thickness(1.5);
        r["ButtonPadding"] = new Thickness(16, 8);

        // Lists and tick boxes: soft green edges rather than near-black ones.
        r["ComboBoxBorderBrush"] = Palette.Brush(Color.Parse("#b9d9a5"));
        r["ComboBoxBorderBrushPointerOver"] = Palette.LeafBrush;
        r["ComboBoxBackground"] = Palette.PaperBrush;
        r["ComboBoxBackgroundPointerOver"] = Palette.MintBrush;
        r["CheckBoxCheckBackgroundStrokeUnchecked"] = Palette.Brush(Color.Parse("#9cc488"));
        r["CheckBoxCheckBackgroundFillUnchecked"] = Palette.PaperBrush;

        // Tool tips: deep green with cream words.
        r["ToolTipBackground"] = Palette.Brush(Palette.LeafDeep);
        r["ToolTipForeground"] = Palette.CreamBrush;
        r["ToolTipBorderBrush"] = Palette.Brush(Palette.LeafDeep);

        // Buttons marked "primary": leaf green with white words. "danger": the warm clay red.
        app.Styles.Add(Colours("primary", Palette.Leaf, Palette.LeafBright, Palette.LeafDeep, Colors.White));
        app.Styles.Add(Colours("danger", Palette.Clay, Color.Parse("#d66a50"), Color.Parse("#a8452f"), Colors.White));
        app.Styles.Add(new Style(x => x.OfType<Button>()) { Setters = { new Setter(Avalonia.Controls.Primitives.TemplatedControl.FontWeightProperty, FontWeight.Bold) } });
    }

    /// <summary>A filled button style for buttons with the given class, in all its states.</summary>
    static Styles Colours(string cls, Color fill, Color hover, Color pressed, Color text)
    {
        IBrush Fill(Color c) => Palette.Brush(c);
        Style State(string? pseudo, Color c)
        {
            Style s = pseudo is null
                ? new Style(x => x.OfType<Button>().Class(cls).Template().OfType<Avalonia.Controls.Presenters.ContentPresenter>())
                : new Style(x => x.OfType<Button>().Class(cls).Class(pseudo).Template().OfType<Avalonia.Controls.Presenters.ContentPresenter>());
            s.Setters.Add(new Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, Fill(c)));
            s.Setters.Add(new Setter(Avalonia.Controls.Presenters.ContentPresenter.BorderBrushProperty, Fill(c)));
            s.Setters.Add(new Setter(Avalonia.Controls.Presenters.ContentPresenter.ForegroundProperty, Fill(text)));
            return s;
        }
        return new Styles { State(null, fill), State(":pointerover", hover), State(":pressed", pressed) };
    }
}
