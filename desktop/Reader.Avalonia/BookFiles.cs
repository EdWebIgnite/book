using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Reader.Core;

namespace Reader.Ui;

/// <summary>
/// A book opened from a folder on disk: its book.json and its page pictures, ready to show.
///
/// Each page picture is kept at several sizes: half size, quarter size and so on, made once when the
/// book opens. Drawing always takes the smallest one that is big enough for the screen, so the
/// computer never has to shrink a huge picture sixty times a second (which is slow, and was the
/// main cause of jerky movement). The full-size pictures are only needed when the camera is close
/// in. They load in the background, and only the last few used are kept.
/// </summary>
public sealed class LoadedBook : IDisposable
{
    sealed class PagePictures
    {
        public required string Path;
        public required PixelSize Size;
        public required bool Opaque;
        /// <summary>Smaller copies, biggest first: half size, quarter size...</summary>
        public required Bitmap[] Smaller;
    }

    /// <summary>The colour of the book's cover board when nothing better can be worked out: a deep blue-grey.</summary>
    public static readonly Color PlainBoard = Color.Parse("#27323f");

    readonly PagePictures[] _pages;
    readonly PictureCache _full = new(keep: 4, drop: 1, atOnce: 2);

    LoadedBook(Book book, string folder, PagePictures[] pages, Color board)
    {
        Book = book;
        Folder = folder;
        _pages = pages;
        Board = board;
        Aspects = pages.Select(p => p.Size.Height > 0 ? (double)p.Size.Width / p.Size.Height : 1).ToArray();
    }

    public Book Book { get; }
    public string Folder { get; }

    /// <summary>Each page picture's width divided by its height, which is what the engine lays the pages out from.</summary>
    public double[] Aspects { get; }

    /// <summary>The colour of the cover board drawn behind the pages: the book's own cover, darkened.</summary>
    public Color Board { get; }

    /// <summary>A full-size page picture has finished loading. Raised on the screen's thread.</summary>
    public event Action? PictureArrived
    {
        add => _full.Arrived += value;
        remove => _full.Arrived -= value;
    }

    /// <summary>Full-size pictures still loading.</summary>
    public int Loading => _full.Loading;

    /// <summary>Load full-size pictures on the spot instead of in the background. For screenshots.</summary>
    public bool LoadAtOnce
    {
        get => _full.Synchronous;
        set => _full.Synchronous = value;
    }

    /// <summary>The size of a page's picture as it is on disk, in pixels.</summary>
    public PixelSize PageSize(int page) => _pages[page].Size;

    /// <summary>True when a page's picture has no see-through parts, so nothing need be painted behind it.</summary>
    public bool PageIsOpaque(int page) => _pages[page].Opaque;

    /// <summary>
    /// The picture to draw a page from when it is <paramref name="pixelsWide"/> pixels wide on screen: the
    /// smallest copy that is at least that big. If that is the full-size picture and it has not loaded
    /// yet, the biggest smaller copy is returned for now and the full one is fetched.
    /// </summary>
    public Bitmap PagePicture(int page, double pixelsWide)
    {
        PagePictures p = _pages[page];
        for (int i = p.Smaller.Length - 1; i >= 0; i--)
        {
            if (p.Smaller[i].PixelSize.Width >= pixelsWide * 0.98) return p.Smaller[i];
        }
        return _full.Get(p.Path) ?? (p.Smaller.Length > 0 ? p.Smaller[0] : Placeholder);
    }

    /// <summary>The smallest copy of a page's picture, for thumbnails such as the corner map.</summary>
    public Bitmap SmallPicture(int page)
    {
        PagePictures p = _pages[page];
        return p.Smaller.Length > 0 ? p.Smaller[^1] : _full.Get(p.Path) ?? Placeholder;
    }

    /// <summary>Start fetching a page's full-size picture, so it is there by the time the camera goes in close.</summary>
    public void Warm(int page)
    {
        if (page >= 0 && page < _pages.Length) _full.Get(_pages[page].Path);
    }

    static Bitmap? _placeholder;
    static Bitmap Placeholder => _placeholder ??= new WriteableBitmap(new PixelSize(2, 2), new Vector(96, 96), Avalonia.Platform.PixelFormats.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);

    /// <summary>
    /// Reads book.json and every page picture. Throws if the folder isn't a book or a picture is missing,
    /// with a message plain enough to show on the shelf.
    /// </summary>
    public static LoadedBook Load(string folder)
    {
        if (!File.Exists(Path.Combine(folder, "book.json"))) throw new FileNotFoundException("There is no book.json in " + folder + ".");
        Book book = BookJson.Load(folder);
        if (book.Pages.Count == 0) throw new InvalidDataException("This book has no pages.");
        var pages = new PagePictures[book.Pages.Count];
        Bitmap? firstFull = null;
        try
        {
            for (int i = 0; i < pages.Length; i++)
            {
                string? path = BookLibrary.SafePath(folder, book.Pages[i].Image);
                if (path is null || !File.Exists(path)) throw new FileNotFoundException($"Page {i + 1}'s picture is missing: {book.Pages[i].Image}");
                var full = new Bitmap(path);
                pages[i] = new PagePictures
                {
                    Path = path, Size = full.PixelSize, Opaque = full.AlphaFormat == Avalonia.Platform.AlphaFormat.Opaque,
                    Smaller = SmallerCopies(full),
                };
                // The first page is about to be looked at, so its full-size picture is kept. The rest load when wanted.
                if (i == 0) firstFull = full; else full.Dispose();
            }
        }
        catch
        {
            firstFull?.Dispose();
            foreach (PagePictures? p in pages) if (p is not null) foreach (Bitmap b in p.Smaller) b.Dispose();
            throw;
        }
        var loaded = new LoadedBook(book, folder, pages, BoardColour(folder, book, pages)) { Cover = LoadCover(folder, book) };
        if (firstFull is not null) loaded._full.Put(pages[0].Path, firstFull);
        return loaded;
    }

    /// <summary>The book's own cover picture, if book.json names one and it can be read. The first page doesn't count.</summary>
    public Bitmap? Cover { get; private init; }

    /// <summary>True when the book has a cover picture to start on.</summary>
    public bool HasCover => Cover is not null;

    static Bitmap? LoadCover(string folder, Book book)
    {
        try
        {
            string? path = BookLibrary.SafePath(folder, book.Cover);
            if (path is null || !File.Exists(path)) return null;
            using FileStream file = File.OpenRead(path);
            // Big enough to be sharp when the closed book fills a large screen, small enough to draw quickly.
            return Bitmap.DecodeToHeight(file, 1400, BitmapInterpolationMode.HighQuality);
        }
        catch (Exception)
        {
            return null;   // a damaged cover: the book just opens without it
        }
    }

    /// <summary>
    /// Half size, quarter size and so on, down to about 500 pixels on the long side. Each is exactly half
    /// the one before, so every pixel is the plain average of four: quick to make and clean to look at.
    /// </summary>
    static Bitmap[] SmallerCopies(Bitmap full)
    {
        var copies = new List<Bitmap>();
        Bitmap from = full;
        while (Math.Max(from.PixelSize.Width, from.PixelSize.Height) >= 1000)
        {
            var half = new PixelSize((from.PixelSize.Width + 1) / 2, (from.PixelSize.Height + 1) / 2);
            Bitmap copy = from.CreateScaledBitmap(half, BitmapInterpolationMode.LowQuality);
            copies.Add(copy);
            from = copy;
        }
        return copies.ToArray();
    }

    /// <summary>The average colour of the cover picture, darkened and calmed down, for the cover board.</summary>
    static Color BoardColour(string folder, Book book, PagePictures[] pages)
    {
        try
        {
            string? cover = BookLibrary.SafePath(folder, book.Cover);
            Bitmap? tiny = null;
            if (cover is not null && File.Exists(cover))
            {
                using FileStream file = File.OpenRead(cover);
                tiny = Bitmap.DecodeToWidth(file, 24, BitmapInterpolationMode.LowQuality);
            }
            Bitmap source = tiny ?? (pages[0].Smaller.Length > 0 ? pages[0].Smaller[^1] : throw new InvalidOperationException());
            try
            {
                int w = source.PixelSize.Width, h = source.PixelSize.Height, stride = w * 4;
                if (w <= 0 || h <= 0 || (long)w * h > 4_000_000) return PlainBoard;
                byte[] pixels = new byte[stride * h];
                var pin = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                try { source.CopyPixels(new PixelRect(0, 0, w, h), pin.AddrOfPinnedObject(), pixels.Length, stride); }
                finally { pin.Free(); }
                bool rgba = source.Format == Avalonia.Platform.PixelFormats.Rgba8888;
                double r = 0, g = 0, b = 0;
                int step = Math.Max(1, (w * h) / 4096) * 4;       // a few thousand samples are plenty
                int count = 0;
                for (int i = 0; i + 3 < pixels.Length; i += step, count++)
                {
                    b += pixels[i + (rgba ? 2 : 0)];
                    g += pixels[i + 1];
                    r += pixels[i + (rgba ? 0 : 2)];
                }
                if (count == 0) return PlainBoard;
                return BoardFrom(r / count, g / count, b / count);
            }
            finally
            {
                tiny?.Dispose();
            }
        }
        catch (Exception)
        {
            return PlainBoard;   // no cover, or one that can't be read: the plain colour will do
        }
    }

    /// <summary>
    /// A cover-board colour from a picture's average colour: the same hue, with enough colour in it to
    /// tell it from grey (an average of a whole painting is always muddy), and dark enough to sit back
    /// behind any page.
    /// </summary>
    static Color BoardFrom(double r, double g, double b)
    {
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), spread = max - min;
        if (max <= 0 || spread < 4) return PlainBoard;            // no hue to speak of
        double hue = max == r ? ((g - b) / spread + 6) % 6 : max == g ? (b - r) / spread + 2 : (r - g) / spread + 4;
        double saturation = Math.Clamp(spread / max * 1.5 + 0.12, 0.3, 0.55);
        const double value = 0.42 * 255;
        double chroma = value * saturation, x = chroma * (1 - Math.Abs(hue % 2 - 1)), m = value - chroma;
        (double R, double G, double B) = (int)Math.Floor(hue) switch
        {
            0 => (chroma, x, 0.0), 1 => (x, chroma, 0.0), 2 => (0.0, chroma, x),
            3 => (0.0, x, chroma), 4 => (x, 0.0, chroma), _ => (chroma, 0.0, x),
        };
        return Color.FromRgb((byte)(R + m), (byte)(G + m), (byte)(B + m));
    }

    /// <summary>The same, off the screen's thread, so a big book doesn't freeze the window while it opens.</summary>
    public static Task<LoadedBook> LoadAsync(string folder) => Task.Run(() => Load(folder));

    public void Dispose()
    {
        Cover?.Dispose();
        _full.Dispose();
        foreach (PagePictures p in _pages) foreach (Bitmap b in p.Smaller) b.Dispose();
    }
}

/// <summary>
/// Pictures that load when they are first asked for: the sharp tiles of a big scene, and the
/// pictures-on-top. Asking never waits. It returns the picture if it is here and otherwise starts
/// fetching it (a few at a time) and returns null, and <see cref="Arrived"/> says when to draw again.
/// The pictures looked at longest ago are forgotten once there are too many.
/// </summary>
public sealed class PictureCache : IDisposable
{
    sealed class Entry
    {
        public Bitmap? Image;
        public bool Ready;
        public long Used;
    }

    readonly Dictionary<string, Entry> _entries = new();
    readonly int _keep, _drop, _atOnce;
    int _loading;
    long _tick;
    bool _disposed;

    /// <param name="keep">How many pictures to hold before forgetting some.</param>
    /// <param name="drop">How many of the oldest to forget when that is passed.</param>
    /// <param name="atOnce">How many may be loading at the same time.</param>
    public PictureCache(int keep = 160, int drop = 40, int atOnce = 6)
    {
        _keep = keep;
        _drop = drop;
        _atOnce = atOnce;
    }

    /// <summary>A picture has finished loading. Raised on the screen's thread.</summary>
    public event Action? Arrived;

    /// <summary>How many pictures are still on their way.</summary>
    public int Loading => _loading;

    public int Count => _entries.Count;

    /// <summary>Load on the calling thread instead of in the background. For taking screenshots.</summary>
    public bool Synchronous { get; set; }

    /// <summary>The picture at this path if it has loaded. Starts loading it otherwise.</summary>
    public Bitmap? Get(string? path)
    {
        if (path is null || _disposed) return null;
        if (!_entries.TryGetValue(path, out Entry? entry))
        {
            if (_loading >= _atOnce) return null;
            entry = new Entry();
            _entries[path] = entry;
            if (Synchronous)
            {
                entry.Image = TryLoad(path);
                entry.Ready = true;
            }
            else
            {
                _loading++;
                Task.Run(() => TryLoad(path)).ContinueWith(t => Dispatcher.UIThread.Post(() => Finish(entry, t.Result)));
            }
            if (_entries.Count > _keep) Trim();
        }
        entry.Used = ++_tick;
        return entry.Ready ? entry.Image : null;
    }

    /// <summary>Hand over a picture that is already loaded, so it need not be read again.</summary>
    public void Put(string path, Bitmap image)
    {
        if (_disposed || _entries.ContainsKey(path)) { image.Dispose(); return; }
        _entries[path] = new Entry { Image = image, Ready = true, Used = ++_tick };
    }

    static Bitmap? TryLoad(string path)
    {
        try { return File.Exists(path) ? new Bitmap(path) : null; }
        catch (Exception) { return null; }   // a missing or damaged file: that picture just doesn't show
    }

    void Finish(Entry entry, Bitmap? image)
    {
        _loading--;
        if (_disposed) { image?.Dispose(); return; }
        entry.Image = image;
        entry.Ready = true;
        Arrived?.Invoke();
    }

    /// <summary>Forget the pictures looked at longest ago.</summary>
    void Trim()
    {
        foreach (var old in _entries.Where(e => e.Value.Ready).OrderBy(e => e.Value.Used).Take(_drop).ToList())
        {
            old.Value.Image?.Dispose();
            _entries.Remove(old.Key);
        }
    }

    public void Clear()
    {
        foreach (Entry e in _entries.Values) e.Image?.Dispose();
        _entries.Clear();
    }

    public void Dispose()
    {
        _disposed = true;
        Clear();
    }
}

/// <summary>
/// Sits between the engine and the sound output and turns "splash.wav" into the full path of that
/// file in the open book's folder. Everything else passes straight through.
/// </summary>
public sealed class BookFolderSound : ISoundOutput
{
    readonly ISoundOutput _inner;

    public BookFolderSound(ISoundOutput inner) => _inner = inner;

    /// <summary>The open book's folder, or null when no book is open.</summary>
    public string? Folder { get; set; }

    public void Tap() => _inner.Tap();
    public void Zoom(bool zoomIn) => _inner.Zoom(zoomIn);
    public void Find() => _inner.Find();
    public void Locked() => _inner.Locked();
    public void Party() => _inner.Party();
    public void Whoosh(double milliseconds) => _inner.Whoosh(milliseconds);
    public void PageTurn() => _inner.PageTurn();
    public void Rain(double level) => _inner.Rain(level);
    public void Thunder(double delaySeconds) => _inner.Thunder(delaySeconds);
    public void StopClip() => _inner.StopClip();
    public bool ClipPlaying => _inner.ClipPlaying;

    public void PlayClip(string fileName)
    {
        string? path = Folder is null ? null : BookLibrary.SafePath(Folder, fileName);
        if (path is not null && File.Exists(path)) _inner.PlayClip(path);
    }
}
