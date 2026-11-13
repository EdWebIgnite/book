using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Reader.Core;

namespace Reader.Ui;

/// <summary>The picture icons on the reader's buttons, drawn on the same 24 x 24 grid as the browser version's.</summary>
static class Icons
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
    public static readonly IBrush Ink = new ImmutableSolidColorBrush(Color.Parse("#3a2f4a"));

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
        _shadow = new BoxShadow { OffsetY = 2, Blur = 8, Color = Color.FromArgb(77, 0, 0, 0) };
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

    void ApplyShadow() => BoxShadow = _ring is BoxShadow ring ? new BoxShadows(ring, new[] { _shadow }) : new BoxShadows(_shadow);

    void SetPressed(bool pressed)
    {
        _pressed = pressed;
        RenderTransform = pressed ? new ScaleTransform(0.92, 0.92) : null;
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

/// <summary>A wide rounded button with words on it, for The End box.</summary>
public sealed class PillButton : Border
{
    bool _pressed;

    public PillButton(string text, bool primary)
    {
        Background = primary ? new ImmutableSolidColorBrush(Color.Parse("#e8730c")) : new ImmutableSolidColorBrush(Color.Parse("#efefef"));
        CornerRadius = new CornerRadius(999);
        Padding = new Thickness(26, 14);
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = false;
        RenderTransformOrigin = RelativePoint.Center;
        AutomationProperties.SetName(this, text);
        Child = Label = new TextBlock
        {
            Text = text, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = primary ? Brushes.White : Brushes.Black,
        };
    }

    public TextBlock Label { get; }
    public event Action? Click;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
        _pressed = true;
        e.Pointer.Capture(this);
        Opacity = 0.85;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Handled = true;
        if (!_pressed) return;
        _pressed = false;
        Opacity = 1;
        e.Pointer.Capture(null);
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) Click?.Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        Opacity = 1;
    }
}
