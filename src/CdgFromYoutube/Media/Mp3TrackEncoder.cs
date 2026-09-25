using System.Globalization;
using CdgFromYoutube.Tooling;

namespace CdgFromYoutube.Media;

/// <summary>
/// Writes the audio track of a video file as an MP3, which is the half of a karaoke pair that a player
/// plays while it draws the matching .cdg file.
/// </summary>
/// <remarks>
/// ffmpeg does the decoding and the resampling, because it reads whatever container and codec the download
/// turned out to be, and its resampler is better than anything a command line tool should grow of its own.
/// </remarks>
public sealed class Mp3TrackEncoder(ExternalTool ffmpeg)
{
    /// <summary>The number of channels written, which is what karaoke players expect.</summary>
    public const int ChannelCount = 2;

    /// <summary>Extracts the audio track of a video, resamples it and writes it as MP3.</summary>
    /// <param name="videoPath">The file to take the audio from.</param>
    /// <param name="mp3Path">The MP3 file to write.</param>
    /// <param name="sampleRate">The sample rate of the MP3, in hertz.</param>
    /// <param name="bitRateKbps">The bit rate of the MP3, in kilobits per second.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    public async Task EncodeAsync(
        string videoPath,
        string mp3Path,
        int sampleRate,
        int bitRateKbps,
        CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "-hide_banner", "-nostdin", "-v", "error",
            "-i", videoPath,
            "-vn", "-map", "0:a:0",
            "-ac", ToArgument(ChannelCount),
            "-ar", ToArgument(sampleRate),
            "-f", "s16le",
            "-acodec", "pcm_s16le",
            "-",
        ];

        LameMp3Encoder encoder = new(sampleRate, ChannelCount, bitRateKbps);
        await using FileStream mp3 = File.Create(mp3Path);
        await ffmpeg.RunAsync(
            arguments,
            (pcm, token) => encoder.EncodeAsync(pcm, mp3, token),
            cancellationToken).ConfigureAwait(false);
    }

    private static string ToArgument(int value) => value.ToString(CultureInfo.InvariantCulture);
}
