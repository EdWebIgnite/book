using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Reader.Core;
using Reader.Ui;

namespace Reader.App;

/// <summary>
/// The first-run welcome, for the parent: three short pages (what it is, how a child uses it, the
/// grown-ups' area and privacy), each one skippable, then the first child's name and picture, then
/// the shelf. The language can be chosen at the top of every page. On a phone the pages fill the
/// screen; on a tablet or a computer they are a card in the middle.
/// </summary>
public sealed class WelcomeView : UserControl
{
    public const int InfoPages = 3;

    readonly IReadOnlyList<Bitmap?> _covers;
    readonly bool _makeChild;
    readonly string _age;
    Thickness _safe;
    ProfileEditor? _editor;
    bool _editorSide;

    /// <param name="covers">Up to three covers to show on the first page.</param>
    /// <param name="makeChild">Whether the welcome ends by making the first child (not when shown again from the grown-ups' area).</param>
    public WelcomeView(IReadOnlyList<Bitmap?> covers, bool makeChild, string age, int page = 0)
    {
        _covers = covers;
        _makeChild = makeChild;
        _age = age;
        Page = page;
        FontFamily = Palette.Font;
        Background = Palette.Brush(Palette.Wall);
        Focusable = true;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        PointerPressed += (_, e) => e.Handled = true;
        Build(new Size(1280, 800));
    }

    /// <summary>0 to 2 for the three pages, 3 for making the first child.</summary>
    public int Page { get; private set; }

    /// <summary>The language was changed at the top: the app builds the welcome again in it.</summary>
    public event Action<string>? LanguageChosen;
    /// <summary>The welcome is over. With a child made, their name, picture and age band.</summary>
    public event Action<(string Name, int Avatar, string Age)?>? Finished;

    public Thickness SafeArea { set { _safe = value; _built = default; Build(Bounds.Size); } }

    /// <summary>Back: the page before. False on the first page.</summary>
    public bool GoBack()
    {
        if (Page == 0) return false;
        Go(Page - 1);
        return true;
    }

    void Go(int page)
    {
        Page = page;
        _built = default;
        Build(Bounds.Size);
    }

    void Next()
    {
        if (Page < InfoPages - 1) { Go(Page + 1); return; }
        if (Page == InfoPages - 1 && _makeChild) { Go(InfoPages); return; }
        Done();
    }

    void Done()
    {
        if (Page == InfoPages && _editor is not null) Finished?.Invoke((_editor.ChildName, _editor.Picture, _editor.Age));
        else Finished?.Invoke(null);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Build(e.NewSize);
    }

    Size _built;

    void Build(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0 || size == _built) return;
        _built = size;
        double w = size.Width, h = size.Height;
        bool side = w > h * 1.15 && h < 640;
        bool card = !side && Math.Min(w, h) >= 600;
        bool small = Math.Min(w, h) < 600;

        // The language, at the top of every page.
        var language = new Choices(Strings.T("parents.language"), Strings.Languages.Select(l => (l.Code, l.Name)), Strings.Language, height: 44)
        {
            Width = 320, HorizontalAlignment = HorizontalAlignment.Center,
        };
        language.Changed += code => LanguageChosen?.Invoke(code);

        Control art = Page switch
        {
            0 => Covers(side ? Math.Min(240, h * 0.5) : small ? Math.Min(200, h * 0.24) : Math.Min(200, h * 0.25)),
            InfoPages => new Border(),
            _ => new Dassie { Look = Dassie.Pose.Sit, Width = small ? 88 : 104, Height = small ? 88 : 104, HorizontalAlignment = HorizontalAlignment.Center },
        };

        var text = new StackPanel { Spacing = side ? 8 : 12 };
        switch (Page)
        {
            case 0:
            {
                var mark = new Wordmark { Size = small ? 46 : 50, HorizontalAlignment = HorizontalAlignment.Center };
                var tag = Kit.Heading(Strings.T("brand.tagline"), small ? 24 : 26);
                tag.TextAlignment = TextAlignment.Center;
                var body = Kit.Text(Strings.T("welcome.1.body"), 16, FontWeight.Normal, Palette.InkSoftBrush);
                body.TextAlignment = TextAlignment.Center;
                text.Children.Add(mark);
                text.Children.Add(tag);
                text.Children.Add(body);
                break;
            }
            case 1:
            case 2:
            {
                string p = Page == 1 ? "welcome.2" : "welcome.3";
                var title = Kit.Heading(Strings.T(p + ".title"), side ? 24 : small ? 28 : 34);
                title.TextAlignment = side ? TextAlignment.Left : TextAlignment.Center;
                text.Children.Add(title);
                string[] icons = Page == 1 ? new[] { Icons.Hand, Icons.Books, Icons.Find } : new[] { Icons.Shield, Icons.Lock, Icons.Device };
                string[] keys = { ".a", ".b", ".c" };
                for (int i = 0; i < 3; i++) text.Children.Add(Promise(icons[i], Strings.T(p + keys[i]), small, side));
                break;
            }
            default:
            {
                var title = Kit.Heading(Strings.T("profile.new"), side ? 24 : small ? 28 : 34);
                text.Children.Add(title);
                if (_editor is not null && _editorSide != side) _editor = new ProfileEditor(_editor.ChildName, _editor.Picture, _editor.Age, compact: small, wide: side);
                _editor ??= new ProfileEditor("", 0, _age, compact: small, wide: side);
                _editorSide = side;
                if (_editor.Parent is Panel old) old.Children.Remove(_editor);
                text.Children.Add(_editor);
                break;
            }
        }

        // Where we are, the main button and Skip.
        var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        int count = _makeChild ? InfoPages + 1 : InfoPages;
        for (int i = 0; i < count; i++)
            dots.Children.Add(new Border { Width = i == Page ? 26 : 10, Height = 10, CornerRadius = new CornerRadius(5), Background = i == Page ? Palette.LeafBrush : Palette.Brush(Palette.InkFaint, 0.5) });
        string mainText = Page < InfoPages - 1 ? Strings.T("common.next")
            : Page == InfoPages - 1 ? (_makeChild ? Strings.T("welcome.start") : Strings.T("common.done"))
            : Strings.T("profile.start");
        var main = new Pill(mainText, PillKind.Main, Page >= InfoPages - 1 ? Icons.Play : Icons.Forward, height: side ? 58 : 64, fontSize: side ? 19 : 21, iconFilled: Page >= InfoPages - 1)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch, MaxWidth = 420,
        };
        main.Click += Next;
        Pill? skip = null;
        if (Page < InfoPages)
        {
            skip = new Pill(Strings.T("welcome.skip"), PillKind.Link, height: 48, fontSize: 17) { HorizontalAlignment = HorizontalAlignment.Center };
            skip.Click += () => { if (_makeChild) Go(InfoPages); else Done(); };
        }
        Control actions;
        if (side)
        {
            // Low screens: Skip beside the main button, to leave the height for the words.
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 16 };
            if (skip is not null) row.Children.Add(skip);
            Grid.SetColumn(main, 1);
            row.Children.Add(main);
            actions = new StackPanel { Spacing = 8, Children = { dots, row } };
        }
        else
        {
            var stack = new StackPanel { Spacing = 10, Children = { dots, main } };
            stack.Children.Add(skip is not null ? skip : new Border { Height = 8 });
            actions = stack;
        }

        Control content;
        if (side)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(Page == 0 ? "0.9*,*" : Page < InfoPages ? "0.4*,*" : "*"), ColumnSpacing = 32 };
            var right = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 10 };
            if (Page < InfoPages) right.Children.Add(language);
            var scroll = new ScrollViewer { Content = text, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(scroll, 1);
            right.Children.Add(scroll);
            Grid.SetRow(actions, 2);
            right.Children.Add(actions);
            if (Page < InfoPages)
            {
                art.VerticalAlignment = VerticalAlignment.Center;
                grid.Children.Add(art);
                Grid.SetColumn(right, 1);
            }
            grid.Children.Add(right);
            right.MaxWidth = 620;
            content = grid;
        }
        else
        {
            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = small ? 12 : 20 };
            grid.Children.Add(language);
            var middle = new StackPanel { Spacing = small ? 16 : 24, VerticalAlignment = VerticalAlignment.Center, Children = { art, text } };
            var scroll = new ScrollViewer { Content = middle, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);
            Grid.SetRow(actions, 2);
            grid.Children.Add(actions);
            content = grid;
        }

        if (card)
        {
            Content = new Border
            {
                Width = Math.Min(600, w - 48), Height = Math.Min(h - 48, 760), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Background = Palette.CreamBrush, CornerRadius = new CornerRadius(30), Padding = new Thickness(36, 28, 36, 24),
                BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 10, Blur = 40, Color = Palette.ShadowOf(Palette.Night ? 0.5 : 0.16) }),
                Child = content,
            };
        }
        else
        {
            content.Margin = new Thickness(20 + _safe.Left, 14 + _safe.Top, 20 + _safe.Right, 18 + _safe.Bottom);
            Content = content;
        }
        main.Focus(NavigationMethod.Pointer);
    }

    /// <summary>One promise: a round icon and a sentence.</summary>
    static Control Promise(string icon, string text, bool small, bool side = false)
    {
        if (side) small = true;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 16 };
        grid.Children.Add(new Border
        {
            Width = side ? 42 : small ? 52 : 60, Height = side ? 42 : small ? 52 : 60, CornerRadius = new CornerRadius(14), Background = Palette.MintBrush, VerticalAlignment = VerticalAlignment.Top,
            Child = Kit.Icon(icon, side ? 22 : small ? 26 : 30, Palette.LeafBrush),
        });
        var t = Kit.Text(text, side ? 16 : small ? 17 : 19, FontWeight.SemiBold);
        t.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(t, 1);
        grid.Children.Add(t);
        return grid;
    }

    /// <summary>Three covers fanned out, with the dassie sitting at their foot.</summary>
    Control Covers(double height)
    {
        double cw = height / 1.5 * 0.9;
        var panel = new Panel { Height = height + 14, Width = cw * 2.6 + 40, HorizontalAlignment = HorizontalAlignment.Center };
        double[] angles = { -9, 0, 9 };
        double[] xs = { -cw * 0.78, 0, cw * 0.78 };
        int[] order = { 0, 2, 1 };
        foreach (int i in order)
        {
            Bitmap? bmp = i < _covers.Count ? _covers[i] : null;
            var cover = new Border
            {
                Width = cw, Height = cw * 1.5, CornerRadius = new CornerRadius(3, 10, 10, 3), ClipToBounds = true,
                Background = Palette.LeafBrush,
                Child = bmp is null ? null : new Image { Source = bmp, Stretch = Stretch.UniformToFill },
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = new TransformGroup { Children = { new RotateTransform(angles[i]), new TranslateTransform(xs[i], i == 1 ? 0 : 10) } },
            };
            var holder = new Border
            {
                Child = cover, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                BoxShadow = default,
            };
            cover.Effect = new DropShadowEffect { OffsetX = 2, OffsetY = 6, BlurRadius = 12, Color = Palette.ShadowOf(1), Opacity = 0.3 };
            panel.Children.Add(holder);
        }
        double d = height * 0.42;
        panel.Children.Add(new Dassie
        {
            Look = Dassie.Pose.Sit, Width = d, Height = d, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            RenderTransform = new TranslateTransform(cw * 1.05, 4),
        });
        return panel;
    }
}
