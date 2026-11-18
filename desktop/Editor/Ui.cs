using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Reader.Ui;

namespace BookEditor;

/// <summary>
/// The editor's look, and small helpers for building its panels in code. It shares the Dassie reader's
/// colours and fonts but is calmer and denser: cream and paper panels, bottle green for the main action,
/// brass only to mark what is selected, Nunito for everything and Fraunces only for the big headings.
/// The page being worked on sits on a neutral grey (or a lighter grey, or paper), never on a colour, so
/// the artwork's own colours can be judged.
///
/// Spacing keeps to a 4 unit grid (4, 8, 12, 16, 24). Type comes in a few sizes: 11 small capitals,
/// 12 help and notes, 13 controls and body, 15 titles of cards, 18 sheet titles, 28 and up in Fraunces.
/// </summary>
static class Ui
{
    // The day colours of the reader (Reader.Avalonia's Theme.Day), copied here rather than read from
    // Palette, so switching the preview to bedtime colours never changes the editor round it.
    public static readonly Color BgColor = Color.Parse("#f4eedc");
    public static readonly IBrush Bg = Brush("#f4eedc");            // behind everything: the start screen, the sheets' room
    public static readonly IBrush Panel = Brush("#fbf7ec");         // the side panels, the bars
    public static readonly IBrush Raised = Brush("#f3ecdb");        // list items, chips, page cards
    public static readonly IBrush RaisedHover = Brush("#ebe1c8");
    public static readonly IBrush Card = Brush("#fffdf8");          // cards and dialogs
    public static readonly IBrush Line = Brush("#e3d9c1");
    public static readonly IBrush LineStrong = Brush("#cdbf9e");
    public static readonly IBrush Text = Brush("#2b2118");
    public static readonly IBrush Muted = Brush("#6b5e50");
    public static readonly IBrush Faint = Brush("#8f8474");
    public static readonly Color AccentColor = Color.Parse("#2f6b50");
    public static readonly IBrush Accent = Brush(AccentColor);      // bottle green: the main action, focus, ticks
    public static readonly IBrush AccentDeep = Brush("#234b3b");    // headings
    public static readonly IBrush AccentInk = Brushes.White;
    public static readonly IBrush AccentWash = Brush("#e9f0e4");
    public static readonly Color SelectColor = Color.Parse("#c99a3a");
    public static readonly IBrush Select = Brush(SelectColor);      // brass: what is selected
    public static readonly IBrush SelectWash = Brush("#f7edd2");
    public static readonly IBrush Gold = Brush("#ffd23f");
    public static readonly IBrush Good = Brush("#2f6b50");
    public static readonly IBrush GoodWash = Brush("#e8f0e3");
    public static readonly IBrush Bad = Brush("#a8473a");
    public static readonly IBrush BadWash = Brush("#f8e7e1");
    public static readonly IBrush Warn = Brush("#94680f");
    public static readonly IBrush WarnWash = Brush("#f8eed3");
    public static readonly IBrush Field = Brush("#fffefb");
    /// <summary>The dimmed room behind a sheet or a question.</summary>
    public static readonly IBrush Scrim = new ImmutableSolidColorBrush(Color.FromArgb(110, 43, 33, 24));
    public static BoxShadows Lifted => new(new BoxShadow { OffsetY = 8, Blur = 32, Color = Color.FromArgb(70, 44, 36, 20) });

    /// <summary>The colour round the page, as chosen under View (a neutral grey to start with).</summary>
    public static string Surround { get; set; } = "grey";
    public static Color StageColor => Surround switch { "light" => Color.Parse("#b9babc"), "paper" => Color.Parse("#ece6d6"), _ => Color.Parse("#4b4d51") };
    public static IBrush Stage => Brush(StageColor);
    /// <summary>Words drawn on the surround: light on the grey, dark on the lighter ones.</summary>
    public static IBrush OnStage => Surround == "grey" ? Brush("#e9e7e2") : Brush("#3a3632");

    public static readonly FontFamily Font = Palette.Font;
    public static readonly FontFamily HeadingFont = Palette.Heading;

    /// <summary>The colour of each stop, in order, as in the browser editor.</summary>
    public static readonly string[] StopColours = { "#ff5a36", "#2f9bff", "#21c07a", "#ffb800", "#c45bff", "#ff4fa3", "#00c2c7", "#9acd32" };
    public static Color StopColour(int i) => Color.Parse(StopColours[((i % StopColours.Length) + StopColours.Length) % StopColours.Length]);

    public static IBrush Brush(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));
    public static IBrush Brush(Color c) => new ImmutableSolidColorBrush(c);

    public static TextBlock Label(string text, double size = 13, IBrush? colour = null, FontWeight weight = FontWeight.Normal, bool wrap = true) => new()
    {
        Text = text, FontSize = size, Foreground = colour ?? Text, FontWeight = weight,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
    };

    /// <summary>A big heading in the serif (start screen, empty book).</summary>
    public static TextBlock Display(string text, double size = 30) => new()
    {
        Text = text, FontSize = size, FontFamily = HeadingFont, FontWeight = FontWeight.Bold, Foreground = AccentDeep, TextWrapping = TextWrapping.Wrap,
        LineHeight = Math.Round(size * 1.18),
    };

    /// <summary>The title of a sheet or a question.</summary>
    public static TextBlock Title(string text) => Label(text, 18, AccentDeep, FontWeight.ExtraBold);

    /// <summary>A small grey line of help under a control.</summary>
    public static TextBlock Help(string text) => new()
    {
        Text = text, FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap, LineHeight = 17, Margin = new Thickness(0, 4, 0, 4),
    };

    /// <summary>The name over a control.</summary>
    public static TextBlock FieldName(string text) => new()
    {
        Text = text, FontSize = 12, FontWeight = FontWeight.Bold, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 4),
    };

    /// <summary>A small heading inside a card, in spaced capitals.</summary>
    public static TextBlock Heading(string text) => new()
    {
        Text = text.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.ExtraBold, LetterSpacing = 1, Foreground = AccentDeep, Margin = new Thickness(0, 16, 0, 4),
    };

    public static Button Button(string text, Action click, string? tip = null, bool primary = false, bool small = false, bool danger = false)
    {
        var b = new Button
        {
            Content = text, FontSize = small ? 12.5 : 13, Padding = small ? new Thickness(10, 3) : new Thickness(14, 5),
            MinHeight = small ? 26 : 32, CornerRadius = new CornerRadius(7), Focusable = true, VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (primary) b.Classes.Add("primary");
        if (danger) b.Classes.Add("danger");
        if (tip is not null) ToolTip.SetTip(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    /// <summary>A button with a drawn icon, and words beside it if given.</summary>
    public static Button IconButton(string icon, string? text, Action click, string tip, bool primary = false, double iconSize = 16)
    {
        var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        // The icon takes the colour of the button's words (from its content presenter), so it follows the primary and disabled looks.
        var path = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(icon), StrokeThickness = 2.1, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round };
        path.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty, new Avalonia.Data.Binding("Foreground")
        {
            RelativeSource = new Avalonia.Data.RelativeSource { Mode = Avalonia.Data.RelativeSourceMode.FindAncestor, AncestorType = typeof(Avalonia.Controls.Presenters.ContentPresenter) },
        });
        face.Children.Add(new Viewbox { Width = iconSize, Height = iconSize, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Child = new Canvas { Width = 24, Height = 24, Children = { path } } });
        if (text is not null) face.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        var b = new Button
        {
            Content = face, FontSize = 13, MinHeight = 32, MinWidth = 32, CornerRadius = new CornerRadius(7), VerticalAlignment = VerticalAlignment.Center,
            Padding = text is null ? new Thickness(7, 5) : new Thickness(10, 5, 12, 5), HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        if (primary) b.Classes.Add("primary");
        ToolTip.SetTip(b, tip);
        Avalonia.Automation.AutomationProperties.SetName(b, text ?? tip);
        b.Click += (_, _) => click();
        return b;
    }

    /// <summary>A thin upright line between groups of buttons.</summary>
    public static Control Divider() => new Border { Width = 1, Height = 20, Background = LineStrong, Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center };

    public static WrapPanel Row(params Control[] items)
    {
        var w = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        foreach (Control c in items)
        {
            c.Margin = new Thickness(0, 0, 8, 8);
            w.Children.Add(c);
        }
        return w;
    }

    /// <summary>A rounded box holding a group of controls.</summary>
    public static Border CardBox(Control content, IBrush? border = null) => new()
    {
        Background = Card, BorderBrush = border ?? Line, BorderThickness = new Thickness(border is null ? 1 : 1.5), CornerRadius = new CornerRadius(8),
        Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8), Child = content,
    };

    /// <summary>A message in a coloured box: a problem, a warning, good news or a note.</summary>
    public static Border Note(Control content, string kind = "note") => new()
    {
        Background = kind switch { "bad" => BadWash, "warn" => WarnWash, "good" => GoodWash, _ => AccentWash },
        BorderBrush = kind switch { "bad" => Bad, "warn" => Warn, "good" => Good, _ => Accent },
        BorderThickness = new Thickness(3, 0, 0, 0), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8), Margin = new Thickness(0, 4, 0, 8), Child = content,
    };

    public static Border Note(string text, string kind = "note") => Note(Label(text, 12.5, Text), kind);

    /// <summary>A section of the right-hand panel that can be folded away.</summary>
    public static Expander Section(string title, Control content, bool open = true, string? tag = null)
    {
        var head = new DockPanel { LastChildFill = false };
        var name = new TextBlock { Text = title, FontWeight = FontWeight.ExtraBold, FontSize = 13.5, Foreground = AccentDeep };
        DockPanel.SetDock(name, Dock.Left);
        head.Children.Add(name);
        if (tag is not null)
        {
            var t = new TextBlock { Text = tag, FontSize = 12, Foreground = Muted, Margin = new Thickness(8, 1, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 200 };
            DockPanel.SetDock(t, Dock.Left);
            head.Children.Add(t);
        }
        return new Expander
        {
            Header = head, Content = content, IsExpanded = open, HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 8),
        };
    }

    public static TextBox TextField(string? value, string placeholder = "", bool multiline = false) => new()
    {
        Text = value ?? "", PlaceholderText = placeholder, FontSize = 13, AcceptsReturn = multiline,
        TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 64 : 0,
    };

    public static CheckBox Check(string text, bool value, Action<bool> changed, string? tip = null)
    {
        var c = new CheckBox { IsChecked = value, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Text }, Margin = new Thickness(0, 4, 0, 0) };
        if (tip is not null) ToolTip.SetTip(c, tip);
        c.IsCheckedChanged += (_, _) => changed(c.IsChecked == true);
        return c;
    }

    public static ComboBox Choice<T>(IReadOnlyList<(T Value, string Text)> options, T value, Action<T> changed)
    {
        var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13 };
        foreach (var (_, text) in options) box.Items.Add(text);
        int at = -1;
        for (int i = 0; i < options.Count; i++) if (EqualityComparer<T>.Default.Equals(options[i].Value, value)) at = i;
        box.SelectedIndex = Math.Max(0, at);
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) changed(options[box.SelectedIndex].Value); };
        return box;
    }

    /// <summary>A slider with its value shown beside its name.</summary>
    public static Control Slider(string name, double min, double max, double step, double value, string unit, Action<double> changed, Action? started = null)
    {
        var output = new TextBlock { FontSize = 12, FontWeight = FontWeight.Bold, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 4) };
        string Show(double v) => v.ToString(step < 0.1 ? "0.00" : step < 1 ? "0.0" : "0", System.Globalization.CultureInfo.InvariantCulture) + unit;
        output.Text = Show(value);
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(FieldName(name));
        Grid.SetColumn(output, 1);
        head.Children.Add(output);
        var slider = new Slider { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), TickFrequency = step, IsSnapToTickEnabled = true, SmallChange = step, LargeChange = step * 4, Margin = new Thickness(0, -8, 0, -6) };
        slider.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, _) => started?.Invoke(), Avalonia.Interactivity.RoutingStrategies.Tunnel);
        slider.ValueChanged += (_, e) =>
        {
            double v = Math.Round(Math.Round((e.NewValue - min) / step) * step + min, 4);
            output.Text = Show(v);
            changed(v);
        };
        return new StackPanel { Children = { head, slider } };
    }

    /// <summary>A number box that can be left empty (null).</summary>
    public static TextBox NumberField(double? value, string placeholder, Action<double?> changed, double min = 0, double max = 999)
    {
        var box = new TextBox { Text = value?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "", PlaceholderText = placeholder, Width = 112, HorizontalAlignment = HorizontalAlignment.Left, FontSize = 13 };
        string last = box.Text ?? "";
        box.TextChanged += (_, _) =>
        {
            if ((box.Text ?? "") == last) return;
            last = box.Text ?? "";
            string t = (box.Text ?? "").Trim().Replace(',', '.');
            if (t.Length == 0) { changed(null); return; }
            if (double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v)) changed(Math.Clamp(v, min, max));
        };
        return box;
    }

    /// <summary>A round coloured number, as on the stops.</summary>
    public static Border Dot(string text, Color colour, double size = 22) => new()
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = Brush(colour), VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = text, FontSize = size * 0.55, FontWeight = FontWeight.Black, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
    };

    /// <summary>A small rounded tag of words on a coloured wash.</summary>
    public static Border Chip(string text, IBrush? back = null, IBrush? ink = null) => new()
    {
        Background = back ?? Raised, CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2), VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = text, FontSize = 11.5, FontWeight = FontWeight.Bold, Foreground = ink ?? Muted },
    };

    /// <summary>A key on the keyboard, drawn as a key cap.</summary>
    public static Border Key(string text) => new()
    {
        Background = Card, BorderBrush = LineStrong, BorderThickness = new Thickness(1, 1, 1, 2), CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 1),
        MinWidth = 22, VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = text, FontSize = 11.5, FontWeight = FontWeight.Bold, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center },
    };

    /// <summary>A two-way switch made of buttons, like the browser editor's segmented control.</summary>
    public static Border Segments(IReadOnlyList<(string Value, string Text, string Tip)> options, string value, Action<string> changed)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var (v, text, tip) in options)
        {
            bool on = v == value;
            var b = new Button
            {
                Content = text, FontSize = 12.5, Padding = new Thickness(10, 3), MinHeight = 24, CornerRadius = new CornerRadius(5),
                Background = on ? Card : Brushes.Transparent, Foreground = on ? Text : Muted, BorderThickness = new Thickness(on ? 1 : 0), BorderBrush = Line,
                FontWeight = on ? FontWeight.Bold : FontWeight.SemiBold,
            };
            b.Classes.Add("flat");
            if (on) b.Classes.Add("on");
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) => changed(v);
            row.Children.Add(b);
        }
        return new Border { Background = Raised, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(2), Child = row, VerticalAlignment = VerticalAlignment.Center };
    }
}

/// <summary>The editor's own line icons, drawn on a 24 unit square like the reader's (Reader.Ui.Icons).</summary>
static class EditorIcons
{
    public const string New = "M6.5 3.5 h7.5 l4 4 v13 h-11.5 z M14 3.5 v4 h4 M12.2 11 v6 M9.2 14 h6";
    public const string Open = Icons.Folder;
    public const string Save = "M5 4.5 h11 l3 3 v11.5 a1 1 0 0 1 -1 1 h-12 a1 1 0 0 1 -1 -1 z M8 4.5 v4.5 h7 v-4.5 M8 20 v-6 h8 v6";
    public const string Undo = "M9 7 l-4 4 4 4 M5 11 h9.5 a4.5 4.5 0 0 1 0 9 h-3";
    public const string Redo = "M15 7 l4 4 -4 4 M19 11 h-9.5 a4.5 4.5 0 0 0 0 9 h3";
    public const string Play = "M8 5.5 v13 l10.5 -6.5 z";
    public const string Check = "M4 6.5 h3 M4 12 h3 M4 17.5 h3 M10 6.5 h10 M10 12 h10 M10 17.5 h6 M16.5 17.5 l1.6 1.6 3 -3.2";
    public const string Export = "M12 3.8 v11 M7.5 8.3 l4.5 -4.5 4.5 4.5 M5 13.5 v5.5 a1 1 0 0 0 1 1 h12 a1 1 0 0 0 1 -1 v-5.5";
    public const string Help = "M3.8 12 a8.2 8.2 0 1 0 16.4 0 a8.2 8.2 0 1 0 -16.4 0 M9.6 9.6 a2.5 2.5 0 1 1 3.6 2.2 c-0.8 0.4 -1.2 1 -1.2 1.9 v0.4 M12 16.8 v0.3";
    public const string Recent = "M3.8 12 a8.2 8.2 0 1 0 16.4 0 a8.2 8.2 0 1 0 -16.4 0 M12 7.5 v4.8 l3.2 2";
    public const string ZoomIn = "M4.5 10.5 a6 6 0 1 0 12 0 a6 6 0 1 0 -12 0 M15 15 l5 5 M10.5 8 v5 M8 10.5 h5";
    public const string ZoomOut = "M4.5 10.5 a6 6 0 1 0 12 0 a6 6 0 1 0 -12 0 M15 15 l5 5 M8 10.5 h5";
    public const string Fit = Icons.Whole;
    public const string Screens = "M3.5 6 h12 v9 h-12 z M7.5 18.5 h4 M17.5 9 h3 v10 h-3 z";
    public const string BookFile = Icons.BookFile;
}
