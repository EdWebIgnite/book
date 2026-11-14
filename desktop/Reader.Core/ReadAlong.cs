namespace Reader.Core;

/// <summary>
/// Read-along: a stop's caption split into words, and which word is being spoken at a moment of its
/// voice recording. Shared by the reader (to light the words up) and the editor (to time them).
/// </summary>
public static class ReadAlong
{
    /// <summary>One word of a caption: where it starts in the text and how long it is.</summary>
    public readonly record struct Word(int Start, int Length);

    /// <summary>
    /// The words of a caption, in order. A word is a run of characters between spaces or line breaks,
    /// so punctuation stays with its word ("Drip." is one word) and the text can be put back together
    /// exactly from the pieces and the gaps between them.
    /// </summary>
    public static List<Word> Words(string? text)
    {
        var words = new List<Word>();
        if (string.IsNullOrEmpty(text)) return words;
        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            if (i > start) words.Add(new Word(start, i - start));
        }
        return words;
    }

    public static int WordCount(string? text) => Words(text).Count;

    /// <summary>
    /// The word being spoken <paramref name="seconds"/> into the sound file: the last word whose start
    /// time has been reached, or -1 before the first word. Never past the last word of the caption.
    /// </summary>
    public static int WordAt(IReadOnlyList<double> times, int wordCount, double seconds)
    {
        int n = Math.Min(times.Count, wordCount);
        int found = -1;
        for (int i = 0; i < n; i++)
        {
            if (times[i] <= seconds + 1e-9) found = i;
            else break;
        }
        return found;
    }

    /// <summary>
    /// When the voice should be treated as finished, in seconds into the file: its trimmed end if it has
    /// one, otherwise a little after the last word starts, otherwise unknown (null).
    /// </summary>
    public static double? EndOf(Voice voice)
    {
        if (voice.End is double end && end > voice.StartOrZero) return end;
        if (voice.Words.Count > 0) return voice.Words[^1] + 0.9;
        return null;
    }

    /// <summary>
    /// True when the word times fit the caption: one time per word, each later than the one before, none
    /// before the start trim or after the end trim. The editor's Check book uses this.
    /// </summary>
    public static bool TimesFit(Voice voice, string? caption)
    {
        int n = WordCount(caption);
        if (voice.Words.Count != n) return false;
        for (int i = 1; i < n; i++) if (voice.Words[i] < voice.Words[i - 1]) return false;
        if (n > 0 && voice.Words[0] < voice.StartOrZero - 0.05) return false;
        if (n > 0 && voice.End is double end && end > 0 && voice.Words[^1] > end) return false;
        return true;
    }
}
