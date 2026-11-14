using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Editor.Core;
using Reader.Core;

namespace BookEditor;

/// <summary>
/// The right-hand panel: settings for whatever is selected first, then the page, its moving effects
/// and pictures on top, then the book. Rebuilt when the selection or the shape of the book changes,
/// but not while something in it is being typed.
/// </summary>
sealed class Inspector : UserControl
{
    readonly EditorState _state;
    readonly MainWindow _window;
    readonly StackPanel _body = new() { Margin = new Thickness(12) };
    readonly ScrollViewer _scroll;
    NarrationPanel? _narration;

    public Inspector(EditorState state, MainWindow window)
    {
        _state = state;
        _window = window;
        _scroll = new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Content = new Border { Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(1, 0, 0, 0), Child = _scroll };
        _state.Changed += (why, source) =>
        {
            if (why is Why.Book or Why.Structure or Why.Selection) Rebuild();
        };
    }

    public NarrationPanel? Narration => _narration;

    /// <summary>Which sections the author has folded away, kept between rebuilds.</summary>
    readonly Dictionary<string, bool> _open = new() { ["Book"] = false, ["Moving effects"] = false, ["Pictures on top"] = false };

    Expander Section(string title, Control content, bool openByDefault = true, string? tag = null)
    {
        var e = Ui.Section(title, content, _open.TryGetValue(title, out bool o) ? o : openByDefault, tag);
        e.PropertyChanged += (_, a) => { if (a.Property == Expander.IsExpandedProperty) _open[title] = e.IsExpanded; };
        return e;
    }

    public void Rebuild()
    {
        Vector scroll = _scroll.Offset;
        _narration?.Detach();
        _narration = null;
        _body.Children.Clear();
        if (!_state.HasBook) return;
        Page? p = _state.Page;
        if (p is not null)
        {
            switch (_state.Selected)
            {
                case Sel.Stop when _state.Stop is Stop s: _body.Children.Add(StopCard(p, s, _state.SelIndex)); break;
                case Sel.Hotspot when _state.Hotspot is Hotspot h: _body.Children.Add(HotspotCard(p, h, _state.SelIndex)); break;
                case Sel.Layer when _state.Layer is Layer l: _body.Children.Add(LayerCard(p, l, _state.SelIndex)); break;
                case Sel.Dry: _body.Children.Add(DryCard(p)); break;
                default: _body.Children.Add(NothingSelected(p)); break;
            }
            _body.Children.Add(Section($"Page {_state.PageIndex + 1}", PageSettings(p)));
            _body.Children.Add(Section("Moving effects", Effects(p), tag: p.Effects.Count == 0 ? "None" : string.Join(", ", p.Effects.Select(e => Names.Effect(e.Type)))));
            _body.Children.Add(Section("Pictures on top", Layers(p), tag: p.Layers.Count == 0 ? "None" : p.Layers.Count.ToString()));
        }
        _body.Children.Add(Section("Book", BookSettings(), openByDefault: p is null));
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _scroll.Offset = scroll, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    // ---------------------------------------------------------------- small builders

    TextBox Text(string? value, string placeholder, string label, string key, Action<string?> set, bool multiline = false, Action<string>? after = null)
    {
        var box = Ui.TextField(value, placeholder, multiline);
        string last = value ?? "";
        box.TextChanged += (_, _) =>
        {
            string t = box.Text ?? "";
            if (t == last) return;          // the box filling itself in is not a change
            last = t;
            _state.Change(label, () => set(t.Length == 0 ? null : t), mergeKey: key, source: this);
            after?.Invoke(t);
        };
        return box;
    }

    static Control Stack(params Control[] items)
    {
        var s = new StackPanel();
        foreach (Control c in items) s.Children.Add(c);
        return s;
    }

    ComboBox StopChoice(Page p, string? first, int value, Action<int> set, string label)
    {
        var options = new List<(int, string)>();
        if (first is not null) options.Add((0, first));
        for (int i = 0; i < p.Stops.Count; i++) options.Add((i + 1, $"Stop {i + 1}" + (string.IsNullOrWhiteSpace(p.Stops[i].Caption) ? "" : ": " + Short(p.Stops[i].Caption!))));
        if (first is null && value < 1) value = 1;
        return Ui.Choice<int>(options, value, v => _state.Change(label, () => set(v), structure: true));
    }

    static string Short(string s) => s.Length > 28 ? s[..26].TrimEnd() + "..." : s;

    // ---------------------------------------------------------------- nothing selected

    Control NothingSelected(Page p)
    {
        string what = p.Explore
            ? "Choose what dragging on the picture makes (intro stops or things to find) with the buttons above the picture."
            : "Drag on the picture to draw where the camera stops. The stops play in order, left to right, along the bottom.";
        return Ui.CardBox(Stack(
            Ui.Label("Nothing selected", 13.5, Ui.Text, FontWeight.Bold),
            Ui.Help(what),
            Ui.Help("Click a box to change it. Drag it to move it, drag a white corner to resize it. Hold Shift to draw a new box on top of another."),
            Ui.Help("Delete removes it, the arrow keys nudge it, Ctrl+D makes a copy. P plays the page. Ctrl+Z undoes. F1 lists every shortcut.")));
    }

    // ---------------------------------------------------------------- a stop

    Control StopCard(Page p, Stop s, int index)
    {
        Color c = Ui.StopColour(index);
        var panel = new StackPanel();
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Ui.Dot((index + 1).ToString(), c), Ui.Label($"Stop {index + 1}", 14, Ui.Text, FontWeight.Bold) } };
        panel.Children.Add(title);
        panel.Children.Add(Ui.Row(
            Ui.Button("Earlier", () => _window.MoveStop(index, index - 1), "Move this stop one place earlier", small: true),
            Ui.Button("Later", () => _window.MoveStop(index, index + 1), "Move this stop one place later", small: true),
            Ui.Button("Duplicate", _window.DuplicateSelected, "Make a copy of this stop (Ctrl+D)", small: true),
            Ui.Button("Copy", _window.CopySelected, "Copy it, to paste on another page (Ctrl+C, then Ctrl+V)", small: true),
            Ui.Button("Delete", _window.DeleteSelected, "Remove this stop (Delete)", small: true),
            Ui.Button("▶ Play", () => _window.OpenPreview(fromStop: true), "Play the move into this stop (P)", small: true)));

        // Words
        panel.Children.Add(Ui.Heading("Words"));
        panel.Children.Add(Ui.FieldName("What is read here (leave empty if the words are in the picture)"));
        var count = Ui.Help("");
        void Count(string text)
        {
            int words = ReadAlong.WordCount(text);
            string lines = "";
            Device side = Device.Find("s22-sideways");
            int l = Framing.CaptionLines(text, side);
            if (l > side.MostCaptionLines) lines = $" That's {l} lines on a phone held sideways: think about splitting it over two stops.";
            string timing = s.Voice is { Words.Count: > 0 } v && v.Words.Count != words ? " The words have changed since they were timed: tap along again." : "";
            count.Text = words == 0 ? "" : $"{words} word{(words == 1 ? "" : "s")}, {text.Length} letters.{lines}{timing}";
            count.Foreground = lines.Length > 0 || timing.Length > 0 ? Ui.Warn : Ui.Muted;
        }
        panel.Children.Add(Text(s.Caption, "", "Words", $"caption{_state.PageIndex}:{index}", v => s.Caption = v, multiline: true, after: Count));
        Count(s.Caption ?? "");
        panel.Children.Add(count);
        panel.Children.Add(Ui.FieldName("Question for the grown-up (optional)"));
        panel.Children.Add(Text(s.Ask, "For example: How do you think she feels?", "Question", $"ask{_state.PageIndex}:{index}", v => s.Ask = v));

        // The recorded voice
        panel.Children.Add(Ui.Heading("Read aloud"));
        _narration = new NarrationPanel(_state, _window, s, _state.PageIndex, index);
        panel.Children.Add(_narration);

        // Look
        panel.Children.Add(Ui.Heading("Look"));
        panel.Children.Add(Ui.FieldName("What happens around it"));
        var styles = new List<(string?, string)> { (null, $"Book's choice ({Names.Style(_state.Book.FocusStyle)})") };
        foreach (string st in new[] { FocusStyles.Dim, FocusStyles.Vignette, FocusStyles.Spotlight, FocusStyles.None }) styles.Add((st, Names.Style(st)));
        panel.Children.Add(Ui.Choice<string?>(styles, s.Style, v => _state.Change("Look", () => s.Style = v, source: this)));
        panel.Children.Add(Ui.Check("Pan to a second box while resting here", s.To is not null, on => _state.Change(on ? "Add a pan" : "Remove the pan", () =>
        {
            if (on)
            {
                RectD b = s.Box;
                double nx = b.X + b.W + 0.02 <= 1 - b.W ? b.X + b.W * 0.6 : Math.Max(0, b.X - b.W * 0.6);
                s.To = new RectD(Math.Clamp(nx, 0, Math.Max(0, 1 - b.W)), b.Y, b.W, b.H);
                s.PanSeconds ??= 4;
            }
            else { s.To = null; s.PanSeconds = null; }
        }, structure: true)));
        if (s.To is not null)
        {
            panel.Children.Add(Ui.Help("The camera arrives at the solid box, then glides to the dashed box. Drag the dashed box to where it should end."));
            panel.Children.Add(Ui.Slider("Glide time", 1, 15, 0.5, s.PanSeconds ?? 4, " s", v => _state.Change("Glide time", () => s.PanSeconds = v, mergeKey: "pan", source: this)));
        }
        panel.Children.Add(Ui.Row(Ui.Button("Make it the whole page", () => _state.Change("Whole page", () => s.Box = new RectD(0, 0, 1, 1), structure: true), small: true)));

        // Timing
        panel.Children.Add(Ui.Heading("Timing"));
        panel.Children.Add(Ui.FieldName("Time the camera takes to get here"));
        var move = Ui.NumberField(s.MoveSeconds, "automatic", v => _state.Change("Move time", () => s.MoveSeconds = v, mergeKey: "move", source: this), 0, 20);
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { move, new TextBlock { Text = "seconds", Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center } } });
        panel.Children.Add(Ui.Help("Empty: the reader decides. 0: cut straight to it."));
        panel.Children.Add(Ui.Check("Go on to the next stop by itself (no tap)", s.Auto, on => _state.Change("Go on by itself", () =>
        {
            s.Auto = on;
            s.HoldSeconds = on ? s.HoldSeconds ?? 1.5 : null;
        }, structure: true)));
        if (s.Auto)
        {
            panel.Children.Add(Ui.Slider("It rests here first for", 0.5, 10, 0.5, s.HoldSeconds ?? 1.5, " s", v => _state.Change("Rest time", () => s.HoldSeconds = v, mergeKey: "hold", source: this)));
            panel.Children.Add(Ui.Help("Use this to chain camera moves into one tap. Keep the words short: a child can't stop it to read. With a recording, it waits for the voice to finish first."));
        }

        // Sound effect
        panel.Children.Add(Ui.Heading("Sound effect"));
        panel.Children.Add(SoundRow(s.Sound, v => s.Sound = v, "Plays when the camera arrives, alongside any recording. For a splash or a door, not for the reading voice."));
        return Ui.CardBox(panel, Ui.Brush(c));
    }

    Control SoundRow(string? current, Action<string?> set, string help)
    {
        var row = new StackPanel();
        if (current is not null)
        {
            row.Children.Add(Ui.Label(current, 12.5, Ui.Text, wrap: false));
            row.Children.Add(Ui.Row(
                Ui.Button("▶ Listen", () => _window.PlaySound(current), small: true),
                Ui.Button("Choose another", () => _window.PickSound(set), small: true),
                Ui.Button("Remove", () => _state.Change("Remove sound", () => set(null), structure: true), small: true)));
        }
        else row.Children.Add(Ui.Row(Ui.Button("Choose a sound file...", () => _window.PickSound(set), small: true)));
        row.Children.Add(Ui.Help(help));
        return row;
    }

    // ---------------------------------------------------------------- a thing to find

    Control HotspotCard(Page p, Hotspot h, int index)
    {
        var panel = new StackPanel();
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Ui.Dot("★", Color.Parse("#a37a00")), Ui.Label($"Thing to find {index + 1}", 14, Ui.Text, FontWeight.Bold) } });
        panel.Children.Add(Ui.Row(
            Ui.Button("Duplicate", _window.DuplicateSelected, "Ctrl+D", small: true),
            Ui.Button("Copy", _window.CopySelected, "Ctrl+C, then Ctrl+V on another explore page", small: true),
            Ui.Button("Delete", _window.DeleteSelected, "Delete", small: true),
            Ui.Button("▶ Play", () => _window.OpenPreview(fromStop: false), "Explore this page in the reader", small: true)));
        panel.Children.Add(Ui.Heading("Words"));
        panel.Children.Add(Ui.FieldName("Name (for example Old Turtle)"));
        panel.Children.Add(Text(h.Name, "", "Name", $"hname{index}", v => h.Name = v));
        panel.Children.Add(Ui.FieldName("What it says when found"));
        panel.Children.Add(Text(h.Caption, "", "Words", $"hcap{index}", v => h.Caption = v, multiline: true));
        panel.Children.Add(Ui.FieldName("Question for the grown-up (optional)"));
        panel.Children.Add(Text(h.Ask, "For example: What else lives in the sea?", "Question", $"hask{index}", v => h.Ask = v));
        panel.Children.Add(Ui.Heading("How it behaves"));
        panel.Children.Add(Ui.FieldName("Story step (leave empty for an extra thing to find)"));
        panel.Children.Add(Ui.NumberField(h.Story > 0 ? h.Story : null, "extra", v => _state.Change("Story step", () => h.Story = v is double n && n >= 1 ? (int)Math.Round(n) : 0, mergeKey: "story", source: this), 0, 99));
        panel.Children.Add(Ui.Help("Story steps open in order: 1, then 2, and so on. The next one always glows softly, and tapping a later one too early says \"First find\" and the name of the next one. Hints and Show me lead to the next story step first, then to the extras in the order they are listed."));
        panel.Children.Add(Ui.FieldName("When found"));
        panel.Children.Add(Ui.Choice<string?>(new List<(string?, string)> { (null, "Sparkle"), ("none", "No sparkle, just the words") }, h.Effect == "none" ? "none" : null,
            v => _state.Change("When found", () => h.Effect = v, source: this)));
        panel.Children.Add(Ui.Heading("Sound"));
        panel.Children.Add(SoundRow(h.Sound, v => h.Sound = v, "Plays when it is found: a voice saying the words, or a noise it makes."));
        return Ui.CardBox(panel, Ui.Gold);
    }

    // ---------------------------------------------------------------- a picture on top

    Control LayerCard(Page p, Layer l, int index)
    {
        var panel = new StackPanel();
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Ui.Dot((index + 1).ToString(), Color.Parse("#7a4fd6")), Ui.Label($"Picture on top {index + 1}", 14, Ui.Text, FontWeight.Bold) } });
        panel.Children.Add(Ui.Label(l.Image, 12, Ui.Muted, wrap: false));
        panel.Children.Add(Ui.Row(Ui.Button("Delete", _window.DeleteSelected, small: true), Ui.Button("Replace picture...", () => _window.ReplaceLayer(index), small: true)));
        panel.Children.Add(Ui.Help("Drag it on the page to place it, drag a corner to size it. It may sit partly off the page, to come in from the side."));
        if (p.Stops.Count > 0)
        {
            panel.Children.Add(Ui.FieldName("It appears"));
            panel.Children.Add(StopChoice(p, "As the page opens", l.From is > 1 ? l.From.Value : 0, v => l.From = v > 1 ? v : null, "When it appears"));
            panel.Children.Add(Ui.FieldName("It goes away"));
            panel.Children.Add(StopChoice(p, "Never, it stays", l.To is >= 1 ? l.To.Value : 0, v => l.To = v >= 1 ? v : null, "When it goes"));
        }
        panel.Children.Add(Ui.Check("It moves somewhere", l.MoveTo is not null, on => _state.Change(on ? "Make it move" : "Stop it moving", () =>
        {
            if (on)
            {
                l.MoveTo = l.Box with { X = Math.Clamp(l.Box.X + l.Box.W * 1.2, -l.Box.W, 1) };
                l.MoveAt ??= Math.Max(1, _state.Selected == Sel.Stop ? _state.SelIndex + 1 : 1);
                l.MoveSeconds ??= 1.2;
            }
            else { l.MoveTo = null; l.MoveAt = null; l.MoveSeconds = null; l.Path = null; }
        }, structure: true)));
        if (l.MoveTo is not null)
        {
            panel.Children.Add(Ui.Help("Drag the faded copy to where it should end up."));
            if (p.Stops.Count > 0)
            {
                panel.Children.Add(Ui.FieldName("It sets off when the camera reaches"));
                panel.Children.Add(StopChoice(p, null, l.MoveAt ?? 1, v => l.MoveAt = v, "When it moves"));
            }
            panel.Children.Add(Ui.Slider("The move takes", 0.3, 8, 0.1, l.MoveSeconds ?? 1.2, " s", v => _state.Change("Move time", () => l.MoveSeconds = v, mergeKey: "lmove", source: this)));
            panel.Children.Add(Ui.FieldName("The way it travels"));
            panel.Children.Add(Ui.Choice<string?>(new List<(string?, string)> { (null, "Straight there"), ("hop", "In a hop, up and over") }, l.Path == "hop" ? "hop" : null, v => _state.Change("Path", () => l.Path = v, source: this)));
        }
        panel.Children.Add(Ui.FieldName("While it waits"));
        panel.Children.Add(Ui.Choice<string?>(new List<(string?, string)> { (null, "It keeps still"), ("bob", "It bobs up and down"), ("sway", "It sways") }, l.Idle, v => _state.Change("While it waits", () => l.Idle = v, source: this)));
        return Ui.CardBox(panel, Ui.Brush("#b08cff"));
    }

    Control DryCard(Page p)
    {
        var panel = new StackPanel();
        panel.Children.Add(Ui.Label("Area the weather misses", 14, Ui.Text, FontWeight.Bold));
        panel.Children.Add(Ui.Help("Rain or snow doesn't fall here, for example under a roof or a tree. Drag it to move it, drag a corner to resize it."));
        panel.Children.Add(Ui.Row(Ui.Button("Delete", _window.DeleteSelected, small: true)));
        return Ui.CardBox(panel, Ui.Brush("#7fd3f2"));
    }

    // ---------------------------------------------------------------- the page

    Control PageSettings(Page p)
    {
        var panel = new StackPanel();
        panel.Children.Add(Ui.FieldName("Page type"));
        panel.Children.Add(Ui.Choice<bool>(new List<(bool, string)> { (false, "Story: the camera moves from stop to stop"), (true, "Explore: the child drags around and finds things") }, p.Explore,
            v => _state.Change("Page type", () => { p.Explore = v; if (v) p.Guidance ??= new Guidance(); }, structure: true)));
        if (p.Explore)
        {
            panel.Children.Add(Ui.Help("Any stops play first as a guided intro. Then the page is handed to the child to drag, zoom and find things."));
            panel.Children.Add(Ui.Check("Hint at things not found yet when the child is idle", p.Guidance.Hints, v => _state.Change("Hints", () => p.Guidance.Hints = v, source: this)));
            panel.Children.Add(Ui.Check("Show a \"Show me\" button", p.Guidance.ShowMe, v => _state.Change("Show me", () => p.Guidance.ShowMe = v, source: this)));
            panel.Children.Add(Ui.Check("Show a small map in the corner (for big scenes)", p.Guidance.Map, v => _state.Change("Map", () => p.Guidance.Map = v, source: this)));
        }
        panel.Children.Add(Ui.Check("Show the whole page first", p.OpenWhole, v => _state.Change("Show the whole page first", () => p.OpenWhole = v, structure: true)));
        if (!p.OpenWhole) panel.Children.Add(Ui.Help("This page opens on its first stop, so nothing on it is given away early. Use Spotlight stops to keep the rest dark."));

        // The picture
        panel.Children.Add(Ui.Heading("Picture"));
        var (w, h) = _state.PictureSize(p.Image);
        bool exists = _state.Project!.Has(p.Image);
        panel.Children.Add(Ui.Label(exists ? $"{p.Image}, {w} x {h} pixels" : $"{p.Image} is missing from the book's folder", 12.5, exists ? Ui.Text : Ui.Bad));
        if (p.Tiles is TileInfo t) panel.Children.Add(Ui.Help($"A big scene, already cut into tiles ({t.Width} x {t.Height}). Only the part being looked at loads sharp."));
        else if (exists && Math.Max(w, h) > Pictures.PhoneLimit)
            panel.Children.Add(Ui.Help(p.Explore ? $"Bigger than phones can draw ({Pictures.PhoneLimit} pixels). Export cuts it into tiles so it stays sharp." : $"Bigger than phones can draw ({Pictures.PhoneLimit} pixels). Export makes a smaller copy for the apps."));
        if (exists && Math.Min(w, h) < Pictures.SoftBelow) panel.Children.Add(new TextBlock { Text = $"Small for a page: it will look soft close up. {Pictures.SoftBelow} pixels or more on the short side is better.", Foreground = Ui.Warn, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (p.Spread is SpreadSource sp)
        {
            panel.Children.Add(Ui.Help($"Joined from two pages: {sp.Left} on the left and {sp.Right} on the right."));
            panel.Children.Add(Ui.Row(
                Ui.Button("Replace left page...", () => _window.ReplaceHalf(true), "The stops stay where they are", small: true),
                Ui.Button("Replace right page...", () => _window.ReplaceHalf(false), "The stops stay where they are", small: true)));
        }
        panel.Children.Add(Ui.Row(
            Ui.Button("Replace picture...", _window.ReplacePicture, "Swap in a new picture. The stops stay where they are on the page.", small: true),
            Ui.Button("Join with next page", _window.JoinWithNext, "Make this page the left half and the next page the right half of one spread", small: true),
            Ui.Button("Use as cover", () => _state.Change("Cover", () => _state.Book.Cover = p.Image, structure: true), "Show this picture on the shelf and on the closed book", small: true)));
        panel.Children.Add(Ui.Heading("Arrange"));
        panel.Children.Add(Ui.Row(
            Ui.Button("Move earlier", () => _window.MovePage(_state.PageIndex, _state.PageIndex - 1), "Page Up changes page; Ctrl+Page Up moves it", small: true),
            Ui.Button("Move later", () => _window.MovePage(_state.PageIndex, _state.PageIndex + 1), small: true),
            Ui.Button("Duplicate", _window.DuplicatePage, "Make a copy of this page with all its stops", small: true),
            Ui.Button("Delete page", _window.DeletePage, "Remove this page from the book. Its picture stays in the folder.", small: true)));
        return panel;
    }

    // ---------------------------------------------------------------- moving effects

    Control Effects(Page p)
    {
        var panel = new StackPanel();
        for (int i = 0; i < p.Effects.Count; i++)
        {
            PageEffect e = p.Effects[i];
            int index = i;
            var box = new StackPanel();
            var head = new DockPanel();
            var remove = Ui.Button("Remove", () => _state.Change("Remove effect", () => { p.Effects.RemoveAt(index); _state.DryTarget = -1; }, structure: true), small: true);
            DockPanel.SetDock(remove, Dock.Right);
            head.Children.Add(remove);
            head.Children.Add(Ui.Label(Names.Effect(e.Type), 13.5, Ui.Text, FontWeight.Bold));
            box.Children.Add(head);
            if (!e.IsFalling)
            {
                if (p.Stops.Count == 0) box.Children.Add(Ui.Help("Draw a stop first: this happens when the camera arrives at one."));
                else
                {
                    box.Children.Add(Ui.FieldName("It happens on arriving at"));
                    box.Children.Add(StopChoice(p, null, e.Stop ?? 1, v => e.Stop = v, "When it happens"));
                }
                if (e.Type == PageEffect.Shake) box.Children.Add(Ui.Slider("How hard", 0.2, 1.5, 0.05, e.StrengthOrDefault, "", v => _state.Change("How hard", () => e.Strength = v, mergeKey: "fxs" + index, source: this)));
                if (e.Type == PageEffect.Lightning) box.Children.Add(Ui.Help("A flash a moment after arriving, then a rumble of thunder."));
            }
            else
            {
                box.Children.Add(Ui.Slider("How much", 0.2, 1, 0.05, e.AmountOrDefault, "", v => _state.Change("How much", () => e.Amount = v, mergeKey: "fxa" + index, source: this)));
                if (p.Stops.Count > 1)
                {
                    box.Children.Add(Ui.FieldName("It starts"));
                    box.Children.Add(StopChoice(p, "As the page opens", e.From is > 1 ? e.From.Value : 0, v => e.From = v > 1 ? v : null, "When it starts"));
                    box.Children.Add(Ui.FieldName("It stops"));
                    box.Children.Add(StopChoice(p, "Never, it stays for the whole page", e.To is >= 1 ? e.To.Value : 0, v => e.To = v >= 1 ? v : null, "When it stops"));
                }
                if (e.Type == PageEffect.Rain)
                {
                    box.Children.Add(Ui.Check("Splashes where it lands", e.Ground is > 0, on => _state.Change("Splashes", () => e.Ground = on ? 0.6 : null, structure: true)));
                    if (e.Ground is > 0) box.Children.Add(Ui.Slider("The ground starts this far down the page", 0.2, 0.95, 0.01, e.Ground.Value, "", v => _state.Change("Ground", () => e.Ground = v, mergeKey: "fxg" + index, source: this)));
                }
                bool drawing = _state.Drawing == DrawKind.Dry && _state.DryTarget == index;
                var row = Ui.Row(Ui.Button(drawing ? "Now drag on the picture..." : "Draw an area it misses", () =>
                {
                    if (drawing) { _state.Drawing = DrawKind.Stop; _state.DryTarget = -1; }
                    else { _state.Drawing = DrawKind.Dry; _state.DryTarget = index; }
                    _state.Select(Sel.None, -1);
                }, "For example under a roof. Then drag on the picture.", small: true));
                if (e.Dry.Count > 0) row.Children.Add(Ui.Button($"Clear areas ({e.Dry.Count})", () => _state.Change("Clear areas", () => e.Dry.Clear(), structure: true), small: true));
                box.Children.Add(row);
            }
            panel.Children.Add(new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 10), Background = Ui.Brush("#1a1d24"), Child = box });
        }
        var add = new List<(string, string)> { ("", "Add an effect...") };
        foreach (string t in new[] { PageEffect.Rain, PageEffect.Snow, PageEffect.Leaves, PageEffect.Sparkles, PageEffect.Lightning, PageEffect.Shake }) add.Add((t, Names.Effect(t)));
        panel.Children.Add(Ui.Choice<string>(add, "", type =>
        {
            if (type.Length == 0) return;
            int at = Math.Max(1, _state.Selected == Sel.Stop ? _state.SelIndex + 1 : 1);
            _state.Change("Add " + Names.Effect(type).ToLowerInvariant(), () => p.Effects.Add(type switch
            {
                PageEffect.Lightning => new PageEffect { Type = type, Stop = at },
                PageEffect.Shake => new PageEffect { Type = type, Stop = at, Strength = 0.6 },
                _ => new PageEffect { Type = type, Amount = 0.6 },
            }), structure: true);
        }));
        if (p.Effects.Any(e => e.IsFalling)) panel.Children.Add(Ui.Help("Paint the picture without rain or snow in it, or you will see both."));
        return panel;
    }

    // ---------------------------------------------------------------- pictures on top

    Control Layers(Page p)
    {
        var panel = new StackPanel();
        for (int i = 0; i < p.Layers.Count; i++)
        {
            Layer l = p.Layers[i];
            int index = i;
            bool active = _state.Selected == Sel.Layer && _state.SelIndex == i;
            var row = new Border
            {
                Background = active ? Ui.Brush("#2a2540") : Brushes.Transparent, BorderBrush = active ? Ui.Brush("#b08cff") : Brushes.Transparent, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 8,
                    Children = { Ui.Dot((i + 1).ToString(), Color.Parse("#7a4fd6")), Ui.Label(l.Image + (l.MoveTo is not null ? " · moves" : "") + (l.Idle is not null ? " · " + l.Idle + "s" : ""), 12.5, Ui.Text, wrap: false) },
                },
            };
            row.PointerPressed += (_, _) => _state.Select(active ? Sel.None : Sel.Layer, index);
            panel.Children.Add(row);
        }
        panel.Children.Add(Ui.Row(Ui.Button("+ Add a picture on top...", _window.AddLayer, "A character or a thing with a see-through background (PNG)", small: true)));
        panel.Children.Add(Ui.Help("Use a PNG with a see-through background. It can appear, go away, move and bob, stop by stop."));
        return panel;
    }

    // ---------------------------------------------------------------- the book

    Control BookSettings()
    {
        Book b = _state.Book;
        var panel = new StackPanel();
        panel.Children.Add(Ui.FieldName("Age line (shown under the title on the shelf)"));
        panel.Children.Add(Text(b.Author, "For example: Ages 4 to 5", "Age line", "author", v => b.Author = v ?? ""));
        panel.Children.Add(Ui.FieldName("Folder name"));
        var id = Text(b.Id, Slug.Make(b.Title), "Folder name", "id", v => b.Id = Slug.Make(v));
        panel.Children.Add(id);
        panel.Children.Add(Ui.Help("Used for the book's folder when it is exported. Lower case, with dashes."));
        panel.Children.Add(Ui.FieldName("How stops are shown, unless a stop says otherwise"));
        var styles = new List<(string, string)>();
        foreach (string st in new[] { FocusStyles.Dim, FocusStyles.Vignette, FocusStyles.Spotlight, FocusStyles.None }) styles.Add((st, Names.Style(st)));
        panel.Children.Add(Ui.Choice<string>(styles, b.FocusStyle, v => _state.Change("Look", () => b.FocusStyle = v, structure: true)));
        panel.Children.Add(Ui.Heading("Cover"));
        panel.Children.Add(Ui.Label(string.IsNullOrEmpty(b.Cover) ? "No cover picture: the book opens straight on page 1." : b.Cover, 12.5, Ui.Text));
        panel.Children.Add(Ui.Row(
            Ui.Button("Choose a cover picture...", _window.PickCover, "A portrait picture, like the front of a real book", small: true),
            Ui.Button("No cover", () => _state.Change("No cover", () => b.Cover = "", structure: true), small: true)));
        panel.Children.Add(Ui.Help("With a cover, the book starts closed on it and the first tap swings it open."));
        panel.Children.Add(Ui.Heading("Folder"));
        panel.Children.Add(Ui.Label(_state.Project!.Folder, 12, Ui.Muted));
        panel.Children.Add(Ui.Row(Ui.Button("Open the folder", _window.ShowFolder, small: true)));
        return panel;
    }
}
