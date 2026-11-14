using Editor.Core;
using Reader.Core;

namespace BookEditor;

/// <summary>What is selected on the page.</summary>
enum Sel { None, Stop, Hotspot, Layer, Dry }

/// <summary>What dragging on an empty part of the page makes.</summary>
enum DrawKind { Stop, Hotspot, Dry }

/// <summary>Why the screens need to catch up.</summary>
enum Why
{
    /// <summary>A whole new book (opened, or undo and redo swapped it): everything rebuilds.</summary>
    Book,
    /// <summary>Pages or stops were added, removed or reordered: lists rebuild.</summary>
    Structure,
    /// <summary>Something inside was changed (a box moved, words typed): redraw, but don't rebuild what's being typed in.</summary>
    Content,
    /// <summary>A different page or thing was selected.</summary>
    Selection,
    /// <summary>Saved, or the unsaved mark changed.</summary>
    Saved,
}

/// <summary>
/// The book being edited and where the editor is in it: the page shown, what is selected, undo and
/// redo, and whether there are unsaved changes. Every change goes through <see cref="Change"/>, so
/// everything can be undone and every screen hears about it.
/// </summary>
sealed class EditorState
{
    public BookProject? Project { get; private set; }
    public History History { get; } = new();
    public bool HasBook => Project is not null;
    public Book Book => Project!.Book;

    public int PageIndex { get; private set; }
    public Sel Selected { get; private set; }
    public int SelIndex { get; private set; } = -1;
    /// <summary>For a selected dry area: which effect it belongs to.</summary>
    public int SelEffect { get; private set; } = -1;

    public DrawKind Drawing { get; set; } = DrawKind.Stop;
    /// <summary>The rain or snow a newly drawn dry area goes with.</summary>
    public int DryTarget { get; set; } = -1;

    public bool Dirty { get; private set; }
    public DateTime? SavedAt { get; private set; }

    /// <summary>Raised after every change, with why and with whoever made it (so a text box needn't rebuild itself).</summary>
    public event Action<Why, object?>? Changed;

    public Page? Page => HasBook && PageIndex >= 0 && PageIndex < Book.Pages.Count ? Book.Pages[PageIndex] : null;

    public Stop? Stop => Selected == Sel.Stop && Page is Page p && SelIndex >= 0 && SelIndex < p.Stops.Count ? p.Stops[SelIndex] : null;
    public Hotspot? Hotspot => Selected == Sel.Hotspot && Page is Page p && SelIndex >= 0 && SelIndex < p.Hotspots.Count ? p.Hotspots[SelIndex] : null;
    public Layer? Layer => Selected == Sel.Layer && Page is Page p && SelIndex >= 0 && SelIndex < p.Layers.Count ? p.Layers[SelIndex] : null;

    /// <summary>The selection as text, kept with each undo step so undo puts the selection back too.</summary>
    string SelectionText => $"{Selected}:{SelIndex}:{SelEffect}";

    public void Open(BookProject project)
    {
        Project = project;
        History.Clear();
        PageIndex = 0;
        Selected = Sel.None;
        SelIndex = -1;
        Drawing = DrawKind.Stop;
        Dirty = false;
        SavedAt = null;
        Changed?.Invoke(Why.Book, null);
    }

    /// <summary>Take a recovered copy of the book in place of the saved one. It counts as unsaved.</summary>
    public void Recover(Book book)
    {
        History.Record(Book, "Get back unsaved changes");
        Project!.Book = book;
        Dirty = true;
        Changed?.Invoke(Why.Book, null);
    }

    public void Close()
    {
        Project = null;
        History.Clear();
        Dirty = false;
        Changed?.Invoke(Why.Book, null);
    }

    /// <summary>
    /// Make a change that can be undone. <paramref name="mergeKey"/> joins quick repeats into one step
    /// (typing, dragging); <paramref name="structure"/> says lists need rebuilding.
    /// </summary>
    public void Change(string label, Action change, string? mergeKey = null, object? source = null, bool structure = false)
    {
        if (!HasBook) return;
        History.Record(Book, label, PageIndex, SelectionText, mergeKey);
        change();
        Dirty = true;
        Changed?.Invoke(structure ? Why.Structure : Why.Content, source);
    }

    /// <summary>
    /// For dragging: remember the book once when a drag first moves, then change it freely while the
    /// mouse moves (call <see cref="Touched"/> after each move), all as one undo step.
    /// </summary>
    public void BeginDrag(string label)
    {
        History.Break();
        History.Record(Book, label, PageIndex, SelectionText);
        Dirty = true;
    }

    public void Touched(object? source = null) => Changed?.Invoke(Why.Content, source);

    public void Undo() => Swap(History.Undo(Book, PageIndex, SelectionText));
    public void Redo() => Swap(History.Redo(Book, PageIndex, SelectionText));

    void Swap((Book Book, History.Entry Entry)? result)
    {
        if (result is not var (book, entry) || Project is null) return;
        Project.Book = book;
        PageIndex = Math.Clamp(entry.Page, 0, Math.Max(0, book.Pages.Count - 1));
        string[] parts = entry.Selection.Split(':');
        if (parts.Length == 3 && Enum.TryParse(parts[0], out Sel sel) && int.TryParse(parts[1], out int i) && int.TryParse(parts[2], out int fx))
        {
            Selected = sel; SelIndex = i; SelEffect = fx;
        }
        else { Selected = Sel.None; SelIndex = -1; }
        ValidateSelection();
        Dirty = true;
        Changed?.Invoke(Why.Book, null);
    }

    public void GoToPage(int index)
    {
        if (!HasBook || Book.Pages.Count == 0) return;
        index = Math.Clamp(index, 0, Book.Pages.Count - 1);
        if (index == PageIndex && Selected == Sel.None) return;
        PageIndex = index;
        Selected = Sel.None;
        SelIndex = -1;
        Drawing = DrawKind.Stop;
        DryTarget = -1;
        History.Break();
        Changed?.Invoke(Why.Selection, null);
    }

    public void Select(Sel kind, int index, int effect = -1)
    {
        Selected = kind;
        SelIndex = kind == Sel.None ? -1 : index;
        SelEffect = effect;
        ValidateSelection();
        History.Break();
        Changed?.Invoke(Why.Selection, null);
    }

    /// <summary>Go to a page and select something on it, in one step (Check book uses this).</summary>
    public void Show(int page, Sel kind, int index)
    {
        if (!HasBook) return;
        PageIndex = Math.Clamp(page, 0, Math.Max(0, Book.Pages.Count - 1));
        Selected = kind;
        SelIndex = index;
        SelEffect = -1;
        ValidateSelection();
        Changed?.Invoke(Why.Selection, null);
    }

    /// <summary>Keep the selection pointing at something that exists after a change.</summary>
    public void ValidateSelection()
    {
        if (!HasBook) return;
        PageIndex = Math.Clamp(PageIndex, 0, Math.Max(0, Book.Pages.Count - 1));
        Page? p = Page;
        bool ok = Selected switch
        {
            Sel.Stop => p is not null && SelIndex >= 0 && SelIndex < p.Stops.Count,
            Sel.Hotspot => p is not null && SelIndex >= 0 && SelIndex < p.Hotspots.Count,
            Sel.Layer => p is not null && SelIndex >= 0 && SelIndex < p.Layers.Count,
            Sel.Dry => p is not null && SelEffect >= 0 && SelEffect < p.Effects.Count && SelIndex >= 0 && SelIndex < p.Effects[SelEffect].Dry.Count,
            _ => true,
        };
        if (!ok) { Selected = Sel.None; SelIndex = -1; SelEffect = -1; }
    }

    /// <summary>After a change made outside <see cref="Change"/> (for example a selection move inside one).</summary>
    public void SetSelectionQuietly(Sel kind, int index, int effect = -1)
    {
        Selected = kind;
        SelIndex = index;
        SelEffect = effect;
    }

    public void SetPageQuietly(int index) => PageIndex = index;

    public void Save()
    {
        if (Project is null) return;
        Project.Save();
        Dirty = false;
        SavedAt = DateTime.Now;
        Changed?.Invoke(Why.Saved, null);
    }

    /// <summary>The size of a page's picture in pixels (cached), or a guess when it can't be read.</summary>
    public (int Width, int Height) PictureSize(string? name)
    {
        if (Project is null || string.IsNullOrEmpty(name)) return (1600, 1000);
        string? path = Project.PathOf(name);
        if (path is null || !File.Exists(path)) return (1600, 1000);
        DateTime stamp = File.GetLastWriteTimeUtc(path);
        if (_sizes.TryGetValue(path, out var known) && known.Stamp == stamp) return known.Size;
        var size = Pictures.Size(path) ?? (1600, 1000);
        _sizes[path] = (stamp, size);
        return size;
    }

    readonly Dictionary<string, (DateTime Stamp, (int, int) Size)> _sizes = new();

    /// <summary>A page's width divided by its height: the tiles' size for a tiled scene, else its picture's.</summary>
    public double Aspect(Page p)
    {
        if (p.Tiles is { Width: > 0, Height: > 0 } t) return (double)t.Width / t.Height;
        var (w, h) = PictureSize(p.Image);
        return h > 0 ? (double)w / h : 1.6;
    }
}
