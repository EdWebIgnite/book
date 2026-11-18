using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Reader.Core;
using Reader.Ui;

namespace Reader.App;

/// <summary>What the parents' area needs from the app around it.</summary>
public interface IParentsHost
{
    AppState State { get; }
    ReaderSettings Settings { get; }
    bool Touch { get; }
    bool CanImport { get; }
    bool CanOpenFolders { get; }
    /// <summary>Every book on the shelf.</summary>
    IReadOnlyList<ShelfEntry> Books { get; }
    /// <summary>True for a book added from a book file (one that can be removed).</summary>
    bool IsAdded(ShelfEntry book);
    /// <summary>The device's own language, as a code ("af", "en").</summary>
    string DeviceLanguage { get; }
    /// <summary>A reading setting changed (the key is the settings' JSON name).</summary>
    void SettingChanged(string key);
    /// <summary>The children or their progress changed: keep it and bring the shelf up to date.</summary>
    void ChildrenChanged();
    void SetLanguage(string choice);
    void SetBedtime(bool on);
    void PickBookFile();
    void OpenFolder();
    /// <summary>Take a book added from a file off the device. False when it couldn't be.</summary>
    bool RemoveBook(ShelfEntry book);
    void ShowWelcome();
    /// <summary>The playground's fine tuning, for testers.</summary>
    Control MakeTuning();
}

/// <summary>
/// The grown-ups' area, behind the gate: the children, reading choices, books, language, bedtime colours,
/// and what the app does and doesn't do with information. A full screen on a phone; a sheet down the
/// right-hand side on a tablet or a computer. Each part that needs more room has a page of its own,
/// with Back at the top left.
/// </summary>
public sealed class ParentsView : UserControl
{
    public enum Section { Home, Reading, Books, Language, About, Child, NewChild, Tuning }

    readonly IParentsHost _host;
    readonly Border _sheet;
    readonly Border _scrim;
    readonly Grid _frame = new() { RowDefinitions = new RowDefinitions("Auto,*") };
    readonly Stack<Section> _back = new();
    Thickness _safe;
    int _versionTaps;
    bool _tuningShown;

    public ParentsView(IParentsHost host, Section start = Section.Home, string? child = null)
    {
        _host = host;
        FontFamily = Palette.Font;
        ChildId = child;
        _scrim = new Border { Background = Palette.Brush(Palette.Forest, 0.45) };
        _scrim.PointerPressed += (_, e) => { e.Handled = true; Closed?.Invoke(); };
        _sheet = new Border { Background = Palette.CreamBrush, Child = _frame, ClipToBounds = true };
        _sheet.PointerPressed += (_, e) => e.Handled = true;
        Content = new Panel { Children = { _scrim, _sheet } };
        KeyboardNavigation.SetTabNavigation(_sheet, KeyboardNavigationMode.Cycle);
        Current = start;
        if (start != Section.Home) _back.Push(Section.Home);
        Show(start);
    }

    public event Action? Closed;

    /// <summary>The page showing, so the area can be built again on the same page (after the language or colours change).</summary>
    public Section Current { get; private set; }
    public string? ChildId { get; private set; }

    /// <summary>A note under Books: a book being added, added, or turned down.</summary>
    public (string Text, bool Good)? BookNote { get; set; }

    public Thickness SafeArea { set { _safe = value; Fit(Bounds.Size); } }

    /// <summary>Back: a page goes back to the one before; the first page closes the area. Returns false if there was nothing to go back from.</summary>
    public bool GoBack()
    {
        if (_back.Count > 0) { Show(_back.Pop()); return true; }
        Closed?.Invoke();
        return true;
    }

    void Open(Section s, string? child = null)
    {
        _back.Push(Current);
        ChildId = child ?? ChildId;
        Show(s);
    }

    /// <summary>Show a page again with what is true now (after a book was added, for example).</summary>
    public void Refresh() => Show(Current);

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Fit(e.NewSize);
    }

    bool Sheet(Size s) => s.Width >= 900 && s.Height >= 560;

    void Fit(Size size)
    {
        if (size.Width <= 0) return;
        bool sheet = Sheet(size);
        _scrim.IsVisible = sheet;
        _sheet.HorizontalAlignment = sheet ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        _sheet.Width = sheet ? 500 : double.NaN;
        _sheet.BoxShadow = sheet ? new BoxShadows(new BoxShadow { OffsetX = -6, Blur = 30, Color = Palette.ShadowOf(0.3) }) : default;
        _frame.Margin = new Thickness(sheet ? 0 : _safe.Left, _safe.Top, _safe.Right, 0);
        _bottomRoom = 28 + _safe.Bottom;
        if (_body is not null) _body.Margin = new Thickness(18, 4, 18, _bottomRoom);
    }

    double _bottomRoom = 28;
    StackPanel? _body;

    // ---------------------------------------------------------------- pages

    void Show(Section s)
    {
        Current = s;
        _frame.Children.Clear();
        bool home = s == Section.Home;
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12, Margin = new Thickness(16, 14, 16, 10) };
        if (!home)
        {
            var back = new RoundTap(Strings.T("common.back"), Icons.Back, 48);
            back.Click += () => GoBack();
            head.Children.Add(back);
        }
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(home ? 4 : 0, 0, 0, 0) };
        if (home) titles.Children.Add(Kit.Label(Brand.Name, 12.5));
        titles.Children.Add(Kit.Heading(Title(s), home ? 30 : 24));
        Grid.SetColumn(titles, 1);
        head.Children.Add(titles);
        var close = new RoundTap(Strings.T("common.close"), Icons.Close, 48);
        close.Click += () => Closed?.Invoke();
        Grid.SetColumn(close, 2);
        head.Children.Add(close);
        _frame.Children.Add(head);

        _body = new StackPanel { Spacing = 10, Margin = new Thickness(18, 4, 18, _bottomRoom) };
        switch (s)
        {
            case Section.Home: HomePage(_body); break;
            case Section.Reading: ReadingPage(_body); break;
            case Section.Books: BooksPage(_body); break;
            case Section.Language: LanguagePage(_body); break;
            case Section.About: AboutPage(_body); break;
            case Section.Child: ChildPage(_body, _host.State.Profiles.FirstOrDefault(p => p.Id == ChildId)); break;
            case Section.NewChild: ChildPage(_body, null); break;
            case Section.Tuning: _body.Children.Add(_host.MakeTuning()); break;
        }
        var scroll = new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        _frame.Children.Add(scroll);
        Fit(Bounds.Size);
    }

    static string Title(Section s) => s switch
    {
        Section.Reading => Strings.T("parents.reading"),
        Section.Books => Strings.T("parents.books"),
        Section.Language => Strings.T("parents.language"),
        Section.About => Strings.T("parents.about"),
        Section.Child => Strings.T("parents.child"),
        Section.NewChild => Strings.T("profile.add"),
        Section.Tuning => Strings.T("about.tuning"),
        _ => Strings.T("parents.title"),
    };

    static Control Group(string? label, params Control[] rows)
    {
        var inner = new StackPanel();
        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0) inner.Children.Add(Kit.Divider());
            inner.Children.Add(rows[i]);
        }
        var card = Kit.Card(inner);
        if (label is null) return card;
        var l = Kit.Label(label);
        l.Margin = new Thickness(6, 14, 0, 2);
        return new StackPanel { Spacing = 6, Children = { l, card } };
    }

    // ---------------------------------------------------------------- the first page

    void HomePage(StackPanel body)
    {
        AppState st = _host.State;
        var rows = new List<Control>();
        foreach (ChildProfile child in st.Profiles)
        {
            ChildProfile c = child;
            var row = new Row(null, string.IsNullOrEmpty(c.Name) ? Strings.T("profile.unnamed") : c.Name, Facts(c), arrow: true, leading: new Avatar(c.Avatar, 48));
            row.Click += () => Open(Section.Child, c.Id);
            rows.Add(row);
        }
        if (st.CanAdd)
        {
            var add = new Row(Icons.Plus, Strings.T("profile.add"), null, arrow: true, titleColour: Palette.LeafBrush);
            add.Click += () => Open(Section.NewChild);
            rows.Add(add);
        }
        else rows.Add(new Row(Icons.Info, Strings.T("profile.full")));
        body.Children.Add(Group(Strings.T("parents.children"), rows.ToArray()));

        var reading = new Row(Icons.Speaker, Strings.T("parents.reading"), Strings.T("parents.reading.sum"), arrow: true);
        reading.Click += () => Open(Section.Reading);
        var books = new Row(Icons.Books, Strings.T("parents.books"), Strings.T("parents.books.sum", _host.Books.Count), arrow: true);
        books.Click += () => Open(Section.Books);
        var language = new Row(Icons.Globe, Strings.T("parents.language"), LanguageNote(), arrow: true);
        language.Click += () => Open(Section.Language);
        var night = new Switch(Strings.T("parents.bedtime"), st.Bedtime);
        night.Toggled += on => _host.SetBedtime(on);
        var bedtime = new Row(Icons.Moon, Strings.T("parents.bedtime"), Strings.T("bedtime.help"), night);
        var about = new Row(Icons.Shield, Strings.T("parents.about"), Strings.T("parents.about.sum"), arrow: true);
        about.Click += () => Open(Section.About);
        body.Children.Add(Group(Strings.T("parents.app"), reading, books, language, bedtime, about));
    }

    string LanguageNote()
    {
        string choice = _host.State.Language;
        string name = Strings.Languages.FirstOrDefault(l => l.Code == Strings.Language).Name ?? "English";
        return choice == "auto" ? Strings.T("lang.device", name) : name;
    }

    /// <summary>"2 books finished · read on 3 days this week": facts, never a score.</summary>
    public static string Facts(ChildProfile c)
    {
        int finished = c.FinishedCount, days = c.DaysThisWeek(DateOnly.FromDateTime(DateTime.Now));
        string a = finished == 0 ? Strings.T("profile.finished.none") : finished == 1 ? Strings.T("profile.finished.one") : Strings.T("profile.finished.many", finished);
        string b = days == 0 ? Strings.T("profile.days.none") : days == 1 ? Strings.T("profile.days.one") : Strings.T("profile.days.many", days);
        return a + " · " + b;
    }

    // ---------------------------------------------------------------- reading

    void ReadingPage(StackPanel body)
    {
        ReaderSettings s = _host.Settings;
        Control Toggle(string icon, string key, string label, string help, Func<bool> get, Action<bool> set)
        {
            var sw = new Switch(label, get());
            sw.Toggled += on => { set(on); _host.SettingChanged(key); };
            return new Row(icon, label, help, sw);
        }
        Control Pick(string icon, string key, string label, string help, (string, string)[] options, Func<string> get, Action<string> set)
        {
            var choices = new Choices(label, options, get());
            choices.Changed += v => { set(v); _host.SettingChanged(key); };
            choices.Margin = new Thickness(16, 0, 16, 14);
            return new StackPanel { Children = { new Row(icon, label, help), choices } };
        }

        body.Children.Add(Group(Strings.T("parents.reading.words"),
            Toggle(Icons.Speaker, "readToMe", Strings.T("set.readToMe"), Strings.T("set.readToMe.help"), () => s.ReadToMe, v => s.ReadToMe = v),
            Toggle(Icons.Eye, "printedWords", Strings.T("set.printedWords"), Strings.T("set.printedWords.help"), () => s.PrintedWords, v => s.PrintedWords = v),
            Toggle(Icons.BookFile, "popUpWords", Strings.T("set.popUpWords"), Strings.T("set.popUpWords.help"), () => s.PopUpWords, v => s.PopUpWords = v),
            Pick(Icons.TextSize, "captionSize", Strings.T("set.captionSize"), "", new[] { ("small", Strings.T("size.small")), ("medium", Strings.T("size.medium")), ("large", Strings.T("size.large")) },
                () => s.CaptionSize, v => s.CaptionSize = v),
            Toggle(Icons.Child, "prompts", Strings.T("set.prompts"), Strings.T("set.prompts.help"), () => s.Prompts, v => s.Prompts = v)));

        body.Children.Add(Group(Strings.T("parents.reading.pages"),
            Pick(Icons.Find, "extras", Strings.T("set.extras"), Strings.T("set.extras.help"), new[] { ("afterStory", Strings.T("extras.afterStory")), ("on", Strings.T("extras.on")), ("off", Strings.T("extras.off")) },
                () => s.Extras, v => s.Extras = v),
            Toggle(Icons.Books, "bookLook", Strings.T("set.bookLook"), Strings.T("set.bookLook.help"), () => s.BookLook, v => s.BookLook = v),
            Toggle(Icons.Play, "autoPlay", Strings.T("set.autoPlay"), Strings.T("set.autoPlay.help"), () => s.AutoPlay, v => s.AutoPlay = v)));

        body.Children.Add(Group(Strings.T("parents.reading.calm"),
            Pick(Icons.Again, "reduceMotion", Strings.T("set.motion"), Strings.T("set.motion.help"), new[] { ("auto", Strings.T("motion.auto")), ("off", Strings.T("motion.off")), ("on", Strings.T("motion.on")) },
                () => s.ReduceMotion, v => s.ReduceMotion = v),
            Toggle(Icons.Speaker, "sound", Strings.T("set.sound"), Strings.T("set.sound.help"), () => s.Sound, v => s.Sound = v)));
        var age = Kit.Text(Strings.T("parents.reading.age"), 14, FontWeight.Normal, Palette.InkSoftBrush);
        age.Margin = new Thickness(6, 4, 6, 0);
        body.Children.Add(age);
    }

    // ---------------------------------------------------------------- books

    void BooksPage(StackPanel body)
    {
        if (_host.CanImport)
        {
            var add = new Pill(Strings.T("books.add"), PillKind.Main, Icons.Plus, height: 56, fontSize: 18);
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Click += () => _host.PickBookFile();
            var help = Kit.Text(_host.Touch ? Strings.T("books.add.help.touch", Brand.Name) : Strings.T("books.add.help.desktop"), 14, FontWeight.Normal, Palette.InkSoftBrush);
            var inner = new StackPanel { Spacing = 10, Margin = new Thickness(16), Children = { add, help } };
            if (BookNote is (string text, bool good))
            {
                inner.Children.Add(new Border
                {
                    Background = Palette.Brush(good ? Palette.Mint : Palette.Night ? Color.Parse("#3a2f4a") : Color.Parse("#f8efd6")),
                    BorderBrush = Palette.Brush(good ? Palette.Tick : Palette.Sun), BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8),
                    Child = new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 10,
                        Children = { Kit.Icon(good ? Icons.Tick : Icons.Info, 20, good ? Palette.Brush(Palette.Tick) : Palette.InkBrush), Wrap(Kit.Text(text, 14, FontWeight.SemiBold)) },
                    },
                });
            }
            body.Children.Add(Kit.Card(inner));
        }
        if (_host.CanOpenFolders)
        {
            var folder = new Pill(Strings.T("books.folder"), PillKind.Plain, Icons.Folder, height: 50, fontSize: 16);
            folder.HorizontalAlignment = HorizontalAlignment.Left;
            folder.Click += () => _host.OpenFolder();
            var help = Kit.Text(Strings.T("books.folder.help"), 14, FontWeight.Normal, Palette.InkSoftBrush);
            body.Children.Add(Kit.Card(new StackPanel { Spacing = 10, Margin = new Thickness(16), Children = { folder, help } }));
        }

        var rows = new List<Control>();
        foreach (ShelfEntry b in _host.Books)
        {
            bool added = _host.IsAdded(b);
            string note = (added ? Strings.T("books.added") : Strings.T("books.builtIn")) + " · " + Strings.T("books.size", SizeMb(b.Folder));
            if (Strings.LanguageTag(b.Language) is { Length: > 0 } tag) note += " · " + tag;
            Control? trailing = null;
            if (added)
            {
                var remove = new Pill(Strings.T("books.remove"), PillKind.Plain, height: 44, fontSize: 15);
                ShelfEntry book = b;
                remove.Click += () => AskRemove(rows, remove, book);
                trailing = remove;
            }
            rows.Add(new Row(Icons.BookFile, b.Title, note, trailing));
        }
        if (rows.Count == 0) rows.Add(new Row(Icons.Info, Strings.T("shelf.empty.title")));
        body.Children.Add(Group(Strings.T("books.onShelf"), rows.ToArray()));
    }

    static Control Wrap(TextBlock t) { t.MaxWidth = 380; return t; }

    void AskRemove(List<Control> rows, Pill remove, ShelfEntry book)
    {
        // Asked once more, so a stray tap doesn't take a book away.
        if (remove.Parent is not Grid grid) return;
        var yes = new Pill(Strings.T("books.remove.yes"), PillKind.Danger, height: 44, fontSize: 15);
        var no = new Pill(Strings.T("books.remove.no"), PillKind.Plain, height: 44, fontSize: 15);
        var ask = new StackPanel { Spacing = 8, Children = { yes, no }, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(ask, 2);
        grid.Children.Remove(remove);
        grid.Children.Add(ask);
        if (grid.Children.OfType<StackPanel>().FirstOrDefault(p => Grid.GetColumn(p) == 1) is StackPanel words && words.Children.FirstOrDefault() is TextBlock title)
            title.Text = Strings.T("books.remove.ask", book.Title);
        no.Click += Refresh;
        yes.Click += () =>
        {
            bool gone = _host.RemoveBook(book);
            BookNote = (gone ? Strings.T("books.removed", book.Title) : Strings.T("books.cantRemove"), gone);
            Refresh();
        };
    }

    static readonly Dictionary<string, string> Sizes = new();

    static string SizeMb(string folder)
    {
        if (Sizes.TryGetValue(folder, out string? known)) return known;
        long bytes = 0;
        try { bytes = new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        double mb = bytes / (1024.0 * 1024.0);
        return Sizes[folder] = mb < 10 ? mb.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace(".", Strings.Language == "af" ? "," : ".") : Math.Round(mb).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    // ---------------------------------------------------------------- language

    void LanguagePage(StackPanel body)
    {
        string chosen = _host.State.Language;
        string device = Strings.Resolve("auto", _host.DeviceLanguage);
        string deviceName = Strings.Languages.FirstOrDefault(l => l.Code == device).Name ?? "English";
        var options = new List<(string Value, string Text)> { ("auto", Strings.T("lang.device", deviceName)) };
        foreach (var (code, name) in Strings.Languages) options.Add((code, name));
        var rows = new List<Control>();
        foreach (var (value, text) in options)
        {
            bool on = value == chosen;
            var mark = new Border
            {
                Width = 30, Height = 30, CornerRadius = new CornerRadius(15),
                Background = on ? Palette.LeafBrush : Brushes.Transparent, BorderBrush = on ? Palette.LeafBrush : Palette.Brush(Palette.InkFaint), BorderThickness = new Thickness(2),
                Child = on ? Kit.Icon(Icons.Tick, 18, Palette.OnLeafBrush) : null,
            };
            string v = value;
            var row = new Row(null, text, null, mark, arrow: true);
            row.Click += () => _host.SetLanguage(v);
            rows.Add(row);
        }
        body.Children.Add(Group(Strings.T("lang.app"), rows.ToArray()));
        var note = Kit.Text(Strings.T("lang.note"), 14, FontWeight.Normal, Palette.InkSoftBrush);
        note.Margin = new Thickness(6, 4, 6, 0);
        body.Children.Add(note);
    }

    // ---------------------------------------------------------------- about and privacy

    void AboutPage(StackPanel body)
    {
        var mark = new Wordmark { Size = 40, HorizontalAlignment = HorizontalAlignment.Left };
        var version = new Tap(Strings.T("about.version", Brand.Name, Brand.Version))
        {
            Child = Kit.Text(Strings.T("about.version", Brand.Name, Brand.Version), 15, FontWeight.SemiBold, Palette.InkSoftBrush),
            Cursor = Cursor.Default, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0, 4),
        };
        // Seven taps on the version show the fine tuning for testers (always shown on a computer).
        version.Click += () => { if (++_versionTaps >= 7 && !_tuningShown) { _tuningShown = true; Refresh(); } };
        body.Children.Add(Kit.Card(new StackPanel
        {
            Margin = new Thickness(18, 16), Spacing = 6,
            Children = { mark, version, Kit.Text(Strings.T("brand.tagline"), 16, FontWeight.Bold) },
        }));

        Control Fact(string icon, string text)
        {
            var t = Kit.Text(text, 16, FontWeight.SemiBold);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 14, Margin = new Thickness(16, 12) };
            grid.Children.Add(Kit.Icon(icon, 24, Palette.LeafBrush));
            Grid.SetColumn(t, 1);
            grid.Children.Add(t);
            return grid;
        }
        body.Children.Add(Group(Strings.T("about.privacy"),
            Fact(Icons.Globe, Strings.T("about.p1", Brand.Name)),
            Fact(Icons.Lock, Strings.T("about.p2")),
            Fact(Icons.Shield, Strings.T("about.p3")),
            Fact(Icons.Info, Strings.T("about.p4")),
            Fact(Icons.Device, Strings.T("about.p5"))));

        var welcome = new Pill(Strings.T("about.welcome"), PillKind.Plain, Icons.Again, height: 50, fontSize: 16);
        welcome.HorizontalAlignment = HorizontalAlignment.Left;
        welcome.Click += () => _host.ShowWelcome();
        welcome.Margin = new Thickness(0, 8, 0, 0);
        body.Children.Add(welcome);
        if (!_host.Touch || _tuningShown)
        {
            var tuning = new Pill(Strings.T("about.tuning"), PillKind.Link, Icons.Chevron, height: 48, fontSize: 15);
            tuning.HorizontalAlignment = HorizontalAlignment.Left;
            tuning.Click += () => Open(Section.Tuning);
            body.Children.Add(tuning);
        }
        var fonts = Kit.Text(Strings.T("about.fonts"), 13, FontWeight.Normal, Palette.InkSoftBrush);
        fonts.Margin = new Thickness(6, 8, 6, 0);
        body.Children.Add(fonts);
    }

    // ---------------------------------------------------------------- one child

    void ChildPage(StackPanel body, ChildProfile? child)
    {
        AppState st = _host.State;
        int avatar = child?.Avatar ?? st.FreeAvatar();
        var editor = new ProfileEditor(child?.Name ?? "", avatar, child?.Age ?? _host.Settings.Age, compact: Bounds.Width < 420);
        body.Children.Add(Kit.Card(new Border { Padding = new Thickness(16), Child = editor }));
        var save = new Pill(child is null ? Strings.T("profile.add") : Strings.T("common.save"), PillKind.Main, Icons.Tick, height: 56, fontSize: 18);
        save.HorizontalAlignment = HorizontalAlignment.Left;
        save.Click += () =>
        {
            if (child is null)
            {
                string current = st.Current;
                st.Add(editor.ChildName, editor.Picture, editor.Age);
                // Adding a child from here doesn't change who is reading now.
                if (current.Length > 0) st.Choose(current);
            }
            else
            {
                child.Name = editor.ChildName;
                child.Avatar = editor.Picture;
                child.Age = editor.Age;
            }
            _host.ChildrenChanged();
            GoBack();
        };
        body.Children.Add(save);
        if (child is null) return;

        // What they have read: facts, never a score.
        var read = new List<Control>();
        foreach (ShelfEntry b in _host.Books)
        {
            if (child.For(b.Id) is not BookProgress p || (!p.Finished && !p.Partway)) continue;
            var parts = new List<string>();
            if (p.Finished) parts.Add(Strings.T("profile.bookFinished", Strings.Date(DateOnly.FromDateTime(p.LastRead.ToLocalTime()))));
            if (p.Partway) parts.Add(Strings.T("shelf.page", p.Page + 1, Math.Max(p.PageCount, p.Page + 1)));
            read.Add(new Row(p.Finished ? Icons.Tick : Icons.BookFile, b.Title, string.Join(" · ", parts)));
        }
        if (read.Count == 0) read.Add(new Row(Icons.Info, Strings.T("profile.nothingYet")));
        read.Insert(0, new Row(Icons.Child, Facts(child)));
        body.Children.Add(Group(Strings.T("profile.books"), read.ToArray()));

        var remove = new Pill(Strings.T("profile.remove"), PillKind.Plain, height: 48, fontSize: 16);
        remove.HorizontalAlignment = HorizontalAlignment.Left;
        remove.Margin = new Thickness(0, 10, 0, 0);
        var holder = new StackPanel { Spacing = 8, Children = { remove } };
        remove.Click += () =>
        {
            holder.Children.Clear();
            holder.Children.Add(Kit.Text(Strings.T("profile.remove.ask", string.IsNullOrEmpty(child.Name) ? Strings.T("profile.unnamed") : child.Name), 16, FontWeight.Bold));
            var yes = new Pill(Strings.T("profile.remove.yes"), PillKind.Danger, height: 48, fontSize: 16);
            var no = new Pill(Strings.T("profile.remove.no"), PillKind.Plain, height: 48, fontSize: 16);
            yes.Click += () => { st.Remove(child.Id); _host.ChildrenChanged(); GoBack(); };
            no.Click += Refresh;
            holder.Children.Add(new WrapPanel { ItemSpacing = 10, LineSpacing = 10, Children = { yes, no } });
        };
        body.Children.Add(holder);
    }
}
