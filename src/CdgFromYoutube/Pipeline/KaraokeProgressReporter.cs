using CdgFromYoutube.Cdg;
using CdgFromYoutube.CommandLine;
using CdgFromYoutube.Imaging;
using CdgFromYoutube.Media;

namespace CdgFromYoutube.Pipeline;

/// <summary>Describes what a conversion is doing, and resolves the audio settings that come with a warning.</summary>
public sealed class KaraokeProgressReporter(IProgressSink progress)
{
    private const int BytesPerKilobyte = 1024;
    private const int BytesPerMegabyte = BytesPerKilobyte * 1024;

    /// <summary>Reports the source video's size, cropping, scaling and audio settings before conversion starts.</summary>
    public void ReportSource(MediaInfo media, KaraokeOptions options, int sampleRate)
    {
        FrameSize raster = CdgVideoFilter.GetRasterSize(options.UseSafeArea);
        FrameSize cropped = options.Crop.Apply(media.VideoSize);
        FrameSize fitted = AspectFit.FitInside(cropped, raster);
        bool isReduced = fitted.Width < cropped.Width || fitted.Height < cropped.Height;
        if (!options.Crop.IsNone)
        {
            progress.Detail($"Cropping the {media.VideoSize} video to {cropped}, cutting {options.Crop} percent from the left, top, right and bottom.");
        }

        progress.Detail(
            $"Scaling the {cropped} picture {(isReduced ? "down" : "up")} to {fitted} inside the {raster} raster.");

        if (sampleRate < media.AudioSampleRate)
        {
            string reason = options.Mp3SampleRate is null
                ? "the highest rate MP3 carries"
                : "the rate that was asked for";
            progress.Detail($"Reducing the audio from {media.AudioSampleRate} Hz to {sampleRate} Hz, {reason}.");
        }
        else if (sampleRate > media.AudioSampleRate)
        {
            progress.Warning(
                $"Raising the audio from {media.AudioSampleRate} Hz to {sampleRate} Hz, the lowest rate MP3 carries.");
        }

        if (options.UseDither)
        {
            progress.Detail("Mixing the two colours of each tile, so that gradients are drawn as a blend.");
        }

        progress.Detail(
            $"A whole screen is {CdgFormat.TileCount} of the {CdgFormat.PacketsPerSecond} packets a second, " +
            $"so a full redraw takes {(double)CdgFormat.TileCount / CdgFormat.PacketsPerSecond:0.#} seconds of playback.");
    }

    /// <summary>Returns the requested bit rate, reduced and warned about when the sample rate cannot carry it.</summary>
    public int ResolveBitRate(int requestedKbps, int sampleRate)
    {
        int maximum = Mp3SampleRate.GetMaximumBitRateKbps(sampleRate);
        if (requestedKbps <= maximum)
        {
            return requestedKbps;
        }

        progress.Warning($"An MP3 at {sampleRate} Hz carries at most {maximum} kbps, so {requestedKbps} kbps is reduced to {maximum} kbps.");
        return maximum;
    }

    /// <summary>Reports the frames, packets and file sizes a finished conversion produced.</summary>
    public void ReportResult(KaraokeResult result)
    {
        progress.Detail(
            $"Graphics: {result.FramesWritten} frames drawn, {result.FramesDropped} dropped, " +
            $"{result.FramesHeldBack} held back as states the picture passed through, " +
            $"which is {result.EffectiveFrameRate:0.00} frames a second over {result.Duration:hh\\:mm\\:ss}.");
        progress.Detail($"Audio: {result.Mp3SampleRate} Hz, {result.Mp3BitRateKbps} kbps, stereo.");
        progress.Completed(
            $"Done: {result.CdgPath} ({FormatBytes(result.CdgSizeBytes)}) " +
            $"and {result.Mp3Path} ({FormatBytes(result.Mp3SizeBytes)}).");
    }

    private static string FormatBytes(long byteCount) => byteCount >= BytesPerMegabyte
        ? $"{byteCount / (double)BytesPerMegabyte:0.0} MB"
        : $"{byteCount / (double)BytesPerKilobyte:0} kB";
}
