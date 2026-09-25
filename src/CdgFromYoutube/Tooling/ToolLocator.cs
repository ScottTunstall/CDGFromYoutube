namespace CdgFromYoutube.Tooling;

/// <summary>
/// Finds yt-dlp, ffmpeg and ffprobe, either from the command line, from a folder next to this program, from
/// a <c>tools</c> folder beside it, from the PATH, or by downloading them.
/// </summary>
public static class ToolLocator
{
    /// <summary>The file name ffmpeg is published under on Windows.</summary>
    public const string FfmpegFileName = "ffmpeg.exe";

    /// <summary>The file name ffprobe is published under on Windows.</summary>
    public const string FfprobeFileName = "ffprobe.exe";

    /// <summary>The file name yt-dlp is published under on Windows.</summary>
    public const string YtDlpFileName = "yt-dlp.exe";

    /// <summary>The file name the Deno JavaScript runtime is published under on Windows.</summary>
    public const string DenoFileName = "deno.exe";

    /// <summary>The folder that downloads land in and that searches look at first.</summary>
    public static string DefaultToolsDirectory => Path.Combine(Environment.CurrentDirectory, "tools");

    /// <summary>
    /// Locates the tools, downloading the missing ones when <paramref name="allowDownload"/> is set.
    /// </summary>
    /// <param name="ffmpegPath">A path given on the command line, or <see langword="null"/> to search.</param>
    /// <param name="ytDlpPath">A path given on the command line, or <see langword="null"/> to search.</param>
    /// <param name="javaScriptRuntimePath">
    /// A path to a JavaScript runtime given on the command line, or <see langword="null"/> to search.
    /// </param>
    /// <param name="allowDownload">Whether to download the tools that could not be found.</param>
    /// <param name="progress">Reports what is happening.</param>
    /// <param name="cancellationToken">Cancels a download.</param>
    public static async Task<ToolPaths> LocateOrDownloadAsync(
        string? ffmpegPath,
        string? ytDlpPath,
        string? javaScriptRuntimePath,
        bool allowDownload,
        IProgressSink progress,
        CancellationToken cancellationToken)
    {
        string? ffmpeg = FindTool(FfmpegFileName, ffmpegPath);
        string? ytDlp = FindTool(YtDlpFileName, ytDlpPath);
        string? javaScriptRuntime = FindTool(DenoFileName, javaScriptRuntimePath);

        if (allowDownload && (ffmpeg is null || ytDlp is null || javaScriptRuntime is null))
        {
            Directory.CreateDirectory(DefaultToolsDirectory);
            ToolDownloader downloader = new(progress);
            if (ytDlp is null)
            {
                await downloader.DownloadYtDlpAsync(DefaultToolsDirectory, cancellationToken).ConfigureAwait(false);
                ytDlp = FindTool(YtDlpFileName, null);
            }

            if (ffmpeg is null)
            {
                await downloader.DownloadFFmpegAsync(DefaultToolsDirectory, cancellationToken).ConfigureAwait(false);
                ffmpeg = FindTool(FfmpegFileName, null);
            }

            if (javaScriptRuntime is null)
            {
                await TryDownloadJavaScriptRuntimeAsync(downloader, progress, cancellationToken).ConfigureAwait(false);
                javaScriptRuntime = FindTool(DenoFileName, null);
            }
        }

        if (ffmpeg is null)
        {
            throw new KaraokeException(MissingToolMessage(FfmpegFileName, "Gyan.FFmpeg"));
        }

        if (ytDlp is null)
        {
            throw new KaraokeException(MissingToolMessage(YtDlpFileName, "yt-dlp.yt-dlp"));
        }

        string? foundFfprobe = FindBeside(ffmpeg, FfprobeFileName) ?? FindTool(FfprobeFileName, null);
        if (foundFfprobe is null)
        {
            throw new KaraokeException(MissingToolMessage(FfprobeFileName, "Gyan.FFmpeg"));
        }

        string ffprobe = foundFfprobe;

        progress.Detail($"yt-dlp: {ytDlp}");
        progress.Detail($"ffmpeg: {ffmpeg}");
        if (javaScriptRuntime is not null)
        {
            progress.Detail($"javascript runtime: {javaScriptRuntime}");
        }

        return new ToolPaths(
            new ExternalTool("ffmpeg", ffmpeg),
            new ExternalTool("ffprobe", ffprobe),
            new ExternalTool("yt-dlp", ytDlp))
        {
            JavaScriptRuntimePath = javaScriptRuntime,
        };
    }

    /// <summary>
    /// Fetches Deno, which yt-dlp only needs for sites that hide how to reach their video behind a
    /// JavaScript challenge. A failure here is reported rather than thrown, because everything still works
    /// without it.
    /// </summary>
    private static async Task TryDownloadJavaScriptRuntimeAsync(
        ToolDownloader downloader,
        IProgressSink progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await downloader
                .DownloadJavaScriptRuntimeAsync(DefaultToolsDirectory, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is KaraokeException or HttpRequestException or IOException or InvalidDataException)
        {
            progress.Warning($"Could not fetch a JavaScript runtime, so some videos may be unreachable: {exception.Message}");
        }
    }

    private static string? FindTool(string fileName, string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            string fullPath = Path.GetFullPath(explicitPath);
            return File.Exists(fullPath)
                ? fullPath
                : throw new KaraokeException($"There is no {fileName} at '{fullPath}'.");
        }

        foreach (string directory in GetSearchDirectories())
        {
            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (pathVariable is null)
        {
            return null;
        }

        foreach (string directory in pathVariable.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(directory.Trim('"'), fileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static string? FindBeside(string executablePath, string fileName)
    {
        string? directory = Path.GetDirectoryName(executablePath);
        if (directory is null)
        {
            return null;
        }

        string candidate = Path.Combine(directory, fileName);
        return File.Exists(candidate) ? candidate : null;
    }

    private static IEnumerable<string> GetSearchDirectories()
    {
        yield return DefaultToolsDirectory;
        yield return AppContext.BaseDirectory;
        yield return Path.Combine(AppContext.BaseDirectory, "tools");
    }

    private static string MissingToolMessage(string fileName, string wingetPackageId) => $"""
        {fileName} was not found.
        Install it with      winget install {wingetPackageId}
        or place it in 'tools' next to this program, or on the PATH.
        Pass --download-tools to fetch yt-dlp and ffmpeg into '{DefaultToolsDirectory}'.
        """;
}
