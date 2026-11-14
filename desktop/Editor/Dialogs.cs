using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace BookEditor;

/// <summary>
/// Questions asked inside the window (not in separate windows), so they look the same everywhere and
/// show up in screenshots. One at a time.
/// </summary>
sealed class Dialogs : Panel
{
    TaskCompletionSource<int>? _answer;
    Action? _cancel;

    public Dialogs()
    {
        IsVisible = false;
        Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
    }

    public bool Showing => IsVisible;

    static Border Box(Control content, double width = 520) => new()
    {
        Width = width, Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20),
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = content,
        BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 16, Blur = 60, Color = Color.FromArgb(150, 0, 0, 0) }),
    };

    /// <summary>Ask a question with some answers. Returns the index of the answer chosen; Esc gives <paramref name="cancel"/>.</summary>
    public Task<int> Ask(string title, string text, string[] answers, int primary = 0, int cancel = -1)
    {
        _answer?.TrySetResult(cancel);
        var tcs = new TaskCompletionSource<int>();
        _answer = tcs;
        _cancel = () => Finish(cancel);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        for (int i = 0; i < answers.Length; i++)
        {
            int n = i;
            row.Children.Add(Ui.Button(answers[i], () => Finish(n), primary: i == primary));
        }
        Children.Clear();
        Children.Add(Box(new StackPanel { Children = { Ui.Label(title, 17, Ui.Text, FontWeight.Bold), new Border { Height = 8 }, Ui.Label(text, 13.5, Ui.Text), row } }));
        IsVisible = true;
        if (row.Children.Count > primary) Avalonia.Threading.Dispatcher.UIThread.Post(() => (row.Children[primary] as Button)?.Focus());
        return tcs.Task;
    }

    /// <summary>Show a panel of its own (the new-book form). It closes with <see cref="Close"/>.</summary>
    public void ShowPanel(Control content, Action onCancel, double width = 560)
    {
        _answer?.TrySetResult(-1);
        _answer = null;
        _cancel = () => { Close(); onCancel(); };
        Children.Clear();
        Children.Add(Box(content, width));
        IsVisible = true;
    }

    void Finish(int answer)
    {
        TaskCompletionSource<int>? a = _answer;
        _answer = null;
        Close();
        a?.TrySetResult(answer);
    }

    public void Close()
    {
        IsVisible = false;
        Children.Clear();
    }

    /// <summary>Esc while a question is showing: the cancel answer.</summary>
    public bool HandleKey(Key key)
    {
        if (!IsVisible) return false;
        if (key == Key.Escape) { _cancel?.Invoke(); return true; }
        return false;
    }
}
