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

/// <summary>
/// The bookshelf: a light wooden bookcase on a soft wall, and every book standing face out on its shelves
/// so its cover shows, as on a nursery shelf. A cover lifts and tilts a little under the pointer or a
/// finger, and the books settle in when the shelf appears. Each of those stops by itself, so a shelf that
/// is just sitting there draws nothing.
/// </summary>
public sealed class ShelfView : UserControl
{
    readonly bool _touch;
    readonly Bookcase _case;
    readonly NoteCard _note;
    readonly StackPanel _page;
    readonly ScrollViewer _scroll;
    readonly List<Bitmap> _covers = new();
    string? _message;
    NoteKind _kind;

    /// <param name="forTouch">The phone and tablet app: no folder buttons and no keyboard hint.</param>
    public ShelfView(bool forTouch = false)
    {
        _touch = forTouch;
        FontFamily = Palette.Font;

        var title = new TextBlock
        {
            Text = "My Books", Foreground = Palette.LeafDeepBrush, FontSize = forTouch ? 32 : 44, FontWeight = FontWeight.Black,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var mark = new Sprig { Width = forTouch ? 34 : 46, Height = forTouch ? 34 : 46, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var hint = new TextBlock
        {
            Text = forTouch ? "Tap a book to read it." : "Pick a book to read. Grown-ups: press S for the settings.",
            Foreground = Palette.InkSoftBrush, FontSize = forTouch ? 14 : 16, FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(forTouch ? 44 : 56, 0, 0, 0),
        };
        var titleBlock = new StackPanel
        {
            Children = { new StackPanel { Orientation = Orientation.Horizontal, Children = { mark, title } }, hint },
        };

        var openFile = ShelfButton("Add a book file", Icons.Plus);
        openFile.Click += (_, _) => OpenFileRequested?.Invoke();
        ToolTip.SetTip(openFile, "Add a book file (.picturebook) from the Book Editor to the shelf. You can also drop one on this window.");
        var open = ShelfButton("Open a book folder", FolderIcon);
        open.Click += (_, _) => OpenFolderRequested?.Invoke();
        ToolTip.SetTip(open, "Open any folder that has a book.json in it (Ctrl+O)");
        var buttons = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12, 6, 0, 0), Children = { openFile, open },
        };
        foreach (Control b in buttons.Children) b.Margin = new Thickness(8, 0, 0, 8);
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(4, 0, 4, forTouch ? 8 : 12) };
        Grid.SetColumn(buttons, 1);
        top.Children.Add(titleBlock);
        if (!forTouch) top.Children.Add(buttons);

        _note = new NoteCard { IsVisible = false, Margin = new Thickness(4, 0, 4, 12), HorizontalAlignment = HorizontalAlignment.Left };
        _case = new Bookcase(forTouch);

        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _page = new StackPanel { Margin = Edge(default), MaxWidth = forTouch ? double.PositiveInfinity : 1640, Children = { top, _note, _case } },
        };
        Content = new Panel { Children = { new Wall(), _scroll } };
    }

    Thickness Edge(Thickness safe) => _touch
        ? new Thickness(14 + safe.Left, 16 + safe.Top, 14 + safe.Right, 16 + safe.Bottom)
        : new Thickness(32 + safe.Left, 26 + safe.Top, 32 + safe.Right, 28 + safe.Bottom);

    /// <summary>A folder, for the "Open a book folder" button.</summary>
    const string FolderIcon = "M3.5 7.5 a2 2 0 0 1 2 -2 h3.5 l2 2 h7.5 a2 2 0 0 1 2 2 v7 a2 2 0 0 1 -2 2 h-13 a2 2 0 0 1 -2 -2 z";

    static Button ShelfButton(string text, string icon)
    {
        Control glyph = Icons.Make(icon, 18, Buttons.TextBrush);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        return new Button
        {
            Focusable = false, CornerRadius = new CornerRadius(22), MinHeight = 44, Padding = new Thickness(14, 6, 18, 6), FontSize = 15,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Children = { glyph, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
            },
        };
    }

    public event Action<ShelfEntry>? BookChosen;

    /// <summary>Room kept clear for a notch, rounded corners or the system bars, added to the usual margin.</summary>
    public Thickness SafeArea
    {
        set => _page.Margin = Edge(value);
    }
    public event Action? OpenFolderRequested;
    /// <summary>"Add a book file" was pressed.</summary>
    public event Action? OpenFileRequested;

    /// <summary>Something to tell the person at the shelf: no books found, a book that wouldn't open, a book being added.</summary>
    public void Say(string? message, NoteKind kind = NoteKind.Info)
    {
        _message = string.IsNullOrEmpty(message) ? null : message;
        _kind = kind;
        ShowNote();
    }

    void ShowNote()
    {
        // With no books at all, the words go on the sign on the empty shelf instead.
        if (_case.Empty && _kind != NoteKind.Busy)
        {
            _note.Show(null, _kind);
            _case.EmptySign.Detail = _message;
            return;
        }
        _case.EmptySign.Detail = null;
        _note.Show(_message, _kind);
    }

    /// <summary>Fill the shelf. Covers load in the background and appear as they arrive.</summary>
    public void Show(IReadOnlyList<ShelfEntry> books, bool coversNow = false)
    {
        foreach (Bitmap b in _covers) b.Dispose();
        _covers.Clear();
        var covers = new List<BookCover>();
        foreach (ShelfEntry entry in books)
        {
            var cover = new BookCover(entry, _touch);
            cover.Click += () => BookChosen?.Invoke(entry);
            covers.Add(cover);
            if (coversNow) SetCover(cover, LoadCover(entry.CoverPath));
            else Task.Run(() => LoadCover(entry.CoverPath)).ContinueWith(t => Dispatcher.UIThread.Post(() => SetCover(cover, t.Result)));
        }
        _case.SetBooks(covers);
        _case.EmptySign.ShowButtons(!_touch, () => OpenFileRequested?.Invoke(), () => OpenFolderRequested?.Invoke());
        ShowNote();
        if (IsVisible) _case.SettleIn();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Back from a book: the books settle in again.
        if (change.Property == IsVisibleProperty && IsVisible) _case.SettleIn();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _case.ViewportHeight = e.NewSize.Height;
    }

    void SetCover(BookCover card, Bitmap? cover)
    {
        if (cover is null) return;
        _covers.Add(cover);
        card.Cover = cover;
    }

    /// <summary>A cover shrunk to shelf size, so a shelf of big paintings doesn't fill the memory.</summary>
    static Bitmap? LoadCover(string path)
    {
        try
        {
            using FileStream file = File.OpenRead(path);
            return Bitmap.DecodeToWidth(file, 480, BitmapInterpolationMode.HighQuality);
        }
        catch (Exception) { return null; }   // no cover: the book keeps its plain green board
    }

    // ================================================================ the wall

    /// <summary>The wall behind the bookcase: soft green, a patch of sunlight in the top corner and a fine paper texture.</summary>
    sealed class Wall : Control
    {
        static readonly IBrush Paint = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Palette.Wall, 0), new GradientStop(Palette.WallLow, 1) },
        }.ToImmutable();

        static readonly IBrush Sunlight = new RadialGradientBrush
        {
            Center = new RelativePoint(0.88, 0.0, RelativeUnit.Relative), GradientOrigin = new RelativePoint(0.88, 0.0, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.55, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(120, 255, 236, 160), 0), new GradientStop(Color.FromArgb(40, 255, 236, 160), 0.5), new GradientStop(Color.FromArgb(0, 255, 236, 160), 1) },
        }.ToImmutable();

        public Wall() => IsHitTestVisible = false;

        public override void Render(DrawingContext ctx)
        {
            var r = new Rect(Bounds.Size);
            ctx.FillRectangle(Paint, r);
            ctx.FillRectangle(Sunlight, r);
            ShelfArt.Speckle(ctx, r, 0.25);
        }
    }

    // ================================================================ a note above the shelf

    /// <summary>A short note on a little card: a coloured badge for what kind of note it is, and the words.</summary>
    sealed class NoteCard : Border
    {
        readonly TextBlock _text = new() { FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Palette.InkBrush, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        readonly Border _badge = new() { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), VerticalAlignment = VerticalAlignment.Top };
        readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        CancellationTokenSource? _busy;

        public NoteCard()
        {
            Background = Palette.Brush(Palette.Paper, 0.94);
            BorderBrush = Palette.LeafPaleBrush;
            BorderThickness = new Thickness(1.5);
            CornerRadius = new CornerRadius(18);
            Padding = new Thickness(12, 10, 18, 10);
            MaxWidth = 760;
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
                NoteKind.Good => (Palette.Leaf, "M7 12.5 l3.2 3.2 L17 8.8", (IBrush)Brushes.White),
                NoteKind.Problem => (Palette.Sun, "M12 6.5 v7 M12 17.2 v0.3", Palette.SunInkBrush),
                NoteKind.Busy => (Palette.Mint, "M12 6.5 v5.5 l3.5 2", Palette.LeafDeepBrush),
                _ => (Palette.Mint, "M12 11 v6 M12 7.2 v0.3", Palette.LeafDeepBrush),
            };
            _badge.Background = Palette.Brush(fill);
            _badge.BorderBrush = kind is NoteKind.Busy or NoteKind.Info ? Palette.LeafPaleBrush : null;
            _badge.BorderThickness = new Thickness(kind is NoteKind.Busy or NoteKind.Info ? 1.5 : 0);
            _badge.Child = Icons.Make(glyph, 22, ink);
            ((Control)_badge.Child).HorizontalAlignment = HorizontalAlignment.Center;
            ((Control)_badge.Child).VerticalAlignment = VerticalAlignment.Center;
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

    // ================================================================ a book on the shelf

    /// <summary>
    /// One book standing face out: its cover (two wide by three high, cropped to fit) with a hint of a
    /// spine down its left edge and a soft shadow. Under the pointer it lifts, tilts a touch and its shadow
    /// grows; pressed, it dips. The movement is a short transition that stops when it gets there.
    /// </summary>
    internal sealed class BookCover : Decorator
    {
        static readonly TransformOperations Rest = TransformOperations.Parse("translateY(0px) rotate(0deg) scale(1)");
        static readonly TransformOperations Lifted = TransformOperations.Parse("translateY(-12px) rotate(-1.6deg) scale(1.035)");
        static readonly TransformOperations LiftedStill = TransformOperations.Parse("translateY(-10px) rotate(0deg) scale(1.02)");
        static readonly TransformOperations Pressed = TransformOperations.Parse("translateY(-3px) rotate(-0.6deg) scale(0.985)");

        static readonly BoxShadows RestShadow = new(
            new BoxShadow { OffsetX = 3, OffsetY = 5, Blur = 10, Color = Palette.ShadowOf(0.30) },
            new[] { new BoxShadow { OffsetX = 1, OffsetY = 1, Blur = 2, Color = Palette.ShadowOf(0.22) } });
        static readonly BoxShadows LiftShadow = new(
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

        static readonly IBrush Board = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Palette.LeafBright, 0), new GradientStop(Palette.Leaf, 1) },
        }.ToImmutable();

        readonly Border _card;
        readonly Image _image = new() { Stretch = Stretch.UniformToFill };
        readonly TextBlock _plainTitle;
        bool _pressed, _over;

        public BookCover(ShelfEntry entry, bool touch)
        {
            Cursor = new Cursor(StandardCursorType.Hand);
            AutomationProperties.SetName(this, entry.Title);
            ToolTip.SetTip(this, entry.Title);
            ToolTip.SetShowDelay(this, 900);
            // A book with no cover picture still looks like a book: a green board with its title.
            _plainTitle = new TextBlock
            {
                Text = entry.Title, Foreground = Brushes.White, FontWeight = FontWeight.ExtraBold, FontSize = touch ? 15 : 20,
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(16),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
            };
            var corners = new CornerRadius(3, 9, 9, 3);
            var spineWidth = new GridLength(touch ? 9 : 14);
            var spine = new Grid { ColumnDefinitions = new ColumnDefinitions { new ColumnDefinition(spineWidth), new ColumnDefinition(GridLength.Star) } };
            spine.Children.Add(new Border { Background = Spine });
            _card = new Border
            {
                CornerRadius = corners,
                BoxShadow = RestShadow,
                RenderTransformOrigin = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                RenderTransform = Rest,
                Child = new Border
                {
                    CornerRadius = corners, ClipToBounds = true, Background = Board,
                    Child = new Panel { Children = { _plainTitle, _image, new Border { Background = Gloss }, spine } },
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

        void Apply()
        {
            bool animate = Motion.Allowed;
            if (animate && _card.Transitions is null)
            {
                _card.Transitions = new Transitions
                {
                    new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(240), Easing = new CubicEaseOut() },
                    new BoxShadowsTransition { Property = Border.BoxShadowProperty, Duration = TimeSpan.FromMilliseconds(240), Easing = new CubicEaseOut() },
                };
            }
            else if (!animate) _card.Transitions = null;
            _card.RenderTransform = _pressed ? Pressed : _over ? (Motion.Reduced ? LiftedStill : Lifted) : Rest;
            _card.BoxShadow = _over || _pressed ? LiftShadow : RestShadow;
        }

        /// <summary>Show the book lifted as if the pointer were on it (for screenshots and the keyboard).</summary>
        public void SetHover(bool over)
        {
            _over = over;
            Apply();
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
            var delay = TimeSpan.FromMilliseconds(70 * order);
            Transitions = new Transitions
            {
                new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(320), Delay = delay, Easing = new QuadraticEaseOut() },
                new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(520), Delay = delay, Easing = new BackEaseOut() },
            };
            Dispatcher.UIThread.Post(() =>
            {
                Opacity = 1;
                RenderTransform = TransformOperations.Parse("translateY(0px)");
            }, DispatcherPriority.Background);
        }
    }

    // ================================================================ the sign on an empty shelf

    /// <summary>What stands on the shelf when there are no books: a little sign saying so, and for grown-ups how to add one.</summary>
    internal sealed class Sign : Border
    {
        readonly TextBlock _detail;
        readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 0) };

        public Sign(bool touch)
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Palette.Paper, 0), new GradientStop(Palette.Cream, 1) },
            };
            BorderBrush = Palette.LeafPaleBrush;
            BorderThickness = new Thickness(2);
            CornerRadius = new CornerRadius(20);
            Padding = touch ? new Thickness(18, 16) : new Thickness(32, 24);
            BoxShadow = new BoxShadows(new BoxShadow { OffsetX = 2, OffsetY = 6, Blur = 14, Color = Palette.ShadowOf(0.22) });
            _detail = new TextBlock
            {
                FontSize = touch ? 13 : 15, Foreground = Palette.InkSoftBrush, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0), IsVisible = false,
            };
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Sprig { Width = touch ? 40 : 56, Height = touch ? 40 : 56, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) },
                    new TextBlock
                    {
                        Text = "No books on the shelf yet", FontSize = touch ? 20 : 28, FontWeight = FontWeight.ExtraBold, Foreground = Palette.LeafDeepBrush,
                        TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                    },
                    _detail, _buttons,
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

        public void ShowButtons(bool show, Action addFile, Action openFolder)
        {
            _buttons.Children.Clear();
            if (!show) return;
            var add = new Button { Content = "Add a book file", Focusable = false, CornerRadius = new CornerRadius(22), MinHeight = 44, FontSize = 15, Padding = new Thickness(20, 8) };
            add.Classes.Add("primary");
            add.Click += (_, _) => addFile();
            var folder = new Button { Content = "Open a book folder", Focusable = false, CornerRadius = new CornerRadius(22), MinHeight = 44, FontSize = 15, Padding = new Thickness(20, 8) };
            folder.Click += (_, _) => openFolder();
            _buttons.Children.Add(add);
            _buttons.Children.Add(folder);
        }
    }
}
