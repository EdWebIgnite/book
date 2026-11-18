using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Reader.Core;
using Reader.Ui;

namespace Reader.Tests;

/// <summary>
/// Opening a book: the pictures read side by side come out as reading them one by one did, problems are
/// reported for the first page that has one, and the printed words made ahead on another thread are the
/// ones drawn. These run Avalonia with no screen, on a thread of its own.
/// </summary>
public class LoadedBookTests
{
    public sealed class TestApp
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Avalonia.Application>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(TestApp)));

    static void OnUiThread(Action test) => Session.Value.Dispatch(test, CancellationToken.None).GetAwaiter().GetResult();

    static string Puddle => Path.Combine(Kit.ContentRoot, "books", "biggest-puddle");

    static byte[] Pixels(Bitmap b)
    {
        var bytes = new byte[b.PixelSize.Width * b.PixelSize.Height * 4];
        var pin = System.Runtime.InteropServices.GCHandle.Alloc(bytes, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { b.CopyPixels(new PixelRect(b.PixelSize), pin.AddrOfPinnedObject(), bytes.Length, b.PixelSize.Width * 4); }
        finally { pin.Free(); }
        return bytes;
    }

    [Fact]
    public void Pages_read_side_by_side_are_the_pictures_reading_one_page_at_a_time_made()
    {
        OnUiThread(() =>
        {
            using LoadedBook book = LoadedBook.Load(Puddle);
            Book json = BookJson.Load(Puddle);
            Assert.Equal(json.Pages.Count, book.Aspects.Length);
            foreach (int page in new[] { 0, 5, 11 })
            {
                using var full = new Bitmap(Path.Combine(Puddle, json.Pages[page].Image));
                Assert.Equal(full.PixelSize, book.PageSize(page));
                Assert.Equal((double)full.PixelSize.Width / full.PixelSize.Height, book.Aspects[page], 9);
                // The half-size copy, made the way it always was: the full picture halved with the quick filter.
                var halfSize = new PixelSize((full.PixelSize.Width + 1) / 2, (full.PixelSize.Height + 1) / 2);
                using Bitmap half = full.CreateScaledBitmap(halfSize, BitmapInterpolationMode.LowQuality);
                Bitmap held = book.PagePicture(page, halfSize.Width);
                Assert.Equal(halfSize, held.PixelSize);
                Assert.Equal(Pixels(half), Pixels(held));
            }
            Assert.True(book.HasCover);
            Assert.NotEqual(LoadedBook.PlainBoard, book.Board);
        });
    }

    [Fact]
    public void The_first_page_with_a_problem_is_the_one_reported()
    {
        OnUiThread(() =>
        {
            string folder = Path.Combine(Path.GetTempPath(), "loaded-book-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string page1 = Path.Combine(Puddle, "page1.jpg");
                File.Copy(page1, Path.Combine(folder, "a.jpg"));
                File.WriteAllBytes(Path.Combine(folder, "bad3.jpg"), new byte[] { 0xFF, 0xD8, 1, 2, 3 });
                File.WriteAllBytes(Path.Combine(folder, "bad5.jpg"), new byte[] { 0xFF, 0xD8, 4, 5, 6 });
                File.Copy(page1, Path.Combine(folder, "b.jpg"));
                string Pages(params string[] images) => "{ \"id\": \"t\", \"title\": \"T\", \"pages\": [" +
                    string.Join(",", images.Select(i => $"{{ \"image\": \"{i}\", \"stops\": [] }}")) + "] }";

                // Missing pictures are found before anything is read: the first missing one is named.
                File.WriteAllText(Path.Combine(folder, "book.json"), Pages("a.jpg", "gone2.jpg", "a.jpg", "gone4.jpg"));
                var missing = Assert.Throws<FileNotFoundException>(() => LoadedBook.Load(folder));
                Assert.Contains("Page 2's picture is missing", missing.Message);

                // Pictures that can't be read: whichever finishes first, the earliest page's problem is the one thrown.
                File.WriteAllText(Path.Combine(folder, "book.json"), Pages("a.jpg", "b.jpg", "bad3.jpg", "a.jpg", "bad5.jpg"));
                Exception broken = Record.Exception(() => LoadedBook.Load(folder))!;
                Assert.NotNull(broken);
                Assert.IsNotType<AggregateException>(broken);
                File.WriteAllText(Path.Combine(folder, "book.json"), Pages("a.jpg", "b.jpg", "a.jpg", "a.jpg", "bad5.jpg"));
                Exception later = Record.Exception(() => LoadedBook.Load(folder))!;
                Assert.Equal(later.GetType(), broken.GetType());

                // And a book that reads loads all the same.
                File.WriteAllText(Path.Combine(folder, "book.json"), Pages("a.jpg", "b.jpg", "a.jpg"));
                using LoadedBook ok = LoadedBook.Load(folder);
                Assert.Equal(3, ok.Aspects.Length);
                Assert.False(ok.HasCover);
            }
            finally
            {
                Dispatcher.UIThread.RunJobs();
                Directory.Delete(folder, true);
            }
        });
    }

    [Fact]
    public void Printed_words_made_ahead_are_kept_and_drawn_and_only_three_pages_are_kept()
    {
        OnUiThread(() =>
        {
            using LoadedBook book = LoadedBook.Load(Puddle);
            // Pages 1 to 4 print words.
            book.PrepareWords(1);
            PrintedWordsPicture? asked = book.Words(1);             // still being made: waits for it rather than making another
            Assert.NotNull(asked);
            Assert.Same(asked, book.Words(1));
            for (int i = 0; i < 50; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
            Assert.Same(asked, book.Words(1));                       // handing over later doesn't replace it

            book.PrepareWords(2);
            for (int i = 0; i < 400 && book.Words(2) is null; i++) Thread.Sleep(5);
            PrintedWordsPicture two = book.Words(2)!;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(two, book.Words(2));

            // A fourth page pushes out the one looked at longest ago.
            PrintedWordsPicture? zero = book.Words(0);
            book.Words(1);
            book.Words(2);
            book.Words(3);
            Dispatcher.UIThread.RunJobs();
            Assert.NotSame(zero, book.Words(0));

            // Words that change are made again; the same words are not.
            Page p = book.Book.Pages[3];
            PrintedWordsPicture before = book.Words(3)!;
            Assert.Same(before, book.Words(3));
            p.Text!.Box = new RectD(p.Text.Box.X, p.Text.Box.Y, p.Text.Box.W * 0.9, p.Text.Box.H);
            PrintedWordsPicture after = book.Words(3)!;
            Assert.NotSame(before, after);
            Assert.NotEqual(before.Key, after.Key);
            p.Stops[0].Caption += " And then some.";
            Assert.NotSame(after, book.Words(3));
            Dispatcher.UIThread.RunJobs();
        });
    }

    [Fact]
    public void A_book_opened_in_the_background_has_its_first_words_ready()
    {
        OnUiThread(() =>
        {
            Task<LoadedBook> loading = LoadedBook.LoadAsync(Puddle, new[] { 0, 2 }, cover: true);
            while (!loading.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(2); }
            using LoadedBook book = loading.Result;
            using LoadedBook plain = LoadedBook.Load(Puddle);
            PrintedWordsPicture ready = book.Words(0)!;
            Assert.Same(ready, book.Words(0));
            Assert.Equal(plain.Words(0)!.Key, ready.Key);
            Assert.Equal(Pixels(plain.Words(0)!.Levels[0]), Pixels(ready.Levels[0]));
            Assert.Equal(plain.Board, book.Board);
            Assert.NotNull(book.Words(2));
            Dispatcher.UIThread.RunJobs();
        });
    }
}
