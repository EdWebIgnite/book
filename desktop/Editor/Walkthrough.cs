using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Editor.Core;
using Reader.Core;

namespace BookEditor;

/// <summary>
/// A step-by-step walk through making a first book, for people new to the editor. Each step says in a
/// sentence or two what to do, a brass ring goes round the real control to click, and the walkthrough
/// moves on by itself when the step has been done (or with Next). It can be stopped at any point and
/// carried on later from Help; how far it got is remembered between runs. It is offered once,
/// on the very first run.
/// </summary>
sealed class Walkthrough
{
    sealed record Step(string Title, string Text, Func<Control?> Target, Func<bool> Done, Func<string?>? Extra = null);

    readonly MainWindow _w;
    readonly List<Step> _steps;
    readonly Canvas _layer = new() { IsVisible = false };
    readonly Border _ring;
    readonly Border _card;
    readonly TextBlock _count, _title, _text, _extra, _done;
    readonly Button _back, _next, _stop;
    readonly DispatcherTimer _timer;
    int _at = -1;
    bool _exported;
    double _doneSince = -1;
    readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    public Walkthrough(MainWindow window)
    {
        _w = window;
        EditorState st = window.State;
        Book B() => st.Book;
        IEnumerable<Stop> Stops() => st.HasBook ? B().Pages.SelectMany(p => p.Stops) : Enumerable.Empty<Stop>();

        _steps = new List<Step>
        {
            new("Start a book",
                "Each book lives in a folder of its own, with its pictures and sounds. Press \"Start a new book\", type the book's title and press \"Make the book\".",
                () => _w.Welcome_.IsVisible ? _w.WelcomeNew : _w.NewButton,
                () => st.HasBook),
            new("Add the pictures",
                "Drag your page pictures onto the window, or press \"+ Add pictures\". A picture can show a whole double-page spread.",
                () => _w.Pages.AddButton,
                () => st.HasBook && B().Pages.Count > 0,
                () => "Are your left and right pages separate pictures? Press \"+ Add left and right pages\" instead, pick the left one and then the right one, and they are joined into one spread."),
            new("Draw a stop",
                "On the big picture, drag a box round the first thing the camera should show. Each box is a stop. Add more in reading order: the camera glides from one to the next.",
                () => _w.Canvas,
                () => st.Page is Page p && p.Stops.Count > 0),
            new("Type the words",
                "With the stop selected, type the words for it in the Words box on the right. These are shown under the picture when the camera gets there.",
                () => _w.Inspector.CaptionBox ?? (Control)_w.Canvas,
                () => Stops().Any(s => !string.IsNullOrWhiteSpace(s.Caption)),
                () => _w.Inspector.CaptionBox is null ? "Click the stop's box on the picture first, to choose it." : null),
            new("Add a voice",
                "Now the words read aloud. Press \"Record\" and read the words, then press Stop. Or press \"Add a recording...\" to use a sound file you made on your phone.",
                () => _w.Inspector.Narration?.AddButtons ?? _w.Inspector.CaptionBox ?? (Control)_w.Canvas,
                () => Stops().Any(s => s.Voice is not null),
                () => _w.Inspector.Narration is null ? "Click the stop's box on the picture first, to choose it." : null),
            new("Time the words",
                "Press \"Time words automatically\". The computer listens to the recording and works out when each word starts, so the words light up as they are read. Press Play beside it to check.",
                () => _w.Inspector.Narration?.AutoButton ?? (Control?)_w.Inspector.Narration ?? _w.Canvas,
                () => Stops().Any(s => s.Voice is { Words.Count: > 0 }),
                () => "A word a little early or late? Click it and nudge it with the buttons. You can also press \"Tap along\" and tap Space as each word starts."),
            new("Try it on a phone",
                "Press Play to see the book in the real reader. At the top, choose \"Galaxy S22 Ultra, upright\" to see it as it will look on a phone. Press Esc to come back.",
                () => _w.PreviewShowing ? _w.Preview.DeviceBox : _w.PlayButton,
                () => _w.PreviewShowing && _w.Preview.Device.Phone),
            new("Check the book",
                "Press \"Check book\". It lists anything that would go wrong, such as a missing picture, or words too long for a phone screen. Click a line to go straight to it.",
                () => _w.CheckButton,
                () => _w.CheckShowing),
            new("Export it",
                "Press Export. Keep \"Also make a book file to send to a phone\" ticked, then press the green Export button.",
                () => _w.ExportShowing ? _w.ExportScreen.ExportButton : _w.ExportButton,
                () => _exported),
            new("Send it to the phone",
                "Export made a book file ending in .picturebook. Send it to the phone by WhatsApp, email or Google Drive, or copy it across with a cable. On the phone, open the file and choose " + Brand.Name + ": the book goes on the shelf.",
                () => null,
                () => false,
                () => "A grown-up can also add it in the app's settings (the gear), under Books, and take it off there again."),
        };

        _ring = new Border
        {
            BorderBrush = Ui.Select, BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(10), IsHitTestVisible = false,
            BoxShadow = new BoxShadows(new BoxShadow { Blur = 18, Spread = 3, Color = Color.FromArgb(190, 217, 180, 90) }),
        };
        _count = Ui.Label("", 11, Ui.Warn, FontWeight.ExtraBold);
        _count.LetterSpacing = 1;
        _title = Ui.Label("", 17, Ui.AccentDeep, FontWeight.ExtraBold);
        _text = Ui.Label("", 13, Ui.Text);
        _extra = Ui.Label("", 12.5, Ui.Muted);
        _done = Ui.Label("Done!", 13.5, Ui.Good, FontWeight.Bold);
        _back = Ui.Button("Back", () => Go(_at - 1), small: true);
        _next = Ui.Button("Next", () => Go(_at + 1), primary: true, small: true);
        _stop = Ui.Button("Stop for now", Pause, "Stop the walkthrough. Carry on later from Help.", small: true);
        _card = new Border
        {
            Width = 390, Background = Ui.Card, BorderBrush = Ui.Select, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12), Padding = new Thickness(20, 16),
            BoxShadow = Ui.Lifted,
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    _count, _title, _text, _extra, _done,
                    new DockPanel
                    {
                        Margin = new Thickness(0, 6, 0, 0), LastChildFill = false,
                        Children = { _stop, new StackPanel { [DockPanel.DockProperty] = Dock.Right, Orientation = Orientation.Horizontal, Spacing = 6, Children = { _back, _next } } },
                    },
                },
            },
        };
        _layer.Children.Add(_ring);
        _layer.Children.Add(_card);
        window.ExportScreen.Exported += _ => _exported = true;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => Refresh());
    }

    /// <summary>The walkthrough's layer, over the window. Clicks go through it except on its card.</summary>
    public Control Layer => _layer;

    public int Count => _steps.Count;
    public bool Running => _at >= 0;
    public int At => _at;

    // ---------------------------------------------------------------- remembering how far it got

    static string File_ => Path.Combine(EditorPaths.AppData, "walkthrough.json");

    /// <summary>True the very first time the editor runs: nothing has been said about the walkthrough yet.</summary>
    public static bool ShouldOffer => !File.Exists(File_);

    /// <summary>The step to carry on from (0 when it was never started or was finished).</summary>
    public int SavedStep
    {
        get
        {
            try { return File.Exists(File_) && JsonNode.Parse(File.ReadAllText(File_)) is JsonObject o && o["step"]?.GetValue<int>() is int n && n < _steps.Count ? Math.Max(0, n) : 0; }
            catch (Exception) { return 0; }
        }
    }

    static void Save(int step, string state)
    {
        try
        {
            Directory.CreateDirectory(EditorPaths.AppData);
            File.WriteAllText(File_, new JsonObject { ["step"] = step, ["state"] = state }.ToJsonString());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>"No thanks" on the first run: don't offer again (it can still be started from Help).</summary>
    public void Decline() => Save(0, "declined");

    // ---------------------------------------------------------------- running it

    public void Start(int step)
    {
        _exported = false;
        _layer.IsVisible = true;
        Go(Math.Clamp(step, 0, _steps.Count - 1));
        _timer.Start();
    }

    void Go(int step)
    {
        if (step >= _steps.Count) { Finish(); return; }
        _at = Math.Max(0, step);
        _doneSince = -1;
        if (_steps[_at].Title == "Export it") _exported = false;
        Save(_at, "running");
        Refresh();
    }

    void Pause()
    {
        Save(_at, "paused");
        Hide();
        _w.Say("Walkthrough stopped. Carry on whenever you like from Help (the ? button at the top right).", 6);
    }

    void Finish()
    {
        Save(0, "finished");
        Hide();
        _w.Say("That's the whole walkthrough. Have fun making books! It's always there again under Help.", 7);
    }

    void Hide()
    {
        _at = -1;
        _timer.Stop();
        _layer.IsVisible = false;
    }

    /// <summary>Move the ring and the card to where the step's control is now, and move on if the step is done.</summary>
    public void Refresh()
    {
        if (_at < 0) return;
        Step s = _steps[_at];
        bool done = s.Done();
        double now = _clock.Elapsed.TotalSeconds;
        if (done && _doneSince < 0) _doneSince = now;
        // Done: say so for a moment, then the next step. (Already done when it started: straight on.)
        if (done && now - _doneSince > 0.9) { Go(_at + 1); return; }

        _count.Text = $"STEP {_at + 1} OF {_steps.Count}";
        _title.Text = s.Title;
        _text.Text = s.Text;
        string? extra = s.Extra?.Invoke();
        _extra.Text = extra ?? "";
        _extra.IsVisible = !string.IsNullOrEmpty(extra);
        _done.IsVisible = done;
        _back.IsVisible = _at > 0;
        _next.Content = _at == _steps.Count - 1 ? "Finish" : done ? "Next" : "Skip this step";

        Size area = _layer.Bounds.Size;
        if (area.Width <= 0 || area.Height <= 0) area = new Size(_w.Bounds.Width, _w.Bounds.Height);
        _card.Measure(new Size(_card.Width, double.PositiveInfinity));
        double cw = _card.Width, ch = _card.DesiredSize.Height;
        Control? target = s.Target();
        Rect? r = null;
        if (target is not null && target.IsEffectivelyVisible && target.Bounds.Width > 0 && target.TranslatePoint(new Point(0, 0), _layer) is Point at)
            r = new Rect(at, target.Bounds.Size);
        _ring.IsVisible = r is not null;
        double x, y;
        if (r is Rect t)
        {
            Rect ring = t.Inflate(5);
            Canvas.SetLeft(_ring, ring.X);
            Canvas.SetTop(_ring, ring.Y);
            _ring.Width = ring.Width;
            _ring.Height = ring.Height;
            if (t.Height > area.Height * 0.4)
            {
                // A big target (the page): the card sits inside it, top right, out of the way of the middle.
                x = t.Right - cw - 24;
                y = t.Y + 24;
            }
            else if (t.Bottom + 14 + ch < area.Height - 8)
            {
                x = t.X + t.Width / 2 - cw / 2;
                y = t.Bottom + 14;
            }
            else
            {
                x = t.X + t.Width / 2 - cw / 2;
                y = t.Y - 14 - ch;
            }
            // A card that would land on the control itself goes beside it instead.
            if (y < t.Bottom && y + ch > t.Y && t.Height <= area.Height * 0.4)
            {
                x = t.X - cw - 16 >= 8 ? t.X - cw - 16 : t.Right + 16;
                y = t.Y;
            }
        }
        else
        {
            x = area.Width / 2 - cw / 2;
            y = area.Height / 2 - ch / 2;
        }
        Canvas.SetLeft(_card, Math.Clamp(x, 8, Math.Max(8, area.Width - cw - 8)));
        Canvas.SetTop(_card, Math.Clamp(y, 8, Math.Max(8, area.Height - ch - 8)));
    }

    /// <summary>For the screenshot hook: move past the steps already done, with no waiting.</summary>
    public void SettleForShot()
    {
        while (_at >= 0 && _at < _steps.Count && _steps[_at].Done()) Go(_at + 1);
        Refresh();
    }
}
