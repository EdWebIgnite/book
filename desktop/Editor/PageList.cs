using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Reader.Core;

namespace BookEditor;

/// <summary>
/// The pages down the left: a small picture of each, what kind of page it is, and the cover marked.
/// Click to go to a page; drag one onto another to move it there.
/// </summary>
sealed class PageList : UserControl
{
    readonly EditorState _state;
    readonly Images _images;
    readonly StackPanel _list = new() { Spacing = 8 };
    readonly MainWindow _window;
    int _dragFrom = -1;

    public PageList(EditorState state, Images images, MainWindow window)
    {
        _state = state;
        _images = images;
        _window = window;
        var add = Ui.Button("+ Add pictures", () => _window.AddPictures(), "Add page pictures. You can also drag picture files onto the window.");
        add.HorizontalAlignment = HorizontalAlignment.Stretch;
        add.HorizontalContentAlignment = HorizontalAlignment.Center;
        var spread = Ui.Button("+ Add left and right pages", () => _window.AddLeftRight(), "Pick a left-hand page and a right-hand page. They are joined into one spread.", small: true);
        spread.HorizontalAlignment = HorizontalAlignment.Stretch;
        spread.HorizontalContentAlignment = HorizontalAlignment.Center;
        var panel = new DockPanel { Margin = new Thickness(10) };
        var bottom = new StackPanel { Spacing = 6, Margin = new Thickness(0, 10, 0, 0), Children = { add, spread } };
        DockPanel.SetDock(bottom, Dock.Bottom);
        panel.Children.Add(bottom);
        panel.Children.Add(new ScrollViewer { Content = _list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Content = new Border { Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 1, 0), Child = panel };
        _state.Changed += (why, _) => { if (why != Why.Content) Rebuild(); };
        _images.Arrived += () => { if (_waiting) Rebuild(); };
        // Let go over the same page: go to it. Over another page: move the dragged page there.
        _list.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_dragFrom < 0) return;
            int from = _dragFrom;
            _dragFrom = -1;
            int to = TargetIndex(e.GetPosition(_list));
            if (to >= 0 && to != from) _window.MovePage(from, to);
            else _state.GoToPage(from);
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
    }

    bool _waiting;

    public void Rebuild()
    {
        _list.Children.Clear();
        _waiting = false;
        if (!_state.HasBook) return;
        Book book = _state.Book;
        for (int i = 0; i < book.Pages.Count; i++) _list.Children.Add(Item(book, i));
        if (book.Pages.Count == 0) _list.Children.Add(Ui.Help("No pages yet. Add pictures below, or drag them onto the window."));
    }

    Control Item(Book book, int i)
    {
        Page p = book.Pages[i];
        bool active = i == _state.PageIndex;
        var img = new Image { Stretch = Stretch.Uniform, MaxHeight = 110 };
        var bmp = _images.Get(_state.Project!.PathOf(p.Image), 320);
        if (bmp is null) _waiting = true;
        img.Source = bmp;
        var picture = new Panel { Children = { new Border { Background = Ui.Stage, CornerRadius = new CornerRadius(6), MinHeight = 60, Child = img, ClipToBounds = true } } };
        if (book.Cover == p.Image)
            picture.Children.Add(new Border { Background = Ui.Accent, CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1), Margin = new Thickness(4), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = Ui.Label("Cover", 10.5, Ui.AccentInk, FontWeight.Bold) });
        if (p.Spread is not null)
            picture.Children.Add(new Border { Background = Ui.Brush("#3a4050"), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1), Margin = new Thickness(4), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Child = Ui.Label("2 pages", 10.5, Ui.Text, FontWeight.Bold) });
        string kind = p.Explore ? $"explore, {p.Hotspots.Count} to find" : $"{p.Stops.Count} stop{(p.Stops.Count == 1 ? "" : "s")}";
        var meta = Ui.Label($"Page {i + 1} · {kind}", 11.5, active ? Ui.Text : Ui.Muted, wrap: false);
        meta.Margin = new Thickness(3, 4, 3, 1);
        var box = new Border
        {
            Background = Ui.Raised, CornerRadius = new CornerRadius(9), Padding = new Thickness(3), BorderThickness = new Thickness(2),
            BorderBrush = active ? Ui.Accent : Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel { Children = { picture, meta } },
        };
        ToolTip.SetTip(box, p.Image);
        int index = i;
        box.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(box).Properties.IsLeftButtonPressed) return;
            _dragFrom = index;      // chosen when the mouse comes up: on this page it goes there, on another it moves there
        };
        return box;
    }

    int TargetIndex(Point inList)
    {
        for (int i = 0; i < _list.Children.Count; i++)
        {
            Rect b = _list.Children[i].Bounds;
            if (inList.Y >= b.Y && inList.Y <= b.Bottom) return i;
        }
        return -1;
    }
}
