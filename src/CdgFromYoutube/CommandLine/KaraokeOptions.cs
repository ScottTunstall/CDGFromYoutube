using CdgFromYoutube.Imaging;

namespace CdgFromYoutube.CommandLine;

/// <summary>Everything a conversion needs to know before it starts.</summary>
public sealed record KaraokeOptions
{
    /// <summary>The frame rate that frames are taken from the video at, before packets are budgeted.</summary>
    public const int DefaultVideoFrameRate = 15;

    /// <summary>The lowest frame rate that may be asked for.</summary>
    public const int MinimumVideoFrameRate = 1;

    /// <summary>The highest frame rate that may be asked for.</summary>
    public const int MaximumVideoFrameRate = 60;

    /// <summary>The MP3 bit rate used when none is asked for.</summary>
    public const int DefaultMp3BitRateKbps = 192;

    /// <summary>The tallest source video that may be asked for.</summary>
    public const int MaximumSourceHeightPixels = 4320;

    /// <summary>The address of the video to convert.</summary>
    public required Uri VideoUrl { get; init; }

    /// <summary>The folder that the .cdg and .mp3 files are written to.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>The base name of the output files, or null to use the title of the video.</summary>
    public string? BaseName { get; init; }

    /// <summary>The frame rate that frames are taken from the video at.</summary>
    public int VideoFrameRate { get; init; } = DefaultVideoFrameRate;

    /// <summary>The bit rate of the MP3 file, in kilobits per second.</summary>
    public int Mp3BitRateKbps { get; init; } = DefaultMp3BitRateKbps;

    /// <summary>The sample rate of the MP3 file, or null to keep the source rate wherever MP3 allows it.</summary>
    public int? Mp3SampleRate { get; init; }

    /// <summary>The tallest source video worth downloading, or null for the best that is offered.</summary>
    public int? MaximumSourceHeight { get; init; }

    /// <summary>Whether tiles mix their two colors to soften gradients.</summary>
    /// <remarks>
    /// Off by default: mixing suits broad gradients, but lettering at this size is about one pixel thick
    /// and a mixed edge pixel has as much chance of taking the background color as the glyph color, which
    /// leaves the letters speckled.
    /// </remarks>
    public bool UseDither { get; init; }

    /// <summary>Whether the image is kept inside the area that all players are guaranteed to show.</summary>
    public bool UseSafeArea { get; init; }

    /// <summary>The margins cut from the source before it is scaled, so that the lyrics fill more of the raster.</summary>
    public CropMargins Crop { get; init; }

    /// <summary>Whether <see cref="Crop"/> is found by looking for the lyrics in the video, rather than given.</summary>
    public bool AutoCrop { get; init; }

    /// <summary>Whether the downloaded video is kept after the conversion.</summary>
    public bool KeepTemporaryFiles { get; init; }

    /// <summary>A path to ffmpeg, or null to search for it.</summary>
    public string? FfmpegPath { get; init; }

    /// <summary>A path to yt-dlp, or null to search for it.</summary>
    public string? YtDlpPath { get; init; }

    /// <summary>A path to the Deno JavaScript runtime, or null to search for it.</summary>
    public string? JavaScriptRuntimePath { get; init; }

    /// <summary>Whether the tools that cannot be found are downloaded.</summary>
    public bool DownloadTools { get; init; }
}
