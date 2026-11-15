using System.Security.Cryptography;

namespace Editor.Core;

/// <summary>
/// The speech recogniser's knowledge of speech: one big file (about 148 MB) that "Time words
/// automatically" needs. It isn't packed with the editor, to keep the download small for people who
/// never use it. The first time it's wanted it is downloaded into the editor's own folder
/// (%APPDATA%\BookEditor\models), checked, and kept, so after that everything works with no internet.
/// It knows many languages, English and Afrikaans among them.
/// </summary>
public sealed class SpeechModel
{
    /// <summary>Where it comes from, how big it is and its fingerprint, so a damaged download is never used.</summary>
    public sealed record Source(string Url, long Size, string Sha256);

    /// <summary>The multilingual file of about 148 MB: a good balance of speed and accuracy on an ordinary laptop.</summary>
    public static readonly Source Standard = new(
        "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
        147_951_465,
        "60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe");

    public SpeechModel(string? folder = null, Source? source = null)
    {
        Folder = folder ?? Path.Combine(EditorPaths.AppData, "models");
        From = source ?? Standard;
    }

    public string Folder { get; }
    public Source From { get; }

    /// <summary>The file, once downloaded.</summary>
    public string FilePath => Path.Combine(Folder, "speech-words.bin");
    string PartPath => FilePath + ".part";

    /// <summary>Downloaded and the right size (its fingerprint was checked when it arrived).</summary>
    public bool Ready => File.Exists(FilePath) && new FileInfo(FilePath).Length == From.Size;

    /// <summary>Megabytes, rounded, for telling people how big the download is.</summary>
    public int Megabytes => (int)Math.Round(From.Size / 1024.0 / 1024.0);

    /// <summary>
    /// Download it. Progress reports bytes so far out of the total. A download that was stopped part way
    /// carries on from where it got to, if the server allows. Throws <see cref="OperationCanceledException"/>
    /// when cancelled (what was fetched is kept for next time), and <see cref="IOException"/> with a plain
    /// message when it can't be fetched or arrives damaged.
    /// </summary>
    public async Task Download(IProgress<(long Done, long Total)>? progress, CancellationToken cancel, HttpMessageHandler? handler = null)
    {
        if (Ready) { progress?.Report((From.Size, From.Size)); return; }
        Directory.CreateDirectory(Folder);
        using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = Timeout.InfiniteTimeSpan;            // a slow line can take a while; cancel stops it
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BookEditor/1.0");
        long have = File.Exists(PartPath) ? new FileInfo(PartPath).Length : 0;
        if (have >= From.Size) { File.Delete(PartPath); have = 0; }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, From.Url);
            if (have > 0) request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(have, null);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
            if (!response.IsSuccessStatusCode) throw new IOException($"The download didn't start (the server said {(int)response.StatusCode}).");
            bool resumed = have > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent;
            if (!resumed) have = 0;
            await using (Stream input = await response.Content.ReadAsStreamAsync(cancel))
            await using (var output = new FileStream(PartPath, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                var buffer = new byte[1 << 16];
                long done = have;
                progress?.Report((done, From.Size));
                int n;
                while ((n = await input.ReadAsync(buffer, cancel)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, n), cancel);
                    done += n;
                    if (done > From.Size) throw new IOException("The download was bigger than it should be.");
                    progress?.Report((done, From.Size));
                }
            }
        }
        catch (HttpRequestException e)
        {
            throw new IOException("Couldn't reach the internet to download it.", e);
        }

        long got = new FileInfo(PartPath).Length;
        if (got != From.Size) throw new IOException("The download stopped before the end. Try again: it carries on from where it got to.");
        string sum;
        await using (FileStream f = File.OpenRead(PartPath)) sum = Convert.ToHexString(await SHA256.HashDataAsync(f, cancel)).ToLowerInvariant();
        if (sum != From.Sha256)
        {
            File.Delete(PartPath);
            throw new IOException("The download arrived damaged. Try again.");
        }
        File.Move(PartPath, FilePath, overwrite: true);
    }

    /// <summary>Delete the downloaded file (and any part download), to free the space.</summary>
    public void Remove()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
        if (File.Exists(PartPath)) File.Delete(PartPath);
    }
}
