namespace CdgFromYoutube.Tooling;

/// <summary>The external tools that a conversion needs.</summary>
/// <param name="Ffmpeg">The ffmpeg executable that decodes video and audio.</param>
/// <param name="Ffprobe">The ffprobe executable that reports the properties of a media file.</param>
/// <param name="YtDlp">The yt-dlp executable that downloads the video.</param>
public sealed record ToolPaths(ExternalTool Ffmpeg, ExternalTool Ffprobe, ExternalTool YtDlp)
{
    /// <summary>
    /// The path of the JavaScript runtime that yt-dlp uses to work out how to reach a video, or
    /// <see langword="null"/> when the machine has none.
    /// </summary>
    public string? JavaScriptRuntimePath { get; init; }
}
