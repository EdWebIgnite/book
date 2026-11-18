using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Editor.Core;
using Reader.Core;
using Reader.Ui;

namespace BookEditor;

/// <summary>The start screen (no book open) and the empty book's first step.</summary>
sealed partial class MainWindow
{
    /// <summary>The start screen's "Start a new book", for the walkthrough to point at.</summary>
    public Button? WelcomeNew { get; private set; }

    // On the very first run, the start screen offers the walkthrough.
    readonly Border _offer = new() { IsVisible = false };
    readonly StackPanel _shelves = new() { Spacing = 0 };
    bool _welcomeWaiting;

    /// <summary>The first run with a book given to open: ask about the walkthrough in a question instead.</summary>
    public async Task AskAboutTour()
    {
        int a = await _dialogs.Ask("New to the " + EditorBrand.Product + "?", "A short walkthrough shows you how to make a book, one step at a time. You can stop it whenever you like and carry on later from Help.",
            new[] { "Show me how", "No thanks" }, 0, 1);
        if (a == 0) Tour.Start(0); else Tour.Decline();
    }

    public void OfferTour()
    {
        _offer.Child = Ui.Note(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Ui.Label("New to the " + EditorBrand.Product + "?", 14, Ui.AccentDeep, FontWeight.ExtraBold),
                Ui.Label("A short walkthrough shows you how to make your first book, one step at a time: pictures, stops, words, a voice, and sending it to a phone. Stop it whenever you like and carry on later from Help.", 12.5, Ui.Text),
                Ui.Row(Ui.Button("Show me how", () => { _offer.IsVisible = false; Tour.Start(0); }, primary: true, small: true), Ui.Button("No thanks", () => { _offer.IsVisible = false; Tour.Decline(); }, small: true)),
            },
        });
        _offer.Margin = new Thickness(0, 16, 0, 0);
        _offer.IsVisible = true;
    }

    Control Welcome()
    {
        Button Wide(Button b)
        {
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.MinHeight = 40;
            b.FontSize = 14;
            return b;
        }
        var actions = new StackPanel
        {
            Spacing = 8, Margin = new Thickness(0, 24, 0, 0),
            Children =
            {
                Wide(WelcomeNew = Ui.IconButton(EditorIcons.New, "Start a new book", () => _ = NewBook(), "Make a new book folder and add its pictures (Ctrl+N)", primary: true, iconSize: 18)),
                Wide(Ui.IconButton(EditorIcons.Open, "Open a book folder...", () => _ = OpenFolderDialog(), "A book's folder, or a folder of page pictures to start a book from (Ctrl+O)", iconSize: 18)),
                Wide(Ui.IconButton(EditorIcons.BookFile, "Open a book file (.picturebook)...", () => _ = OpenBookFileDialog(), "A book someone exported and sent you. It is unpacked into a folder of its own to edit (Ctrl+Shift+O)", iconSize: 18)),
            },
        };
        var left = new StackPanel
        {
            Width = 320, VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                new Dassie { Look = Dassie.Pose.Sit, Width = 84, Height = 84, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-4, 0, 0, 8) },
                Ui.Display("Make a picture book", 32),
                new Border { Height = 8 },
                Ui.Label("Each book is a folder holding its pictures, sounds and a book.json file. Start a new one, open one you have, or open a book file someone sent you.", 13.5, Ui.Muted),
                actions,
                _offer,
                new Border { Height = 16 },
                Ui.Help("You can also drop a book folder or a .picturebook file anywhere on this window. Press F1 for the keyboard shortcuts."),
            },
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,48,*"), MaxWidth = 1120, Margin = new Thickness(40, 40, 40, 32),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        grid.Children.Add(left);
        Grid.SetColumn(_shelves, 2);
        grid.Children.Add(_shelves);
        var root = new Border { Background = Ui.Bg, Child = new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } };
        root.AttachedToVisualTree += (_, _) => FillWelcome();
        root.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty && root.IsVisible) FillWelcome(); };
        _images.Arrived += () => { if (_welcomeWaiting && root.IsVisible) FillWelcome(); };
        return root;
    }

    void FillWelcome()
    {
        _shelves.Children.Clear();
        _welcomeWaiting = false;
        var recent = _recent.Existing();
        _shelves.Children.Add(ShelfHeading("Recent books", recent.Count > 0 ? "Click one to carry on with it" : null));
        if (recent.Count == 0)
            _shelves.Children.Add(Ui.CardBox(Ui.Label("Books you open or start are listed here, newest first, so you can carry on where you left off.", 13, Ui.Muted)));
        else _shelves.Children.Add(Cards(recent.Take(9)));
        string? content = BookLibrary.FindContent(null, AppContext.BaseDirectory, Environment.CurrentDirectory);
        if (content is not null)
        {
            var samples = new List<string>();
            try { samples = BookLibrary.List(content).Select(b => b.Folder).Where(f => !recent.Contains(Path.GetFullPath(f))).ToList(); } catch (Exception) { }
            if (samples.Count > 0)
            {
                _shelves.Children.Add(ShelfHeading("Books in the reader's content folder", content));
                _shelves.Children.Add(Cards(samples));
            }
        }
    }

    static Control ShelfHeading(string title, string? note)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 12) };
        row.Children.Add(Ui.Label(title, 15, Ui.AccentDeep, FontWeight.ExtraBold, wrap: false));
        if (note is not null)
        {
            var n = Ui.Label(note, 12, Ui.Faint, wrap: false);
            n.VerticalAlignment = VerticalAlignment.Bottom;
            n.Margin = new Thickness(0, 0, 0, 2);
            n.MaxWidth = 420;
            row.Children.Add(n);
        }
        return row;
    }

    Control Cards(IEnumerable<string> folders)
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16), ItemSpacing = 12, LineSpacing = 12 };
        foreach (string f in folders) wrap.Children.Add(BookCard(f));
        return wrap;
    }

    /// <summary>A book on the start screen: its cover, title, folder and when it was last changed.</summary>
    Control BookCard(string folder)
    {
        string title = Path.GetFileName(folder);
        string? cover = null;
        try
        {
            ShelfEntry e = BookLibrary.Describe(folder);
            title = e.Title;
            cover = File.Exists(e.CoverPath) ? e.CoverPath : null;
        }
        catch (Exception) { }
        if (cover is null)
        {
            try { cover = BookJson.Load(folder).Pages.Select(p => Path.Combine(folder, p.Image)).FirstOrDefault(File.Exists); } catch (Exception) { }
        }
        var picture = new Border { Height = 112, Background = Ui.Raised, CornerRadius = new CornerRadius(6, 6, 0, 0), ClipToBounds = true };
        if (cover is not null)
        {
            var bmp = _images.Get(cover, 400);
            if (bmp is null) _welcomeWaiting = true;
            picture.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        }
        else picture.Child = new Dassie { Look = Dassie.Pose.Peek, Width = 72, Height = 52, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        string changed = "";
        try { DateTime t = File.GetLastWriteTime(Path.Combine(folder, "book.json")); if (t.Year > 2000) changed = "Changed " + t.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture); } catch (Exception) { }
        var name = Ui.Label(title, 13.5, Ui.Text, FontWeight.ExtraBold);
        name.MaxLines = 2;
        name.Height = 38;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var where = Ui.Label(folder, 11.5, Ui.Faint, wrap: false);
        where.TextTrimming = TextTrimming.PrefixCharacterEllipsis;
        var words = new StackPanel { Spacing = 2, Margin = new Thickness(12, 8, 12, 12), Children = { name, where } };
        if (changed.Length > 0) words.Children.Add(Ui.Label(changed, 11.5, Ui.Muted, wrap: false));
        var b = new Button
        {
            Width = 208, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(8), HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top, Background = Ui.Card, BorderBrush = Ui.Line, BorderThickness = new Thickness(1),
            Content = new StackPanel { Children = { picture, words } },
        };
        b.Classes.Add("card");
        ToolTip.SetTip(b, "Open " + folder);
        b.Click += (_, _) => _ = OpenBook(folder);
        return b;
    }

    /// <summary>What an empty book shows over the page: the first step, with the buttons for it.</summary>
    Control EmptyBook()
    {
        var add = Ui.IconButton(Reader.Ui.Icons.Plus, "Add page pictures...", AddPictures, "Choose the page pictures. A picture can be a whole double-page spread.", primary: true);
        var pair = Ui.IconButton(Reader.Ui.Icons.Plus, "Add a left and a right page...", AddLeftRight, "Pick a left-hand page and a right-hand page: they are joined into one spread.");
        return new Border
        {
            IsVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 480, Margin = new Thickness(24),
            Background = Ui.Card, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(28, 24), BoxShadow = Ui.Lifted,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new Dassie { Look = Dassie.Pose.Peek, Width = 80, Height = 58, HorizontalAlignment = HorizontalAlignment.Left },
                    Ui.Display("Add the page pictures", 24),
                    Ui.Label("Each picture becomes a page. A picture can be a whole double-page spread, or add a left and a right page and they are joined into one spread.", 13, Ui.Muted),
                    Ui.Row(add, pair),
                    Ui.Help("Or drag picture files from a folder onto this window. Pictures named as pairs (page3-left.png and page3-right.png) are joined by themselves."),
                },
            },
        };
    }
}
