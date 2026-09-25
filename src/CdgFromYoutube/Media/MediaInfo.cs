using CdgFromYoutube.Imaging;

namespace CdgFromYoutube.Media;

/// <summary>The properties of a media file that a conversion depends on.</summary>
public sealed record MediaInfo
{
    /// <summary>The length of the file.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>The width of the video stream in pixels, or zero when there is none.</summary>
    public required int VideoWidth { get; init; }

    /// <summary>The height of the video stream in pixels, or zero when there is none.</summary>
    public required int VideoHeight { get; init; }

    /// <summary>The frame rate of the video stream, or zero when it is not reported.</summary>
    public required double VideoFrameRate { get; init; }

    /// <summary>The sample rate of the audio stream in hertz, or zero when there is none.</summary>
    public required int AudioSampleRate { get; init; }

    /// <summary>The number of channels of the audio stream.</summary>
    public required int AudioChannelCount { get; init; }

    /// <summary>Whether the file holds a video stream.</summary>
    public bool HasVideo => VideoWidth > 0 && VideoHeight > 0;

    /// <summary>Whether the file holds an audio stream.</summary>
    public bool HasAudio => AudioSampleRate > 0;

    /// <summary>The size of the video stream.</summary>
    public FrameSize VideoSize => new(VideoWidth, VideoHeight);
}
