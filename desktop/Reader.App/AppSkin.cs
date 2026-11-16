using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Reader.Ui;

namespace Reader.App;

/// <summary>
/// Puts the skin on the standard controls: the Fluent theme with leaf green as its accent, rounded
/// tactile buttons with plain dark words, and the app's font everywhere. The colours themselves are in
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

        // Buttons: a light tactile face with plain dark words (see Buttons in Palette.cs); the shadow,
        // the lip and the sink when pressed are in the styles below.
        Buttons.Look plain = Buttons.Plain;
        r["ButtonBackground"] = Buttons.Face(plain);
        r["ButtonBackgroundPointerOver"] = Buttons.HoverFace(plain);
        r["ButtonBackgroundPressed"] = Buttons.PressedFace(plain);
        r["ButtonBackgroundDisabled"] = Buttons.Face(plain, 0.6);
        r["ButtonForeground"] = Buttons.TextBrush;
        r["ButtonForegroundPointerOver"] = Buttons.TextBrush;
        r["ButtonForegroundPressed"] = Buttons.TextBrush;
        r["ButtonForegroundDisabled"] = Palette.InkFaintBrush;
        r["ButtonBorderBrush"] = Buttons.Edge(plain);
        r["ButtonBorderBrushPointerOver"] = Buttons.Edge(plain);
        r["ButtonBorderBrushPressed"] = Buttons.Edge(plain);
        r["ButtonBorderBrushDisabled"] = Buttons.Edge(plain, 0.6);
        r["ButtonBorderThemeThickness"] = Buttons.Lip;
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

        // Every button: a soft shadow, and pressed it sinks: the face moves down, the lip thins and the shadow shrinks.
        app.Styles.Add(new Style(x => x.OfType<Button>()) { Setters = { new Setter(Avalonia.Controls.Primitives.TemplatedControl.FontWeightProperty, FontWeight.Bold) } });
        app.Styles.Add(new Style(x => x.OfType<Button>().Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.BoxShadowProperty, new BoxShadows(Buttons.Shadow)) },
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class(":pressed").Template().OfType<ContentPresenter>())
        {
            Setters =
            {
                new Setter(ContentPresenter.BoxShadowProperty, new BoxShadows(Buttons.ShadowPressed)),
                new Setter(ContentPresenter.BorderThicknessProperty, Buttons.LipPressed),
                new Setter(Visual.RenderTransformProperty, new TranslateTransform(0, 1.5)),
            },
        });
        // Buttons marked "primary": a leaf green face with white words. "danger": the warm clay red.
        app.Styles.Add(Colours("primary", Buttons.Green));
        app.Styles.Add(Colours("danger", Buttons.Red));
    }

    /// <summary>A coloured button style for buttons with the given class, in all its states.</summary>
    static Styles Colours(string cls, Buttons.Look look)
    {
        Style State(string? pseudo, IBrush face)
        {
            Style s = pseudo is null
                ? new Style(x => x.OfType<Button>().Class(cls).Template().OfType<ContentPresenter>())
                : new Style(x => x.OfType<Button>().Class(cls).Class(pseudo).Template().OfType<ContentPresenter>());
            s.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, face));
            s.Setters.Add(new Setter(ContentPresenter.BorderBrushProperty, Buttons.Edge(look)));
            s.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, Brushes.White));
            return s;
        }
        return new Styles { State(null, Buttons.Face(look)), State(":pointerover", Buttons.HoverFace(look)), State(":pressed", Buttons.PressedFace(look)) };
    }
}
