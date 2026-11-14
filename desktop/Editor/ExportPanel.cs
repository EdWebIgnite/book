using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Editor.Core;
using Reader.Core;

namespace BookEditor;

/// <summary>
/// Export: make a copy of the book ready for the reader apps, with pictures at a sensible size for
/// phones, and optionally a zip file and a copy in a reader's books folder.
/// </summary>
sealed class ExportPanel : UserControl
{
    readonly EditorState _state;
    readonly MainWindow _window;
    readonly StackPanel _body = new() { Spacing = 2 };
    string _size = "phone";
    bool _zip = true, _addToLibrary;
    string _destination = "";
    string? _content;
    bool _busy;

    public ExportPanel(EditorState state, MainWindow window)
    {
        _state = state;
        _window = window;
        Content = new Border
        {
            Background = Brush(Color.FromArgb(150, 0, 0, 0)),
            Child = new Border
            {
                Width = 640, MaxHeight = 760, Background = Ui.Panel, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14),
                Padding = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 16, Blur = 60, Color = Color.FromArgb(150, 0, 0, 0) }),
                Child = new ScrollViewer { Content = _body },
            },
        };
    }

    static IBrush Brush(Color c) => new SolidColorBrush(c);

    public void Open()
    {
        if (!_state.HasBook) return;
        if (_destination.Length == 0) _destination = Path.Combine(Path.GetDirectoryName(_state.Project!.Folder) ?? EditorPaths.DefaultBooksFolder, "Exported books");
        _content ??= BookLibrary.FindContent(null, AppContext.BaseDirectory, Environment.CurrentDirectory);
        Build();
    }

    void Build(Control? result = null)
    {
        _body.Children.Clear();
        _body.Children.Add(Ui.Label("Export the book", 18, Ui.Text, FontWeight.Bold));
        _body.Children.Add(Ui.Help("Makes a copy of the book that is ready for the reader apps. The book you are editing is not changed."));
        int problems = BookChecker.Check(_state.Project!).Count(i => i.Level == Severity.Problem);
        if (problems > 0)
        {
            _body.Children.Add(new Border
            {
                Background = Ui.Brush("#3a1f1f"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 6, 0, 6),
                Child = new StackPanel
                {
                    Children =
                    {
                        Ui.Label($"Check book found {(problems == 1 ? "a problem" : problems + " problems")} to fix first. You can still export, but those parts will be missing.", 12.5, Ui.Text),
                        Ui.Row(Ui.Button("Open Check book", () => { _window.CloseExport(); _window.OpenCheck(); }, small: true)),
                    },
                },
            });
        }

        _body.Children.Add(Ui.FieldName("Picture size"));
        _body.Children.Add(Ui.Choice<string>(new List<(string, string)>
        {
            ("phone", "For phones and tablets (recommended): no picture bigger than 4096 pixels"),
            ("small", "Smaller download: no bigger than 2560 pixels"),
            ("originals", "Keep the pictures exactly as they are"),
        }, _size, v => _size = v));
        _body.Children.Add(Ui.Help("Page pictures without see-through parts become JPEG files, which are much smaller. Big explore scenes are cut into tiles, so a phone only loads the part being looked at. Pictures on top stay PNG."));
        _body.Children.Add(Ui.Check("Also make a zip file, to send or keep", _zip, v => _zip = v));

        _body.Children.Add(Ui.FieldName("Put the exported book in"));
        _body.Children.Add(FolderRow(_destination, async () => { if (await _window.PickFolder("Where should the exported book go?") is string f) { _destination = f; Build(); } }));

        _body.Children.Add(Ui.Check("Also add it to a reader's books (a content folder)", _addToLibrary && _content is not null, v => { _addToLibrary = v; Build(); }));
        if (_addToLibrary || _content is not null)
        {
            _body.Children.Add(FolderRow(_content ?? "No content folder chosen", async () => { if (await _window.PickFolder("Choose the reader's content folder (the one with library.json in it)") is string f) { _content = f; _addToLibrary = true; Build(); } }));
            _body.Children.Add(Ui.Help("The book is copied into content/books and added to the end of content/library.json. BookReader.exe shows it next time it starts, if that content folder is beside it or is the repo's."));
        }

        var go = Ui.Button(_busy ? "Exporting..." : "Export", Export, primary: true);
        go.IsEnabled = !_busy;
        _body.Children.Add(Ui.Row(go, Ui.Button("Close", () => _window.CloseExport(), "Esc")));
        if (result is not null) _body.Children.Add(result);

        _body.Children.Add(Ui.Heading("Getting a book into the phone app"));
        _body.Children.Add(Ui.Help("The phone app carries its books inside it. To add one: put the exported folder in the repo's content/books folder and its name in content/library.json (the tick box above does both), then build the app again with publish-android.sh and install the new APK. The Windows reader can open any exported folder straight away with \"Open a book folder\"."));
    }

    static Control FolderRow(string folder, Action pick)
    {
        var text = Ui.Label(folder, 12, Ui.Text);
        var change = Ui.Button("Change...", pick, small: true);
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        g.Children.Add(new Border { Background = Ui.Field, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(8, 6), Child = text });
        Grid.SetColumn(change, 1);
        g.Children.Add(change);
        return g;
    }

    void Export()
    {
        if (_busy) return;
        _busy = true;
        ExportOptions options = (_size switch { "small" => ExportOptions.SmallDownload, "originals" => ExportOptions.Originals, _ => ExportOptions.Phone }) with { Zip = _zip };
        BookProject project = _state.Project!;
        string destination = _destination;
        string? content = _addToLibrary ? _content : null;
        var progressText = Ui.Label("Starting...", 12.5, Ui.Muted);
        Build(progressText);
        var progress = new Progress<string>(s => progressText.Text = s + "...");
        Task.Run(() =>
        {
            try
            {
                ExportResult r = Exporter.Export(project, destination, options, progress);
                string? added = content is not null ? Exporter.AddToLibrary(r.Folder, content) : null;
                return (Result: r, Added: added, Error: (string?)null);
            }
            catch (Exception e) { return (Result: (ExportResult?)null, Added: (string?)null, Error: (string?)e.Message); }
        }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _busy = false;
            var (r, added, error) = t.Result;
            if (error is not null || r is null) { Build(new TextBlock { Text = "Export didn't finish: " + error, Foreground = Ui.Bad, TextWrapping = TextWrapping.Wrap, FontSize = 12.5 }); return; }
            var done = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };
            done.Children.Add(Ui.Label("Exported.", 14, Ui.Good, FontWeight.Bold));
            done.Children.Add(Ui.Label($"{r.Files} files, {Exporter.Size(r.BytesAfter)} (the pictures were {Exporter.Size(r.BytesBefore)}).", 12.5, Ui.Text));
            done.Children.Add(Ui.Label("Folder: " + r.Folder, 12, Ui.Muted));
            if (r.ZipFile is not null) done.Children.Add(Ui.Label("Zip file: " + r.ZipFile, 12, Ui.Muted));
            if (added is not null) done.Children.Add(Ui.Label("Added to the reader's books: " + added, 12, Ui.Muted));
            foreach (string note in r.Notes.Take(12)) done.Children.Add(Ui.Help(note));
            done.Children.Add(Ui.Row(Ui.Button("Open the folder", () => _window.Reveal(r.Folder), small: true)));
            Build(done);
        }));
    }
}
