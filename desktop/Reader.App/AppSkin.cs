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
    static Application? _app;
    static readonly Styles _coloured = new();
    static FluentTheme? _fluent;

    /// <param name="phone">The phone app. The same look for now; the phone gets its own tuning later.</param>
    public static void Install(Application app, bool phone = false)
    {
        _app = app;
        _fluent = new FluentTheme();
        app.Styles.Add(_fluent);
        var r = app.Resources;
        r["ContentControlThemeFontFamily"] = Palette.Font;
        r["ControlCornerRadius"] = new CornerRadius(12);
        r["OverlayCornerRadius"] = new CornerRadius(14);
        r["ButtonBorderThemeThickness"] = Buttons.Lip;
        r["ButtonPadding"] = new Thickness(16, 8);

        // Every button: a soft shadow, and pressed it sinks: the face moves down, the lip thins and the shadow shrinks.
        app.Styles.Add(new Style(x => x.OfType<Button>()) { Setters = { new Setter(Avalonia.Controls.Primitives.TemplatedControl.FontWeightProperty, FontWeight.Bold) } });
        app.Styles.Add(_coloured);
        Apply();
    }

    /// <summary>Put the colours of the theme in use (day or bedtime) on the standard controls. Called again when bedtime colours are switched.</summary>
    public static void Apply()
    {
        if (_app is not Application app || _fluent is null) return;
        ColorPaletteResources Make() => new()
        {
            Accent = Palette.Leaf,
            RegionColor = Palette.Cream,
            BaseHigh = Palette.Ink,
            ChromeAltLow = Palette.Ink,
            ErrorText = Palette.Clay,
        };
        _fluent.Palettes[ThemeVariant.Light] = Make();
        _fluent.Palettes[ThemeVariant.Dark] = Make();
        // The app is light by day and dark at bedtime, whatever the computer's or phone's own theme is.
        app.RequestedThemeVariant = Palette.Night ? ThemeVariant.Dark : ThemeVariant.Light;

        var r = app.Resources;
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

        // Lists, tick boxes and text boxes: soft edges rather than near-black ones.
        r["ComboBoxBorderBrush"] = Palette.Brush(Palette.Night ? Color.Parse("#5a62b0") : Color.Parse("#c3b998"));
        r["ComboBoxBorderBrushPointerOver"] = Palette.LeafBrush;
        r["ComboBoxBackground"] = Palette.PaperBrush;
        r["ComboBoxBackgroundPointerOver"] = Palette.MintBrush;
        r["CheckBoxCheckBackgroundStrokeUnchecked"] = Palette.Brush(Palette.Night ? Color.Parse("#8c92d0") : Color.Parse("#a99a72"));
        r["CheckBoxCheckBackgroundFillUnchecked"] = Palette.PaperBrush;
        r["TextControlBackground"] = Palette.PaperBrush;
        r["TextControlBackgroundPointerOver"] = Palette.PaperBrush;
        r["TextControlBackgroundFocused"] = Palette.PaperBrush;
        r["TextControlForeground"] = Palette.InkBrush;
        r["TextControlForegroundPointerOver"] = Palette.InkBrush;
        r["TextControlForegroundFocused"] = Palette.InkBrush;
        r["TextControlBorderBrush"] = Palette.Brush(Palette.Night ? Color.Parse("#6870b8") : Color.Parse("#a99a72"));
        r["TextControlBorderBrushPointerOver"] = Palette.LeafBrush;
        r["TextControlBorderBrushFocused"] = Palette.LeafBrush;
        r["TextControlPlaceholderForeground"] = Palette.InkSoftBrush;
        r["TextControlSelectionHighlightColor"] = Palette.Brush(Palette.Leaf, 0.35);

        // Tool tips: deep green (bedtime: the dark card) with light words.
        r["ToolTipBackground"] = Palette.Brush(Palette.Night ? Palette.Paper : Palette.LeafDeep);
        r["ToolTipForeground"] = Palette.Night ? Palette.InkBrush : Palette.CreamBrush;
        r["ToolTipBorderBrush"] = Palette.Brush(Palette.Night ? Palette.LeafPale : Palette.LeafDeep);

        _coloured.Clear();
        _coloured.Add(new Style(x => x.OfType<Button>().Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.BoxShadowProperty, new BoxShadows(Buttons.Shadow)) },
        });
        _coloured.Add(new Style(x => x.OfType<Button>().Class(":pressed").Template().OfType<ContentPresenter>())
        {
            Setters =
            {
                new Setter(ContentPresenter.BoxShadowProperty, new BoxShadows(Buttons.ShadowPressed)),
                new Setter(ContentPresenter.BorderThicknessProperty, Buttons.LipPressed),
                new Setter(Visual.RenderTransformProperty, new TranslateTransform(0, 1.5)),
            },
        });
        // Buttons marked "primary": the main-action face. "danger": the warm red.
        _coloured.Add(Colours("primary", Buttons.Green));
        _coloured.Add(Colours("danger", Buttons.Red));
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
            s.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, new SolidColorBrush(look.Ink)));
            return s;
        }
        return new Styles { State(null, Buttons.Face(look)), State(":pointerover", Buttons.HoverFace(look)), State(":pressed", Buttons.PressedFace(look)) };
    }
}
