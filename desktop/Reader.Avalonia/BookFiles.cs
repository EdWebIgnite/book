using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Reader.Core;

namespace Reader.Ui;

/// <summary>A book opened from a folder on disk: its book.json and its page pictures, ready to show.</summary>
public sealed class LoadedBook : IDisposable
{
    readonly Bitmap[] _pages;

    LoadedBook(Book book, string folder, Bitmap[] pages)
    {
        Book = book;
        Folder = folder;
        _pages = pages;
        Aspects = pages.Select(p => p.PixelSize.Height > 0 ? (double)p.PixelSize.Width / p.PixelSize.Height : 1).ToArray();
    }

    public Book Book { get; }
    public string Folder { get; }

    /// <summary>Each page picture's width divided by its height, which is what the engine lays the pages out from.</summary>
    public double[] Aspects { get; }

    public Bitmap PageImage(int page) => _pages[page];

    /// <summary>
    /// Reads book.json and every page picture. Throws if the folder isn't a book or a picture is missing,
    /// with a message plain enough to show on the shelf.
    /// </summary>
    public static LoadedBook Load(string folder)
    {
        if (!File.Exists(Path.Combine(folder, "book.json"))) throw new FileNotFoundException("There is no book.json in " + folder + ".");
        Book book = BookJson.Load(folder);
        if (book.Pages.Count == 0) throw new InvalidDataException("This book has no pages.");
        var pages = new Bitmap[book.Pages.Count];
        try
        {
            for (int i = 0; i < pages.Length; i++)
            {
                string? path = BookLibrary.SafePath(folder, book.Pages[i].Image);
                if (path is null || !File.Exists(path)) throw new FileNotFoundException($"Page {i + 1}'s picture is missing: {book.Pages[i].Image}");
                pages[i] = new Bitmap(path);
            }
        }
        catch
        {
            foreach (Bitmap? b in pages) b?.Dispose();
            throw;
        }
        return new LoadedBook(book, folder, pages);
    }

    /// <summary>The same, off the screen's thread, so a big book doesn't freeze the window while it opens.</summary>
    public static Task<LoadedBook> LoadAsync(string folder) => Task.Run(() => Load(folder));

    public void Dispose()
    {
        foreach (Bitmap b in _pages) b.Dispose();
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
