using Reader.Core;

namespace Editor.Core;

/// <summary>How much a problem matters.</summary>
public enum Severity
{
    /// <summary>Something will be missing or broken in the apps.</summary>
    Problem,
    /// <summary>It works, but it may look wrong on some screens.</summary>
    Warning,
    /// <summary>Worth a look, may well be fine.</summary>
    Tip,
}

/// <summary>What a problem is about, so the editor can jump to it.</summary>
public enum Spot { Book, Page, Stop, Hotspot, Layer, Effect, File }

/// <summary>One thing Check book found. Page and Index count from 0; the text counts from 1, as people do.</summary>
public sealed record Issue(Severity Level, string Text, int Page = -1, Spot Kind = Spot.Book, int Index = -1, string? File = null);

/// <summary>
/// Check book: looks through a book for things that will go wrong in the apps or look wrong on some
/// screens. Nothing here changes the book.
/// </summary>
public static class BookChecker
{
    /// <summary>A caption longer than this many characters is worth a second look on any screen.</summary>
    public const int LongCaption = 160;

    public static List<Issue> Check(BookProject project, Func<string, (int Width, int Height)?>? sizeOf = null)
    {
        sizeOf ??= name => project.PathOf(name) is string p && File.Exists(p) ? Pictures.Size(p) : null;
        var issues = new List<Issue>();
        Book book = project.Book;
        void Add(Severity s, string text, int page = -1, Spot kind = Spot.Book, int index = -1, string? file = null) =>
            issues.Add(new Issue(s, text, page, kind, index, file));

        if (string.IsNullOrWhiteSpace(book.Title)) Add(Severity.Problem, "The book has no title. Type one at the top.");
        if (book.Pages.Count == 0) { Add(Severity.Problem, "The book has no pages yet. Add some pictures."); return issues; }
        if (string.IsNullOrEmpty(book.Cover)) Add(Severity.Tip, "There is no cover picture, so the book opens straight on page 1 and the shelf shows page 1.");
        else if (!project.Has(book.Cover)) Add(Severity.Problem, $"The cover picture is missing: {book.Cover}", file: book.Cover);

        Device sideways = Device.Find("s22-sideways"), cheap = Device.Find("cheap-sideways"), upright = Device.Find("cheap-upright");
        bool anyVoice = book.Pages.Any(p => p.Stops.Any(s => s.Voice is not null));
        var silent = new List<(int Page, int Stop)>();     // stops with words but no recording, in a book that has recordings

        // The shape most guided pages have, to spot the odd one out.
        var shapes = new List<double>();
        var sizes = new Dictionary<int, (int W, int H)>();
        for (int i = 0; i < book.Pages.Count; i++)
        {
            Page p = book.Pages[i];
            if (p.Tiles is TileInfo t && t.Width > 0) sizes[i] = (t.Width, t.Height);
            else if (sizeOf(p.Image) is (int w, int h)) sizes[i] = (w, h);
            if (!p.Explore && sizes.TryGetValue(i, out var sz) && sz.H > 0) shapes.Add((double)sz.W / sz.H);
        }
        double usual = shapes.Count > 0 ? shapes.GroupBy(a => Math.Round(a, 2)).OrderByDescending(g => g.Count()).First().Key : 0;

        for (int i = 0; i < book.Pages.Count; i++)
        {
            Page p = book.Pages[i];
            string pg = $"Page {i + 1}";
            if (string.IsNullOrEmpty(p.Image)) { Add(Severity.Problem, $"{pg} has no picture.", i, Spot.Page); continue; }
            if (!project.Has(p.Image)) Add(Severity.Problem, $"{pg}'s picture is missing: {p.Image}", i, Spot.Page, file: p.Image);
            if (p.Tiles is TileInfo tiles && ProjectTilesMissing(project, tiles)) Add(Severity.Problem, $"{pg} is a big scene cut into tiles, but its tiles folder is missing.", i, Spot.Page);

            double aspect = sizes.TryGetValue(i, out var size) && size.H > 0 ? (double)size.W / size.H : 1.6;
            if (sizes.ContainsKey(i))
            {
                int big = Math.Max(size.W, size.H), small = Math.Min(size.W, size.H);
                if (p.Tiles is null && big > Pictures.PhoneLimit)
                {
                    Add(p.Explore ? Severity.Warning : Severity.Tip,
                        p.Explore ? $"{pg}'s picture is {size.W} x {size.H}. Phones can't show more than {Pictures.PhoneLimit} pixels sharply, so Export cuts it into tiles."
                                  : $"{pg}'s picture is {size.W} x {size.H}, more than phones can draw ({Pictures.PhoneLimit}). Export makes a smaller copy for the apps.",
                        i, Spot.Page);
                }
                if (small < Pictures.SoftBelow)
                    Add(Severity.Warning, $"{pg}'s picture is only {size.W} x {size.H}. It will look soft when the camera goes in close. {Pictures.SoftBelow} pixels or more on the short side is better.", i, Spot.Page);
                if (!p.Explore && usual > 0 && Math.Abs(aspect - usual) / usual > 0.03)
                    Add(Severity.Warning, $"{pg} is a different shape from most pages ({size.W} x {size.H}). In the open book it will show with a gap or be turned with a fade instead of a page turn.", i, Spot.Page);
            }
            if (p.Spread is SpreadSource sp)
            {
                if (sp.Left.Length > 0 && !project.Has(sp.Left)) Add(Severity.Tip, $"{pg} was joined from a left-hand page that is no longer in the folder ({sp.Left}). The joined picture is fine; you just can't replace one side on its own.", i, Spot.Page);
                if (sp.Right.Length > 0 && !project.Has(sp.Right)) Add(Severity.Tip, $"{pg} was joined from a right-hand page that is no longer in the folder ({sp.Right}).", i, Spot.Page);
            }

            if (!p.Explore && p.Stops.Count == 0) Add(Severity.Tip, $"{pg} has no stops, so it is only shown whole. Drag on the picture to add stops.", i, Spot.Page);

            for (int s = 0; s < p.Stops.Count; s++)
            {
                Stop st = p.Stops[s];
                string at = $"{pg}, stop {s + 1}";
                if (Outside(st.Box)) Add(Severity.Warning, $"{at} reaches outside the picture.", i, Spot.Stop, s);
                if (st.To is RectD to && Outside(to)) Add(Severity.Warning, $"{at}: where the pan ends reaches outside the picture.", i, Spot.Stop, s);
                if (st.Box.W < 0.02 || st.Box.H < 0.02) Add(Severity.Warning, $"{at} is very small. The camera will go in so close that the picture looks blurry.", i, Spot.Stop, s);
                if (string.IsNullOrWhiteSpace(st.Caption) && !p.Explore && st.Voice is null)
                    Add(Severity.Tip, $"{at} has no words. That's fine if the words are in the picture.", i, Spot.Stop, s);
                if (!string.IsNullOrWhiteSpace(st.Caption))
                {
                    int sideLines = Math.Max(Framing.CaptionLines(st.Caption, sideways), Framing.CaptionLines(st.Caption, cheap));
                    int upLines = Framing.CaptionLines(st.Caption, upright);
                    if (sideLines > sideways.MostCaptionLines)
                        Add(Severity.Warning, $"{at}: the words take {sideLines} lines on a phone held sideways and will cover much of the picture. Try splitting them over two stops.", i, Spot.Stop, s);
                    else if (upLines > upright.MostCaptionLines)
                        Add(Severity.Warning, $"{at}: the words take {upLines} lines on a phone held upright.", i, Spot.Stop, s);
                }
                if (!string.IsNullOrEmpty(st.Sound) && !project.Has(st.Sound)) Add(Severity.Problem, $"{at}: the sound file is missing: {st.Sound}", i, Spot.Stop, s, st.Sound);
                if (st.Voice is Voice v)
                {
                    if (!project.Has(v.File)) Add(Severity.Problem, $"{at}: the recording is missing: {v.File}", i, Spot.Stop, s, v.File);
                    else if (v.Words.Count == 0 && ReadAlong.WordCount(st.Caption) > 0) Add(Severity.Tip, $"{at} has a recording but its words aren't timed yet, so they won't light up. Use Tap along.", i, Spot.Stop, s);
                    else if (v.Words.Count > 0 && !ReadAlong.TimesFit(v, st.Caption))
                        Add(Severity.Warning, $"{at}: the word timings don't match the words any more (the words were changed after timing). Tap along again.", i, Spot.Stop, s);
                }
                else if (anyVoice && !string.IsNullOrWhiteSpace(st.Caption)) silent.Add((i, s));
                if (aspect >= 1.2 && !p.Explore && Framing.CrossesCrease(st.To ?? st.Box, aspect) && st.Box.W < 0.45 && ShouldNoteCrease(st))
                    Add(Severity.Tip, $"{at} sits across the middle of the spread. On an upright phone the crease fades out as the camera goes in, so check it in the preview.", i, Spot.Stop, s);
            }

            if (p.Explore)
            {
                if (p.Hotspots.Count == 0) Add(Severity.Warning, $"{pg} is an explore page with nothing to find. Add some things to find.", i, Spot.Page);
                var steps = p.Hotspots.Where(h => h.Story > 0).Select(h => h.Story).OrderBy(n => n).ToList();
                for (int k = 0; k < steps.Count; k++)
                {
                    if (steps[k] != k + 1) { Add(Severity.Warning, $"{pg}: the story steps should be numbered 1, 2, 3 with no gaps or repeats. Now they are {string.Join(", ", steps)}.", i, Spot.Page); break; }
                }
                for (int h = 0; h < p.Hotspots.Count; h++)
                {
                    Hotspot hs = p.Hotspots[h];
                    string at = $"{pg}, thing to find {h + 1}" + (string.IsNullOrEmpty(hs.Name) ? "" : $" ({hs.Name})");
                    if (Outside(hs.Box)) Add(Severity.Warning, $"{at} reaches outside the picture.", i, Spot.Hotspot, h);
                    if (string.IsNullOrWhiteSpace(hs.Caption)) Add(Severity.Tip, $"{at} says nothing when found.", i, Spot.Hotspot, h);
                    if (hs.Story > 0 && string.IsNullOrWhiteSpace(hs.Name)) Add(Severity.Tip, $"{at} is a story step with no name, so \"First find...\" can't say what to look for.", i, Spot.Hotspot, h);
                    if (!string.IsNullOrEmpty(hs.Sound) && !project.Has(hs.Sound)) Add(Severity.Problem, $"{at}: the sound file is missing: {hs.Sound}", i, Spot.Hotspot, h, hs.Sound);
                }
            }

            for (int e = 0; e < p.Effects.Count; e++)
            {
                PageEffect fx = p.Effects[e];
                int most = p.Stops.Count;
                int[] refs = { fx.From ?? 0, fx.To ?? 0, fx.Stop ?? 0 };
                if (refs.Any(r => r > most)) Add(Severity.Warning, $"{pg}: the {Names.Effect(fx.Type).ToLowerInvariant()} is set to a stop the page doesn't have (it has {most}).", i, Spot.Effect, e);
            }
            for (int l = 0; l < p.Layers.Count; l++)
            {
                Layer layer = p.Layers[l];
                if (!project.Has(layer.Image)) Add(Severity.Problem, $"{pg}: the picture on top is missing: {layer.Image}", i, Spot.Layer, l, layer.Image);
                int most = p.Stops.Count;
                if ((layer.From ?? 0) > most || (layer.To ?? 0) > most || (layer.MoveAt ?? 0) > most)
                    Add(Severity.Warning, $"{pg}: picture on top {l + 1} is set to a stop the page doesn't have (it has {most}).", i, Spot.Layer, l);
            }
        }

        // A few silent stops are listed one by one; many are one line, so they don't bury everything else.
        if (silent.Count is > 0 and <= 3)
            foreach (var (pg, st) in silent) Add(Severity.Warning, $"Page {pg + 1}, stop {st + 1} has no recording, but other stops do. With Read to me on, this stop will be silent.", pg, Spot.Stop, st);
        else if (silent.Count > 3)
        {
            string list = string.Join("; ", silent.GroupBy(x => x.Page).Select(g => $"page {g.Key + 1} stop{(g.Count() == 1 ? "" : "s")} {string.Join(", ", g.Select(x => x.Stop + 1))}"));
            Add(Severity.Warning, $"{silent.Count} stops have words but no recording, though other stops have one. With Read to me on, they will be silent: {list}.", silent[0].Page, Spot.Stop, silent[0].Stop);
        }

        foreach (string f in project.UnusedFiles())
            Add(Severity.Tip, $"Not used by the book: {f}. Export leaves it out.", -1, Spot.File, -1, f);

        return issues.OrderBy(x => x.Level).ThenBy(x => x.Page < 0 ? int.MaxValue : x.Page).ToList();
    }

    static bool ShouldNoteCrease(Stop s) => s.Box.H < 0.9;

    static bool Outside(RectD b) => b.X < -0.005 || b.Y < -0.005 || b.Right > 1.005 || b.Bottom > 1.005;

    static bool ProjectTilesMissing(BookProject project, TileInfo t) =>
        BookProject.TilesFolder(t) is string folder && !Directory.Exists(Path.Combine(project.Folder, folder.TrimEnd('/')));
}

/// <summary>The words people see for the format's names.</summary>
public static class Names
{
    public static string Effect(string type) => type switch
    {
        PageEffect.Rain => "Rain",
        PageEffect.Snow => "Snow",
        PageEffect.Leaves => "Falling leaves",
        PageEffect.Sparkles => "Sparkles",
        PageEffect.Lightning => "Lightning and thunder",
        PageEffect.Shake => "Camera shake",
        _ => type,
    };

    public static string Style(string? style) => style switch
    {
        FocusStyles.Dim => "Dim the rest",
        FocusStyles.Vignette => "Soft dark edges",
        FocusStyles.Spotlight => "Spotlight (everything else dark)",
        FocusStyles.None => "Nothing, just frame it",
        _ => "Book's choice",
    };
}
