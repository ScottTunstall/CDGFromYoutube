namespace CdgFromYoutube.Pipeline;

/// <summary>What a finished conversion produced.</summary>
public sealed record KaraokeResult
{
    /// <summary>The path of the CD+G file that was written.</summary>
    public required string CdgPath { get; init; }

    /// <summary>The path of the MP3 file that was written.</summary>
    public required string Mp3Path { get; init; }

    /// <summary>The size of the CD+G file in bytes.</summary>
    public required long CdgSizeBytes { get; init; }

    /// <summary>The size of the MP3 file in bytes.</summary>
    public required long Mp3SizeBytes { get; init; }

    /// <summary>The number of packets in the CD+G file.</summary>
    public required long PacketCount { get; init; }

    /// <summary>The number of frames that were drawn.</summary>
    public required int FramesWritten { get; init; }

    /// <summary>The number of frames that were dropped because the screen could not be redrawn in time.</summary>
    public required int FramesDropped { get; init; }

    /// <summary>The number of frames that were passed over because the picture was only passing through them.</summary>
    public required int FramesHeldBack { get; init; }

    /// <summary>The length of the track.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>The sample rate of the MP3 file, in hertz.</summary>
    public required int Mp3SampleRate { get; init; }

    /// <summary>The bit rate of the MP3 file, in kilobits per second.</summary>
    public required int Mp3BitRateKbps { get; init; }

    /// <summary>The number of frames per second that the graphics turned out to show.</summary>
    public double EffectiveFrameRate =>
        Duration.TotalSeconds > 0 ? FramesWritten / Duration.TotalSeconds : 0;
}
