using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Editor.Core;
using Reader.Core;

namespace BookEditor;

/// <summary>
/// The camera's journey through the page, left to right along the bottom: how the page opens, a card
/// for each stop showing what it frames, and what joins one stop to the next (a tap, or going on by
/// itself). Drag a card onto another to change the order. Explore pages list the things to find too.
/// </summary>
sealed class Timeline : UserControl
{
    readonly EditorState _state;
    readonly Images _images;
    readonly MainWindow _window;
    readonly StackPanel _track = new() { Orientation = Orientation.Horizontal };
    readonly StackPanel _head = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
    readonly ScrollViewer _scroll;
    int _dragFrom = -1;
    bool _waiting;

    public Timeline(EditorState state, Images images, MainWindow window)
    {
        _state = state;
        _images = images;
        _window = window;
        _scroll = new ScrollViewer { Content = _track, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var dock = new DockPanel { Margin = new Thickness(12, 8, 12, 8) };
        DockPanel.SetDock(_head, Dock.Top);
        dock.Children.Add(_head);
        dock.Children.Add(_scroll);
        Content = new Border { Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 1, 0, 0), Child = dock };
        Height = 176;
        _state.Changed += (why, source) => { if (source is not PageCanvas) Rebuild(); };   // not on every mouse move of a drag
        _images.Arrived += () => { if (_waiting) Rebuild(); };
        // Let go over the same card: select it. Over another card: move the stop there.
        _track.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_dragFrom < 0) return;
            int from = _dragFrom;
            _dragFrom = -1;
            int to = CardAt(e.GetPosition(_track));
            if (to >= 0 && to != from) _window.MoveStop(from, to);
            else _state.Select(Sel.Stop, from);
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public void Rebuild()
    {
        _head.Children.Clear();
        _track.Children.Clear();
        _waiting = false;
        if (_state.Page is not Page p) return;
        int n = p.Stops.Count;
        _head.Children.Add(Ui.Label($"Page {_state.PageIndex + 1}", 13.5, Ui.Text, FontWeight.Bold, wrap: false));
        string sub = n > 0 ? $"{n} stop{(n == 1 ? "" : "s")}" : p.Explore ? "No intro stops" : "No stops yet: drag on the picture to draw one";
        if (p.Explore) sub += $" · {p.Hotspots.Count} to find";
        int voiced = p.Stops.Count(s => s.Voice is not null);
        if (voiced > 0) sub += $" · {voiced} with a recording";
        _head.Children.Add(Ui.Label(sub, 12.5, Ui.Muted, wrap: false));
        foreach (PageEffect e in p.Effects)
            _head.Children.Add(Chip(Names.Effect(e.Type) + (e.Stop is > 0 && !e.IsFalling ? " at stop " + e.Stop : e.From > 1 ? " from stop " + e.From : ""), "#1d3040", "#9fd8f3"));
        if (p.Layers.Count > 0) _head.Children.Add(Chip($"{p.Layers.Count} picture{(p.Layers.Count == 1 ? "" : "s")} on top", "#2c2444", "#cdb8ff"));
        _head.Children.Add(Ui.Button("+ Whole page stop", () => _window.AddWholePageStop(), "A stop that shows the whole page, for example to end on the full picture", small: true));
        _head.Children.Add(Ui.Button("▶ Play this page", () => _window.OpenPreview(fromStop: false), "Play this page in the reader (P)", small: true));

        if (p.OpenWhole && (n > 0 || !p.Explore))
        {
            _track.Children.Add(Ghost("Opens on the whole page"));
            if (n > 0) _track.Children.Add(Link(false, "tap"));
        }
        for (int i = 0; i < n; i++)
        {
            _track.Children.Add(Card(p, i));
            if (i < n - 1) _track.Children.Add(Link(p.Stops[i].Auto, p.Stops[i].Auto ? $"after {p.Stops[i].HoldSeconds ?? 1.5:0.#} s" : "tap"));
        }
        if (p.Explore)
        {
            if (n > 0) _track.Children.Add(Link(false, "tap"));
            _track.Children.Add(Ghost("The child explores"));
            for (int i = 0; i < p.Hotspots.Count; i++) _track.Children.Add(HotChip(p, i));
            if (p.Hotspots.Count == 0) _track.Children.Add(new Border { VerticalAlignment = VerticalAlignment.Center, Child = Ui.Help("Nothing to find yet. Choose \"Things to find\" above the picture and drag on it.") });
        }
    }

    static Border Chip(string text, string bg, string fg) => new()
    {
        Background = Ui.Brush(bg), CornerRadius = new CornerRadius(999), Padding = new Thickness(10, 3), VerticalAlignment = VerticalAlignment.Center,
        Child = Ui.Label(text, 12, Ui.Brush(fg), FontWeight.SemiBold, wrap: false),
    };

    static Border Ghost(string text) => new()
    {
        Width = 92, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 4),
        Child = new TextBlock { Text = text, FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
    };

    static Control Link(bool auto, string text) => new StackPanel
    {
        Width = 50, VerticalAlignment = VerticalAlignment.Center, Spacing = 3,
        Children =
        {
            new Border { Height = 2, Width = 26, Background = auto ? Ui.Accent : Ui.Line, HorizontalAlignment = HorizontalAlignment.Center },
            new TextBlock { Text = text, FontSize = 10.5, FontWeight = FontWeight.SemiBold, Foreground = auto ? Ui.Accent : Ui.Muted, HorizontalAlignment = HorizontalAlignment.Center },
        },
    };

    Control Card(Page p, int i)
    {
        Stop s = p.Stops[i];
        Color c = Ui.StopColour(i);
        bool active = _state.Selected == Sel.Stop && _state.SelIndex == i;
        var thumb = new Image { Width = 136, Height = 62, Stretch = Stretch.UniformToFill, Source = Crop(p, s.Box) };
        var picture = new Panel
        {
            Children =
            {
                new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Background = Ui.Stage, Child = thumb },
                new Border { Margin = new Thickness(4), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = Ui.Dot((i + 1).ToString(), c) },
                Badges(s),
            },
        };
        var cap = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(s.Caption) ? "No words" : s.Caption, FontSize = 12, LineHeight = 15.5, MaxLines = 2, Height = 31,
            TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = string.IsNullOrWhiteSpace(s.Caption) ? Ui.Muted : Ui.Text, FontStyle = string.IsNullOrWhiteSpace(s.Caption) ? FontStyle.Italic : FontStyle.Normal,
        };
        var card = new Border
        {
            Width = 150, Background = active ? Ui.Brush("#2f3441") : Ui.Raised, CornerRadius = new CornerRadius(10), Padding = new Thickness(5),
            BorderThickness = new Thickness(2), BorderBrush = active ? Ui.Brush(c) : Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel { Spacing = 5, Children = { picture, cap } }, Margin = new Thickness(0, 0, 0, 4),
        };
        ToolTip.SetTip(card, "Click to select. Drag onto another card to change the order.");
        int index = i;
        card.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed) return;
            _dragFrom = index;      // chosen when the mouse comes up: on this card it is selected, on another it moves there
        };
        return card;
    }

    /// <summary>Which stop's card is under a point on the strip.</summary>
    int CardAt(Point at)
    {
        int stop = -1;
        foreach (Control c in _track.Children)
        {
            if (c is Border { Width: 150 }) stop++;
            else continue;
            if (at.X >= c.Bounds.X && at.X <= c.Bounds.Right) return stop;
        }
        return -1;
    }

    static Control Badges(Stop s)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4) };
        void Add(string text, string tip)
        {
            var b = new Border { Background = Ui.Brush("#c80e0f13"), CornerRadius = new CornerRadius(5), Padding = new Thickness(5, 1), Child = Ui.Label(text, 10.5, Ui.Text, FontWeight.SemiBold, wrap: false) };
            ToolTip.SetTip(b, tip);
            row.Children.Add(b);
        }
        if (s.MoveSeconds is double m) Add(m == 0 ? "cut" : $"{m:0.#}s", "Time the camera takes to get here");
        if (s.To is not null) Add($"glide {s.PanSeconds ?? 4:0.#}s", "Glides to a second box");
        if (s.Style == FocusStyles.Spotlight) Add("spot", "Spotlight");
        if (s.Sound is not null) Add("♪", "Plays " + s.Sound);
        if (s.Voice is not null) Add(s.Voice.Words.Count > 0 ? "voice ✓" : "voice", s.Voice.Words.Count > 0 ? "Has a recording with timed words" : "Has a recording; its words aren't timed yet");
        return row;
    }

    Control HotChip(Page p, int i)
    {
        Hotspot h = p.Hotspots[i];
        bool active = _state.Selected == Sel.Hotspot && _state.SelIndex == i;
        var chip = new Border
        {
            Background = Ui.Raised, CornerRadius = new CornerRadius(999), Padding = new Thickness(6, 5, 11, 5), Margin = new Thickness(0, 0, 6, 0),
            BorderThickness = new Thickness(2), BorderBrush = active ? Ui.Gold : Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 200,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6,
                Children = { Ui.Dot(h.Story > 0 ? h.Story.ToString() : "★", Color.Parse("#a37a00")), Ui.Label(h.Name ?? h.Caption ?? $"Thing {i + 1}", 12.5, Ui.Text, wrap: false) },
            },
        };
        ToolTip.SetTip(chip, (h.Story > 0 ? $"Story step {h.Story}. " : "Extra to find. ") + (h.Caption ?? ""));
        chip.PointerPressed += (_, _) => _state.Select(Sel.Hotspot, i);
        return chip;
    }

    /// <summary>What a stop frames, widened to the card's shape, cut from a small copy of the page.</summary>
    IImage? Crop(Page p, RectD box)
    {
        Bitmap? bmp = _images.Get(_state.Project!.PathOf(p.Image), 900);
        if (bmp is null) { _waiting = true; return null; }
        double iw = bmp.PixelSize.Width, ih = bmp.PixelSize.Height;
        double sw = Math.Max(1, box.W * iw), sh = Math.Max(1, box.H * ih);
        const double shape = 136.0 / 62;
        if (sw / sh < shape) sw = sh * shape; else sh = sw / shape;
        double k = Math.Min(1, Math.Min(iw / sw, ih / sh));
        sw *= k; sh *= k;
        double sx = Math.Clamp((box.X + box.W / 2) * iw - sw / 2, 0, iw - sw), sy = Math.Clamp((box.Y + box.H / 2) * ih - sh / 2, 0, ih - sh);
        var rect = new PixelRect((int)sx, (int)sy, Math.Max(1, (int)sw), Math.Max(1, (int)sh));
        return new CroppedBitmap(bmp, rect);
    }
}
