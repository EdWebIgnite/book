using Editor.Core;
using Reader.Core;

namespace Reader.Tests;

public class EditorSettingsTests
{
    [Fact]
    public void Settings_come_back_as_they_were_saved()
    {
        using var t = new TempFolder();
        var s = new EditorSettings
        {
            File = t["settings.json"], Window = new PixelBox(120, 80, 1300, 760), Maximized = true, LastFolder = t.Path,
            Surround = "paper", Frames = new() { "tablet-sideways" }, Crease = false,
        };
        Assert.True(s.Save());
        EditorSettings back = EditorSettings.Load(t["settings.json"]);
        Assert.Equal(new PixelBox(120, 80, 1300, 760), back.Window);
        Assert.True(back.Maximized);
        Assert.Equal(t.Path, back.LastFolder);
        Assert.Equal("paper", back.Surround);
        Assert.Equal(new[] { "tablet-sideways" }, back.Frames);
        Assert.False(back.Crease);
    }

    [Fact]
    public void A_missing_or_damaged_settings_file_gives_the_defaults()
    {
        using var t = new TempFolder();
        EditorSettings none = EditorSettings.Load(t["nothing.json"]);
        Assert.Null(none.Window);
        Assert.Equal("grey", none.Surround);
        Assert.True(none.Crease);
        File.WriteAllText(t["bad.json"], "{ \"window\": { \"x\": \"left\" }, \"surround\": \"purple\", ");
        EditorSettings bad = EditorSettings.Load(t["bad.json"]);
        Assert.Null(bad.Window);
        Assert.Equal("grey", bad.Surround);
        File.WriteAllText(t["odd.json"], "{ \"window\": { \"x\": 1, \"y\": 2, \"width\": -5, \"height\": 9 }, \"surround\": \"purple\", \"frames\": [1, \"window\"] }");
        EditorSettings odd = EditorSettings.Load(t["odd.json"]);
        Assert.Null(odd.Window);
        Assert.Equal("grey", odd.Surround);
        Assert.Equal(new[] { "window" }, odd.Frames);
    }

    [Fact]
    public void Picking_a_file_remembers_its_folder()
    {
        using var t = new TempFolder();
        var s = new EditorSettings { File = t["s.json"] };
        s.Picked(Path.Combine(t.Path, "picture.png"));
        Assert.Equal(t.Path, s.LastFolder);
        s.Picked(t.Path);
        Assert.Equal(t.Path, s.LastFolder);
    }

    static readonly PixelBox Laptop = new(0, 0, 1366, 728);
    static readonly PixelBox Second = new(1366, 0, 1920, 1040);

    [Fact]
    public void A_remembered_window_comes_back_where_it_was()
    {
        var at = new PixelBox(1500, 100, 1200, 800);
        Assert.Equal(at, WindowPlacement.Fit(at, new[] { Laptop, Second }, 960, 560));
    }

    [Fact]
    public void A_window_left_on_an_unplugged_screen_comes_back_onto_the_one_there_is()
    {
        PixelBox? fitted = WindowPlacement.Fit(new PixelBox(1500, 100, 1600, 900), new[] { Laptop }, 960, 560);
        Assert.NotNull(fitted);
        PixelBox f = fitted!.Value;
        Assert.True(f.X >= 0 && f.Y >= 0 && f.Right <= Laptop.Right && f.Bottom <= Laptop.Bottom, f.ToString());
        Assert.Equal(1366, f.Width);
        Assert.Equal(728, f.Height);
    }

    [Fact]
    public void A_window_half_off_the_screen_is_moved_fully_onto_it()
    {
        PixelBox f = WindowPlacement.Fit(new PixelBox(-300, 600, 1000, 600), new[] { Laptop }, 960, 560)!.Value;
        Assert.Equal(0, f.X);
        Assert.Equal(728 - 600, f.Y);
        Assert.Equal(1000, f.Width);
    }

    [Fact]
    public void Nothing_remembered_means_the_usual_place()
    {
        Assert.Null(WindowPlacement.Fit(null, new[] { Laptop }, 960, 560));
        var (w, h) = WindowPlacement.FirstSize(1093, 582, 960, 560);
        Assert.True(w <= 1093 && w >= 960, w.ToString());
        Assert.True(h <= 582 && h >= 560, h.ToString());
        Assert.Equal((1600.0, 1000.0), WindowPlacement.FirstSize(2560, 1400, 960, 560));
    }
}

public class EditorBookFileTests
{
    [Fact]
    public void A_book_file_opens_as_a_new_folder_named_after_the_book()
    {
        using var t = new TempFolder();
        string book = Path.Combine(t.Path, "src", "moss");
        Directory.CreateDirectory(book);
        Pictures.MakePlain(Path.Combine(book, "p1.jpg"), 320, 200, SkiaSharp.SKColors.Orange);
        var b = new Book { Id = "moss", Title = "Moss", Pages = { new Page { Image = "p1.jpg", Stops = { new Stop { Box = new RectD(0, 0, 1, 1), Caption = "Hello." } } } } };
        File.WriteAllText(Path.Combine(book, "book.json"), BookJson.ToJsonString(b));
        string file = t["moss.picturebook"];
        BookPackage.Create(book, file);
        string parent = t["Picture books"];

        BookFileOpener.Outcome first = BookFileOpener.Unpack(file, parent);
        Assert.True(first.Ok, first.Message);
        Assert.True(File.Exists(Path.Combine(first.Folder!, "book.json")));
        Assert.Equal(parent, Path.GetDirectoryName(first.Folder));
        Assert.Equal("moss", Path.GetFileName(first.Folder));

        // Opening it again never writes over the first copy.
        BookFileOpener.Outcome second = BookFileOpener.Unpack(file, parent);
        Assert.True(second.Ok, second.Message);
        Assert.NotEqual(first.Folder, second.Folder);
        Assert.EndsWith("(2)", second.Folder);
        // Nothing is left over from unpacking.
        Assert.Equal(2, Directory.GetDirectories(parent).Length);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void A_file_that_is_not_a_book_says_so_and_leaves_nothing_behind()
    {
        using var t = new TempFolder();
        File.WriteAllText(t["not.picturebook"], "hello");
        string parent = t["out"];
        BookFileOpener.Outcome r = BookFileOpener.Unpack(t["not.picturebook"], parent);
        Assert.False(r.Ok);
        Assert.StartsWith("That book file couldn't be opened.", r.Message);
        Assert.Empty(Directory.GetDirectories(parent));
    }

    [Fact]
    public void Unique_folder_names_count_up()
    {
        using var t = new TempFolder();
        Assert.Equal(t["a"], BookFileOpener.UniqueFolder(t.Path, "a"));
        Directory.CreateDirectory(t["a"]);
        Directory.CreateDirectory(t["a (2)"]);
        Assert.Equal(t["a (3)"], BookFileOpener.UniqueFolder(t.Path, "a"));
    }
}

public class IcoFileTests
{
    static byte[] FakePng(int n) => new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, (byte)n };

    [Fact]
    public void An_icon_file_lists_every_size_and_holds_each_picture()
    {
        var pictures = IcoFile.Sizes.Select(s => (s, FakePng(s))).ToList();
        using var ms = new MemoryStream();
        IcoFile.Write(ms, pictures);
        ms.Position = 0;
        Assert.Equal(IcoFile.Sizes, IcoFile.ReadSizes(ms));
        byte[] all = ms.ToArray();
        Assert.Equal(6 + 16 * pictures.Count + pictures.Sum(p => p.Item2.Length), all.Length);
        // The 256 picture is last, where its directory entry says.
        int offset = BitConverter.ToInt32(all, 6 + 16 * 5 + 12);
        Assert.Equal(256 & 0xff, all[offset + 8]);
    }

    [Fact]
    public void Only_png_pictures_of_icon_sizes_go_in()
    {
        Assert.Throws<ArgumentException>(() => IcoFile.Write(new MemoryStream(), new List<(int, byte[])>()));
        Assert.Throws<ArgumentException>(() => IcoFile.Write(new MemoryStream(), new List<(int, byte[])> { (512, FakePng(1)) }));
        Assert.Throws<ArgumentException>(() => IcoFile.Write(new MemoryStream(), new List<(int, byte[])> { (16, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }) }));
    }
}
