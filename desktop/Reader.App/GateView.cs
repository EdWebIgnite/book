using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Reader.Core;
using Reader.Ui;

namespace Reader.App;

/// <summary>
/// The grown-ups' gate: three numbers written as words ("seven, two, nine", or "sewe, twee, nege") to
/// tap on a number pad. A child who can't read yet can't follow it; a grown-up does it in a few
/// seconds. New numbers every time, a rest of half a minute after three wrong tries, and no way to
/// switch it off. On a phone it fills the screen; on a wide screen it is a card in the middle. The
/// keyboard's number keys work too.
/// </summary>
public sealed class GateView : UserControl
{
    readonly GateChallenge _gate;
    readonly Func<double> _now;
    readonly Border[] _boxes = new Border[GateChallenge.Length];
    readonly TextBlock _words, _message;
    readonly List<Tap> _keys = new();
    readonly Border _card;
    readonly Grid _layout;
    readonly StackPanel _ask;
    readonly Grid _pad;
    readonly RoundTap _close;
    DispatcherTimer? _timer;
    bool _wide;

    public GateView(Func<double>? now = null, Random? random = null)
    {
        _now = now ?? (() => Environment.TickCount64);
        _gate = new GateChallenge(random);
        FontFamily = Palette.Font;
        Focusable = true;

        _close = new RoundTap(Strings.T("common.close"), Icons.Close, 52);
        _close.Click += () => Cancelled?.Invoke();
        _close.HorizontalAlignment = HorizontalAlignment.Left;
        _close.VerticalAlignment = VerticalAlignment.Top;

        var lockCircle = new Border
        {
            Width = 72, Height = 72, CornerRadius = new CornerRadius(36), Background = Palette.MintBrush, HorizontalAlignment = HorizontalAlignment.Center,
            Child = Kit.Icon(Icons.Lock, 34, Palette.LeafBrush),
        };
        var title = Kit.Heading(Strings.T("gate.title"), 30);
        title.TextAlignment = TextAlignment.Center;
        title.HorizontalAlignment = HorizontalAlignment.Center;
        var ask = Kit.Text(Strings.T("gate.ask"), 17, FontWeight.SemiBold, Palette.InkSoftBrush);
        ask.TextAlignment = TextAlignment.Center;
        _words = Kit.Text("", 30, FontWeight.ExtraBold, Palette.LeafBrush);
        _words.TextAlignment = TextAlignment.Center;
        _words.Margin = new Thickness(0, 6, 0, 4);
        var boxes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        for (int i = 0; i < _boxes.Length; i++)
        {
            _boxes[i] = new Border { Width = 60, Height = 68, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(2.5) };
            boxes.Children.Add(_boxes[i]);
        }
        _message = Kit.Text("", 15, FontWeight.Bold, Palette.InkSoftBrush);
        _message.TextAlignment = TextAlignment.Center;
        _message.MinHeight = 22;
        _ask = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { lockCircle, title, ask, _words, boxes, _message } };

        _pad = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), ColumnSpacing = 12, RowSpacing = 12, VerticalAlignment = VerticalAlignment.Center };
        for (int n = 0; n <= 9; n++)
        {
            int digit = n;
            var key = new Pill(n.ToString(System.Globalization.CultureInfo.InvariantCulture), PillKind.Plain, height: 62, fontSize: 28);
            key.Click += () => Press(digit);
            int index = n == 0 ? 10 : n - 1;
            Grid.SetRow(key, index / 3);
            Grid.SetColumn(key, index % 3);
            _pad.Children.Add(key);
            _keys.Add(key);
        }
        var back = new Pill(Strings.T("gate.delete"), PillKind.Link, Icons.Back, height: 62, fontSize: 20, iconSize: 30);
        back.Label.IsVisible = false;
        back.Click += () => { _gate.Delete(); Show(); };
        Avalonia.Automation.AutomationProperties.SetName(back, Strings.T("gate.delete"));
        Grid.SetRow(back, 3);
        Grid.SetColumn(back, 2);
        _pad.Children.Add(back);
        _keys.Add(back);

        _layout = new Grid();
        _card = new Border { Child = new Panel { Children = { _layout, _close } } };
        Content = new Panel { Children = { new Border { Background = Palette.Brush(Palette.Forest, 0.6) }, _card } };
        // A touch anywhere here is never a touch on what is underneath.
        PointerPressed += (_, e) => e.Handled = true;
        Show();
        Arrange(new Size(1280, 800));
    }

    /// <summary>The right numbers were tapped.</summary>
    public event Action? Opened;
    /// <summary>Closed without opening.</summary>
    public event Action? Cancelled;

    /// <summary>Room kept clear for a notch, rounded corners or the system bars.</summary>
    public Thickness SafeArea { get; set; }

    /// <summary>The numbers asked for, for tests and screenshots.</summary>
    public int[] Digits => _gate.Digits;

    /// <summary>The number keys on a keyboard, Backspace and Esc.</summary>
    public bool HandleKey(Key key)
    {
        int digit = key switch
        {
            >= Key.D0 and <= Key.D9 => key - Key.D0,
            >= Key.NumPad0 and <= Key.NumPad9 => key - Key.NumPad0,
            _ => -1,
        };
        if (digit >= 0) { Press(digit); return true; }
        if (key == Key.Back) { _gate.Delete(); Show(); return true; }
        if (key == Key.Escape) { Cancelled?.Invoke(); return true; }
        return false;
    }

    /// <summary>A number was tapped (also used by screenshots).</summary>
    public void Press(int digit)
    {
        GateChallenge.Outcome outcome = _gate.Tap(digit, _now());
        if (outcome == GateChallenge.Outcome.Opened) { Opened?.Invoke(); return; }
        if (outcome == GateChallenge.Outcome.Resting) StartRest();
        Show();
    }

    void StartRest()
    {
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            if (!_gate.Resting(_now())) { _timer?.Stop(); _timer = null; }
            Show();
        };
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
    }

    void Show()
    {
        double now = _now();
        bool resting = _gate.Resting(now);
        _words.Text = _gate.Words(Strings.Language);
        _words.Opacity = resting ? 0.35 : 1;
        for (int i = 0; i < _boxes.Length; i++)
        {
            bool filled = i < _gate.Typed.Count;
            _boxes[i].Background = Palette.PaperBrush;
            _boxes[i].BorderBrush = filled ? Palette.LeafBrush : i == _gate.Typed.Count && !resting ? Palette.Brush(Palette.Leaf, 0.45) : Palette.LeafPaleBrush;
            _boxes[i].Child = filled ? new TextBlock
            {
                Text = _gate.Typed[i].ToString(System.Globalization.CultureInfo.InvariantCulture), FontSize = 30, FontWeight = FontWeight.ExtraBold,
                Foreground = Palette.InkBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            } : null;
        }
        _message.Text = resting ? Strings.T("gate.rest", _gate.RestSeconds(now)) : _gate.JustMissed ? Strings.T("gate.miss") : "";
        _message.Foreground = resting || _gate.JustMissed ? Palette.ClayBrush : Palette.InkSoftBrush;
        foreach (Tap k in _keys) k.Enabled = !resting;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Arrange(e.NewSize);
    }

    void Arrange(Size size)
    {
        double w = size.Width, h = size.Height;
        if (w <= 0 || h <= 0) return;
        bool side = w > h * 1.15 && h < 720;
        _wide = Math.Min(w, h) >= 600 && !side;
        _layout.Children.Clear();
        _layout.RowDefinitions.Clear();
        _layout.ColumnDefinitions.Clear();
        if (side)
        {
            // Low and wide (a phone on its side): the numbers to tap on the left, the pad on the right.
            _layout.ColumnDefinitions = new ColumnDefinitions("*,*");
            _layout.ColumnSpacing = 36;
            _layout.VerticalAlignment = VerticalAlignment.Stretch;
            _ask.Spacing = 4;
            _layout.Children.Add(_ask);
            Grid.SetColumn(_pad, 1);
            _pad.Width = Math.Min(330, w * 0.4);
            _pad.HorizontalAlignment = HorizontalAlignment.Center;
            foreach (Tap k in _keys) k.MinHeight = Math.Min(62, (h - 60 - SafeArea.Top - SafeArea.Bottom) / 4 - 10);
            _layout.Children.Add(_pad);
        }
        else
        {
            _layout.RowDefinitions = new RowDefinitions("Auto,Auto");
            _layout.RowSpacing = 30;
            _layout.VerticalAlignment = VerticalAlignment.Center;
            _ask.Spacing = 8;
            Grid.SetColumn(_pad, 0);
            Grid.SetRow(_pad, 1);
            _pad.Width = double.NaN;
            _pad.MaxWidth = 360;
            _pad.HorizontalAlignment = HorizontalAlignment.Stretch;
            foreach (Tap k in _keys) k.MinHeight = 62;
            _layout.Children.Add(_ask);
            _layout.Children.Add(_pad);
        }
        if (_wide)
        {
            _card.Width = 460;
            _card.Height = double.NaN;
            _card.HorizontalAlignment = HorizontalAlignment.Center;
            _card.VerticalAlignment = VerticalAlignment.Center;
            _card.CornerRadius = new CornerRadius(28);
            _card.Padding = new Thickness(32, 28, 32, 34);
            _card.Background = Palette.CreamBrush;
            _card.BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 10, Blur = 34, Color = Palette.ShadowOf(0.4) });
            _layout.Margin = new Thickness(0, 30, 0, 0);
        }
        else
        {
            _card.Width = double.NaN;
            _card.HorizontalAlignment = HorizontalAlignment.Stretch;
            _card.VerticalAlignment = VerticalAlignment.Stretch;
            _card.CornerRadius = default;
            _card.Background = Palette.CreamBrush;
            _card.BoxShadow = default;
            _card.Padding = new Thickness(16 + SafeArea.Left, 12 + SafeArea.Top, 16 + SafeArea.Right, 24 + SafeArea.Bottom);
            _layout.Margin = side ? new Thickness(60, 0, 20, 0) : new Thickness(12, 40, 12, 8);
            _layout.MaxWidth = 720;
        }
    }
}
