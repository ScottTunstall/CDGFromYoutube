namespace CdgFromYoutube.CommandLine;

/// <summary>Options for fetching the external tools without converting a video.</summary>
public sealed record ToolSetupOptions
{
    /// <summary>A path to ffmpeg, or null to search for it.</summary>
    public string? FfmpegPath { get; init; }

    /// <summary>A path to yt-dlp, or null to search for it.</summary>
    public string? YtDlpPath { get; init; }

    /// <summary>A path to the Deno JavaScript runtime, or null to search for it.</summary>
    public string? JavaScriptRuntimePath { get; init; }
}
