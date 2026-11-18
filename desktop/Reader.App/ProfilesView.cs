using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Reader.Core;
using Reader.Ui;

namespace Reader.App;

/// <summary>
/// "Who is reading?": each child's picture and name, big enough for small fingers, and "Add a child"
/// (which goes through the grown-ups' gate). Shown at the start when there is more than one child, and
/// from the child's picture on the shelf. A tap on a child goes straight to their shelf.
/// </summary>
public sealed class ProfilesView : UserControl
{
    readonly AppState _state;
    readonly bool _canClose;
    readonly Grid _root = new() { RowDefinitions = new RowDefinitions("Auto,*") };
    Thickness _safe;

    public ProfilesView(AppState state, bool canClose)
    {
        _state = state;
        _canClose = canClose;
        FontFamily = Palette.Font;
        Background = Palette.Brush(Palette.Wall);
        Content = _root;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        PointerPressed += (_, e) => e.Handled = true;
        Build(new Size(1280, 800));
    }

    public event Action<ChildProfile>? Chosen;
    public event Action? AddRequested;
    public event Action? GrownUpsRequested;
    public event Action? Closed;

    public Thickness SafeArea { set { _safe = value; Build(Bounds.Size); } }

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
        bool low = h < 560, small = Math.Min(w, h) < 600;
        _root.Children.Clear();

        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(16 + _safe.Left, 10 + _safe.Top, 16 + _safe.Right, 0), MinHeight = 56 };
        if (_canClose)
        {
            var close = new RoundTap(Strings.T("common.close"), Icons.Close, 52);
            close.Click += () => Closed?.Invoke();
            bar.Children.Add(close);
        }
        var mark = new Wordmark { Size = small ? 28 : 34, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumnSpan(mark, 3);
        bar.Children.Insert(0, mark);
        var grown = new Pill(Strings.T("shelf.grownups"), PillKind.Link, Icons.Lock, height: 48, fontSize: 15);
        grown.Label.IsVisible = !(small && !low);
        grown.Label.Foreground = Palette.InkSoftBrush;
        grown.Click += () => GrownUpsRequested?.Invoke();
        Grid.SetColumn(grown, 2);
        bar.Children.Add(grown);
        _root.Children.Add(bar);

        double d = low ? 104 : small ? 124 : 156;
        var heading = Kit.Heading(Strings.T("profile.who"), low ? 28 : small ? 34 : 44);
        heading.TextAlignment = TextAlignment.Center;
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        heading.Margin = new Thickness(0, low ? 0 : 18, 0, low ? 14 : 34);
        var people = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, ItemSpacing = small ? 22 : 40, LineSpacing = small ? 18 : 30 };
        foreach (ChildProfile child in _state.Profiles)
        {
            var pick = new Person(child, d);
            ChildProfile c = child;
            pick.Click += () => Chosen?.Invoke(c);
            people.Children.Add(pick);
        }
        if (_state.CanAdd)
        {
            var add = new AddPerson(d);
            add.Click += () => AddRequested?.Invoke();
            people.Children.Add(add);
        }
        people.MaxWidth = small && !low ? 2 * d + 40 + 24 : 1100;
        var body = new StackPanel { Children = { heading, people }, Margin = new Thickness(16 + _safe.Left, 0, 16 + _safe.Right, 24 + _safe.Bottom), VerticalAlignment = VerticalAlignment.Center };
        var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        _root.Children.Add(scroll);
    }

    /// <summary>The first child's picture, for screenshots and the keyboard.</summary>
    public void FocusFirst()
    {
        foreach (Control c in this.GetVisualDescendantsOf<Person>()) { c.Focus(NavigationMethod.Tab); return; }
    }

    /// <summary>One child: their picture, big, and their name under it.</summary>
    sealed class Person : Tap
    {
        readonly Avatar _avatar;

        public Person(ChildProfile child, double d) : base(string.IsNullOrEmpty(child.Name) ? Strings.T("profile.unnamed") : child.Name)
        {
            CornerRadius = new CornerRadius(24);
            Padding = new Thickness(8);
            _avatar = new Avatar(child.Avatar, d) { HorizontalAlignment = HorizontalAlignment.Center };
            var name = Kit.Text(string.IsNullOrEmpty(child.Name) ? " " : child.Name, d > 120 ? 24 : 21, FontWeight.ExtraBold, wrap: false);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.MaxWidth = d + 30;
            Child = new StackPanel { Spacing = 10, Children = { _avatar, name } };
            Restyle();
        }

        protected override void Restyle()
        {
            if (_avatar is null) return;
            Background = Over || Pressed ? Palette.Brush(Palette.Paper, 0.6) : Brushes.Transparent;
            _avatar.RenderTransform = Pressed ? new ScaleTransform(0.96, 0.96) : null;
        }
    }

    /// <summary>A dashed circle with a plus: add a child (a grown-up job, so it shows a small lock).</summary>
    sealed class AddPerson : Tap
    {
        public AddPerson(double d) : base(Strings.T("profile.add"))
        {
            CornerRadius = new CornerRadius(24);
            Padding = new Thickness(8);
            var ring = new Panel
            {
                Width = d, Height = d, HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new Ellipse { Stroke = Palette.Brush(Palette.InkFaint), StrokeThickness = 3, StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 3, 2.4 } },
                    Kit.Icon(Icons.Plus, d * 0.32, Palette.InkSoftBrush),
                },
            };
            var label = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center,
                Children = { Kit.Icon(Icons.Lock, 16, Palette.InkSoftBrush), Kit.Text(Strings.T("profile.add"), d > 120 ? 19 : 17, FontWeight.Bold, Palette.InkSoftBrush, wrap: false) },
            };
            Child = new StackPanel { Spacing = 12, Children = { ring, label } };
            Restyle();
        }

        protected override void Restyle() => Background = Over || Pressed ? Palette.Brush(Palette.Paper, 0.6) : Brushes.Transparent;
    }
}

/// <summary>Visual-tree helper: every descendant of a kind.</summary>
static class TreeExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOf<T>(this Visual root) where T : Visual
    {
        foreach (Visual v in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root))
            if (v is T t) yield return t;
    }
}

/// <summary>
/// A child's name, picture and age band, for adding a child or changing one. The name may be left
/// empty; it is never more than 12 letters and is kept on this device only.
/// </summary>
public sealed class ProfileEditor : UserControl
{
    readonly TextBox _name;
    readonly List<(Tap Tap, Avatar Avatar)> _pictures = new();
    readonly Choices _age;
    int _avatar;

    /// <param name="wide">Low and wide (a phone on its side): the name and age on the left, the pictures on the right.</param>
    public ProfileEditor(string name, int avatar, string age, bool compact = false, bool wide = false)
    {
        _avatar = avatar;
        FontFamily = Palette.Font;
        _name = new TextBox
        {
            Text = name, MaxLength = AppState.MaxNameLength, FontSize = 20, FontWeight = FontWeight.Bold, MinHeight = 52,
            Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(14), PlaceholderText = Strings.T("profile.name"),
        };
        Avalonia.Automation.AutomationProperties.SetName(_name, Strings.T("profile.name"));
        var nameHint = Kit.Text(Strings.T("profile.name.hint"), 14, FontWeight.Normal, Palette.InkSoftBrush);

        var grid = new UniformGrid { Columns = 4, HorizontalAlignment = HorizontalAlignment.Stretch, MaxWidth = 400 };
        double d = compact || wide ? 56 : 66;
        for (int i = 0; i < AppState.AvatarCount; i++)
        {
            int index = i;
            var av = new Avatar(i, d) { Chosen = i == avatar };
            var tap = new Tap(Strings.T("avatar." + i)) { Child = av, Padding = new Thickness(6), CornerRadius = new CornerRadius(d), HorizontalAlignment = HorizontalAlignment.Center };
            tap.Click += () => Pick(index);
            grid.Children.Add(tap);
            _pictures.Add((tap, av));
        }

        _age = new Choices(Strings.T("profile.age"), ReaderSettings.AgePresets.Keys.Select(k => (k, Strings.T("age." + k))), ReaderSettings.AgePresets.ContainsKey(age) ? age : "3-5");
        var ageHint = Kit.Text(Strings.T("profile.age.hint"), 14, FontWeight.Normal, Palette.InkSoftBrush);

        if (wide)
        {
            var left = new StackPanel { Spacing = 8, Children = { Kit.Label(Strings.T("profile.name")), _name, nameHint, Spacer(6), Kit.Label(Strings.T("profile.age")), _age } };
            var right = new StackPanel { Spacing = 8, Children = { Kit.Label(Strings.T("profile.picture")), grid } };
            var two = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 28 };
            two.Children.Add(left);
            Grid.SetColumn(right, 1);
            two.Children.Add(right);
            Content = two;
            return;
        }
        Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Kit.Label(Strings.T("profile.name")), _name, nameHint,
                Spacer(12), Kit.Label(Strings.T("profile.picture")), grid,
                Spacer(12), Kit.Label(Strings.T("profile.age")), _age, ageHint,
            },
        };
    }

    static Control Spacer(double h) => new Border { Height = h };

    void Pick(int index)
    {
        _avatar = index;
        foreach (var (_, av) in _pictures) av.Chosen = av.Index == index;
    }

    public string ChildName => AppState.CleanName(_name.Text);
    public int Picture => _avatar;
    public string Age => _age.Value;
}
