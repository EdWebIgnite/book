using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace BookEditor;

/// <summary>
/// The editor's look, and small helpers for building its panels in code. A dark studio, as in the
/// browser editor: the page being worked on is the brightest thing on screen.
/// </summary>
static class Ui
{
    public static readonly Color BgColor = Color.Parse("#14161b");
    public static readonly IBrush Bg = Brush("#14161b");
    public static readonly IBrush Panel = Brush("#1c1f26");
    public static readonly IBrush Raised = Brush("#272b35");
    public static readonly IBrush RaisedHover = Brush("#2e3340");
    public static readonly IBrush Card = Brush("#20232b");
    public static readonly IBrush Line = Brush("#343946");
    public static readonly IBrush Text = Brush("#eceef3");
    public static readonly IBrush Muted = Brush("#9ba3b4");
    public static readonly IBrush Accent = Brush("#ff8a2a");
    public static readonly Color AccentColor = Color.Parse("#ff8a2a");
    public static readonly IBrush AccentInk = Brush("#201104");
    public static readonly IBrush Stage = Brush("#0e0f13");
    public static readonly IBrush Gold = Brush("#ffd23f");
    public static readonly IBrush Good = Brush("#5fd38d");
    public static readonly IBrush Bad = Brush("#ff6b6b");
    public static readonly IBrush Warn = Brush("#ffc145");
    public static readonly IBrush Field = Brush("#15181e");

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

    /// <summary>A small grey line of help under a control.</summary>
    public static TextBlock Help(string text) => new()
    {
        Text = text, FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap, LineHeight = 17, Margin = new Thickness(0, 4, 0, 2),
    };

    /// <summary>The name over a control.</summary>
    public static TextBlock FieldName(string text) => new()
    {
        Text = text, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 4),
    };

    /// <summary>A small heading inside a card.</summary>
    public static TextBlock Heading(string text) => new()
    {
        Text = text.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.Bold, LetterSpacing = 0.8, Foreground = Muted, Margin = new Thickness(0, 14, 0, 2),
    };

    public static Button Button(string text, Action click, string? tip = null, bool primary = false, bool small = false)
    {
        var b = new Button
        {
            Content = text, FontSize = small ? 12.5 : 13.5, Padding = small ? new Thickness(9, 4) : new Thickness(12, 7),
            CornerRadius = new CornerRadius(small ? 7 : 8), Focusable = true, VerticalAlignment = VerticalAlignment.Center,
        };
        if (primary)
        {
            b.Background = Accent;
            b.Foreground = AccentInk;
            b.FontWeight = FontWeight.Bold;
            b.Classes.Add("primary");
        }
        if (tip is not null) ToolTip.SetTip(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    public static WrapPanel Row(params Control[] items)
    {
        var w = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 2) };
        foreach (Control c in items)
        {
            c.Margin = new Thickness(0, 0, 6, 6);
            w.Children.Add(c);
        }
        return w;
    }

    /// <summary>A rounded box holding a group of controls.</summary>
    public static Border CardBox(Control content, IBrush? border = null) => new()
    {
        Background = Card, BorderBrush = border ?? Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
        Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 10), Child = content,
    };

    /// <summary>A section of the right-hand panel that can be folded away.</summary>
    public static Expander Section(string title, Control content, bool open = true, string? tag = null)
    {
        var head = new DockPanel { LastChildFill = false };
        var name = new TextBlock { Text = title, FontWeight = FontWeight.Bold, FontSize = 13.5, Foreground = Text };
        DockPanel.SetDock(name, Dock.Left);
        head.Children.Add(name);
        if (tag is not null)
        {
            var t = new TextBlock { Text = tag, FontSize = 11.5, Foreground = Muted, Margin = new Thickness(8, 1, 0, 0) };
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
        Text = value ?? "", PlaceholderText = placeholder, FontSize = 13.5, AcceptsReturn = multiline,
        TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 64 : 0,
    };

    public static CheckBox Check(string text, bool value, Action<bool> changed, string? tip = null)
    {
        var c = new CheckBox { IsChecked = value, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13.5, Foreground = Text }, Margin = new Thickness(0, 4, 0, 0) };
        if (tip is not null) ToolTip.SetTip(c, tip);
        c.IsCheckedChanged += (_, _) => changed(c.IsChecked == true);
        return c;
    }

    public static ComboBox Choice<T>(IReadOnlyList<(T Value, string Text)> options, T value, Action<T> changed)
    {
        var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13.5 };
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
        var output = new TextBlock { FontSize = 12, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 4) };
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
        var box = new TextBox { Text = value?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "", PlaceholderText = placeholder, Width = 110, HorizontalAlignment = HorizontalAlignment.Left, FontSize = 13.5 };
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
        Child = new TextBlock { Text = text, FontSize = size * 0.55, FontWeight = FontWeight.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
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
                Content = text, FontSize = 12.5, Padding = new Thickness(10, 4), CornerRadius = new CornerRadius(7),
                Background = on ? Raised : Brushes.Transparent, Foreground = on ? Text : Muted, BorderThickness = new Thickness(0),
            };
            if (on) b.Classes.Add("on");
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) => changed(v);
            row.Children.Add(b);
        }
        return new Border { Background = Field, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(2), Child = row, VerticalAlignment = VerticalAlignment.Center };
    }
}
