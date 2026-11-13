using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Reader.Core;

namespace Player;

/// <summary>The bookshelf: every book in the content folder as a cover and a title. A port of the library in web/index.html.</summary>
sealed class ShelfView : UserControl
{
    public static readonly IBrush Cream = new ImmutableSolidColorBrush(Color.Parse("#fff7ec"));
    public static readonly IBrush Orange = new ImmutableSolidColorBrush(Color.Parse("#e8730c"));
    static readonly IBrush Brown = new ImmutableSolidColorBrush(Color.Parse("#6b5b4b"));

    readonly CardGrid _grid = new();
    readonly TextBlock _message;
    readonly List<Bitmap> _covers = new();

    public ShelfView()
    {
        Background = Cream;
        var heading = new TextBlock { Text = "My Books", Foreground = Orange, FontSize = 40, FontWeight = FontWeight.Black, Margin = new Thickness(0, 0, 0, 4) };
        var hint = new TextBlock
        {
            Text = "Pick a book, then press the gear (or S) for the settings.",
            Foreground = Brown, FontSize = 16, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
        };
        var open = new Button { Content = "Open a book folder…", HorizontalAlignment = HorizontalAlignment.Right, Focusable = false };
        open.Click += (_, _) => OpenFolderRequested?.Invoke();
        ToolTip.SetTip(open, "Open any folder that has a book.json in it (Ctrl+O)");
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 20) };
        Grid.SetColumn(open, 1);
        top.Children.Add(hint);
        top.Children.Add(open);

        _message = new TextBlock { Foreground = Brown, FontSize = 16, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(0, 0, 0, 16) };
        _grid.MaxWidth = 1100;
        _grid.HorizontalAlignment = HorizontalAlignment.Left;

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new StackPanel { Margin = new Thickness(24), Children = { heading, top, _message, _grid } },
        };
    }

    public event Action<ShelfEntry>? BookChosen;
    public event Action? OpenFolderRequested;

    /// <summary>Something to tell the person at the shelf: no books found, or a book that wouldn't open.</summary>
    public void Say(string? message)
    {
        _message.Text = message ?? "";
        _message.IsVisible = !string.IsNullOrEmpty(message);
    }

    /// <summary>Fill the shelf. Covers load in the background and appear as they arrive.</summary>
    public void Show(IReadOnlyList<ShelfEntry> books, bool coversNow = false)
    {
        _grid.Children.Clear();
        foreach (Bitmap b in _covers) b.Dispose();
        _covers.Clear();
        foreach (ShelfEntry entry in books)
        {
            var card = new BookCard(entry);
            card.Click += () => BookChosen?.Invoke(entry);
            _grid.Children.Add(card);
            if (coversNow) SetCover(card, LoadCover(entry.CoverPath));
            else Task.Run(() => LoadCover(entry.CoverPath)).ContinueWith(t => Dispatcher.UIThread.Post(() => SetCover(card, t.Result)));
        }
    }

    void SetCover(BookCard card, Bitmap? cover)
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
        catch (Exception) { return null; }   // no cover: the card keeps its grey rectangle
    }

    /// <summary>One book: its cover (two wide by three high, cropped to fit) over its title.</summary>
    sealed class BookCard : Border
    {
        readonly Image _image = new() { Stretch = Stretch.UniformToFill };
        readonly Border _picture;
        bool _pressed;

        public BookCard(ShelfEntry entry)
        {
            Background = Brushes.White;
            CornerRadius = new CornerRadius(16);
            ClipToBounds = true;
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 3, Blur = 10, Color = Color.FromArgb(31, 0, 0, 0) });
            Cursor = new Cursor(StandardCursorType.Hand);
            AutomationProperties.SetName(this, entry.Title);
            _picture = new Border { Background = new ImmutableSolidColorBrush(Color.Parse("#eeeeee")), ClipToBounds = true, Child = _image };
            var title = new TextBlock
            {
                Text = entry.Title, FontWeight = FontWeight.Bold, FontSize = 17, TextWrapping = TextWrapping.Wrap,
                Foreground = new ImmutableSolidColorBrush(Color.Parse("#222222")), Margin = new Thickness(12, 10),
            };
            // The rounded corners clip the picture too.
            Child = new Border { CornerRadius = new CornerRadius(16), ClipToBounds = true, Child = new StackPanel { Children = { _picture, title } } };
            RenderTransform = new TranslateTransform();
        }

        public event Action? Click;

        public Bitmap? Cover { set => _image.Source = value; }

        /// <summary>The card grid decides how wide a card is; the picture is then one and a half times as tall.</summary>
        public void SetCardWidth(double width) => _picture.Height = Math.Round(width * 1.5);

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            RenderTransform = new TranslateTransform(0, -3);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            RenderTransform = new TranslateTransform();
            _pressed = false;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            _pressed = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_pressed && new Rect(Bounds.Size).Contains(e.GetPosition(this))) Click?.Invoke();
            _pressed = false;
        }
    }

    /// <summary>
    /// Lays the cards out like the browser's grid: as many columns as fit at 170 units or more each,
    /// all the same width, sharing out whatever room is left.
    /// </summary>
    sealed class CardGrid : Panel
    {
        const double MinCard = 170, Gap = 18;

        (int Columns, double Width) Columns(double available)
        {
            if (double.IsInfinity(available) || available <= 0) available = 1100;
            int columns = Math.Max(1, (int)Math.Floor((available + Gap) / (MinCard + Gap)));
            return (columns, (available - Gap * (columns - 1)) / columns);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double available = Math.Min(availableSize.Width, MaxWidth);
            var (columns, width) = Columns(available);
            double height = 0, rowHeight = 0;
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] is BookCard card) card.SetCardWidth(width);
                Children[i].Measure(new Size(width, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
                if (i % columns == columns - 1 || i == Children.Count - 1)
                {
                    height += rowHeight + (i == Children.Count - 1 ? 0 : Gap);
                    rowHeight = 0;
                }
            }
            return new Size(double.IsInfinity(available) ? columns * (width + Gap) - Gap : available, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var (columns, width) = Columns(finalSize.Width);
            double y = 0;
            for (int start = 0; start < Children.Count; start += columns)
            {
                int end = Math.Min(Children.Count, start + columns);
                double rowHeight = 0;
                for (int i = start; i < end; i++) rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
                for (int i = start; i < end; i++) Children[i].Arrange(new Rect((i - start) * (width + Gap), y, width, rowHeight));
                y += rowHeight + Gap;
            }
            return finalSize;
        }
    }
}
