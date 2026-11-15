using System.Text;
using Reader.Core;

namespace Editor.Core;

/// <summary>A word the speech recogniser heard, with when it starts and ends in the recording (seconds).</summary>
public sealed record HeardWord(string Text, double Start, double End);

/// <summary>
/// Working out when each word of a caption starts, from what the speech recogniser heard. What it hears
/// is rarely exactly the caption: punctuation differs, "3" is heard as "three", a name is misheard, a
/// word is dropped or one word comes out as two. So the two lists of words are lined up the way a spell
/// checker lines up letters (the cheapest way to turn one into the other, where two words that sound
/// alike cost little to swap), and caption words with no partner are placed between their neighbours.
/// </summary>
public static class WordAligner
{
    /// <summary>The result: one start time per caption word, and how many of the words were heard clearly.</summary>
    public sealed record Result(List<double> Times, int Clear, int Total)
    {
        /// <summary>Caption words that weren't heard clearly and were placed between the others.</summary>
        public int Guessed => Total - Clear;
    }

    // What each kind of step in the lining up costs. Swapping two words costs 1 minus how alike they are.
    const double SkipCaptionWord = 0.75;    // a caption word that wasn't heard
    const double SkipHeardWord = 0.6;       // something heard that isn't in the caption ("um", a repeat)
    const double Merge = 0.1;               // extra for one word heard as two, or two as one
    const double MergeLikeness = 0.8;       // ...which is only tried when the joined words are this alike
    const double ClearLikeness = 0.6;       // at least this alike counts as heard clearly
    const double MinGap = 0.05;             // words are at least this far apart, as with tapping along

    /// <summary>
    /// Line the heard words up with the caption and give every caption word a start time within the
    /// trimmed part of the recording (<paramref name="start"/> to <paramref name="end"/>). Null when
    /// nothing heard resembles the caption at all, so there's nothing to go by.
    /// </summary>
    public static Result? Align(string? caption, IReadOnlyList<HeardWord> heard, double start, double end)
    {
        List<string> words = CaptionWords(caption);
        int n = words.Count;
        // Only what was heard inside the trim counts, and words are kept in time order.
        // (A word's end is never before its start: recognisers' rough ends can be, once the start is put right.)
        var h = heard.Select(w => w.End < w.Start + MinGap ? w with { End = w.Start + MinGap } : w)
                     .Where(w => Normal(w.Text).Length > 0 && w.End > start - 0.05 && w.Start < end + 0.05)
                     .OrderBy(w => w.Start).ToList();
        int m = h.Count;
        if (n == 0 || m == 0 || end <= start) return null;
        string[] c = words.Select(Normal).ToArray();
        string[] hn = h.Select(w => Normal(w.Text)).ToArray();

        // cost[i, j]: the cheapest way to line up the first i caption words with the first j heard words.
        var cost = new double[n + 1, m + 1];
        var move = new byte[n + 1, m + 1];   // 1 swap, 2 skip caption, 3 skip heard, 4 one as two heard, 5 two as one heard
        for (int i = 0; i <= n; i++)
        {
            for (int j = 0; j <= m; j++)
            {
                if (i == 0 && j == 0) continue;
                double best = double.MaxValue;
                byte how = 0;
                void Try(double v, byte k) { if (v < best) { best = v; how = k; } }
                if (i > 0 && j > 0) Try(cost[i - 1, j - 1] + 1 - Likeness(c[i - 1], hn[j - 1]), 1);
                if (i > 0) Try(cost[i - 1, j] + SkipCaptionWord, 2);
                if (j > 0) Try(cost[i, j - 1] + SkipHeardWord, 3);
                // Merging only when the joined words really are the same word ("rain drop" for "raindrop").
                if (i > 0 && j > 1 && Likeness(c[i - 1], hn[j - 2] + hn[j - 1]) is double l4 && l4 >= MergeLikeness) Try(cost[i - 1, j - 2] + 1 - l4 + Merge, 4);
                if (i > 1 && j > 0 && Likeness(c[i - 2] + c[i - 1], hn[j - 1]) is double l5 && l5 >= MergeLikeness) Try(cost[i - 2, j - 1] + 1 - l5 + Merge, 5);
                cost[i, j] = best;
                move[i, j] = how;
            }
        }

        // Walk back through the choices, noting each caption word's partner.
        var at = new double?[n];
        var until = new double?[n];          // when the partner ends, for placing unheard words after it
        var alike = new double[n];
        for (int i = n, j = m; i > 0 || j > 0;)
        {
            switch (move[i, j])
            {
                case 1:
                    at[i - 1] = h[j - 1].Start; until[i - 1] = h[j - 1].End; alike[i - 1] = Likeness(c[i - 1], hn[j - 1]);
                    i--; j--; break;
                case 2: i--; break;
                case 3: j--; break;
                case 4:
                    at[i - 1] = h[j - 2].Start; until[i - 1] = h[j - 1].End; alike[i - 1] = Likeness(c[i - 1], hn[j - 2] + hn[j - 1]);
                    i--; j -= 2; break;
                case 5:
                {
                    // Two caption words heard as one: share the heard word between them by length.
                    HeardWord w = h[j - 1];
                    double share = c[i - 2].Length / (double)Math.Max(1, c[i - 2].Length + c[i - 1].Length);
                    double split = w.Start + (w.End - w.Start) * share;
                    double l = Likeness(c[i - 2] + c[i - 1], hn[j - 1]);
                    at[i - 2] = w.Start; until[i - 2] = split; alike[i - 2] = l;
                    at[i - 1] = split; until[i - 1] = w.End; alike[i - 1] = l;
                    i -= 2; j--; break;
                }
                default: i = 0; j = 0; break;
            }
        }

        // Partners that would put the words out of order (a repeat heard far away) are let go.
        double last = double.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            if (at[i] is not double t) continue;
            if (t < start - 0.05 || t > end || t <= last + MinGap / 2) { at[i] = null; until[i] = null; alike[i] = 0; continue; }
            last = t;
        }
        if (!alike.Any(a => a >= 0.5)) return null;

        // Unheard words go between the words either side, each given time in proportion to its length.
        var times = new double[n];
        int k = 0;
        while (k < n)
        {
            if (at[k] is double t) { times[k] = t; k++; continue; }
            int from = k;
            while (k < n && at[k] is null) k++;
            double lo = from == 0 ? start : until[from - 1] ?? times[from - 1] + MinGap;
            double hi = k < n ? at[k]!.Value : (h[^1].End > lo ? Math.Min(end, h[^1].End) : end);
            if (from > 0) lo = Math.Max(lo, times[from - 1] + MinGap);
            if (hi - lo < MinGap * (k - from)) hi = lo + MinGap * (k - from);
            double total = 0;
            for (int i = from; i < k; i++) total += words[i].Length + 2.0;
            double place = lo;
            for (int i = from; i < k; i++)
            {
                times[i] = place;
                place += (hi - lo) * (words[i].Length + 2.0) / total;
            }
        }

        // In order, apart, and inside the trim.
        for (int i = 0; i < n; i++)
        {
            double floor = i == 0 ? start : times[i - 1] + MinGap;
            times[i] = Math.Max(times[i], floor);
        }
        double ceiling = end - MinGap;
        for (int i = n - 1; i >= 0; i--)
        {
            times[i] = Math.Min(times[i], ceiling);
            ceiling = times[i] - MinGap;
        }
        for (int i = 0; i < n; i++) times[i] = Math.Max(times[i], start);
        return new Result(times.Select(t => Math.Round(t, 3)).ToList(), alike.Count(a => a >= ClearLikeness), n);
    }

    /// <summary>The caption's words as the reader counts them (whatever lies between spaces).</summary>
    public static List<string> CaptionWords(string? caption) =>
        ReadAlong.Words(caption).Select(w => caption!.Substring(w.Start, w.Length)).ToList();

    /// <summary>How alike two words are, 0 to 1, after both are made plain (see <see cref="Normal"/>).</summary>
    public static double Likeness(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1;
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1;
        return 1 - Distance(a, b) / (double)Math.Max(a.Length, b.Length);
    }

    /// <summary>The fewest letters to add, take away or change to turn one word into the other.</summary>
    static int Distance(string a, string b)
    {
        var row = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) row[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            int diag = row[0];
            row[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int up = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diag + (a[i - 1] == b[j - 1] ? 0 : 1));
                diag = up;
            }
        }
        return row[b.Length];
    }

    /// <summary>
    /// A word made plain for comparing: lower case, no accents, no punctuation, and numbers written as
    /// digits whichever way they were written ("three", "drie" and "3" all become "3").
    /// </summary>
    public static string Normal(string word)
    {
        var sb = new StringBuilder(word.Length);
        foreach (char ch in word.ToLowerInvariant())
        {
            char c = Plain(ch);
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        }
        string s = sb.ToString();
        return Numbers.TryGetValue(s, out string? digits) ? digits : s;
    }

    const string Accented = "àáâãäåāèéêëēìíîïīòóôõöøōùúûüūýÿçñßŉ";
    const string Unaccented = "aaaaaaaeeeeeiiiiioooooooouuuuuyycnsn";

    static char Plain(char c)
    {
        int i = Accented.IndexOf(c);
        return i >= 0 ? Unaccented[i] : c;
    }

    // English and Afrikaans number words up to twenty, and the tens.
    static readonly Dictionary<string, string> Numbers = Build(
        ("zero", 0), ("nought", 0), ("one", 1), ("two", 2), ("three", 3), ("four", 4), ("five", 5), ("six", 6), ("seven", 7), ("eight", 8), ("nine", 9),
        ("ten", 10), ("eleven", 11), ("twelve", 12), ("thirteen", 13), ("fourteen", 14), ("fifteen", 15), ("sixteen", 16), ("seventeen", 17),
        ("eighteen", 18), ("nineteen", 19), ("twenty", 20), ("thirty", 30), ("forty", 40), ("fifty", 50), ("sixty", 60), ("seventy", 70),
        ("eighty", 80), ("ninety", 90), ("hundred", 100),
        ("nul", 0), ("een", 1), ("twee", 2), ("drie", 3), ("vier", 4), ("vyf", 5), ("ses", 6), ("sewe", 7), ("agt", 8), ("nege", 9), ("tien", 10),
        ("elf", 11), ("twaalf", 12), ("dertien", 13), ("veertien", 14), ("vyftien", 15), ("sestien", 16), ("sewentien", 17), ("agtien", 18),
        ("negentien", 19), ("twintig", 20), ("dertig", 30), ("veertig", 40), ("vyftig", 50), ("sestig", 60), ("sewentig", 70), ("tagtig", 80),
        ("negentig", 90), ("honderd", 100));

    static Dictionary<string, string> Build(params (string Word, int Value)[] pairs) =>
        pairs.ToDictionary(p => p.Word, p => p.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

/// <summary>
/// Turning what the recogniser reports into words. It reports pieces of words (" pud", "dle"), each with
/// its time in hundredths of a second; a piece starting with a space begins a new word. Markers it adds
/// for itself ("[_BEG_]", "[_TT_123]", "&lt;|en|&gt;") are left out.
/// </summary>
public static class HeardPieces
{
    /// <summary>
    /// One piece: its text, the stretch of time it was given, and (when known, otherwise -1) the moment
    /// the recogniser's attention lined it up with the sound, which tends to fall late in the piece.
    /// </summary>
    public readonly record struct Piece(string Text, double Start, double End, double Aligned = -1);

    public static List<HeardWord> ToWords(IEnumerable<Piece> pieces, double offset = 0)
    {
        var words = new List<HeardWord>();
        var text = new StringBuilder();
        double start = 0, end = 0;
        void Finish()
        {
            string t = text.ToString().Trim();
            if (WordAligner.Normal(t).Length > 0) words.Add(new HeardWord(t, start + offset, end + offset));
            text.Clear();
        }
        foreach (Piece p in pieces)
        {
            if (p.Text.Length == 0 || IsMarker(p.Text)) continue;
            bool newWord = p.Text.StartsWith(' ') || text.Length == 0;
            if (newWord)
            {
                if (text.Length > 0) Finish();
                // The stretch given to a piece tends to start early (it takes in the pause before the word)
                // and the attention point tends to come late, so halfway between the two is the better guess.
                start = p.Aligned > p.Start ? (p.Start + p.Aligned) / 2 : p.Start;
            }
            text.Append(p.Text);
            end = Math.Max(end, p.End);
            // Punctuation on its own ends nothing: "." after "seen" is part of "seen".
        }
        if (text.Length > 0) Finish();
        return words;
    }

    static bool IsMarker(string t)
    {
        string s = t.Trim();
        return (s.StartsWith("[_", StringComparison.Ordinal) && s.EndsWith(']')) || (s.StartsWith("<|", StringComparison.Ordinal) && s.EndsWith("|>"));
    }
}

/// <summary>
/// Putting each word's start exactly where its sound begins, when there is a pause before it. Speech
/// recognisers give times to within a few tenths of a second; a pause shows up in the recording as quiet,
/// so a start that falls in the quiet is moved on to where the sound starts again, and a start a little
/// way into a word that follows a pause is moved back to where the word begins. Words that run into each
/// other (no quiet between them) keep their times.
/// </summary>
public static class Onsets
{
    const double Frame = 0.01;          // loudness is measured every hundredth of a second

    public static List<HeardWord> Snap(ReadOnlySpan<float> samples, int rate, IReadOnlyList<HeardWord> words, double reach = 0.3)
    {
        double[] loud = Loudness(samples, rate);
        var result = new List<HeardWord>(words.Count);
        if (loud.Length == 0) { result.AddRange(words); return result; }
        double peak = loud.Max();
        double quiet = Math.Max(0.004, peak * 0.08);
        bool Quiet(int f) => f < 0 || f >= loud.Length || loud[f] < quiet;
        int F(double t) => (int)Math.Floor(t / Frame);

        for (int i = 0; i < words.Count; i++)
        {
            HeardWord w = words[i];
            double lo = Math.Max(i > 0 ? result[i - 1].Start + 0.05 : 0, w.Start - reach);
            double hi = Math.Min(i + 1 < words.Count ? words[i + 1].Start - 0.05 : double.MaxValue, w.Start + reach);
            int f = F(w.Start), flo = F(lo), fhi = F(hi);
            double start = w.Start;
            if (Quiet(f))
            {
                // In a pause: on to where the sound starts again.
                for (int g = f + 1; g <= fhi && g < loud.Length; g++) if (!Quiet(g)) { start = g * Frame; break; }
            }
            else
            {
                // In sound: back to the start of it, if a pause comes soon enough before.
                for (int g = f - 1; g >= flo; g--)
                {
                    if (Quiet(g)) { start = (g + 1) * Frame; break; }
                }
            }
            result.Add(w with { Start = Math.Round(start, 3) });
        }
        return result;
    }

    /// <summary>How loud each hundredth of a second is (root mean square).</summary>
    public static double[] Loudness(ReadOnlySpan<float> samples, int rate)
    {
        int size = Math.Max(1, (int)(rate * Frame));
        var loud = new double[samples.Length / size];
        for (int f = 0; f < loud.Length; f++)
        {
            double sum = 0;
            for (int k = f * size; k < (f + 1) * size; k++) sum += samples[k] * samples[k];
            loud[f] = Math.Sqrt(sum / size);
        }
        return loud;
    }
}
