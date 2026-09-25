using CdgFromYoutube.Tooling;

namespace CdgFromYoutube.Media;

/// <summary>Downloads a YouTube video with yt-dlp.</summary>
public sealed class YouTubeDownloader(ExternalTool ytDlp, IProgressSink progress, string? javaScriptRuntimePath)
{
    /// <summary>The stem of the downloaded file, which keeps the name independent of the title.</summary>
    private const string OutputFileName = "source";

    /// <summary>The name of the JavaScript runtime that yt-dlp is asked to use.</summary>
    private const string JavaScriptRuntimeName = "deno";

    /// <summary>How many of yt-dlp's complaints are passed on before the rest are dropped.</summary>
    private const int MaximumReportedWarnings = 5;

    /// <summary>
    /// The container the video and audio streams are merged into. Matroska accepts any combination of
    /// codecs, so a merge never fails the way it can when the streams are forced into an MP4 file.
    /// </summary>
    private const string MergeContainer = "mkv";

    /// <summary>Downloads the best video and audio the site offers for a URL.</summary>
    /// <param name="videoUrl">The address of the video.</param>
    /// <param name="destinationDirectory">The folder to download into.</param>
    /// <param name="maximumSourceHeight">The tallest source worth downloading, or null for the best.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    public async Task<YouTubeDownload> DownloadAsync(
        Uri videoUrl,
        string destinationDirectory,
        int? maximumSourceHeight,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(videoUrl);

        IReadOnlyList<string> arguments = BuildArguments(videoUrl, destinationDirectory, maximumSourceHeight);
        ExternalToolResult result = await ytDlp.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        ReportWarnings(result.StandardError);
        return ParseOutput(result.StandardOutput, destinationDirectory);
    }

    /// <summary>Builds the yt-dlp command line that downloads a video.</summary>
    /// <remarks>
    /// yt-dlp needs a JavaScript runtime to work out how to reach many YouTube videos; without one it
    /// either falls back to worse formats or the media request is refused outright.
    /// </remarks>
    public IReadOnlyList<string> BuildArguments(
        Uri videoUrl,
        string destinationDirectory,
        int? maximumSourceHeight)
    {
        ArgumentNullException.ThrowIfNull(videoUrl);

        List<string> arguments =
        [
            "--no-playlist",
            "--no-progress",
            "--merge-output-format", MergeContainer,
            "--format", BuildFormatSelector(maximumSourceHeight),
            "--output", Path.Combine(destinationDirectory, $"{OutputFileName}.%(ext)s"),
            "--print", "after_move:%(title)s",
            "--print", "after_move:%(filepath)s",
        ];

        if (javaScriptRuntimePath is not null)
        {
            arguments.Add("--js-runtimes");
            arguments.Add($"{JavaScriptRuntimeName}:{javaScriptRuntimePath}");
        }

        arguments.Add(videoUrl.AbsoluteUri);
        return arguments;
    }

    /// <summary>
    /// Passes on what yt-dlp complained about. It often explains a fallback that would otherwise be a
    /// mystery, such as a missing JavaScript runtime that costs access to the better formats.
    /// </summary>
    private void ReportWarnings(string standardError)
    {
        int reported = 0;
        foreach (string line in standardError.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith("WARNING", StringComparison.Ordinal))
            {
                continue;
            }

            progress.Warning(line);
            reported++;
            if (reported >= MaximumReportedWarnings)
            {
                break;
            }
        }
    }

    private static string BuildFormatSelector(int? maximumSourceHeight) =>
        maximumSourceHeight is int height
            ? $"bv*[height<={height}]+ba/b[height<={height}]"
            : "bv*+ba/b";

    private static YouTubeDownload ParseOutput(string standardOutput, string destinationDirectory)
    {
        string[] lines = [.. standardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        if (lines.Length >= 2 && File.Exists(lines[^1]))
        {
            return new YouTubeDownload(lines[^2], lines[^1]);
        }

        string newest = Directory
            .EnumerateFiles(destinationDirectory, $"{OutputFileName}.*")
            .Where(path => !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new KaraokeException("yt-dlp finished without leaving a file behind.");

        return new YouTubeDownload(lines.Length > 0 ? lines[0] : OutputFileName, newest);
    }
}
