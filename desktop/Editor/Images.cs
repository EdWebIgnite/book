using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace BookEditor;

/// <summary>
/// Pictures for the editor's screens, read at the size they are shown: small ones for the page list
/// and the stop strip, a screen-sized one for the page being edited. Read in the background and kept,
/// and read again if the file changes on disk.
/// </summary>
sealed class Images
{
    sealed class Entry
    {
        public Bitmap? Bitmap;
        public DateTime Stamp;
        public bool Loading;
        public long Used;
    }

    readonly Dictionary<(string, int), Entry> _entries = new();
    long _tick;

    /// <summary>Raised on the screen's thread when a picture has finished loading.</summary>
    public event Action? Arrived;

    /// <summary>Read pictures on the spot rather than in the background (for screenshots).</summary>
    public bool Synchronous { get; set; }

    /// <summary>A picture no wider than <paramref name="width"/> pixels, or null while it loads.</summary>
    public Bitmap? Get(string? path, int width)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        DateTime stamp = File.GetLastWriteTimeUtc(path);
        var key = (path, width);
        if (_entries.TryGetValue(key, out Entry? e))
        {
            e.Used = ++_tick;
            if (e.Stamp == stamp && (e.Bitmap is not null || e.Loading)) return e.Bitmap;
        }
        else
        {
            e = new Entry { Used = ++_tick };
            _entries[key] = e;
            Trim();
        }
        e.Stamp = stamp;
        if (Synchronous)
        {
            e.Bitmap = Read(path, width);
            return e.Bitmap;
        }
        if (e.Loading) return e.Bitmap;
        e.Loading = true;
        Entry entry = e;
        Task.Run(() =>
        {
            Bitmap? b = Read(path, width);
            Dispatcher.UIThread.Post(() =>
            {
                entry.Loading = false;
                Bitmap? old = entry.Bitmap;
                entry.Bitmap = b;
                if (old is not null && !ReferenceEquals(old, b)) Dispatcher.UIThread.Post(old.Dispose, DispatcherPriority.Background);
                Arrived?.Invoke();
            });
        });
        return e.Bitmap;
    }

    static Bitmap? Read(string path, int width)
    {
        try
        {
            var size = Editor.Core.Pictures.Size(path);
            using FileStream f = File.OpenRead(path);
            if (size is (int w, int h) && w > width) return Bitmap.DecodeToWidth(f, width, BitmapInterpolationMode.HighQuality);
            return new Bitmap(f);
        }
        catch (Exception) { return null; }
    }

    void Trim()
    {
        if (_entries.Count <= 160) return;
        foreach (var old in _entries.OrderBy(x => x.Value.Used).Take(40).ToList())
        {
            _entries.Remove(old.Key);
            if (old.Value.Bitmap is Bitmap b) Dispatcher.UIThread.Post(b.Dispose, DispatcherPriority.Background);
        }
    }
}
