using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Reader.Core;
using Reader.Ui;

namespace BookEditor;

/// <summary>The editor's name as people see it, made from the reader's brand so a new name follows everywhere.</summary>
static class EditorBrand
{
    public const string Name = Brand.Name + " Book Editor";

    /// <summary>The part after the wordmark in the title bar.</summary>
    public const string Product = "Book Editor";
}

/// <summary>
/// Puts the Dassie look on the standard controls, the way the reader's AppSkin does, but smaller and
/// tighter for a desk tool: the reader's raised buttons (a face lighter at the top, a darker lip, a
/// soft shadow, sinking when pressed) at 32 units high instead of 48, corners of 7, Nunito everywhere,
/// bottle green for the main action and for focus. Always the day colours.
/// </summary>
static class EditorSkin
{
    public static void Install(Application app)
    {
        var fluent = new FluentTheme();
        var colours = new ColorPaletteResources
        {
            Accent = Ui.AccentColor, RegionColor = Color.Parse("#fbf7ec"), BaseHigh = Color.Parse("#2b2118"), ChromeAltLow = Color.Parse("#2b2118"),
            ErrorText = Color.Parse("#a8473a"), AltHigh = Color.Parse("#fffdf8"), ChromeLow = Color.Parse("#f3ecdb"), ChromeMediumLow = Color.Parse("#fbf7ec"),
            ChromeMedium = Color.Parse("#ece3cf"), ChromeHigh = Color.Parse("#cdbf9e"), ListLow = Color.Parse("#f3ecdb"), ListMedium = Color.Parse("#ebe1c8"),
            BaseLow = Color.Parse("#e3d9c1"), BaseMediumLow = Color.Parse("#a99a72"), BaseMedium = Color.Parse("#6b5e50"), BaseMediumHigh = Color.Parse("#4a3f33"),
        };
        fluent.Palettes[ThemeVariant.Light] = colours;
        app.Styles.Add(fluent);
        app.RequestedThemeVariant = ThemeVariant.Light;

        var r = app.Resources;
        r["ContentControlThemeFontFamily"] = Ui.Font;
        r["ControlContentThemeFontSize"] = 13.0;
        r["ControlCornerRadius"] = new CornerRadius(6);
        r["OverlayCornerRadius"] = new CornerRadius(8);
        r["ButtonBorderThemeThickness"] = Buttons.Lip;
        r["ButtonPadding"] = new Thickness(14, 5);
        r["TextControlThemePadding"] = new Thickness(10, 6, 8, 6);
        r["TextControlThemeMinHeight"] = 32.0;
        r["ComboBoxMinHeight"] = 32.0;

        Buttons.Look plain = Buttons.Plain;
        r["ButtonBackground"] = Buttons.Face(plain);
        r["ButtonBackgroundPointerOver"] = Buttons.HoverFace(plain);
        r["ButtonBackgroundPressed"] = Buttons.PressedFace(plain);
        r["ButtonBackgroundDisabled"] = Buttons.Face(plain, 0.55);
        r["ButtonForeground"] = Buttons.TextBrush;
        r["ButtonForegroundPointerOver"] = Buttons.TextBrush;
        r["ButtonForegroundPressed"] = Buttons.TextBrush;
        r["ButtonForegroundDisabled"] = Ui.Faint;
        r["ButtonBorderBrush"] = Buttons.Edge(plain);
        r["ButtonBorderBrushPointerOver"] = Buttons.Edge(plain);
        r["ButtonBorderBrushPressed"] = Buttons.Edge(plain);
        r["ButtonBorderBrushDisabled"] = Buttons.Edge(plain, 0.5);

        IBrush soft = Ui.Brush("#cdbf9e");
        r["ComboBoxBorderBrush"] = soft;
        r["ComboBoxBorderBrushPointerOver"] = Ui.Accent;
        r["ComboBoxBackground"] = Ui.Field;
        r["ComboBoxBackgroundPointerOver"] = Ui.Card;
        r["ComboBoxDropDownBackground"] = Ui.Card;
        r["CheckBoxCheckBackgroundStrokeUnchecked"] = Ui.Brush("#a99a72");
        r["CheckBoxCheckBackgroundFillUnchecked"] = Ui.Field;
        r["TextControlBackground"] = Ui.Field;
        r["TextControlBackgroundPointerOver"] = Ui.Field;
        r["TextControlBackgroundFocused"] = Ui.Field;
        r["TextControlBackgroundDisabled"] = Ui.Raised;
        r["TextControlForeground"] = Ui.Text;
        r["TextControlForegroundPointerOver"] = Ui.Text;
        r["TextControlForegroundFocused"] = Ui.Text;
        r["TextControlBorderBrush"] = soft;
        r["TextControlBorderBrushPointerOver"] = Ui.Accent;
        r["TextControlBorderBrushFocused"] = Ui.Accent;
        r["TextControlPlaceholderForeground"] = Ui.Faint;
        r["TextControlPlaceholderForegroundFocused"] = Ui.Faint;
        r["TextControlSelectionHighlightColor"] = Ui.Brush(Color.FromArgb(90, 47, 107, 80));

        r["ToolTipBackground"] = Ui.AccentDeep;
        r["ToolTipForeground"] = Ui.Brush("#fbf5e6");
        r["ToolTipBorderBrush"] = Ui.AccentDeep;

        // The folding sections on the right: cards on the panel.
        r["ExpanderHeaderBackground"] = Ui.Card;
        r["ExpanderHeaderBackgroundPointerOver"] = Ui.Brush("#fbf5e6");
        r["ExpanderHeaderBackgroundPressed"] = Ui.Brush("#f6efdd");
        r["ExpanderContentBackground"] = Ui.Card;
        foreach (string key in new[] { "ExpanderHeaderBorderBrush", "ExpanderHeaderBorderBrushPointerOver", "ExpanderHeaderBorderBrushPressed", "ExpanderContentBorderBrush" })
            r[key] = Ui.Line;
        r["ExpanderHeaderForeground"] = Ui.AccentDeep;
        r["ExpanderChevronForeground"] = Ui.Muted;
        r["ExpanderContentPadding"] = new Thickness(12, 4, 12, 12);
        r["ExpanderHeaderPadding"] = new Thickness(12, 0, 12, 0);
        r["ExpanderMinHeight"] = 40.0;

        // Menus: paper with green hover.
        r["MenuFlyoutPresenterBackground"] = Ui.Card;
        r["MenuFlyoutPresenterBorderBrush"] = Ui.Line;
        r["MenuFlyoutItemBackgroundPointerOver"] = Ui.AccentWash;
        r["MenuFlyoutItemKeyboardAcceleratorTextForeground"] = Ui.Faint;
        r["MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver"] = Ui.Muted;
        r["MenuBarBackground"] = Brushes.Transparent;
        r["MenuBarItemBackgroundPointerOver"] = Ui.Raised;
        r["MenuBarItemBackgroundSelected"] = Ui.RaisedHover;
        r["FlyoutPresenterBackground"] = Ui.Card;
        r["FlyoutBorderThemeBrush"] = Ui.Line;

        r["ScrollBarThumbFill"] = Ui.LineStrong;
        r["SliderTrackFill"] = Ui.Line;
        r["SliderThumbBackground"] = Ui.Accent;

        var s = new Styles();
        s.Add(new Style(x => x.OfType<Button>()) { Setters = { new Setter(TemplatedControl_FontWeight, FontWeight.Bold) } });
        s.Add(new Style(x => x.OfType<Button>().Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.BoxShadowProperty, new BoxShadows(SmallShadow)) },
        });
        s.Add(new Style(x => x.OfType<Button>().Class(":pressed").Template().OfType<ContentPresenter>())
        {
            Setters =
            {
                new Setter(ContentPresenter.BoxShadowProperty, new BoxShadows(SmallShadowPressed)),
                new Setter(ContentPresenter.BorderThicknessProperty, Buttons.LipPressed),
                new Setter(Visual.RenderTransformProperty, new TranslateTransform(0, 1)),
            },
        });
        // Flat buttons (inside the segmented switch): no lip, no shadow, no sinking.
        s.Add(new Style(x => x.OfType<Button>().Class("flat").Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.BoxShadowProperty, new BoxShadows()) },
        });
        s.Add(new Style(x => x.OfType<Button>().Class("flat").Class(":pressed").Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(Visual.RenderTransformProperty, null), new Setter(ContentPresenter.BorderThicknessProperty, new Thickness(0)) },
        });
        s.Add(Colours("primary", Buttons.Green));
        s.Add(Colours("danger", Buttons.Red));
        s.Add(new Style(x => x.OfType<MenuItem>()) { Setters = { new Setter(MenuItem.FontSizeProperty, 13.0) } });
        app.Styles.Add(s);
    }

    static readonly AvaloniaProperty TemplatedControl_FontWeight = Avalonia.Controls.Primitives.TemplatedControl.FontWeightProperty;

    static BoxShadow SmallShadow => new() { OffsetY = 1, Blur = 3, Color = Color.FromArgb(48, 44, 36, 20) };
    static BoxShadow SmallShadowPressed => new() { OffsetY = 0, Blur = 1, Color = Color.FromArgb(40, 44, 36, 20) };

    /// <summary>A coloured button style for buttons with the given class, in all its states but disabled.</summary>
    static Styles Colours(string cls, Buttons.Look look)
    {
        Style State(string? pseudo, IBrush face)
        {
            Style s = pseudo is null
                ? new Style(x => x.OfType<Button>().Class(cls).Not(y => y.Class(":disabled")).Template().OfType<ContentPresenter>())
                : new Style(x => x.OfType<Button>().Class(cls).Class(pseudo).Template().OfType<ContentPresenter>());
            s.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, face));
            s.Setters.Add(new Setter(ContentPresenter.BorderBrushProperty, Buttons.Edge(look)));
            s.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, new SolidColorBrush(look.Ink)));
            return s;
        }
        return new Styles { State(null, Buttons.Face(look)), State(":pointerover", Buttons.HoverFace(look)), State(":pressed", Buttons.PressedFace(look)) };
    }
}
