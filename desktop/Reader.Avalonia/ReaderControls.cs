using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Transformation;
using Reader.Core;

namespace Reader.Ui;

/// <summary>The picture icons on the reader's buttons, drawn on the same 24 x 24 grid as the browser version's.</summary>
public static class Icons
{
    public const string Back = "M15 5 l-7 7 7 7";
    public const string Forward = "M9 5 l7 7 -7 7";
    public const string Plus = "M12 5 v14 M5 12 h14";
    public const string Minus = "M5 12 h14";
    /// <summary>Four corners pointing out: see the whole page.</summary>
    public const string Whole = "M4 9 V4 h5 M20 9 V4 h-5 M4 15 v5 h5 M20 15 v5 h-5";
    /// <summary>Four corners pointing in: back to the close-up.</summary>
    public const string CloseUp = "M9 4 v5 H4 M15 4 v5 h5 M9 20 v-5 H4 M15 20 v-5 h5";
    public const string Gear = "M8.8 12 a3.2 3.2 0 1 0 6.4 0 a3.2 3.2 0 1 0 -6.4 0 " +
                               "M12 2.5 v3 M12 18.5 v3 M2.5 12 h3 M18.5 12 h3 M5.3 5.3 l2.1 2.1 M16.6 16.6 l2.1 2.1 M18.7 5.3 l-2.1 2.1 M7.4 16.6 l-2.1 2.1";
    public const string Star = "M12 3 l2.6 5.6 6.1 0.7 -4.5 4.2 1.2 6 -5.4 -3 -5.4 3 1.2 -6 L3.3 9.3 l6.1 -0.7 z";
    /// <summary>Three books standing on a shelf: back to my books.</summary>
    public const string Books = "M4.5 5 h3.6 v14 h-3.6 z M10 5 h3.6 v14 h-3.6 z M15.4 6.2 l3.4 -1 3 12.8 -3.4 0.9 z";
    /// <summary>A padlock: for grown-ups.</summary>
    public const string Lock = "M7 10.5 V8 a5 5 0 0 1 10 0 v2.5 M5.5 10.5 h13 v9 h-13 z";
    /// <summary>Two arrows, one each way: change who is reading.</summary>
    public const string Swap = "M5 8.5 h13 l-3.5 -3.5 M19 15.5 H6 l3.5 3.5";
    public const string Close = "M6.5 6.5 L17.5 17.5 M17.5 6.5 L6.5 17.5";
    public const string Chevron = "M9.5 5.5 l6.5 6.5 -6.5 6.5";
    public const string Play = "M8 5.2 v13.6 l11 -6.8 z";
    public const string Tick = "M5.5 12.5 l4.2 4.2 L18.5 7.5";
    /// <summary>A round arrow: again.</summary>
    public const string Again = "M5.5 12 a6.5 6.5 0 1 0 2.2 -4.9 M5 4.5 v4 h4";
    public const string Shield = "M12 3.5 l7 2.8 v5.2 c0 4.4 -3 7.6 -7 9 c-4 -1.4 -7 -4.6 -7 -9 V6.3 z M8.8 12 l2.3 2.3 4.3 -4.5";
    public const string Globe = "M3.8 12 a8.2 8.2 0 1 0 16.4 0 a8.2 8.2 0 1 0 -16.4 0 M3.8 12 h16.4 M12 3.8 c-4 4.6 -4 11.8 0 16.4 M12 3.8 c4 4.6 4 11.8 0 16.4";
    public const string Moon = "M15.5 4.2 A8.2 8.2 0 1 0 19.8 15.8 A6.6 6.6 0 0 1 15.5 4.2 z";
    public const string Speaker = "M4.5 9.5 h3.5 l4.5 -4 v13 l-4.5 -4 H4.5 z M15.5 9 a4.2 4.2 0 0 1 0 6 M17.8 6.6 a7.6 7.6 0 0 1 0 10.8";
    public const string BookFile = "M6.5 3.5 h7.5 l4 4 v13 h-11.5 z M14 3.5 v4 h4 M9.5 12.5 h5 M9.5 16 h5";
    public const string Folder = "M3.5 7.5 a2 2 0 0 1 2 -2 h3.5 l2 2 h7.5 a2 2 0 0 1 2 2 v7 a2 2 0 0 1 -2 2 h-13 a2 2 0 0 1 -2 -2 z";
    public const string Child = "M9 7 a3 3 0 1 0 6 0 a3 3 0 1 0 -6 0 M5.5 20 c0.5 -4.5 3 -7 6.5 -7 s6 2.5 6.5 7";
    public const string Info = "M3.8 12 a8.2 8.2 0 1 0 16.4 0 a8.2 8.2 0 1 0 -16.4 0 M12 11 v5.5 M12 7.6 v0.4";
    /// <summary>A finger tapping.</summary>
    public const string Hand = "M9.5 12 V5.5 a1.5 1.5 0 0 1 3 0 v5 m0 -1 a1.5 1.5 0 0 1 3 0 v1.5 m0 -0.6 a1.5 1.5 0 0 1 3 0 v4.6 c0 3 -2 5.5 -5.5 5.5 h-1 c-2 0 -3.4 -1 -4.5 -2.6 l-2.4 -3.6 a1.5 1.5 0 0 1 2.4 -1.8 l1.5 1.8";
    /// <summary>A magnifying glass: things to find.</summary>
    public const string Find = "M4.5 10.5 a6 6 0 1 0 12 0 a6 6 0 1 0 -12 0 M15 15 l5 5";
    public const string Device = "M7.5 3.5 h9 a1.5 1.5 0 0 1 1.5 1.5 v14 a1.5 1.5 0 0 1 -1.5 1.5 h-9 a1.5 1.5 0 0 1 -1.5 -1.5 v-14 a1.5 1.5 0 0 1 1.5 -1.5 z M11 17.5 h2";
    /// <summary>A big and a small letter A: the size of the words.</summary>
    public const string TextSize = "M3 19 l5 -13 5 13 M4.8 14.6 h6.4 M14 19 l3.4 -8.6 3.4 8.6 M15.2 16.2 h4.4";
    public const string Eye = "M2.8 12 c2.4 -4.2 5.6 -6.3 9.2 -6.3 s6.8 2.1 9.2 6.3 c-2.4 4.2 -5.6 6.3 -9.2 6.3 S5.2 16.2 2.8 12 z M9.2 12 a2.8 2.8 0 1 0 5.6 0 a2.8 2.8 0 1 0 -5.6 0";

    /// <summary>An icon as a control. Stroked like a felt-tip line unless <paramref name="filled"/>.</summary>
    public static Control Make(string data, double size, IBrush colour, bool filled = false)
    {
        var path = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(data) };
        if (filled) path.Fill = colour;
        else
        {
            path.Stroke = colour;
            path.StrokeThickness = 2.6;
            path.StrokeLineCap = PenLineCap.Round;
            path.StrokeJoin = PenLineJoin.Round;
        }
        return new Viewbox
        {
            Width = size, Height = size, Stretch = Stretch.Uniform, IsHitTestVisible = false,
            Child = new Canvas { Width = 24, Height = 24, Children = { path } },
        };
    }
}

/// <summary>
/// A round picture button. 56 units is about 1.5 cm; the arrows children use most are bigger still.
/// It never takes the keyboard focus, so Space and the arrow keys always go to the reader.
/// </summary>
public class RoundButton : Border
{
    static readonly IBrush Paper = new ImmutableSolidColorBrush(Color.FromArgb(235, 255, 255, 255));   // rgba(255,255,255,.92)
    /// <summary>The icons' colour: the plain dark text colour of the skin's buttons.</summary>
    public static IBrush Ink => Buttons.TextBrush;

    bool _pressed, _enabled = true;
    BoxShadow _shadow;
    BoxShadow? _ring;

    public RoundButton(string label, double diameter = 56)
    {
        Background = Paper;
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.Hand);
        RenderTransformOrigin = RelativePoint.Center;
        AutomationProperties.SetName(this, label);
        ToolTip.SetTip(this, label);
        _shadow = new BoxShadow { OffsetY = 2, Blur = 8, Color = Palette.ShadowOf(0.3) };
        Diameter = diameter;
        ApplyShadow();
    }

    public event Action? Click;
    /// <summary>Pressed and let go (or slid off): for the hold-to-open lock on the gear.</summary>
    public event Action? Pressed, Released;

    public double Diameter
    {
        get => Width;
        set { Width = Height = value; CornerRadius = new CornerRadius(value / 2); }
    }

    /// <summary>The drop shadow under the button.</summary>
    public BoxShadow Shadow
    {
        get => _shadow;
        set { _shadow = value; ApplyShadow(); }
    }

    /// <summary>An extra coloured ring around the button, for the Show me pulse and the gear's hold timer.</summary>
    public BoxShadow? Ring
    {
        get => _ring;
        set { if (_ring != value) { _ring = value; ApplyShadow(); } }
    }

    /// <summary>How see-through the button is when it can't be used.</summary>
    public double DisabledOpacity { get; set; } = 0.35;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Opacity = value ? 1 : DisabledOpacity;
            Cursor = new Cursor(value ? StandardCursorType.Hand : StandardCursorType.Arrow);
            if (!value) SetPressed(false);
        }
    }

    public void SetIcon(string data, double size, IBrush? colour = null, bool filled = false)
    {
        Control icon = Icons.Make(data, size, colour ?? Ink, filled);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
        Child = icon;
    }

    void ApplyShadow()
    {
        BoxShadow shadow = _pressed && _look is not null ? Buttons.ShadowPressed : _shadow;
        BoxShadow = _ring is BoxShadow ring ? new BoxShadows(ring, new[] { shadow }) : new BoxShadows(shadow);
    }

    Buttons.Look? _look;
    double _alpha = 1;

    /// <summary>
    /// The tactile face (see <see cref="Buttons"/>): a soft top-to-bottom face, a lip along the bottom, and
    /// a sink when pressed. <paramref name="glass"/> is the see-through version that floats over the picture
    /// on a phone on its side: the same face, faint, with no edge.
    /// </summary>
    public void SetLook(Buttons.Look look, bool glass, double glassAlpha = 0.55)
    {
        _look = look;
        _alpha = glass ? glassAlpha : 1;
        ApplyLook();
    }

    void ApplyLook()
    {
        if (_look is not Buttons.Look look) return;
        bool glass = _alpha < 1;
        Background = _pressed && !glass ? Buttons.PressedFace(look) : Buttons.Face(look, glass ? _alpha : 0.97);
        BorderBrush = glass ? null : Buttons.Edge(look);
        BorderThickness = glass ? default : _pressed ? Buttons.LipPressed : Buttons.Lip;
    }

    void SetPressed(bool pressed)
    {
        _pressed = pressed;
        // Pressed, the button sinks a little: down a pixel or two, a touch smaller, its shadow tucked in.
        RenderTransform = pressed ? (_look is null ? new ScaleTransform(0.92, 0.92) : TransformOperations.Parse("translateY(2px) scale(0.96)")) : null;
        ApplyLook();
        ApplyShadow();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;                      // a press on a button is never also a tap on the picture
        if (!_enabled) return;
        e.Pointer.Capture(this);
        SetPressed(true);
        Pressed?.Invoke();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Handled = true;
        if (!_pressed) return;
        SetPressed(false);
        e.Pointer.Capture(null);
        Released?.Invoke();
        if (_enabled && new Rect(Bounds.Size).Contains(e.GetPosition(this))) Click?.Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!_pressed) return;
        SetPressed(false);
        Released?.Invoke();
    }
}

/// <summary>A wide rounded button with words on it (and a picture, or a little cover), for The End box.</summary>
public sealed class PillButton : Border
{
    bool _pressed;

    public PillButton(string text, bool primary, string? icon = null, Avalonia.Media.Imaging.Bitmap? thumb = null, string? trailing = null, double height = 60, double fontSize = 20)
    {
        _look = primary ? Buttons.Green : Buttons.Plain;
        ApplyLook(false);
        MinHeight = height;
        Padding = new Thickness(thumb is null ? height * 0.45 : height * 0.16, 6, height * 0.45, 6);
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = false;
        RenderTransformOrigin = RelativePoint.Center;
        AutomationProperties.SetName(this, text);
        var ink = new ImmutableSolidColorBrush(_look.Ink);
        Label = new TextBlock
        {
            Text = text, FontSize = fontSize, FontWeight = FontWeight.ExtraBold, VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Foreground = ink,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = fontSize * 0.55, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (thumb is not null)
        {
            row.Children.Add(new Border
            {
                Width = (height - 14) / 1.5, Height = height - 14, CornerRadius = new CornerRadius(2, 5, 5, 2), ClipToBounds = true,
                Child = new Image { Source = thumb, Stretch = Stretch.UniformToFill },
            });
        }
        if (icon is not null)
        {
            Control i = Icons.Make(icon, fontSize * 1.2, ink);
            i.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(i);
        }
        row.Children.Add(Label);
        if (trailing is not null)
        {
            Control i = Icons.Make(trailing, fontSize, ink);
            i.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(i);
        }
        Child = row;
    }

    readonly Buttons.Look _look;

    /// <summary>The tactile face, lip and shadow; pressed, the face darkens, the lip thins and the button sinks.</summary>
    void ApplyLook(bool pressed)
    {
        Background = pressed ? Buttons.PressedFace(_look) : Buttons.Face(_look);
        BorderBrush = Buttons.Edge(_look);
        BorderThickness = pressed ? Buttons.LipPressed : Buttons.Lip;
        BoxShadow = new BoxShadows(pressed ? Buttons.ShadowPressed : new BoxShadow { OffsetY = 3, Blur = 7, Color = Palette.ShadowOf(Palette.Night ? 0.5 : 0.24) });
        RenderTransform = pressed ? new TranslateTransform(0, 2) : null;
    }

    public TextBlock Label { get; }
    public event Action? Click;

    /// <summary>Act as if pressed and let go.</summary>
    public void Press() => Click?.Invoke();

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // Fully round ends. (A huge corner radius with a thicker bottom edge draws as an oval.)
        CornerRadius = new CornerRadius(e.NewSize.Height / 2);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
        _pressed = true;
        e.Pointer.Capture(this);
        ApplyLook(true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Handled = true;
        if (!_pressed) return;
        _pressed = false;
        ApplyLook(false);
        e.Pointer.Capture(null);
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) Click?.Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        ApplyLook(false);
    }
}
