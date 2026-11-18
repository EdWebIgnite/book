using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Editor.Core;

namespace BookEditor;

/// <summary>
/// Check book: everything that will go wrong in the apps or look wrong on some screens, most serious
/// first. Click a line to go to it.
/// </summary>
sealed class CheckPanel : UserControl
{
    readonly EditorState _state;
    readonly MainWindow _window;
    readonly StackPanel _list = new() { Spacing = 6 };
    readonly TextBlock _summary = Ui.Label("", 12.5, Ui.Muted);

    public CheckPanel(EditorState state, MainWindow window)
    {
        _state = state;
        _window = window;
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var close = Ui.Button("Close", () => _window.CloseCheck(), "Esc", small: true);
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(Ui.Title("Check book"));
        var again = Ui.Button("Check again", Run, small: true);
        var tidy = Ui.Button("Move unused files aside", Tidy, "Moves files the book doesn't use into a folder called _unused inside the book's folder. Nothing is deleted.", small: true);
        var top = new StackPanel { Children = { head, _summary, Ui.Row(again, tidy) } };
        var dock = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);
        dock.Children.Add(new ScrollViewer { Content = _list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Content = new Border
        {
            Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(1, 0, 0, 0), Child = dock,
            BoxShadow = new BoxShadows(new BoxShadow { OffsetX = -4, Blur = 24, Color = Color.FromArgb(60, 44, 36, 20) }),
        };
    }

    public int ProblemCount { get; private set; }

    public void Run()
    {
        _list.Children.Clear();
        if (!_state.HasBook) return;
        List<Issue> issues = BookChecker.Check(_state.Project!, measureWords: Reader.Ui.WordsLayout.Measure);
        int problems = issues.Count(i => i.Level == Severity.Problem), warnings = issues.Count(i => i.Level == Severity.Warning), tips = issues.Count(i => i.Level == Severity.Tip);
        ProblemCount = problems;
        _summary.Text = issues.Count == 0 ? "Nothing to fix. The book is ready to export."
            : $"{problems} to fix, {warnings} to check, {Count(tips, "tip")}. \"Fix\" means something will be missing in the apps. \"Check\" may look wrong on some screens. Tips may well be fine.";
        if (issues.Count == 0) _list.Children.Add(Ui.Note(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { new Reader.Ui.Dassie { Look = Reader.Ui.Dassie.Pose.Sit, Width = 48, Height = 48 }, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Ui.Label("All good.", 14, Ui.Good, FontWeight.ExtraBold), Ui.Label("Nothing to fix, check or think about. The book is ready to export.", 12.5, Ui.Text) } } } }, "good"));
        foreach (Issue issue in issues) _list.Children.Add(Line(issue));
    }

    static string Count(int n, string word) => n == 1 ? $"1 {word}" : $"{n} {word}s";

    Control Line(Issue issue)
    {
        (IBrush colour, string label) = issue.Level switch
        {
            Severity.Problem => (Ui.Bad, "Fix"),
            Severity.Warning => (Ui.Warn, "Check"),
            _ => (Ui.Muted, "Tip"),
        };
        var badge = new Border { Background = colour, CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 1), MinWidth = 44, VerticalAlignment = VerticalAlignment.Top, Child = new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeight.ExtraBold, Foreground = Ui.AccentInk, HorizontalAlignment = HorizontalAlignment.Center } };
        var text = Ui.Label(issue.Text, 12.5, Ui.Text);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
        row.Children.Add(badge);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        var inner = new StackPanel { Spacing = 4, Children = { row } };
        var box = new Border { Background = Ui.Card, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8), Child = inner };
        if (issue.Page >= 0)
        {
            var go = Ui.Label($"Go to page {issue.Page + 1} \u203A", 11.5, Ui.Accent, FontWeight.ExtraBold, wrap: false);
            go.HorizontalAlignment = HorizontalAlignment.Right;
            inner.Children.Add(go);
            box.Cursor = new Cursor(StandardCursorType.Hand);
            ToolTip.SetTip(box, "Go there");
            box.PointerEntered += (_, _) => box.BorderBrush = Ui.Accent;
            box.PointerExited += (_, _) => box.BorderBrush = Ui.Line;
            box.PointerPressed += (_, _) =>
            {
                Sel kind = issue.Kind switch { Spot.Stop => Sel.Stop, Spot.Hotspot => Sel.Hotspot, Spot.Layer => Sel.Layer, Spot.Words => Sel.Words, _ => Sel.None };
                _state.Show(issue.Page, kind, issue.Index);
            };
        }
        return box;
    }

    void Tidy()
    {
        int moved = _state.Project!.MoveUnusedAside();
        _window.Say(moved == 0 ? "There were no unused files." : $"Moved {moved} unused file{(moved == 1 ? "" : "s")} into the _unused folder.");
        Run();
    }
}
