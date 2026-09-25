using System.IO.Compression;

namespace CdgFromYoutube.Tooling;

/// <summary>
/// Fetches yt-dlp and ffmpeg, so that the program can run on a machine where they are not installed.
/// </summary>
/// <remarks>
/// Both downloads come from their project's official release pages: yt-dlp publishes a single executable,
/// and the Windows FFmpeg build is the one that gyan.dev serves at a fixed address.
/// </remarks>
public sealed class ToolDownloader(IProgressSink progress)
{
    /// <summary>The address that always serves the newest yt-dlp executable.</summary>
    public const string YtDlpDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

    /// <summary>The address that always serves the newest Windows FFmpeg essentials build.</summary>
    public const string FfmpegDownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    /// <summary>The address that always serves the newest Deno build for 64 bit Windows.</summary>
    public const string DenoDownloadUrl =
        "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";

    private const string UserAgent = "cdgfromyoutube/1.0";

    /// <summary>Downloads yt-dlp into the given folder.</summary>
    public async Task DownloadYtDlpAsync(string destinationDirectory, CancellationToken cancellationToken)
    {
        string destination = Path.Combine(destinationDirectory, ToolLocator.YtDlpFileName);
        progress.Stage("Downloading yt-dlp...");
        await DownloadFileAsync(YtDlpDownloadUrl, destination, cancellationToken).ConfigureAwait(false);
        progress.Detail($"Saved {destination}");
    }

    /// <summary>Downloads FFmpeg and unpacks ffmpeg and ffprobe into the given folder.</summary>
    public async Task DownloadFFmpegAsync(string destinationDirectory, CancellationToken cancellationToken)
    {
        progress.Stage("Downloading ffmpeg, which is about fifty megabytes...");
        string archivePath = Path.Combine(Path.GetTempPath(), $"ffmpeg-{Guid.NewGuid():N}.zip");
        try
        {
            await DownloadFileAsync(FfmpegDownloadUrl, archivePath, cancellationToken).ConfigureAwait(false);
            ExtractExecutables(
                archivePath,
                destinationDirectory,
                ToolLocator.FfmpegFileName,
                ToolLocator.FfprobeFileName);
            progress.Detail($"Saved {Path.Combine(destinationDirectory, ToolLocator.FfmpegFileName)}");
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    /// <summary>Downloads Deno, which yt-dlp uses to work out how to reach some videos.</summary>
    public async Task DownloadJavaScriptRuntimeAsync(string destinationDirectory, CancellationToken cancellationToken)
    {
        progress.Stage("Downloading Deno, which yt-dlp needs for some videos...");
        string archivePath = Path.Combine(Path.GetTempPath(), $"deno-{Guid.NewGuid():N}.zip");
        try
        {
            await DownloadFileAsync(DenoDownloadUrl, archivePath, cancellationToken).ConfigureAwait(false);
            ExtractExecutables(archivePath, destinationDirectory, ToolLocator.DenoFileName);
            progress.Detail($"Saved {Path.Combine(destinationDirectory, ToolLocator.DenoFileName)}");
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    private static void ExtractExecutables(string archivePath, string destinationDirectory, params string[] fileNames)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (string fileName in fileNames)
        {
            ZipArchiveEntry entry = archive.Entries.FirstOrDefault(
                    candidate => candidate.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                ?? throw new KaraokeException($"The downloaded archive has no {fileName}.");

            entry.ExtractToFile(Path.Combine(destinationDirectory, fileName), overwrite: true);
        }
    }

    private static async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        using HttpResponseMessage response = await client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using FileStream destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }
}
