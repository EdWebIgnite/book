using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.Threading;
using Reader.Core;
using Reader.Ui;

namespace Reader.App;

/// <summary>What kind of note the shelf is showing, which sets its little badge.</summary>
public enum NoteKind { Info, Busy, Good, Problem }

/// <summary>Everything the shelf shows for the child who is reading.</summary>
public sealed record ShelfModel(
    IReadOnlyList<ShelfEntry> Books,
    Func<string, BookProgress?> Progress,
    ChildProfile? Child,
    int ChildCount,
    ShelfEntry? CarryOn);

/// <summary>
/// The bookshelf: the child's name on a brass plate, their books standing face out in a cherry
/// bookcase with a pale back, a "Carry on reading" card for the book they were last part of the way
/// through, a ribbon on books that are partly read and a tick on finished ones. Along the top: the
/// app's name, who is reading (to change child) and the grown-ups' button, which leads to the gate.
/// A cover lifts a little under the pointer or a finger and the books settle in when the shelf
/// appears. Each of those stops by itself, so a shelf that is just sitting there draws nothing.
/// </summary>
public sealed class ShelfView : UserControl
{
    readonly bool _touch;
    readonly Grid _root;
    readonly Grid _top;
    readonly NoteCard _note;
    readonly StackPanel _page;
    readonly ScrollViewer _scroll;
    readonly Border _cardAbove = new();
    Bookcase _case;
    bool _small, _upright;
    Thickness _safe;
    ShelfModel? _model;
    CarryOnCard? _card;
    string? _message;
    NoteKind _kind;
    readonly List<BookCover> _covers = new();

    /// <param name="forTouch">The phone and tablet app.</param>
    public ShelfView(bool forTouch = false)
    {
        _touch = forTouch;
        FontFamily = Palette.Font;
        _small = forTouch;
        _case = new Bookcase(_small);

        _top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 12 };
        _note = new NoteCard { IsVisible = false, Margin = new Thickness(0, 0, 0, 12), HorizontalAlignment = HorizontalAlignment.Left };
        _page = new StackPanel { Children = { _note, _cardAbove, _case } };
        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _page,
        };
        _root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(_scroll, 1);
        _root.Children.Add(_top);
        _root.Children.Add(_scroll);
        Content = new Panel { Children = { new Wall(), _root } };
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        Layout(new Size(1280, 800));
    }

    /// <summary>A book was chosen: open it (at the child's place, if they have one).</summary>
    public event Action<ShelfEntry>? BookChosen;
    /// <summary>The child's picture at the top was pressed: show who is reading.</summary>
    public event Action? ChildRequested;
    /// <summary>The grown-ups' button: the gate, then the parents' area.</summary>
    public event Action? GrownUpsRequested;
    /// <summary>"Add a book" on an empty shelf: the gate, then the parents' Books page.</summary>
    public event Action? AddBookRequested;

    /// <summary>Room kept clear for a notch, rounded corners or the system bars.</summary>
    public Thickness SafeArea
    {
        set { _safe = value; Layout(Bounds.Size); }
    }

    /// <summary>Something to tell the person at the shelf: a book that wouldn't open, a book being added.</summary>
    public void Say(string? message, NoteKind kind = NoteKind.Info)
    {
        _message = string.IsNullOrEmpty(message) ? null : message;
        _kind = kind;
        ShowNote();
    }

    void ShowNote()
    {
        // With no books at all, the words go on the sign on the empty shelf instead.
        if (_case.Empty && _kind != NoteKind.Busy && _kind != NoteKind.Good)
        {
            _note.Show(null, _kind);
            _case.EmptySign.Detail = _message;
            return;
        }
        _case.EmptySign.Detail = null;
        _note.Show(_message, _kind);
    }

    /// <summary>Fill the shelf. Covers load in the background and appear as they arrive.</summary>
    public void Show(ShelfModel model, bool coversNow = false)
    {
        _model = model;
        BuildTop();
        _covers.Clear();
        bool mixed = ShelfEntry.MixedLanguages(model.Books);
        foreach (ShelfEntry entry in model.Books)
        {
            var cover = new BookCover(entry, _touch, model.Progress(entry.Id), mixed ? Strings.LanguageTag(entry.Language) : "");
            cover.Click += () => BookChosen?.Invoke(entry);
            _covers.Add(cover);
            Pictures.Get(entry.CoverPath, 480, coversNow, b => cover.Cover = b);
        }
        _card = null;
        if (model.CarryOn is ShelfEntry carry && model.Progress(carry.Id) is BookProgress p && p.Partway)
        {
            _card = new CarryOnCard(carry, p);
            _card.Click += () => BookChosen?.Invoke(carry);
            string picture = p.Page >= 0 && p.Page < carry.PageImages.Count ? System.IO.Path.Combine(carry.Folder, carry.PageImages[p.Page]) : carry.CoverPath;
            CarryOnCard card = _card;
            Pictures.Get(picture, 640, coversNow, b => card.Picture = b ?? card.Picture);
        }
        Place();
        ShowNote();
        if (IsVisible) _case.SettleIn();
    }

    /// <summary>The words on the brass plate: "Lia's books", or "My books".</summary>
    string Plate => Strings.BooksOf(_model?.Child?.Name);

    void Place()
    {
        _case.SetBooks(_covers);
        _case.Plate = Plate;
        _case.EmptySign.ShowButton(() => AddBookRequested?.Invoke());
        _cardAbove.Child = null;
        _case.SetCard(null);
        if (_card is null) { _cardAbove.IsVisible = false; return; }
        _card.Strip = _upright;
        if (_upright)
        {
            _card.Height = _small ? 124 : 190;
            _cardAbove.Child = _card;
            _cardAbove.IsVisible = true;
            _cardAbove.Margin = new Thickness(_small ? 2 : 8, 0, _small ? 2 : 8, _small ? 14 : 22);
        }
        else
        {
            _card.Height = double.NaN;
            _cardAbove.IsVisible = false;
            _case.SetCard(_card);
        }
    }

    // ---------------------------------------------------------------- the bar along the top

    void BuildTop()
    {
        _top.Children.Clear();
        bool low = Bounds.Height > 0 && Bounds.Height < 520;
        var mark = new Wordmark { Size = _small ? (low ? 28 : 32) : 42, VerticalAlignment = VerticalAlignment.Center };
        _top.Children.Add(mark);

        ChildProfile? child = _model?.Child;
        if (child is not null)
        {
            var chip = new Chip(child, _model!.ChildCount > 1, _small);
            chip.Click += () => ChildRequested?.Invoke();
            Grid.SetColumn(chip, 1);
            _top.Children.Add(chip);
        }
        bool words = !(_small && _upright);
        var grown = new GrownUpsButton(words, _small);
        grown.Click += () => GrownUpsRequested?.Invoke();
        Grid.SetColumn(grown, 2);
        _top.Children.Add(grown);
    }

    // ---------------------------------------------------------------- sizes

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Layout(e.NewSize);
    }

    void Layout(Size size)
    {
        double w = size.Width > 0 ? size.Width : 1280, h = size.Height > 0 ? size.Height : 800;
        // Phone sizes on a phone, and on a computer whose window is small.
        bool small = Math.Min(w, h) < 600;
        bool upright = h > w * 1.05;
        bool low = h < 520;
        if (small != _small)
        {
            _small = small;
            var fresh = new Bookcase(small);
            _page.Children.Remove(_case);
            _case = fresh;
            _page.Children.Add(_case);
        }
        bool rebuildTop = upright != _upright || _top.Children.Count == 0;
        _upright = upright;
        double edge = small ? 14 : w < 900 ? 24 : 44;
        double topH = small ? (low ? 54 : 64) : 84;
        _top.Height = topH;
        _top.Margin = new Thickness(edge + _safe.Left, (small ? 6 : 10) + _safe.Top, edge + _safe.Right, small ? 2 : 8);
        // Keep the bottom clear of the gesture bar, and on a tablet of the wrists resting on its edge.
        double bottom = (_touch && !small ? 48 : small ? 16 : 24) + _safe.Bottom;
        _page.Margin = new Thickness(edge + _safe.Left, small ? 4 : 8, edge + _safe.Right, bottom);
        _page.MaxWidth = 1600;
        _case.AllowLowShelf = !upright && !low && !small;
        _case.MaxCover = upright && !small ? 240 : double.NaN;
        double above = _top.Height + _top.Margin.Top + _top.Margin.Bottom + _page.Margin.Top + bottom;
        if (upright && _card is not null) above += (small ? 124 : 190) + (small ? 14 : 22);
        _case.ViewportHeight = Math.Max(240, h - above);
        if (_model is not null && (rebuildTop || _top.Children.Count == 0)) { BuildTop(); Place(); }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Back from a book: the books settle in again.
        if (change.Property == IsVisibleProperty && IsVisible) _case.SettleIn();
    }

    /// <summary>A book on the shelf, for screenshots.</summary>
    internal BookCover? Cover(int index) => index >= 0 && index < _covers.Count ? _covers[index] : null;

    /// <summary>Give the keyboard's focus to the first thing on the shelf (the card, or the first book).</summary>
    public void FocusFirst(bool ring = false)
    {
        Control? first = _card is not null ? _card : _covers.FirstOrDefault();
        if (first is null) return;
        first.Focus(NavigationMethod.Tab);
        if (ring && first is Tap t) t.ShowRing();
        if (ring && first is BookCover c) c.ShowRing();
    }

    // ================================================================ pictures, loaded once

    /// <summary>Covers and page pictures shrunk to shelf size, kept while the app runs so going back to the shelf is instant.</summary>
    static class Pictures
    {
        static readonly Dictionary<(string, int), Bitmap?> Cache = new();

        public static void Get(string path, int width, bool now, Action<Bitmap?> then)
        {
            var key = (path, width);
            if (Cache.TryGetValue(key, out Bitmap? known)) { then(known); return; }
            if (now) { then(Cache[key] = Load(path, width)); return; }
            Task.Run(() => Load(path, width)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                Cache[key] = t.Result;
                then(t.Result);
            }));
        }

        static Bitmap? Load(string path, int width)
        {
            try
            {
                using FileStream file = File.OpenRead(path);
                return Bitmap.DecodeToWidth(file, width, BitmapInterpolationMode.HighQuality);
            }
            catch (Exception) { return null; }   // no picture: the book keeps its plain board
        }
    }

    // ================================================================ the wall

    /// <summary>
    /// The wall behind the bookcase: plain warm cream with faint broad stripes, quiet so the covers stand
    /// out (at bedtime, night blue with a few still stars). Drawn only when the window changes size.
    /// </summary>
    sealed class Wall : Control
    {
        public Wall() => IsHitTestVisible = false;

        public override void Render(DrawingContext ctx)
        {
            var r = new Rect(Bounds.Size);
            if (r.Width <= 0 || r.Height <= 0) return;
            double w = r.Width, h = r.Height;
            ctx.FillRectangle(Palette.Brush(Palette.Wall), r);
            if (!Palette.Night)
            {
                const double repeat = 84, band = 34;
                var bandBrush = Palette.Brush(Palette.WallStripe, 0.55);
                for (double x = 18; x < w + repeat; x += repeat) ctx.FillRectangle(bandBrush, new Rect(x, 0, band, h));
                ShelfArt.Speckle(ctx, r, 0.22);
            }
            else
            {
                // A few small still stars, always in the same places.
                var rnd = new Random(7);
                for (int i = 0; i < (int)(w * h / 9000); i++)
                {
                    double x = rnd.NextDouble() * w, y = rnd.NextDouble() * h, s = 0.6 + rnd.NextDouble() * 1.2;
                    ctx.DrawEllipse(new ImmutableSolidColorBrush(Color.Parse("#fff4dc"), 0.18 + rnd.NextDouble() * 0.35), null, new Point(x, y), s, s);
                }
            }
            ctx.FillRectangle(new RadialGradientBrush
            {
                RadiusX = new RelativeScalar(0.8, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.8, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Palette.ShadowOf(0), 0.55), new GradientStop(Palette.ShadowOf(Palette.Night ? 0.35 : 0.08), 1) },
            }, r);
        }
    }

    // ================================================================ the bar's buttons

    /// <summary>Who is reading: their picture and name. Pressed, it shows everyone, to change child.</summary>
    sealed class Chip : Tap
    {
        public Chip(ChildProfile child, bool others, bool small) : base(Strings.T("profile.switch"))
        {
            ToolTip.SetTip(this, Strings.T("profile.switch"));
            Height = small ? 52 : 60;
            CornerRadius = new CornerRadius(Height / 2);
            Padding = new Thickness(5, 4, string.IsNullOrEmpty(child.Name) && !others ? 5 : 16, 4);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new Avatar(child.Avatar, Height - 10) { VerticalAlignment = VerticalAlignment.Center });
            if (!string.IsNullOrEmpty(child.Name))
            {
                var name = Kit.Text(child.Name, small ? 18 : 20, FontWeight.ExtraBold, wrap: false);
                name.VerticalAlignment = VerticalAlignment.Center;
                name.LineHeight = double.NaN;
                row.Children.Add(name);
            }
            if (others) row.Children.Add(Kit.Icon(Icons.Swap, small ? 18 : 20, Palette.InkSoftBrush));
            Child = row;
            Restyle();
        }

        protected override void Restyle()
        {
            Buttons.Look look = Buttons.Plain;
            Background = Pressed ? Buttons.PressedFace(look) : Over ? Buttons.HoverFace(look) : Buttons.Face(look);
            BorderBrush = Buttons.Edge(look);
            BorderThickness = Pressed ? Buttons.LipPressed : Buttons.Lip;
            Shadow = new BoxShadows(Pressed ? Buttons.ShadowPressed : Buttons.Shadow);
        }
    }

    /// <summary>The way into the grown-ups' area: a lock, and the word when there is room. Smaller and quieter than anything a child presses.</summary>
    sealed class GrownUpsButton : Tap
    {
        public GrownUpsButton(bool words, bool small) : base(Strings.T("shelf.grownups"))
        {
            ToolTip.SetTip(this, Strings.T("shelf.grownups"));
            Height = 48;
            MinWidth = 48;
            CornerRadius = new CornerRadius(24);
            Padding = new Thickness(words ? 14 : 0, 0, words ? 18 : 0, 0);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            row.Children.Add(Kit.Icon(Icons.Lock, 20, Palette.InkSoftBrush));
            if (words) row.Children.Add(Kit.Text(Strings.T("shelf.grownups"), small ? 15 : 16, FontWeight.Bold, Palette.InkSoftBrush, wrap: false));
            Child = row;
            BorderThickness = new Thickness(1.5);
            VerticalAlignment = VerticalAlignment.Center;
            Restyle();
        }

        protected override void Restyle()
        {
            Background = Palette.Brush(Palette.Paper, Over || Pressed ? 0.95 : 0.6);
            BorderBrush = Palette.Brush(Palette.Night ? Palette.LeafPale : Color.Parse("#d9ccae"));
        }
    }

    // ================================================================ a note above the shelf

    /// <summary>A short note on a little card: a badge for what kind of note it is (by shape, not only colour), and the words.</summary>
    sealed class NoteCard : Border
    {
        readonly TextBlock _text = new() { FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        readonly Border _badge = new() { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), VerticalAlignment = VerticalAlignment.Top };
        readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        CancellationTokenSource? _busy;

        public NoteCard()
        {
            Background = Palette.Brush(Palette.Paper, 0.96);
            BorderBrush = Palette.LeafPaleBrush;
            BorderThickness = new Thickness(1.5);
            CornerRadius = new CornerRadius(18);
            Padding = new Thickness(12, 10, 18, 10);
            MaxWidth = 760;
            _text.Foreground = Palette.InkBrush;
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 3, Blur = 12, Color = Palette.ShadowOf(0.10) });
            for (int i = 0; i < 3; i++) _dots.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = Palette.LeafBrush });
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
            Grid.SetColumn(_text, 1);
            Grid.SetColumn(_dots, 2);
            row.Children.Add(_badge);
            row.Children.Add(_text);
            row.Children.Add(_dots);
            Child = row;
        }

        public void Show(string? text, NoteKind kind)
        {
            IsVisible = text is not null;
            _text.Text = text ?? "";
            (Color fill, string glyph, IBrush ink) = kind switch
            {
                NoteKind.Good => (Palette.Tick, Icons.Tick, (IBrush)Palette.OnLeafBrush),
                NoteKind.Problem => (Palette.Sun, "M12 6.5 v7 M12 17.2 v0.3", Palette.SunInkBrush),
                NoteKind.Busy => (Palette.Mint, "M12 6.5 v5.5 l3.5 2", Palette.LeafDeepBrush),
                _ => (Palette.Mint, "M12 11 v6 M12 7.2 v0.3", Palette.LeafDeepBrush),
            };
            _badge.Background = Palette.Brush(fill);
            _badge.Child = Kit.Icon(glyph, 22, ink);
            bool busy = IsVisible && kind == NoteKind.Busy;
            _dots.IsVisible = busy;
            Bounce(busy);
        }

        /// <summary>The three dots bob one after another while something is being got ready, and stop when it is.</summary>
        void Bounce(bool on)
        {
            _busy?.Cancel();
            _busy = null;
            foreach (Control d in _dots.Children) d.Opacity = 1;
            if (!on || !Motion.Allowed) return;
            _busy = new CancellationTokenSource();
            for (int i = 0; i < _dots.Children.Count; i++)
            {
                var a = new Animation
                {
                    Duration = TimeSpan.FromMilliseconds(1100), IterationCount = IterationCount.Infinite, Delay = TimeSpan.FromMilliseconds(160 * i),
                    Easing = new SineEaseInOut(),
                    Children =
                    {
                        new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.25) } },
                        new KeyFrame { Cue = new Cue(0.45), Setters = { new Setter(OpacityProperty, 1.0) } },
                        new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0.25) } },
                    },
                };
                _ = a.RunAsync(_dots.Children[i], _busy.Token);
            }
        }
    }

    // ================================================================ "Carry on reading"

    /// <summary>
    /// The book the child was last part of the way through: a picture of the page they were on, the
    /// title, "Page 7 of 12" and a Carry on button. The whole card is one thing to press. On a wide
    /// screen it stands on the shelf two books wide; on a phone held upright it is a strip above the case.
    /// </summary>
    sealed class CarryOnCard : Tap
    {
        readonly ShelfEntry _entry;
        readonly BookProgress _progress;
        Bitmap? _picture;
        int _built = -1;

        public CarryOnCard(ShelfEntry entry, BookProgress progress) : base(Strings.T("shelf.carryOn.label") + ": " + entry.Title)
        {
            _entry = entry;
            _progress = progress;
            CornerRadius = new CornerRadius(18);
            BorderThickness = new Thickness(2.5);
            Padding = new Thickness(10);
            ClipToBounds = false;
            Restyle();
        }

        /// <summary>A strip (upright phones and tablets) rather than a card standing on the shelf.</summary>
        public bool Strip { get; set; }

        public Bitmap? Picture
        {
            get => _picture;
            set { _picture = value; _built = -1; Build(Bounds.Size); }
        }

        protected override void Restyle()
        {
            Background = Palette.Brush(Pressed ? Palette.Cream : Palette.Paper);
            BorderBrush = Palette.Brush(Palette.Night ? Palette.Sun : Color.Parse("#d9b45a"));
            Shadow = new BoxShadows(new BoxShadow { OffsetY = Pressed ? 1 : Over ? 8 : 4, Blur = Over ? 22 : 14, Color = Palette.ShadowOf(Palette.Night ? 0.5 : 0.2) });
            RenderTransform = Pressed ? new TranslateTransform(0, 1.5) : Over ? new TranslateTransform(0, -2) : null;
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            Build(e.NewSize);
        }

        string PageText
        {
            get
            {
                int count = _progress.PageCount > 0 ? _progress.PageCount : _entry.PageCount;
                return Strings.T("shelf.page", _progress.Page + 1, Math.Max(count, _progress.Page + 1));
            }
        }

        Control PictureBox(double radius) => new Border
        {
            CornerRadius = new CornerRadius(radius), ClipToBounds = true, Background = Palette.MintBrush,
            Child = new Image { Source = _picture, Stretch = Stretch.UniformToFill },
        };

        void Build(Size size)
        {
            if (size.Width <= 0 || size.Height <= 0) return;
            int kind = Strip ? 0 : size.Height >= 250 && size.Height > size.Width * 0.62 ? 1 : 2;
            if (kind == _built) return;
            _built = kind;
            bool roomy = size.Height >= 160;
            var label = Kit.Label(Strings.T("shelf.carryOn.label"), roomy ? 13 : 11);
            label.TextWrapping = TextWrapping.NoWrap;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.LetterSpacing = roomy ? 1.1 : 0.6;
            var title = Kit.Heading(_entry.Title, kind == 1 ? 23 : roomy ? 21 : 18);
            title.MaxLines = 2;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            var page = Kit.Text(PageText, roomy ? 15 : 13, FontWeight.SemiBold, Palette.InkSoftBrush, wrap: false);
            var words = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Children = { label, title, page } };
            switch (kind)
            {
                case 0:
                {
                    // A strip: picture, words, and a round play button.
                    double d = size.Height >= 150 ? 72 : size.Width < 420 ? 56 : 60;
                    var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = size.Width < 500 ? 12 : 18 };
                    Control pic = PictureBox(12);
                    pic.Width = Math.Round((size.Height - 20) * (size.Width < 420 ? 0.82 : size.Width < 500 ? 1.05 : 1.45));
                    grid.Children.Add(pic);
                    Grid.SetColumn(words, 1);
                    grid.Children.Add(words);
                    var play = new Border
                    {
                        Width = d, Height = d, CornerRadius = new CornerRadius(d / 2), VerticalAlignment = VerticalAlignment.Center,
                        Background = Buttons.Face(Buttons.Green), BorderBrush = Buttons.Edge(Buttons.Green), BorderThickness = Buttons.Lip,
                        BoxShadow = new BoxShadows(Buttons.Shadow), Child = Kit.Icon(Icons.Play, d * 0.42, new ImmutableSolidColorBrush(Buttons.Green.Ink), filled: true),
                    };
                    ((Control)play.Child).Margin = new Thickness(d * 0.06, 0, 0, 0);
                    Grid.SetColumn(play, 2);
                    grid.Children.Add(play);
                    Child = grid;
                    break;
                }
                case 1:
                {
                    // Standing on the shelf: the picture on top, then the words and the button.
                    var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto"), RowSpacing = 10 };
                    grid.Children.Add(PictureBox(10));
                    words.Margin = new Thickness(6, 0);
                    Grid.SetRow(words, 1);
                    grid.Children.Add(words);
                    Control button = ButtonLook(52);
                    Grid.SetRow(button, 2);
                    grid.Children.Add(button);
                    Child = grid;
                    break;
                }
                default:
                {
                    // Short and wide: the picture on the left, the words and button beside it. When the card is
                    // small (a phone on its side) the words and the button need all of it, so the picture goes.
                    bool roomForPicture = size.Width >= 330;
                    var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(roomForPicture ? "0.8*,*" : "0,*"), ColumnSpacing = roomForPicture ? 12 : 0 };
                    if (roomForPicture) grid.Children.Add(PictureBox(10));
                    var right = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 8 };
                    right.Children.Add(words);
                    Control button = ButtonLook(size.Height >= 180 ? 50 : 44);
                    Grid.SetRow(button, 1);
                    right.Children.Add(button);
                    Grid.SetColumn(right, 1);
                    grid.Children.Add(right);
                    Child = grid;
                    break;
                }
            }
        }

        /// <summary>What looks like the card's button. The whole card is pressed, so it is only a picture of one.</summary>
        static Control ButtonLook(double height)
        {
            Buttons.Look look = Buttons.Green;
            var ink = new ImmutableSolidColorBrush(look.Ink);
            return new Border
            {
                Height = height, CornerRadius = new CornerRadius(height / 2), Background = Buttons.Face(look), BorderBrush = Buttons.Edge(look), BorderThickness = Buttons.Lip,
                BoxShadow = new BoxShadows(Buttons.Shadow),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Children = { Kit.Icon(Icons.Play, height * 0.38, ink, filled: true), Kit.Text(Strings.T("shelf.carryOn.button"), height * 0.4, FontWeight.ExtraBold, ink, wrap: false) },
                },
            };
        }
    }

    // ================================================================ a book on the shelf

    /// <summary>
    /// One book standing face out: its cover (two wide by three high, cropped to fit) with a hint of a
    /// spine down its left edge and a soft shadow, a ribbon when it is partly read, a tick when it has
    /// been read to the end, and its language when the shelf holds more than one. Under the pointer it
    /// lifts, tilts a touch and its shadow grows; pressed, it dips.
    /// </summary>
    internal sealed class BookCover : Decorator
    {
        static readonly TransformOperations Rest = TransformOperations.Parse("translateY(0px) rotate(0deg) scale(1)");
        static readonly TransformOperations Lifted = TransformOperations.Parse("translateY(-12px) rotate(-1.6deg) scale(1.035)");
        static readonly TransformOperations LiftedStill = TransformOperations.Parse("translateY(-10px) rotate(0deg) scale(1.02)");
        static readonly TransformOperations Pressed = TransformOperations.Parse("translateY(-3px) rotate(-0.6deg) scale(0.985)");

        static BoxShadows RestShadow => new(
            new BoxShadow { OffsetX = 3, OffsetY = 5, Blur = 10, Color = Palette.ShadowOf(0.30) },
            new[] { new BoxShadow { OffsetX = 1, OffsetY = 1, Blur = 2, Color = Palette.ShadowOf(0.22) } });
        static BoxShadows LiftShadow => new(
            new BoxShadow { OffsetX = 6, OffsetY = 18, Blur = 26, Color = Palette.ShadowOf(0.30) },
            new[] { new BoxShadow { OffsetX = 1, OffsetY = 2, Blur = 4, Color = Palette.ShadowOf(0.16) } });

        static readonly IBrush Spine = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(90, 30, 20, 8), 0), new GradientStop(Color.FromArgb(30, 30, 20, 8), 0.35),
                new GradientStop(Color.FromArgb(70, 255, 255, 255), 0.45), new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.6),
                new GradientStop(Color.FromArgb(18, 30, 20, 8), 1),
            },
        }.ToImmutable();

        static readonly IBrush Gloss = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0.6, 0.5, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(46, 255, 255, 255), 0), new GradientStop(Color.FromArgb(0, 255, 255, 255), 1) },
        }.ToImmutable();

        readonly Border _card;
        readonly Image _image = new() { Stretch = Stretch.UniformToFill };
        readonly TextBlock _plainTitle;
        readonly Panel _marks = new() { IsHitTestVisible = false };
        readonly BookProgress? _progress;
        readonly string _tag;
        bool _pressed, _over, _ring;

        public BookCover(ShelfEntry entry, bool touch, BookProgress? progress, string languageTag)
        {
            _progress = progress;
            _tag = languageTag;
            Cursor = new Cursor(StandardCursorType.Hand);
            Focusable = Kit.Keyboard;
            IsTabStop = Kit.Keyboard;
            string state = progress is { Finished: true } ? Strings.T("shelf.finished") : progress is { Partway: true } ? Strings.T("shelf.partway") : "";
            AutomationProperties.SetName(this, state.Length > 0 ? $"{entry.Title}, {state}" : entry.Title);
            if (!touch)
            {
                ToolTip.SetTip(this, entry.Title);
                ToolTip.SetShowDelay(this, 900);
            }
            // A book with no cover picture still looks like a book: a board with its title.
            _plainTitle = new TextBlock
            {
                Text = entry.Title, Foreground = Brushes.White, FontWeight = FontWeight.ExtraBold, FontSize = touch ? 15 : 20,
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(16),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
            };
            var corners = new CornerRadius(3, 9, 9, 3);
            var spine = new Grid { ColumnDefinitions = new ColumnDefinitions { new ColumnDefinition(new GridLength(touch ? 9 : 14)), new ColumnDefinition(GridLength.Star) } };
            spine.Children.Add(new Border { Background = Spine });
            var board = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#4a8a6b"), 0), new GradientStop(Color.Parse("#2f6b50"), 1) },
            };
            _card = new Border
            {
                CornerRadius = corners,
                BoxShadow = RestShadow,
                RenderTransformOrigin = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                RenderTransform = Rest,
                Child = new Panel
                {
                    Children =
                    {
                        new Border
                        {
                            CornerRadius = corners, ClipToBounds = true, Background = board,
                            Child = new Panel { Children = { _plainTitle, _image, new Border { Background = Gloss }, spine } },
                        },
                        _marks,
                    },
                },
            };
            Child = _card;
            RenderTransformOrigin = new RelativePoint(0.5, 1, RelativeUnit.Relative);
        }

        public event Action? Click;

        public Bitmap? Cover
        {
            set
            {
                _image.Source = value;
                _plainTitle.IsVisible = value is null;
            }
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            Marks(e.NewSize);
        }

        /// <summary>The ribbon, the tick and the language tag, sized to the cover.</summary>
        void Marks(Size size)
        {
            _marks.Children.Clear();
            double w = size.Width;
            if (w <= 0) return;
            if (_progress is { Partway: true })
            {
                // A ribbon bookmark hanging from the top edge, with a notched end: "part of the way through".
                double rw = Math.Clamp(w * 0.13, 16, 26), rh = rw * 2.1;
                var ribbon = new Avalonia.Controls.Shapes.Path
                {
                    Data = Geometry.Parse($"M0,-3 H{rw} V{rh} L{rw / 2},{rh - rw * 0.42} L0,{rh} Z"),
                    Fill = Palette.Brush(Palette.Ribbon), Stroke = Palette.Brush(Colors.White, 0.85), StrokeThickness = 1.2,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, w * 0.12, 0),
                    Effect = new DropShadowEffect { OffsetX = 0, OffsetY = 1.5, BlurRadius = 3, Color = Palette.ShadowOf(1), Opacity = 0.35 },
                };
                _marks.Children.Add(ribbon);
            }
            if (_progress is { Finished: true })
            {
                // A round tick over the lower corner: "read to the end".
                double d = Math.Clamp(w * 0.26, 34, 50);
                var tick = new Border
                {
                    Width = d, Height = d, CornerRadius = new CornerRadius(d / 2), Background = Palette.Brush(Palette.Tick),
                    BorderBrush = Brushes.White, BorderThickness = new Thickness(Math.Max(2.5, d * 0.07)),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, -d * 0.3, -d * 0.12),
                    BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 5, Color = Palette.ShadowOf(0.35) }),
                    Child = Kit.Icon(Icons.Tick, d * 0.6, Palette.Night ? Palette.Brush(Color.Parse("#1b1f4b")) : Brushes.White),
                };
                _marks.Children.Add(tick);
            }
            if (_tag.Length > 0)
            {
                var tag = new Border
                {
                    Background = Palette.Brush(Color.Parse("#fffcf4"), 0.94), CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 2),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(w * 0.1, 0, 0, w * 0.06),
                    BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 1, Blur = 3, Color = Palette.ShadowOf(0.3) }),
                    Child = new TextBlock { Text = _tag, FontSize = Math.Clamp(w * 0.085, 11, 15), FontWeight = FontWeight.Black, Foreground = Palette.Brush(Color.Parse("#2b2118")), LetterSpacing = 0.6 },
                };
                _marks.Children.Add(tag);
            }
        }

        void Apply()
        {
            bool animate = Motion.Allowed;
            if (animate && _card.Transitions is null)
            {
                _card.Transitions = new Transitions
                {
                    new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(220), Easing = new CubicEaseOut() },
                };
            }
            else if (!animate) _card.Transitions = null;
            bool up = _over || _ring;
            _card.RenderTransform = _pressed ? Pressed : up ? (Motion.Reduced ? LiftedStill : Lifted) : Rest;
            BoxShadows shadow = up || _pressed ? LiftShadow : RestShadow;
            if (_ring)
            {
                var all = new List<BoxShadow>();
                foreach (BoxShadow b in Kit.FocusRing) all.Add(b);
                foreach (BoxShadow b in shadow) all.Add(b);
                shadow = new BoxShadows(all[0], all.Skip(1).ToArray());
            }
            _card.BoxShadow = shadow;
        }

        /// <summary>Show the book lifted as if the pointer were on it (for screenshots).</summary>
        public void SetHover(bool over)
        {
            _over = over;
            Apply();
        }

        public void ShowRing()
        {
            _ring = true;
            Apply();
        }

        protected override void OnGotFocus(FocusChangedEventArgs e)
        {
            base.OnGotFocus(e);
            _ring = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
            Apply();
        }

        protected override void OnLostFocus(FocusChangedEventArgs e)
        {
            base.OnLostFocus(e);
            _ring = false;
            Apply();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || e.Key is not (Key.Enter or Key.Space)) return;
            e.Handled = true;
            Click?.Invoke();
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            _over = true;
            Apply();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _over = false;
            _pressed = false;
            Apply();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            _pressed = true;
            Apply();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            bool inside = new Rect(Bounds.Size).Contains(e.GetPosition(this));
            _pressed = false;
            // A finger lifting off a touch screen leaves nothing hovering, so the book goes back down.
            if (e.Pointer.Type != PointerType.Mouse) _over = false;
            Apply();
            if (inside) Click?.Invoke();
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            if (!_pressed) return;
            _pressed = false;
            Apply();
        }

        /// <summary>The settle as the shelf appears: start a little high and see-through, then drop gently into place.</summary>
        public void Settle(int order)
        {
            Transitions = null;
            if (!Motion.Allowed)
            {
                Opacity = 1;
                RenderTransform = null;
                return;
            }
            Opacity = 0;
            RenderTransform = TransformOperations.Parse("translateY(-16px)");
            var delay = TimeSpan.FromMilliseconds(60 * order);
            Transitions = new Transitions
            {
                new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(260), Delay = delay, Easing = new QuadraticEaseOut() },
                new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(300), Delay = delay, Easing = new CubicEaseOut() },
            };
            Dispatcher.UIThread.Post(() =>
            {
                Opacity = 1;
                RenderTransform = TransformOperations.Parse("translateY(0px)");
            }, DispatcherPriority.Background);
        }
    }

    // ================================================================ the sign on an empty shelf

    /// <summary>What stands on the shelf when there are no books: the dassie, a sign saying so, and for grown-ups a way to add one.</summary>
    internal sealed class Sign : Border
    {
        readonly TextBlock _detail;
        readonly Border _button = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) };

        public Sign(bool small)
        {
            Background = Palette.PaperBrush;
            BorderBrush = Palette.LeafPaleBrush;
            BorderThickness = new Thickness(1.5);
            CornerRadius = new CornerRadius(22);
            Padding = small ? new Thickness(18, 16) : new Thickness(32, 24);
            BoxShadow = new BoxShadows(new BoxShadow { OffsetX = 2, OffsetY = 6, Blur = 14, Color = Palette.ShadowOf(0.2) });
            _detail = new TextBlock
            {
                FontSize = small ? 14 : 15, Foreground = Palette.InkSoftBrush, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), IsVisible = false,
            };
            var title = Kit.Heading(Strings.T("shelf.empty.title"), small ? 24 : 30);
            title.TextAlignment = TextAlignment.Center;
            title.HorizontalAlignment = HorizontalAlignment.Center;
            var body = Kit.Text(Strings.T("shelf.empty.body"), small ? 16 : 18, FontWeight.SemiBold, Palette.InkSoftBrush);
            body.TextAlignment = TextAlignment.Center;
            body.HorizontalAlignment = HorizontalAlignment.Center;
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Dassie { Look = Dassie.Pose.Sit, Width = small ? 72 : 96, Height = small ? 72 : 96, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) },
                    title, body, _detail, _button,
                },
            };
        }

        public string? Detail
        {
            set
            {
                _detail.Text = value ?? "";
                _detail.IsVisible = !string.IsNullOrEmpty(value);
            }
        }

        public void ShowButton(Action add)
        {
            var pill = new Pill(Strings.T("shelf.empty.add"), PillKind.Plain, Icons.Lock, height: 52, fontSize: 17);
            pill.Click += add;
            _button.Child = pill;
        }
    }
}
