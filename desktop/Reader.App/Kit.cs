using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Reader.Core;
using Reader.Ui;

namespace Reader.App;

/// <summary>
/// The app's own small set of controls for the screens around the reader (welcome, who is reading,
/// the gate, the parents' area): buttons, switches, choices, cards and rows, all in the skin's colours
/// so they follow bedtime colours, and all usable with the keyboard on a computer, with a clear ring
/// round whatever has the focus.
/// </summary>
public static class Kit
{
    /// <summary>A computer with a keyboard: controls take the focus (Tab, Enter, Space). Off on phones and tablets.</summary>
    public static bool Keyboard { get; set; } = true;

    /// <summary>The ring round the control that has the keyboard's focus: 3 units of ink with a light halo, so it shows on any colour.</summary>
    public static BoxShadows FocusRing => new(
        new BoxShadow { Spread = 4, Color = Palette.Focus },
        new[] { new BoxShadow { Spread = 7, Color = Palette.Night ? Color.FromArgb(120, 20, 23, 58) : Color.FromArgb(150, 255, 255, 255) } });

    public static TextBlock Text(string text, double size, FontWeight weight = FontWeight.Normal, IBrush? colour = null, bool wrap = true) => new()
    {
        Text = text, FontSize = size, FontWeight = weight, Foreground = colour ?? Palette.InkBrush,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, LineHeight = Math.Round(size * 1.32),
        TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
    };

    /// <summary>A big heading in the serif.</summary>
    public static TextBlock Heading(string text, double size) => new()
    {
        Text = text, FontSize = size, FontWeight = FontWeight.Bold, FontFamily = Palette.Heading, Foreground = Palette.LeafDeepBrush,
        TextWrapping = TextWrapping.Wrap, LineHeight = Math.Round(size * 1.18),
    };

    /// <summary>A small heading over a group of rows, in spaced capitals.</summary>
    public static TextBlock Label(string text, double size = 13) => new()
    {
        Text = text.ToUpper(System.Globalization.CultureInfo.InvariantCulture), FontSize = size, FontWeight = FontWeight.ExtraBold, LetterSpacing = 1.1,
        Foreground = Palette.Night ? Palette.InkSoftBrush : Palette.Brush(Palette.Ribbon), TextWrapping = TextWrapping.Wrap,
    };

    public static Control Icon(string data, double size, IBrush? colour = null, bool filled = false)
    {
        Control c = Icons.Make(data, size, colour ?? Palette.InkBrush, filled);
        c.VerticalAlignment = VerticalAlignment.Center;
        c.HorizontalAlignment = HorizontalAlignment.Center;
        return c;
    }

    /// <summary>A white or dark card with soft corners and a soft shadow, the parents' area's grouping.</summary>
    public static Border Card(Control child, double radius = 20) => new()
    {
        Background = Palette.PaperBrush, CornerRadius = new CornerRadius(radius), Child = child, ClipToBounds = false,
        BorderBrush = Palette.LeafPaleBrush, BorderThickness = new Thickness(1),
        BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 10, Color = Palette.ShadowOf(Palette.Night ? 0.35 : 0.08) }),
    };

    /// <summary>A thin line between rows of a card.</summary>
    public static Control Divider() => new Border { Height = 1, Background = Palette.LeafPaleBrush, Margin = new Thickness(16, 0) };
}

/// <summary>
/// Something to press: answers the finger as it lands (the look changes at once) and acts when it
/// lifts inside, takes Enter and Space when it has the keyboard focus, and shows the focus ring.
/// </summary>
public class Tap : Border
{
    bool _pressed, _over, _ring, _enabled = true;

    public Tap(string? name = null)
    {
        Focusable = Kit.Keyboard;
        IsTabStop = Kit.Keyboard;
        Cursor = new Cursor(StandardCursorType.Hand);
        if (name is not null) AutomationProperties.SetName(this, name);
    }

    public event Action? Click;

    public bool Pressed => _pressed;
    public bool Over => _over;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Opacity = value ? 1 : 0.45;
            Cursor = new Cursor(value ? StandardCursorType.Hand : StandardCursorType.Arrow);
            Focusable = value && Kit.Keyboard;
            Restyle();
        }
    }

    /// <summary>Act as if pressed and let go (the keyboard, and screenshots).</summary>
    public void Press()
    {
        if (_enabled) Click?.Invoke();
    }

    /// <summary>Set the look for the current state. Called whenever pressed, hovered or focus changes.</summary>
    protected virtual void Restyle() { }

    /// <summary>The control's own shadow; the focus ring is added round it.</summary>
    protected BoxShadows Shadow { get => _shadow; set { _shadow = value; ApplyShadow(); } }
    BoxShadows _shadow;

    void ApplyShadow()
    {
        if (!_ring) { BoxShadow = _shadow; return; }
        var all = new List<BoxShadow>();
        foreach (BoxShadow s in Kit.FocusRing) all.Add(s);
        foreach (BoxShadow s in _shadow) all.Add(s);
        BoxShadow = new BoxShadows(all[0], all.Skip(1).ToArray());
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _ring = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        ApplyShadow();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _ring = false;
        ApplyShadow();
    }

    /// <summary>Show the focus ring as if the keyboard had come here (screenshots).</summary>
    public void ShowRing()
    {
        _ring = true;
        ApplyShadow();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !_enabled) return;
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Click?.Invoke();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _over = e.Pointer.Type == PointerType.Mouse;
        Restyle();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _over = false;
        Restyle();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
        if (!_enabled) return;
        _pressed = true;
        e.Pointer.Capture(this);
        Restyle();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Handled = true;
        if (!_pressed) return;
        _pressed = false;
        e.Pointer.Capture(null);
        Restyle();
        if (_enabled && new Rect(Bounds.Size).Contains(e.GetPosition(this))) Click?.Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!_pressed) return;
        _pressed = false;
        Restyle();
    }
}

/// <summary>What a <see cref="Pill"/> looks like.</summary>
public enum PillKind
{
    /// <summary>The one main action of a screen: bottle green (lamp gold at bedtime).</summary>
    Main,
    /// <summary>An ordinary button: the cream tactile face.</summary>
    Plain,
    /// <summary>For removing something.</summary>
    Danger,
    /// <summary>No face at all, just words: "Skip".</summary>
    Link,
}

/// <summary>A rounded button with words and an optional picture, in the skin's tactile style.</summary>
public sealed class Pill : Tap
{
    readonly PillKind _kind;
    readonly TextBlock _label;
    readonly Control? _icon;

    public Pill(string text, PillKind kind = PillKind.Plain, string? icon = null, double height = 56, double fontSize = 19, bool iconFilled = false, double? iconSize = null) : base(text)
    {
        _kind = kind;
        MinHeight = height;
        Padding = new Thickness(height * 0.42, 6, height * 0.46, 6);
        _label = new TextBlock
        {
            Text = text, FontSize = fontSize, FontWeight = FontWeight.ExtraBold, VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, LineHeight = Math.Round(fontSize * 1.2),
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = fontSize * 0.5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (icon is not null)
        {
            _icon = Icons.Make(icon, iconSize ?? fontSize * 1.15, Ink, iconFilled);
            _icon.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(_icon);
        }
        row.Children.Add(_label);
        Child = row;
        Restyle();
    }

    public TextBlock Label => _label;

    Buttons.Look? Look => _kind switch { PillKind.Main => Buttons.Green, PillKind.Danger => Buttons.Red, PillKind.Plain => Buttons.Plain, _ => null };

    IBrush Ink => Look is Buttons.Look l ? new ImmutableSolidColorBrush(l.Ink) : Palette.Night ? Palette.InkBrush : Palette.LeafDeepBrush;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        CornerRadius = new CornerRadius(Math.Min(e.NewSize.Height / 2, 30));
    }

    protected override void Restyle()
    {
        if (_label is null) return;
        IBrush ink = Ink;
        _label.Foreground = ink;
        if (_icon is Viewbox { Child: Canvas { Children: [Avalonia.Controls.Shapes.Path p] } })
        {
            if (p.Fill is not null) p.Fill = ink;
            else p.Stroke = ink;
        }
        if (Look is not Buttons.Look look)
        {
            Background = Over || Pressed ? Palette.Brush(Palette.Leaf, 0.08) : Brushes.Transparent;
            _label.TextDecorations = Over ? TextDecorations.Underline : null;
            Shadow = default;
            return;
        }
        Background = Pressed ? Buttons.PressedFace(look) : Over ? Buttons.HoverFace(look) : Buttons.Face(look);
        BorderBrush = Buttons.Edge(look);
        BorderThickness = Pressed ? Buttons.LipPressed : Buttons.Lip;
        Shadow = new BoxShadows(Pressed ? Buttons.ShadowPressed : new BoxShadow { OffsetY = 3, Blur = 8, Color = Palette.ShadowOf(Palette.Night ? 0.5 : 0.22) });
        RenderTransform = Pressed ? new TranslateTransform(0, 1.5) : null;
    }
}

/// <summary>A round button with a picture: close, back.</summary>
public sealed class RoundTap : Tap
{
    readonly string _icon;
    readonly double _iconSize;

    public RoundTap(string name, string icon, double diameter = 48, double? iconSize = null) : base(name)
    {
        _icon = icon;
        _iconSize = iconSize ?? diameter * 0.46;
        Width = Height = diameter;
        CornerRadius = new CornerRadius(diameter / 2);
        ToolTip.SetTip(this, name);
        Restyle();
    }

    protected override void Restyle()
    {
        if (_icon is null) return;
        Buttons.Look look = Buttons.Plain;
        Background = Pressed ? Buttons.PressedFace(look) : Over ? Buttons.HoverFace(look) : Buttons.Face(look);
        BorderBrush = Buttons.Edge(look);
        BorderThickness = Pressed ? Buttons.LipPressed : Buttons.Lip;
        Shadow = new BoxShadows(Pressed ? Buttons.ShadowPressed : Buttons.Shadow);
        Child = Kit.Icon(_icon, _iconSize, new ImmutableSolidColorBrush(look.Ink));
        RenderTransform = Pressed ? new TranslateTransform(0, 1.5) : null;
    }
}

/// <summary>An on-off switch, 54 by 32, with a tick in its knob when on, so it isn't told by colour alone.</summary>
public sealed class Switch : Tap
{
    bool _on;
    readonly Border _knob;

    public Switch(string name, bool on) : base(name)
    {
        _on = on;
        Width = 58;
        Height = 34;
        CornerRadius = new CornerRadius(17);
        BorderThickness = new Thickness(1.5);
        _knob = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), VerticalAlignment = VerticalAlignment.Center };
        Child = _knob;
        Click += () => { On = !_on; Toggled?.Invoke(_on); };
        Restyle();
    }

    public event Action<bool>? Toggled;

    public bool On
    {
        get => _on;
        set { _on = value; Restyle(); }
    }

    protected override void Restyle()
    {
        if (_knob is null) return;
        AutomationProperties.SetHelpText(this, _on ? Strings.T("common.on") : Strings.T("common.off"));
        Background = _on ? Palette.LeafBrush : Palette.Brush(Palette.Night ? Color.Parse("#4a5096") : Color.Parse("#d8cfbb"));
        BorderBrush = _on ? Palette.LeafBrush : Palette.Brush(Palette.Night ? Color.Parse("#6870b8") : Color.Parse("#a99a72"));
        _knob.Background = Palette.Night && _on ? Palette.Brush(Color.Parse("#1b1f4b")) : Brushes.White;
        _knob.BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 1, Blur = 3, Color = Palette.ShadowOf(0.3) });
        _knob.HorizontalAlignment = _on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        _knob.Margin = new Thickness(3, 0);
        _knob.Child = _on ? Kit.Icon(Icons.Tick, 16, Palette.LeafBrush) : null;
        Shadow = default;
    }
}

/// <summary>A row of two to four choices, one of them chosen: the chosen one is raised and carries a tick.</summary>
public sealed class Choices : Border
{
    readonly List<(string Value, Option Tap)> _options = new();
    string _value;

    public Choices(string name, IEnumerable<(string Value, string Text)> options, string value, double height = 48)
    {
        _value = value;
        AutomationProperties.SetName(this, name);
        Background = Palette.Night ? Palette.Brush(Color.Parse("#20245a")) : Palette.Brush(Color.Parse("#efe7d4"));
        CornerRadius = new CornerRadius(height / 2 + 4);
        Padding = new Thickness(4);
        var grid = new Grid();
        foreach (var (v, text) in options)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var tap = new Option(text, height);
            string chosen = v;
            tap.Click += () => { Value = chosen; Changed?.Invoke(chosen); };
            Grid.SetColumn(tap, _options.Count);
            grid.Children.Add(tap);
            _options.Add((v, tap));
        }
        Child = grid;
        Show();
    }

    public event Action<string>? Changed;

    public string Value
    {
        get => _value;
        set { _value = value; Show(); }
    }

    void Show()
    {
        foreach (var (v, tap) in _options) tap.Chosen = v == _value;
    }

    sealed class Option : Tap
    {
        readonly TextBlock _text;
        bool _chosen;

        public Option(string text, double height) : base(text)
        {
            MinHeight = height;
            CornerRadius = new CornerRadius(height / 2);
            Padding = new Thickness(8, 4);
            _text = new TextBlock
            {
                Text = text, FontSize = 15, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, LineHeight = 18,
            };
            Child = _text;
            Restyle();
        }

        public bool Chosen
        {
            get => _chosen;
            set { _chosen = value; Restyle(); }
        }

        protected override void Restyle()
        {
            if (_text is null) return;
            AutomationProperties.SetHelpText(this, _chosen ? "chosen" : "");
            Background = _chosen ? Palette.PaperBrush : Over ? Palette.Brush(Palette.Paper, 0.5) : Brushes.Transparent;
            _text.Foreground = _chosen ? Palette.InkBrush : Palette.InkSoftBrush;
            _text.TextDecorations = _chosen ? TextDecorations.Underline : null;
            Shadow = _chosen ? new BoxShadows(new BoxShadow { OffsetY = 1, Blur = 4, Color = Palette.ShadowOf(Palette.Night ? 0.5 : 0.18) }) : default;
            BorderBrush = _chosen ? Palette.LeafBrush : null;
            BorderThickness = new Thickness(_chosen ? 2 : 0);
        }
    }
}

/// <summary>
/// One row of a card in the parents' area: an icon, a title and a line of explanation, and on the right
/// a switch, a choice, or an arrow to a page of its own. A row with an arrow is pressed as a whole.
/// </summary>
public sealed class Row : Tap
{
    readonly bool _pressable;

    public Row(string? icon, string title, string? note = null, Control? trailing = null, bool arrow = false, Control? leading = null, IBrush? titleColour = null) : base(title)
    {
        _pressable = arrow;
        Focusable = arrow && Kit.Keyboard;
        IsTabStop = arrow && Kit.Keyboard;
        if (!arrow) Cursor = Cursor.Default;
        MinHeight = 64;
        Padding = new Thickness(16, 10, 14, 10);
        CornerRadius = new CornerRadius(12);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 14 };
        Control? lead = leading ?? (icon is null ? null : Kit.Icon(icon, 24, Palette.Night ? Palette.LeafBrush : Palette.LeafBrush));
        if (lead is not null)
        {
            lead.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(lead);
        }
        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        words.Children.Add(Kit.Text(title, 17, FontWeight.Bold, titleColour));
        if (!string.IsNullOrEmpty(note)) words.Children.Add(Kit.Text(note!, 14, FontWeight.Normal, Palette.InkSoftBrush));
        Grid.SetColumn(words, 1);
        grid.Children.Add(words);
        Control? right = trailing ?? (arrow ? Kit.Icon(Icons.Chevron, 20, Palette.InkSoftBrush) : null);
        if (right is not null)
        {
            right.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(right, 2);
            grid.Children.Add(right);
        }
        Child = grid;
        Restyle();
    }

    protected override void Restyle()
    {
        Background = _pressable && (Over || Pressed) ? Palette.Brush(Palette.Leaf, Pressed ? 0.12 : 0.06) : Brushes.Transparent;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        // A row that only holds a switch or a choice is not pressed as a whole.
        if (!_pressable) return;
        base.OnPointerPressed(e);
    }
}

/// <summary>
/// The children's pictures: a white shape on a colour (a sun, a leaf, a star, a drop, a flower, a moon,
/// a kite, a heart), never a photo. Told apart by shape as well as colour.
/// </summary>
public sealed class Avatar : Control
{
    public static readonly Color[] Colours =
    {
        Color.Parse("#d99a22"), Color.Parse("#3f8a5f"), Color.Parse("#7563c0"), Color.Parse("#3577b0"),
        Color.Parse("#c4567f"), Color.Parse("#2f4a7a"), Color.Parse("#d0682f"), Color.Parse("#bb4039"),
    };

    static readonly string[] Shapes =
    {
        "M7.6 12 a4.4 4.4 0 1 0 8.8 0 a4.4 4.4 0 1 0 -8.8 0 z",
        "M5 19 C5 10.5 10.5 5 19 5 C19 13.5 13.5 19 5 19 Z",
        Icons.Star,
        "M12 3.2 C12 3.2 5.6 10.8 5.6 14.8 a6.4 6.4 0 0 0 12.8 0 C18.4 10.8 12 3.2 12 3.2 Z",
        "M12 12 h0.01",
        Icons.Moon,
        "M12 2.8 L18.2 10 L12 18.2 L5.8 10 Z",
        "M12 20 C5.2 15.2 3.6 11.6 3.6 8.9 A4.3 4.3 0 0 1 12 7.3 A4.3 4.3 0 0 1 20.4 8.9 C20.4 11.6 18.8 15.2 12 20 Z",
    };

    static readonly Geometry[] Parsed = Shapes.Select(Geometry.Parse).ToArray();
    static readonly Geometry SunRays = Geometry.Parse("M12 3 v2.6 M12 18.4 V21 M3 12 h2.6 M18.4 12 H21 M5.6 5.6 l1.8 1.8 M16.6 16.6 l1.8 1.8 M18.4 5.6 l-1.8 1.8 M7.4 16.6 l-1.8 1.8");
    static readonly Geometry LeafVein = Geometry.Parse("M6.8 17.2 L14.5 9.5");
    static readonly Geometry KiteTail = Geometry.Parse("M12 18.2 c-1.6 1.4 1.2 2.2 -0.6 3.6");
    static readonly Geometry KiteCross = Geometry.Parse("M12 2.8 V18.2 M5.8 10 H18.2");

    public static readonly StyledProperty<int> IndexProperty = AvaloniaProperty.Register<Avatar, int>(nameof(Index));
    public static readonly StyledProperty<bool> ChosenProperty = AvaloniaProperty.Register<Avatar, bool>(nameof(Chosen));

    static Avatar() => AffectsRender<Avatar>(IndexProperty, ChosenProperty);

    public Avatar(int index, double size)
    {
        Index = index;
        Width = Height = size;
        IsHitTestVisible = false;
    }

    public int Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }

    /// <summary>A thick ring and a tick show which picture is chosen.</summary>
    public bool Chosen { get => GetValue(ChosenProperty); set => SetValue(ChosenProperty, value); }

    public override void Render(DrawingContext ctx) => Draw(ctx, Index, new Rect(Bounds.Size), Chosen);

    public static void Draw(DrawingContext ctx, int index, Rect r, bool chosen = false)
    {
        int i = ((index % Colours.Length) + Colours.Length) % Colours.Length;
        double d = Math.Min(r.Width, r.Height), rad = d / 2;
        var c = r.Center;
        // A white rim, as a sticker, then the colour.
        ctx.DrawEllipse(new ImmutableSolidColorBrush(Palette.ShadowOf(0.2)), null, new Point(c.X, c.Y + d * 0.03), rad, rad);
        ctx.DrawEllipse(Brushes.White, null, c, rad, rad);
        double inner = rad * 0.9;
        ctx.DrawEllipse(new ImmutableSolidColorBrush(Colours[i]), null, c, inner, inner);
        double k = inner * 1.15 / 12;
        using (ctx.PushTransform(Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(k, k) * Matrix.CreateTranslation(c.X, c.Y)))
        {
            var white = Brushes.White;
            var pen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.White), 2.1, lineCap: PenLineCap.Round);
            var vein = new ImmutablePen(new ImmutableSolidColorBrush(Colours[i]), 1.3, lineCap: PenLineCap.Round);
            if (i == 0) ctx.DrawGeometry(null, pen, SunRays);
            if (i == 4)
            {
                for (int n = 0; n < 5; n++)
                {
                    double a = n * Math.PI * 2 / 5 - Math.PI / 2;
                    ctx.DrawEllipse(white, null, new Point(12 + Math.Cos(a) * 4.8, 12 + Math.Sin(a) * 4.8), 3.6, 3.6);
                }
                ctx.DrawEllipse(new ImmutableSolidColorBrush(Colours[i]), null, new Point(12, 12), 2.4, 2.4);
            }
            else ctx.DrawGeometry(white, null, Parsed[i]);
            if (i == 1) ctx.DrawGeometry(null, vein, LeafVein);
            if (i == 6)
            {
                ctx.DrawGeometry(null, vein, KiteCross);
                ctx.DrawGeometry(null, new ImmutablePen(new ImmutableSolidColorBrush(Colors.White), 1.3, lineCap: PenLineCap.Round), KiteTail);
            }
        }
        if (chosen)
        {
            ctx.DrawEllipse(null, new ImmutablePen(new ImmutableSolidColorBrush(Palette.Leaf), Math.Max(3, d * 0.05)), c, rad + d * 0.06, rad + d * 0.06);
            double t = d * 0.3;
            var tc = new Point(c.X + rad * 0.72, c.Y + rad * 0.72);
            ctx.DrawEllipse(new ImmutableSolidColorBrush(Palette.Leaf), new ImmutablePen(Brushes.White, Math.Max(2, d * 0.03)), tc, t / 2, t / 2);
            using (ctx.PushTransform(Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(t / 30, t / 30) * Matrix.CreateTranslation(tc.X, tc.Y)))
                ctx.DrawGeometry(null, new ImmutablePen(new ImmutableSolidColorBrush(Palette.OnLeaf), 3, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), Geometry.Parse(Icons.Tick));
        }
    }
}
