using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Editor.Core;
using Reader.Core;
using Reader.Ui;

namespace BookEditor;

/// <summary>
/// Every keyboard shortcut, in one list: the Help sheet shows it, and the menus and tool tips take
/// their key names from it, so they can't disagree.
/// </summary>
static class Shortcuts
{
    public sealed record Item(string Group, string Keys, string What);

    public static readonly Item[] All =
    {
        new("Book", "Ctrl+N", "Start a new book"),
        new("Book", "Ctrl+O", "Open a book folder"),
        new("Book", "Ctrl+Shift+O", "Open a book file (.picturebook)"),
        new("Book", "Ctrl+S", "Save"),
        new("Book", "Ctrl+W", "Close the book"),
        new("Book", "P or F5", "Play from the selected stop"),
        new("Book", "F7", "Check book"),
        new("Book", "Ctrl+E", "Export"),
        new("Edit", "Ctrl+Z", "Undo"),
        new("Edit", "Ctrl+Y or Ctrl+Shift+Z", "Redo"),
        new("Edit", "Ctrl+C, Ctrl+V", "Copy what is selected, paste it on another page"),
        new("Edit", "Ctrl+D", "Duplicate what is selected"),
        new("Edit", "Delete", "Remove what is selected"),
        new("Edit", "Esc", "Clear the selection, or close what is open"),
        new("On the picture", "Drag", "Draw a stop (or a thing to find, on explore pages)"),
        new("On the picture", "Shift+drag", "Draw over another box"),
        new("On the picture", "Arrow keys", "Nudge what is selected (Shift: further)"),
        new("On the picture", "Alt+arrow keys", "Change its size"),
        new("On the picture", "[ and ]", "Select the stop before or after"),
        new("Pages", "Page Up, Page Down", "Go to the page before or after"),
        new("Pages", "Home, End", "Go to the first or last page"),
        new("Pages", "Ctrl+Page Up, Ctrl+Page Down", "Move the page earlier or later"),
        new("View", "Ctrl+wheel", "Zoom where the mouse is"),
        new("View", "Ctrl+= and Ctrl+-", "Zoom in and out"),
        new("View", "Ctrl+0", "Fit the whole page"),
        new("View", "Ctrl+1", "Actual pixels (100%)"),
        new("View", "Space+drag, middle button", "Move the zoomed picture"),
        new("Read aloud", "Space", "Tap along: mark the start of each word"),
        new("Read aloud", "Backspace", "Tap along: take the last mark back"),
        new("Read aloud", ", and .", "Nudge the chosen word earlier or later"),
        new("Help", "F1", "This sheet"),
    };

    /// <summary>The keys for a command, as written in the list.</summary>
    public static string Keys(string what) => All.FirstOrDefault(i => i.What == what)?.Keys ?? "";

    /// <summary>A tool tip: what it does, then its keys in brackets.</summary>
    public static string Tip(string text, string what) => Keys(what) is { Length: > 0 } k ? $"{text} ({k})" : text;
}

sealed partial class MainWindow
{
    Button _undo = null!, _redo = null!, _save = null!, _recentButton = null!;
    readonly List<Control> _needBook = new();
    readonly List<MenuItem> _needBookMenu = new();
    MenuItem _recentMenu = null!;
    Avalonia.Controls.Shapes.Ellipse _saveDot = null!;
    TextBlock _saveText = null!, _where = null!, _zoomText = null!;
    Control _about = null!;
    PixelBox? _normalBox;
    Control _zoomBar = null!;

    // ---------------------------------------------------------------- the top bar

    Control TopBar()
    {
        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
            Children =
            {
                new Wordmark { Size = 21, Ink = Ui.AccentDeep, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -6, 0, 0) },
                (_product = new TextBlock { Text = EditorBrand.Product, FontSize = 13, FontWeight = FontWeight.ExtraBold, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center }),
            },
        };
        ToolTip.SetTip(brand, EditorBrand.Name);

        var menu = Menus();
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        tools.Children.Add(NewButton = Ui.IconButton(EditorIcons.New, null, () => _ = NewBook(), Shortcuts.Tip("Start a new book", "Start a new book")));
        tools.Children.Add(Ui.IconButton(EditorIcons.Open, null, () => _ = OpenFolderDialog(), Shortcuts.Tip("Open a book folder", "Open a book folder")));
        tools.Children.Add(_recentButton = Ui.IconButton(EditorIcons.Recent, null, ShowRecent, "Books opened lately"));
        tools.Children.Add(Ui.Divider());
        tools.Children.Add(_undo = Ui.IconButton(EditorIcons.Undo, null, _state.Undo, "Undo (Ctrl+Z)"));
        tools.Children.Add(_redo = Ui.IconButton(EditorIcons.Redo, null, _state.Redo, "Redo (Ctrl+Y)"));

        var output = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        output.Children.Add(PlayButton = Ui.IconButton(EditorIcons.Play, "Play", () => OpenPreview(fromStop: true), "Play the book in the real reader, from the selected stop (P or F5)"));
        output.Children.Add(CheckButton = Ui.IconButton(EditorIcons.Check, "Check", OpenCheck, "Check book: look for anything that will go wrong in the apps (F7)"));
        output.Children.Add(ExportButton = Ui.IconButton(EditorIcons.Export, "Export", OpenExport, "Make a copy ready for the apps, and a book file to send to a phone (Ctrl+E)"));
        output.Children.Add(Ui.Divider());
        output.Children.Add(_save = Ui.IconButton(EditorIcons.Save, "Save", Save, "Save (Ctrl+S). While there are unsaved changes a safety copy is also kept every few seconds."));
        output.Children.Add(HelpButton = Ui.IconButton(EditorIcons.Help, null, ShowHelp, "Help: keyboard shortcuts and the step-by-step walkthrough (F1)"));
        _needBook.AddRange(new Control[] { PlayButton, CheckButton, ExportButton, _save });

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto"), Margin = new Thickness(12, 0) };
        grid.Children.Add(brand);
        Grid.SetColumn(menu, 1);
        grid.Children.Add(menu);
        Grid.SetColumn(tools, 2);
        grid.Children.Add(tools);
        Grid.SetColumn(_title, 3);
        grid.Children.Add(_title);
        Grid.SetColumn(output, 4);
        grid.Children.Add(output);
        var bar = new Border { Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Height = 52, Child = grid };
        // On a narrow window (a laptop at 125%, say) the words beside the tools go, leaving their icons and tool tips.
        bar.SizeChanged += (_, e) => Compact(e.NewSize.Width < 1240);
        return bar;
    }

    TextBlock _product = null!;
    bool? _compact;

    void Compact(bool on)
    {
        if (_compact == on) return;
        _compact = on;
        _product.IsVisible = !on;
        foreach (Button b in new[] { PlayButton!, CheckButton!, ExportButton!, _save })
        {
            if (b.Content is not StackPanel face) continue;
            foreach (TextBlock t in face.Children.OfType<TextBlock>()) t.IsVisible = !on;
            b.Padding = on ? new Thickness(7, 5) : new Thickness(10, 5, 12, 5);
        }
    }

    MenuItem Item(string header, Action click, string? keys = null, bool needsBook = true)
    {
        var m = new MenuItem { Header = header };
        if (keys is { Length: > 0 }) m.InputGesture = KeyGesture.Parse(keys.Split(' ')[0]);
        m.Click += (_, _) => click();
        if (needsBook) _needBookMenu.Add(m);
        return m;
    }

    static Separator Line() => new();

    Menu Menus()
    {
        _recentMenu = new MenuItem { Header = "Open recent" };
        var file = new MenuItem
        {
            Header = "_File",
            Items =
            {
                Item("New book...", () => _ = NewBook(), "Ctrl+N", needsBook: false),
                Item("Open a book folder...", () => _ = OpenFolderDialog(), "Ctrl+O", needsBook: false),
                Item("Open a book file (.picturebook)...", () => _ = OpenBookFileDialog(), "Ctrl+Shift+O", needsBook: false),
                _recentMenu,
                Line(),
                Item("Save", Save, "Ctrl+S"),
                Item("Show the book's folder", ShowFolder),
                Item("Show the book file", ShowBookFile),
                Line(),
                Item("Export...", OpenExport, "Ctrl+E"),
                Line(),
                Item("Close the book", () => _ = CloseBook(), "Ctrl+W"),
                Item("Quit", Close, null, needsBook: false),
            },
        };
        var edit = new MenuItem
        {
            Header = "_Edit",
            Items =
            {
                Item("Undo", _state.Undo, "Ctrl+Z"),
                Item("Redo", _state.Redo, "Ctrl+Y"),
                Line(),
                Item("Copy", CopySelected, "Ctrl+C"),
                Item("Paste", Paste, "Ctrl+V"),
                Item("Duplicate", DuplicateSelected, "Ctrl+D"),
                Item("Delete", DeleteSelected, "Delete"),
                Line(),
                Item("Add a whole page stop", AddWholePageStop),
                Item("Select the next stop", () => SelectStop(+1), "OemCloseBrackets"),
                Item("Select the stop before", () => SelectStop(-1), "OemOpenBrackets"),
            },
        };
        var frames = new MenuItem { Header = "Screens shown round a stop" };
        foreach (var (id, text) in ScreenChoices)
        {
            var m = new MenuItem { Header = text, ToggleType = MenuItemToggleType.CheckBox, IsChecked = _canvas.Overlays.Frames.Contains(id) };
            m.Click += (_, _) => ToggleFrame(id);
            frames.Items.Add(m);
        }
        frames.SubmenuOpened += (_, _) =>
        {
            int i = 0;
            foreach (var (id, _) in ScreenChoices) ((MenuItem)frames.Items[i++]!).IsChecked = _canvas.Overlays.Frames.Contains(id);
        };
        var surround = new MenuItem { Header = "Round the page" };
        foreach (var (id, text) in new[] { ("grey", "Mid grey"), ("light", "Light grey"), ("paper", "Paper") })
        {
            var m = new MenuItem { Header = text, ToggleType = MenuItemToggleType.Radio, GroupName = "surround", IsChecked = Ui.Surround == id };
            m.Click += (_, _) => SetSurround(id);
            surround.Items.Add(m);
        }
        var crease = new MenuItem { Header = "Middle of a spread", ToggleType = MenuItemToggleType.CheckBox, IsChecked = _canvas.Overlays.Crease };
        crease.Click += (_, _) => { SetCrease(!_canvas.Overlays.Crease); crease.IsChecked = _canvas.Overlays.Crease; };
        var view = new MenuItem
        {
            Header = "_View",
            Items =
            {
                Item("Zoom in", () => _canvas.ZoomBy(1.25), "Ctrl+OemPlus"),
                Item("Zoom out", () => _canvas.ZoomBy(1 / 1.25), "Ctrl+OemMinus"),
                Item("Fit the whole page", _canvas.Fit, "Ctrl+D0"),
                Item("Actual pixels (100%)", _canvas.ActualSize, "Ctrl+D1"),
                Line(),
                crease, frames, surround,
            },
        };
        var book = new MenuItem
        {
            Header = "_Book",
            Items =
            {
                Item("Add page pictures...", AddPictures),
                Item("Add a left and a right page...", AddLeftRight),
                Line(),
                Item("Page before", () => _state.GoToPage(_state.PageIndex - 1), "PageUp"),
                Item("Page after", () => _state.GoToPage(_state.PageIndex + 1), "PageDown"),
                Item("First page", () => _state.GoToPage(0), "Home"),
                Item("Last page", () => _state.GoToPage(_state.Book.Pages.Count - 1), "End"),
                Item("Move the page earlier", () => MovePage(_state.PageIndex, _state.PageIndex - 1), "Ctrl+PageUp"),
                Item("Move the page later", () => MovePage(_state.PageIndex, _state.PageIndex + 1), "Ctrl+PageDown"),
                Line(),
                Item("Play from the selected stop", () => OpenPreview(fromStop: true), "F5"),
                Item("Check book", OpenCheck, "F7"),
                Item("Time the words of every recording...", () => _ = Timing.TimeAll()),
            },
        };
        var help = new MenuItem
        {
            Header = "_Help",
            Items =
            {
                Item("Keyboard shortcuts and tips", ShowHelp, "F1", needsBook: false),
                Item("Step-by-step walkthrough", () => { _help.IsVisible = false; Tour.Start(Tour.SavedStep > 0 && Tour.SavedStep < Tour.Count ? Tour.SavedStep : 0); }, null, needsBook: false),
                Line(),
                Item("About " + EditorBrand.Name, ShowAbout, null, needsBook: false),
            },
        };
        FillRecentMenu();
        return new Menu { Items = { file, edit, view, book, help }, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
    }

    static readonly (string Id, string Text)[] ScreenChoices =
    {
        ("s22-upright", "Phone upright"), ("s22-sideways", "Phone sideways"), ("cheap-sideways", "Small phone"), ("tablet-sideways", "Tablet"), ("window", "Computer"),
    };

    void SelectStop(int step)
    {
        if (_state.Page is not Page p || p.Stops.Count == 0) return;
        _state.Select(Sel.Stop, _state.Selected == Sel.Stop ? Math.Clamp(_state.SelIndex + step, 0, p.Stops.Count - 1) : step > 0 ? 0 : p.Stops.Count - 1);
    }

    void ToggleFrame(string id)
    {
        if (!_canvas.Overlays.Frames.Remove(id)) _canvas.Overlays.Frames.Add(id);
        Settings.Frames = ScreenChoices.Select(c => c.Id).Where(_canvas.Overlays.Frames.Contains).ToList();
        if (!ScreenshotMode) Settings.Save();
        _canvas.InvalidateVisual();
        RefreshCanvasBar();
    }

    void SetCrease(bool on)
    {
        _canvas.Overlays.Crease = on;
        Settings.Crease = on;
        if (!ScreenshotMode) Settings.Save();
        _canvas.InvalidateVisual();
        RefreshCanvasBar();
    }

    void SetSurround(string id)
    {
        Ui.Surround = id;
        Settings.Surround = id;
        if (!ScreenshotMode) Settings.Save();
        _canvas.InvalidateVisual();
    }

    void FillRecentMenu()
    {
        _recentMenu.Items.Clear();
        var recent = _recent.Existing();
        if (recent.Count == 0) _recentMenu.Items.Add(new MenuItem { Header = "No books opened yet", IsEnabled = false });
        foreach (string f in recent.Take(10))
        {
            string title = Path.GetFileName(f);
            try { title = BookLibrary.Describe(f).Title; } catch (Exception) { }
            var item = new MenuItem { Header = title };
            ToolTip.SetTip(item, f);
            item.Click += (_, _) => _ = OpenBook(f);
            _recentMenu.Items.Add(item);
        }
    }

    void ShowRecent()
    {
        var menu = new MenuFlyout();
        var recent = _recent.Existing();
        if (recent.Count == 0) menu.Items.Add(new MenuItem { Header = "No books opened yet", IsEnabled = false });
        foreach (string f in recent.Take(10))
        {
            string title = Path.GetFileName(f);
            try { title = BookLibrary.Describe(f).Title; } catch (Exception) { }
            var item = new MenuItem { Header = new StackPanel { Children = { Ui.Label(title, 13, Ui.Text, FontWeight.Bold, wrap: false), Ui.Label(f, 11.5, Ui.Muted, wrap: false) } } };
            item.Click += (_, _) => _ = OpenBook(f);
            menu.Items.Add(item);
        }
        menu.ShowAt(_recentButton);
    }

    // ---------------------------------------------------------------- the status bar

    Control StatusBar()
    {
        _saveDot = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        _saveText = Ui.Label("", 12, Ui.Muted, FontWeight.SemiBold, wrap: false);
        _saveText.VerticalAlignment = VerticalAlignment.Center;
        _where = Ui.Label("", 12, Ui.Muted, wrap: false);
        _where.VerticalAlignment = VerticalAlignment.Center;
        _zoomText = new TextBlock { FontSize = 12, FontWeight = FontWeight.Bold, Foreground = Ui.Text, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, MinWidth = 40 };
        Button Small(string icon, Action a, string tip)
        {
            var b = Ui.IconButton(icon, null, a, tip, iconSize: 14);
            b.MinHeight = 22;
            b.MinWidth = 26;
            b.Padding = new Thickness(5, 2);
            b.Classes.Add("flat");
            b.Background = Brushes.Transparent;
            b.BorderThickness = new Thickness(0);
            return b;
        }
        var zoom = _zoomBar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Small(EditorIcons.ZoomOut, () => _canvas.ZoomBy(1 / 1.25), "Zoom out (Ctrl+-)"),
                _zoomText,
                Small(EditorIcons.ZoomIn, () => _canvas.ZoomBy(1.25), "Zoom in (Ctrl+=)"),
                Small(EditorIcons.Fit, _canvas.Fit, "Fit the whole page (Ctrl+0)"),
            },
        };
        ToolTip.SetTip(_zoomText, "How big the picture is shown: 100% is one pixel of the picture to one of the screen. Ctrl+1 goes to 100%, Ctrl+0 fits the page.");
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { _saveDot, _saveText, new Border { Width = 1, Height = 14, Background = Ui.Line, Margin = new Thickness(4, 0) }, _where } };
        var dock = new DockPanel { Margin = new Thickness(12, 0, 8, 0) };
        DockPanel.SetDock(zoom, Dock.Right);
        dock.Children.Add(zoom);
        dock.Children.Add(left);
        return new Border { Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 1, 0, 0), Height = 28, Child = dock };
    }

    void RefreshChrome()
    {
        bool has = _state.HasBook;
        _welcome.IsVisible = !has;
        _workspace.IsVisible = has;
        _title.IsVisible = has;
        foreach (Control b in _needBook) b.IsEnabled = has;
        foreach (MenuItem m in _needBookMenu) m.IsEnabled = has;
        _undo.IsEnabled = _state.History.CanUndo;
        _redo.IsEnabled = _state.History.CanRedo;
        ToolTip.SetTip(_undo, _state.History.UndoLabel is string u ? $"Undo {u.ToLowerInvariant()} (Ctrl+Z)" : "Nothing to undo");
        ToolTip.SetTip(_redo, _state.History.RedoLabel is string r ? $"Redo {r.ToLowerInvariant()} (Ctrl+Y)" : "Nothing to redo");
        // Save is the main action only while there is something to save.
        _save.Classes.Set("primary", has && _state.Dirty);
        _empty.IsVisible = has && _state.Book.Pages.Count == 0;
        Title = has ? $"{(_state.Dirty ? "* " : "")}{(_state.Book.Title.Length > 0 ? _state.Book.Title : "Untitled")} - {EditorBrand.Name}" : EditorBrand.Name;
        RefreshStatus();
    }

    void RefreshStatus()
    {
        bool has = _state.HasBook;
        _saveDot.Fill = !has ? Ui.Line : _state.Dirty ? Ui.Select : Ui.Good;
        _saveText.Text = !has ? "No book open"
            : _state.Dirty ? "Changes not saved yet"
            : _state.SavedAt is DateTime t ? $"All changes saved at {t:HH:mm}" : "No changes";
        ToolTip.SetTip(_saveText, has && _state.Dirty ? "Ctrl+S saves. Until then a safety copy is kept every 15 seconds, so nothing is lost if the computer stops." : null);
        _zoomBar.IsVisible = has && _state.Page is not null;
        if (!has) { _where.Text = ""; _zoomText.Text = ""; return; }
        var parts = new List<string>();
        Book book = _state.Book;
        if (_state.Page is Page p)
        {
            parts.Add($"Page {_state.PageIndex + 1} of {book.Pages.Count}");
            parts.Add(p.Explore ? $"explore page, {p.Hotspots.Count} to find" : $"{p.Stops.Count} stop{(p.Stops.Count == 1 ? "" : "s")}");
            string? sel = _state.Selected switch
            {
                Sel.Stop => $"stop {_state.SelIndex + 1} selected",
                Sel.Hotspot => $"thing to find {_state.SelIndex + 1} selected",
                Sel.Layer => $"picture on top {_state.SelIndex + 1} selected",
                Sel.Dry => "dry area selected",
                Sel.Words => "printed words selected",
                _ => null,
            };
            if (sel is not null) parts.Add(sel);
        }
        else parts.Add("No pages yet");
        _where.Text = string.Join("  ·  ", parts);
        _zoomText.Text = _state.Page is null ? "" : $"{Math.Round(_canvas.ActualScale * 100):0}%";
    }

    // ---------------------------------------------------------------- above the page

    void RefreshCanvasBar()
    {
        var left = new DockPanel { VerticalAlignment = VerticalAlignment.Center, LastChildFill = true };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        if (_state.Page is Page p)
        {
            if (p.Explore)
            {
                left.Children.Add(Ui.Label("Dragging draws", 12.5, Ui.Muted, wrap: false));
                left.Children.Add(Ui.Segments(new List<(string, string, string)>
                {
                    ("stop", "Intro stops", "Stops the camera visits before the child explores"),
                    ("hot", "Things to find", "Things the child taps to find"),
                }, _state.Drawing == DrawKind.Hotspot ? "hot" : "stop", v => { _state.Drawing = v == "hot" ? DrawKind.Hotspot : DrawKind.Stop; RefreshCanvasBar(); }));
            }
            else if (_state.Drawing == DrawKind.Dry)
                left.Children.Add(Ui.Chip("Drag on the picture to mark an area the weather misses. Esc stops.", Ui.WarnWash, Ui.Warn));
            else
                left.Children.Add(Ui.Label(p.Stops.Count == 0 ? "Drag on the picture to draw the first stop." : "Drag on the picture to draw a stop. Click a box to change it.", 12.5, Ui.Muted, wrap: false));

            if (_state.Aspect(p) >= 1.2 && !p.Explore)
                right.Children.Add(Ui.Check("Middle of the spread", _canvas.Overlays.Crease, SetCrease, "Show the crease of a double-page spread and its left and right pages"));
            var screens = new Button { Content = ScreensLabel(), FontSize = 12.5, Padding = new Thickness(10, 3), MinHeight = 26, CornerRadius = new CornerRadius(7) };
            ToolTip.SetTip(screens, "Around the selected stop, a frame shows what each chosen screen will see. Upright phones show one page of a spread at a time.");
            var flyout = new MenuFlyout();
            foreach (var (id, text) in ScreenChoices)
            {
                var m = new MenuItem { Header = text, ToggleType = MenuItemToggleType.CheckBox, IsChecked = _canvas.Overlays.Frames.Contains(id) };
                m.Click += (_, _) => ToggleFrame(id);
                flyout.Items.Add(m);
            }
            screens.Flyout = flyout;
            right.Children.Add(screens);
        }
        foreach (Control c in left.Children) { c.VerticalAlignment = VerticalAlignment.Center; if (c != left.Children[^1]) { DockPanel.SetDock(c, Dock.Left); c.Margin = new Thickness(0, 0, 8, 0); } }
        var dock = new DockPanel();
        DockPanel.SetDock(right, Dock.Right);
        right.Margin = new Thickness(12, 0, 0, 0);
        dock.Children.Add(right);
        dock.Children.Add(left);
        _canvasBar.Child = dock;
        _canvasBar.IsVisible = _state.Page is not null;
        _timeline.IsVisible = _state.Page is not null;
    }

    string ScreensLabel()
    {
        var on = ScreenChoices.Where(c => _canvas.Overlays.Frames.Contains(c.Id)).Select(c => c.Text).ToList();
        return "Screens: " + (on.Count == 0 ? "none" : on.Count <= 2 ? string.Join(", ", on) : $"{on.Count} shown") + "  ▾";
    }

    // ---------------------------------------------------------------- messages

    /// <summary>A short message at the bottom of the window that goes away by itself.</summary>
    public void Say(string text, double seconds = 5) => Toast(text, seconds, problem: false);

    /// <summary>Something went wrong: the same message, in the warning colour, left up longer.</summary>
    public void Problem(string text, double seconds = 10) => Toast(text, seconds, problem: true);

    void Toast(string text, double seconds, bool problem)
    {
        _toastText.Text = text;
        _toast.Background = problem ? Ui.Bad : Ui.AccentDeep;
        _toast.IsVisible = true;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer(TimeSpan.FromSeconds(Math.Max(seconds, 2 + text.Length / 25.0)), DispatcherPriority.Normal, (_, _) => { _toast.IsVisible = false; _toastTimer?.Stop(); });
        _toastTimer.Start();
    }

    // ---------------------------------------------------------------- sheets: help, about

    /// <summary>A sheet over the window: a titled card with a Close button, on a dimmed room.</summary>
    static Control Sheet(string title, Control body, Action close, double width, bool stretch = false)
    {
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var x = Ui.Button("Close", close, "Esc", small: true);
        DockPanel.SetDock(x, Dock.Right);
        head.Children.Add(x);
        head.Children.Add(Ui.Title(title));
        var inner = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        inner.Children.Add(head);
        inner.Children.Add(body);
        return new Border
        {
            Background = Ui.Scrim,
            Child = new Border
            {
                MaxWidth = width, Background = Ui.Card, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(24, 20),
                Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = stretch ? VerticalAlignment.Stretch : VerticalAlignment.Center,
                BoxShadow = Ui.Lifted, Child = inner,
            },
        };
    }

    readonly WrapPanel _tourButtons = new() { Margin = new Thickness(0, 0, 0, 8) };

    void ShowHelp()
    {
        if (!_help.IsVisible) FillTourButtons();
        _help.IsVisible = !_help.IsVisible;
    }

    /// <summary>The walkthrough buttons at the top of the help, which change with how far the walkthrough has got.</summary>
    void FillTourButtons()
    {
        _tourButtons.Children.Clear();
        int at = Tour.SavedStep;
        if (at > 0 && at < Tour.Count)
        {
            _tourButtons.Children.Add(Ui.Button($"Carry on the walkthrough (step {at + 1} of {Tour.Count})", () => { _help.IsVisible = false; Tour.Start(at); }, primary: true));
            _tourButtons.Children.Add(Ui.Button("Start it again", () => { _help.IsVisible = false; Tour.Start(0); }));
        }
        else _tourButtons.Children.Add(Ui.Button("Show me step by step how to make a book", () => { _help.IsVisible = false; Tour.Start(0); }, "A short walk through making a first book, pointing at each thing to click", primary: true));
        _tourButtons.Children.Add(Ui.Button("Show the book file", ShowBookFile, "The book's book.json as text, for sorting out problems"));
        foreach (Control c in _tourButtons.Children) c.Margin = new Thickness(0, 0, 8, 8);
    }

    Control HelpSheet()
    {
        // The shortcuts in groups, two columns of groups when there is room.
        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,24,*") };
        var col = new[] { new StackPanel { Spacing = 0 }, new StackPanel { Spacing = 0 } };
        string[] groups = Shortcuts.All.Select(i => i.Group).Distinct().ToArray();
        int half = (Shortcuts.All.Length + 1) / 2, count = 0;
        foreach (string g in groups)
        {
            StackPanel into = count < half ? col[0] : col[1];
            into.Children.Add(Ui.Heading(g));
            foreach (Shortcuts.Item i in Shortcuts.All.Where(i => i.Group == g))
            {
                count++;
                var keys = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
                string join = i.Keys.Contains(" or ") ? " or " : i.Keys.Contains(" and ") ? " and " : ", ";
                foreach (string k in i.Keys.Split(join))
                {
                    if (keys.Children.Count > 0) keys.Children.Add(new TextBlock { Text = join.Trim(), FontSize = 11.5, Foreground = Ui.Faint, Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center });
                    keys.Children.Add(Ui.Key(k));
                }
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*"), Margin = new Thickness(0, 3) };
                row.Children.Add(keys);
                var what = Ui.Label(i.What, 12.5, Ui.Text);
                what.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(what, 1);
                row.Children.Add(what);
                into.Children.Add(row);
            }
        }
        columns.Children.Add(col[0]);
        Grid.SetColumn(col[1], 2);
        columns.Children.Add(col[1]);
        var body = new StackPanel
        {
            Children =
            {
                _tourButtons,
                columns,
                Ui.Heading("How a book is made"),
                Ui.Help("1. Add the page pictures. A picture can be a whole spread, or add a left and a right page and the editor joins them into one spread picture (it remembers the two halves, so either can be replaced later)."),
                Ui.Help("2. On each page, drag to draw where the camera stops, in reading order. Type the words for each stop. Add a pan, effects, pictures on top or sounds if the page wants them."),
                Ui.Help("3. Record or add the voice reading each stop's words, then press \"Time words automatically\" so they light up as they are read. Click a word to nudge it, or Tap along to time them by hand. Things to find can have a voice too."),
                Ui.Help("4. Press Play to see it in the real reader, on a phone or a computer. Check book lists anything that will go wrong. Export makes the copy for the apps, and a .picturebook file to send to a phone: open it there and the " + Brand.Name + " app adds it to the shelf."),
                Ui.Help("Changes are kept in a safety copy every few seconds, so if the computer stops before you save, the book offers them back next time."),
            },
        };
        return Sheet("Keyboard shortcuts and tips", new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 12, 0) }, ShowHelp, 860, stretch: true);
    }

    void ShowAbout()
    {
        _help.IsVisible = false;
        _about.IsVisible = true;
    }

    Control AboutSheet()
    {
        var body = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 16,
                    Children =
                    {
                        new Image { Source = AppIconPicture(96), Width = 72, Height = 72, VerticalAlignment = VerticalAlignment.Center },
                        new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new Wordmark { Size = 30, Ink = Ui.AccentDeep }, new TextBlock { Text = EditorBrand.Product, FontSize = 18, FontWeight = FontWeight.ExtraBold, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 5) } } },
                                Ui.Label("Version " + Brand.Version, 12.5, Ui.Muted),
                            },
                        },
                    },
                },
                new Border { Height = 4 },
                Ui.Label("For making picture books for the " + Brand.Name + " reader: pictures, camera stops, things to find, narration with words that light up, printed words, moving effects, and a book file to send to a phone.", 13, Ui.Text),
                Ui.Help("Nunito and Fraunces are open fonts under the SIL Open Font License; the licences are in the Licences folder beside the program. Word timing uses Whisper.net (MIT licence) and runs on this computer: recordings never leave it."),
                Ui.Help("The editor's own settings, recent books and safety copies are kept in " + EditorPaths.AppData + "."),
            },
        };
        return Sheet("About", body, () => _about.IsVisible = false, 540);
    }

    /// <summary>The editor's icon as a picture, for the start screen and About.</summary>
    static Avalonia.Media.Imaging.Bitmap? AppIconPicture(int pixels)
    {
        try { return AppIcon.Render(pixels, editor: true); }
        catch (Exception) { return null; }
    }

    // ---------------------------------------------------------------- the window's place, between runs

    void PlaceWindow()
    {
        try
        {
            IReadOnlyList<Avalonia.Platform.Screen> screens = Screens.All;
            if (screens.Count == 0) return;
            // A little off each work area's height leaves room for the title bar, which is outside the size kept.
            PixelBox Area(Avalonia.Platform.Screen sc) => new(sc.WorkingArea.X, sc.WorkingArea.Y, sc.WorkingArea.Width, Math.Max(100, sc.WorkingArea.Height - (int)(32 * sc.Scaling)));
            var areas = screens.Select(Area).ToList();
            Avalonia.Platform.Screen first = Screens.Primary ?? screens[0];
            if (WindowPlacement.Fit(Settings.Window, areas, (int)(SmallestWidth * first.Scaling), (int)(SmallestHeight * first.Scaling)) is PixelBox b)
            {
                Avalonia.Platform.Screen on = screens.OrderByDescending(sc => Area(sc).Overlap(b)).First();
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new PixelPoint(b.X, b.Y);
                Width = Math.Max(SmallestWidth, b.Width / on.Scaling);
                Height = Math.Max(SmallestHeight, b.Height / on.Scaling);
                _normalBox = b;
                if (Settings.Maximized) WindowState = WindowState.Maximized;
            }
            else
            {
                var (w, h) = WindowPlacement.FirstSize(first.WorkingArea.Width / first.Scaling, first.WorkingArea.Height / first.Scaling - 32, SmallestWidth, SmallestHeight);
                Width = w;
                Height = h;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
        catch (Exception) { }
        PositionChanged += (_, _) => NoteNormal();
        SizeChanged += (_, _) => NoteNormal();
    }

    /// <summary>The window's place while it is neither maximised nor minimised, so un-maximising next time comes back there.</summary>
    void NoteNormal()
    {
        if (WindowState != WindowState.Normal || !IsVisible) return;
        double k = RenderScaling;
        _normalBox = new PixelBox(Position.X, Position.Y, (int)Math.Round(ClientSize.Width * k), (int)Math.Round(ClientSize.Height * k));
    }

    void RememberWindow()
    {
        NoteNormal();
        if (_normalBox is PixelBox b) Settings.Window = b;
        Settings.Maximized = WindowState == WindowState.Maximized;
        Settings.Save();
    }

    // ---------------------------------------------------------------- closing a book, opening a book file

    public async Task CloseBook()
    {
        if (!_state.HasBook || !await OkToLeave()) return;
        _preview.Stop();
        _preview.IsVisible = false;
        _check.IsVisible = false;
        _export.IsVisible = false;
        _state.Close();
        SetTitleBox();
    }

    public async Task OpenBookFileDialog()
    {
        if (!await OkToLeave()) return;
        string? file = await PickOneFile("Open a book file", new[] { "*" + BookPackage.Extension, "*.zip" }, "Book files");
        if (file is not null) await OpenBookFile(file, askFirst: false);
    }

    /// <summary>
    /// Open a .picturebook (made by Export, perhaps on another computer): it is unpacked into a new
    /// folder of its own in the books folder, and that folder is opened.
    /// </summary>
    public async Task OpenBookFile(string file, bool askFirst = true)
    {
        if (askFirst && !await OkToLeave()) return;
        string parent = EditorPaths.DefaultBooksFolder;
        BookFileOpener.Outcome r = BookFileOpener.Unpack(file, parent);
        if (!r.Ok || r.Folder is null) { Problem(r.Message); return; }
        await OpenBook(r.Folder, askFirst: false);
        Say(r.Message, 10);
    }
}
