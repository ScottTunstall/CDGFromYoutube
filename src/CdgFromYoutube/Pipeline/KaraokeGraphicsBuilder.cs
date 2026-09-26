using CdgFromYoutube.Cdg;
using CdgFromYoutube.CommandLine;
using CdgFromYoutube.Imaging;
using CdgFromYoutube.Media;
using CdgFromYoutube.Tooling;

namespace CdgFromYoutube.Pipeline;

/// <summary>Turns a downloaded video into CD+G graphics: the lyric crop, the palette and the tile encoding.</summary>
public sealed class KaraokeGraphicsBuilder(ExternalTool ffmpeg, IProgressSink progress)
{
    /// <summary>The rate the video is sampled at while the lyrics are found or the palette is chosen.</summary>
    private const double AnalysisFramesPerSecond = 1;

    /// <summary>The width frames are scaled to while the lyrics are looked for, which is plenty to find a box.</summary>
    private const int LyricAreaAnalysisWidth = 320;

    /// <summary>Samples the video and returns the margins that crop it down to the lyrics.</summary>
    public async Task<CropMargins> DetectLyricAreaAsync(
        string videoPath,
        MediaInfo media,
        CancellationToken cancellationToken)
    {
        FrameSize analysis = new(
            LyricAreaAnalysisWidth,
            Math.Max(2, (int)Math.Round(LyricAreaAnalysisWidth * (double)media.VideoHeight / media.VideoWidth / 2) * 2));
        LyricAreaDetector detector = new(analysis.Width, analysis.Height);
        await new VideoFrameReader(ffmpeg)
            .ReadScaledAsync(
                videoPath,
                AnalysisFramesPerSecond,
                analysis,
                (_, rgbPixels) => detector.AddFrame(rgbPixels),
                cancellationToken)
            .ConfigureAwait(false);

        return detector.GetCropMargins();
    }

    /// <summary>Samples the video and chooses the sixteen colors the graphics are drawn with.</summary>
    public async Task<CdgPalette> BuildPaletteAsync(
        string videoPath,
        KaraokeOptions options,
        CancellationToken cancellationToken)
    {
        CdgColorHistogram histogram = new();
        int sampledFrames = await new VideoFrameReader(ffmpeg)
            .ReadAsync(
                videoPath,
                AnalysisFramesPerSecond,
                options.UseSafeArea,
                options.Crop,
                sharpen: !options.Antialias,
                (_, rgbPixels) => histogram.AddFrame(rgbPixels),
                cancellationToken)
            .ConfigureAwait(false);

        if (sampledFrames == 0)
        {
            throw new KaraokeException("The video produced no frames to take colors from.");
        }

        progress.Detail($"Sampled {sampledFrames} frames and {histogram.SampleCount} pixels.");
        if (!options.Antialias)
        {
            return CdgPaletteBuilder.Build(histogram, [CdgColor.Black]);
        }

        CdgPalette palette = CdgRampPaletteBuilder.Build(histogram);
        progress.Detail($"Antialiasing with {palette.Ramps.Count} lyric color(s).");
        return palette;
    }

    /// <summary>Encodes the video as CD+G graphics timed against <paramref name="duration"/>.</summary>
    public async Task<CdgEncoder> EncodeAsync(
        string videoPath,
        string cdgPath,
        KaraokeOptions options,
        CdgPalette palette,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        await using FileStream cdg = File.Create(cdgPath);
        CdgWriter writer = new(cdg);
        CdgEncoder encoder = new(writer, palette, duration);
        encoder.WritePrologue();

        CdgTileEncoder tileEncoder = new(palette, options.UseDither);
        double durationSeconds = duration.TotalSeconds;

        await new VideoFrameReader(ffmpeg)
            .ReadAsync(
                videoPath,
                options.VideoFrameRate,
                options.UseSafeArea,
                options.Crop,
                sharpen: !options.Antialias,
                (frameIndex, rgbPixels) =>
                {
                    TimeSpan timestamp = TimeSpan.FromSeconds((double)frameIndex / options.VideoFrameRate);
                    encoder.WriteFrame(timestamp, tileEncoder.Encode(rgbPixels));
                    progress.Progress(
                        timestamp.TotalSeconds / durationSeconds,
                        $"{encoder.FramesWritten} frames drawn, {encoder.FramesDropped} dropped, " +
                        $"{encoder.FramesHeldBack} held back, {encoder.PacketsWritten} packets");
                },
                cancellationToken)
            .ConfigureAwait(false);

        encoder.WriteEpilogue();
        progress.FinishProgress();
        return encoder;
    }
}
