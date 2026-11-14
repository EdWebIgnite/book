using Reader.Core;
using SkiaSharp;

namespace Editor.Core;

/// <summary>
/// Reading, joining, shrinking and cutting pictures, for adding pages and for Export. Uses SkiaSharp,
/// the same picture library the screen is drawn with.
/// </summary>
public static class Pictures
{
    /// <summary>The biggest picture most phone graphics chips can draw in one go. The phone app shrinks anything bigger as it loads.</summary>
    public const int PhoneLimit = 4096;

    /// <summary>Below this many pixels on the short side, a page looks soft when the camera goes in close on a tablet.</summary>
    public const int SoftBelow = 1000;

    /// <summary>A picture's size in pixels, read from its header without decoding it. Null if it can't be read.</summary>
    public static (int Width, int Height)? Size(string path)
    {
        try
        {
            using var codec = SKCodec.Create(path);
            if (codec is null) return null;
            SKSizeI s = codec.Info.Size;
            // A photo turned on its side by its camera tag is still stored the other way round; the readers
            // draw the pixels as stored, so that is the size that counts.
            return (s.Width, s.Height);
        }
        catch (Exception) { return null; }
    }

    /// <summary>True when the picture has see-through parts in its format (a PNG or WebP with alpha).</summary>
    public static bool HasAlpha(string path)
    {
        try
        {
            using var codec = SKCodec.Create(path);
            return codec is not null && codec.Info.AlphaType != SKAlphaType.Opaque;
        }
        catch (Exception) { return false; }
    }

    static SKBitmap Decode(string path, int maxSide = 0)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException("This picture can't be read: " + Path.GetFileName(path));
        SKImageInfo info = codec.Info;
        int w = info.Width, h = info.Height;
        if (maxSide > 0 && Math.Max(w, h) > maxSide * 2)
        {
            // Let the decoder skip pixels for very big pictures; the final size is made below.
            SKSizeI scaled = codec.GetScaledDimensions((float)maxSide / Math.Max(w, h) * 1.5f);
            w = scaled.Width; h = scaled.Height;
        }
        var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul));
        SKCodecResult r = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (r is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            using SKBitmap? plain = SKBitmap.Decode(path) ?? throw new InvalidDataException("This picture can't be read: " + Path.GetFileName(path));
            return plain.Copy();
        }
        return bitmap;
    }

    static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        if (source.Width == width && source.Height == height) return source.Copy();
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, source.AlphaType);
        return source.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell)) ?? throw new InvalidOperationException("Couldn't resize the picture.");
    }

    static void Write(SKBitmap bitmap, string path, int quality = 88)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        SKEncodedImageFormat format = ext switch { ".png" => SKEncodedImageFormat.Png, ".webp" => SKEncodedImageFormat.Webp, _ => SKEncodedImageFormat.Jpeg };
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(format, quality) ?? throw new InvalidOperationException("Couldn't write " + Path.GetFileName(path));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + ".writing";
        using (FileStream f = File.Create(temp)) data.SaveTo(f);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// Join a left-hand and a right-hand page into one spread picture, side by side. If the two are not the
    /// same height, the right-hand page is scaled to match the left. Returns the new picture's size.
    /// </summary>
    public static (int Width, int Height) JoinSpread(string left, string right, string output, int quality = 92)
    {
        using SKBitmap l = Decode(left), r0 = Decode(right);
        using SKBitmap r = r0.Height == l.Height ? r0.Copy() : Resize(r0, Math.Max(1, (int)Math.Round(r0.Width * (double)l.Height / r0.Height)), l.Height);
        var info = new SKImageInfo(l.Width + r.Width, l.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var spread = new SKBitmap(info);
        using (var canvas = new SKCanvas(spread))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(l, 0, 0);
            canvas.DrawBitmap(r, l.Width, 0);
        }
        Write(spread, output, quality);
        return (info.Width, info.Height);
    }

    /// <summary>
    /// Write a copy of a picture no bigger than <paramref name="maxSide"/> pixels either way (0: any size),
    /// in the format the output name says. Returns the size written.
    /// </summary>
    public static (int Width, int Height) SaveSmaller(string input, string output, int maxSide, int quality = 85)
    {
        using SKBitmap source = Decode(input, maxSide);
        (int w, int h) = Fit(Size(input) ?? (source.Width, source.Height), maxSide);
        using SKBitmap scaled = Resize(source, w, h);
        Write(scaled, output, quality);
        return (w, h);
    }

    /// <summary>The size a picture becomes when fitted inside a square of <paramref name="maxSide"/> (0: unchanged).</summary>
    public static (int Width, int Height) Fit((int Width, int Height) size, int maxSide)
    {
        int big = Math.Max(size.Width, size.Height);
        if (maxSide <= 0 || big <= maxSide) return size;
        double k = (double)maxSide / big;
        return (Math.Max(1, (int)Math.Round(size.Width * k)), Math.Max(1, (int)Math.Round(size.Height * k)));
    }

    /// <summary>
    /// Cut a big scene into tiles, the way the readers expect: a small overview picture of the whole scene
    /// (the page's image), then the picture at full size and at half size in square tiles. Only the
    /// tiles under the camera are loaded, so a huge scene stays quick on a phone. Writes into
    /// <paramref name="bookFolder"/> and returns the tile description and the overview's name.
    /// </summary>
    public static (TileInfo Tiles, string Overview) CutTiles(string input, string bookFolder, string baseName, int tileSize = 1024, int levels = 2, int overviewWidth = 2048, int maxSide = 0)
    {
        using SKBitmap original = Decode(input);
        using SKBitmap full = maxSide > 0 && Math.Max(original.Width, original.Height) > maxSide
            ? Resize(original, Fit((original.Width, original.Height), maxSide).Width, Fit((original.Width, original.Height), maxSide).Height)
            : original.Copy();
        int W = full.Width, H = full.Height;
        string tilesDir = baseName + "_tiles";
        for (int z = 0; z < levels; z++)
        {
            int f = 1 << z, lw = (W + f - 1) / f, lh = (H + f - 1) / f;
            using SKBitmap level = z == 0 ? full.Copy() : Resize(full, lw, lh);
            for (int ty = 0; ty * tileSize < lh; ty++)
            {
                for (int tx = 0; tx * tileSize < lw; tx++)
                {
                    int w = Math.Min(tileSize, lw - tx * tileSize), h = Math.Min(tileSize, lh - ty * tileSize);
                    using var tile = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque));
                    level.ExtractSubset(tile, SKRectI.Create(tx * tileSize, ty * tileSize, w, h));
                    using SKBitmap copy = tile.Copy();
                    Write(copy, Path.Combine(bookFolder, tilesDir, z.ToString(), $"{tx}_{ty}.jpg"), 85);
                }
            }
        }
        int ow = Math.Min(overviewWidth, W), oh = Math.Max(1, (int)Math.Round((double)ow * H / W));
        string overview = baseName + "_overview.jpg";
        using (SKBitmap small = Resize(full, ow, oh)) Write(small, Path.Combine(bookFolder, overview), 85);
        return (new TileInfo { Width = W, Height = H, Size = tileSize, Levels = levels, Path = $"{tilesDir}/{{z}}/{{x}}_{{y}}.jpg" }, overview);
    }

    /// <summary>A plain picture of one colour, for tests and for placeholders.</summary>
    public static void MakePlain(string output, int width, int height, SKColor colour, SKColor? stripe = null)
    {
        using var b = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var c = new SKCanvas(b))
        {
            c.Clear(colour);
            if (stripe is SKColor s) using (var paint = new SKPaint { Color = s }) c.DrawRect(0, 0, width / 4f, height, paint);
        }
        Write(b, output, 90);
    }

    /// <summary>The colour of one pixel, for tests.</summary>
    public static SKColor PixelAt(string path, int x, int y)
    {
        using SKBitmap b = SKBitmap.Decode(path);
        return b.GetPixel(x, y);
    }
}

/// <summary>
/// How a set of picture files becomes pages: each picture is a page, except a pair named as the left
/// and right pages of one spread (page3-left.png and page3-right.png, or page3_L.jpg and page3_R.jpg),
/// which become one page made by joining them.
/// </summary>
public static class PagePlan
{
    /// <summary>One page to make: a single picture, or a left and right page to join.</summary>
    public sealed record Item(string? Single, string? Left, string? Right)
    {
        public bool IsPair => Left is not null && Right is not null;
    }

    static readonly System.Text.RegularExpressions.Regex SideName = new(@"[-_ ]?(left|right|l|r)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>"left", "right", or "" when the name doesn't say.</summary>
    public static string Side(string path) => SideName.Match(Path.GetFileNameWithoutExtension(path)).Groups[1].Value.ToLowerInvariant() switch
    {
        "left" or "l" => "left",
        "right" or "r" => "right",
        _ => "",
    };

    /// <summary>The name without its left or right ending, for matching the halves and naming the spread.</summary>
    public static string Stem(string path) => SideName.Replace(Path.GetFileNameWithoutExtension(path), "");

    public static List<Item> Make(IEnumerable<string> files)
    {
        var sorted = files.OrderBy(f => Path.GetFileName(f), NaturalOrder.Instance).ToList();
        var used = new HashSet<string>();
        var items = new List<Item>();
        foreach (string f in sorted)
        {
            if (!used.Add(f)) continue;
            string side = Side(f), stem = Stem(f);
            if (side.Length > 0 && stem.Length > 0)
            {
                string want = side == "left" ? "right" : "left";
                string? other = sorted.FirstOrDefault(o => !used.Contains(o) && Side(o) == want && Stem(o).Equals(stem, StringComparison.OrdinalIgnoreCase));
                if (other is not null)
                {
                    used.Add(other);
                    items.Add(side == "left" ? new Item(null, f, other) : new Item(null, other, f));
                    continue;
                }
            }
            items.Add(new Item(f, null, null));
        }
        return items;
    }

    /// <summary>
    /// Where a box on one of two pages lands on the spread made by joining them. The right page is scaled
    /// to the left page's height, as <see cref="Pictures.JoinSpread"/> does.
    /// </summary>
    public static RectD OnSpread(RectD box, bool rightPage, (int Width, int Height) left, (int Width, int Height) right)
    {
        double lw = left.Width, rw = right.Width * (double)left.Height / Math.Max(1, right.Height), w = lw + rw;
        return rightPage
            ? new RectD((lw + box.X * rw) / w, box.Y, box.W * rw / w, box.H)
            : new RectD(box.X * lw / w, box.Y, box.W * lw / w, box.H);
    }
}
