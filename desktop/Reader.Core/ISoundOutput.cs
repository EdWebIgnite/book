namespace Reader.Core;

/// <summary>
/// The sounds the reader asks for. The engine only says what should be heard; something else
/// (Reader.Audio on the desktop) makes the noise. Every method must be safe to call when there is
/// no sound device at all.
/// </summary>
public interface ISoundOutput
{
    /// <summary>A touch on empty space.</summary>
    void Tap();

    /// <summary>The zoom buttons: two rising notes for in, two falling for out.</summary>
    void Zoom(bool zoomIn);

    /// <summary>Something was found.</summary>
    void Find();

    /// <summary>A story step tapped too early.</summary>
    void Locked();

    /// <summary>The story on a page was finished.</summary>
    void Party();

    /// <summary>Soft rush of air for a camera move that takes this many milliseconds.</summary>
    void Whoosh(double milliseconds);

    /// <summary>A soft, papery swish as a page is turned.</summary>
    void PageTurn();

    /// <summary>Steady rain, as loud as <paramref name="level"/> (0 to 1). Call again to change or stop it.</summary>
    void Rain(double level);

    /// <summary>A low rumble starting after <paramref name="delaySeconds"/>.</summary>
    void Thunder(double delaySeconds);

    /// <summary>Play a sound file from the book's folder: narration or a sound effect. One at a time.</summary>
    void PlayClip(string fileName);

    void StopClip();

    /// <summary>True while a sound file is still playing. A stop that goes on by itself waits for it.</summary>
    bool ClipPlaying { get; }

    /// <summary>
    /// Play a stop's recorded voice from <paramref name="fromSeconds"/> to <paramref name="toSeconds"/>
    /// (0 or less: to the end). The voice has its own channel, so it plays alongside a sound effect.
    /// </summary>
    void PlayVoice(string fileName, double fromSeconds, double toSeconds) { }

    void StopVoice() { }

    /// <summary>True while the voice is loading or still playing.</summary>
    bool VoicePlaying => false;

    /// <summary>
    /// How far into the sound file the voice has got, in seconds, or -1 when the output can't tell (then
    /// the reader keeps time itself). While the file is still loading this stays at the start.
    /// </summary>
    double VoicePosition => -1;
}

/// <summary>Silence, for tests and for machines with no sound.</summary>
public sealed class NoSound : ISoundOutput
{
    public void Tap() { }
    public void Zoom(bool zoomIn) { }
    public void Find() { }
    public void Locked() { }
    public void Party() { }
    public void Whoosh(double milliseconds) { }
    public void PageTurn() { }
    public void Rain(double level) { }
    public void Thunder(double delaySeconds) { }
    public void PlayClip(string fileName) { }
    public void StopClip() { }
    public bool ClipPlaying => false;
}
