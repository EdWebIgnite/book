namespace Editor.Core;

/// <summary>
/// A Windows icon file (.ico) made of PNG pictures, one per size. Windows Vista and later read PNG
/// pictures inside an icon at every size, so no bitmaps need making. Used by "BookEditor --make-icons"
/// to make the program icons from the drawn ones.
/// </summary>
public static class IcoFile
{
    /// <summary>The sizes Windows asks for: the taskbar, Explorer's views, the Start menu, Alt+Tab.</summary>
    public static readonly int[] Sizes = { 16, 24, 32, 48, 64, 256 };

    /// <summary>Write the pictures, each a square PNG of the given size, smallest first.</summary>
    public static void Write(Stream to, IReadOnlyList<(int Size, byte[] Png)> pictures)
    {
        if (pictures.Count == 0 || pictures.Count > ushort.MaxValue) throw new ArgumentException("An icon needs between 1 and 65535 pictures.", nameof(pictures));
        foreach (var (size, png) in pictures)
            if (size < 1 || size > 256 || png.Length < 8 || png[0] != 0x89 || png[1] != (byte)'P') throw new ArgumentException("Each picture must be a PNG from 1 to 256 pixels.", nameof(pictures));
        var w = new BinaryWriter(to, System.Text.Encoding.ASCII, leaveOpen: true);
        w.Write((ushort)0);                    // reserved
        w.Write((ushort)1);                    // an icon (2 would be a cursor)
        w.Write((ushort)pictures.Count);
        int offset = 6 + 16 * pictures.Count;
        foreach (var (size, png) in pictures)
        {
            w.Write((byte)(size >= 256 ? 0 : size));   // 0 means 256
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0);                  // no palette
            w.Write((byte)0);
            w.Write((ushort)1);                // colour planes
            w.Write((ushort)32);               // bits per pixel
            w.Write(png.Length);
            w.Write(offset);
            offset += png.Length;
        }
        foreach (var (_, png) in pictures) w.Write(png);
        w.Flush();
    }

    /// <summary>The sizes held in an icon file, read from its directory (for checking one).</summary>
    public static List<int> ReadSizes(Stream from)
    {
        var r = new BinaryReader(from, System.Text.Encoding.ASCII, leaveOpen: true);
        if (r.ReadUInt16() != 0 || r.ReadUInt16() != 1) throw new InvalidDataException("Not an icon file.");
        int count = r.ReadUInt16();
        var sizes = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int width = r.ReadByte();
            r.ReadBytes(15);
            sizes.Add(width == 0 ? 256 : width);
        }
        return sizes;
    }
}
