using CdgFromYoutube.Cdg;
using CdgFromYoutube.CommandLine;
using CdgFromYoutube.Imaging;
using CdgFromYoutube.Media;
using CdgFromYoutube.Tooling;

namespace CdgFromYoutube.Pipeline;

/// <summary>Turns a YouTube URL into a CD+G file and the MP3 that goes with it.</summary>
public sealed class KaraokePipeline(ToolPaths tools, IProgressSink progress)
{
    /// <summary>The rate the video is sampled at while the palette is chosen.</summary>
    private const double PaletteFramesPerSecond = 1;

    /// <summary>The width frames are scaled to while the lyrics are looked for, which is plenty to find a box.</summary>
    private const int LyricAreaAnalysisWidth = 320;

    private const int BytesPerKilobyte = 1024;
    private const int BytesPerMegabyte = BytesPerKilobyte * 1024;

    /// <summary>Converts one video.</summary>
    /// <param name="options">What to convert and how.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    public async Task<KaraokeResult> RunAsync(KaraokeOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        using TempWorkspace workspace = new(options.KeepTemporaryFiles);
        if (options.KeepTemporaryFiles)
        {
            progress.Detail($"Keeping the download in {workspace.DirectoryPath}");
        }

        progress.Stage("Downloading the video...");
        YouTubeDownload download = await new YouTubeDownloader(tools.YtDlp, progress, tools.JavaScriptRuntimePath)
            .DownloadAsync(options.VideoUrl, workspace.DirectoryPath, options.MaximumSourceHeight, cancellationToken)
            .ConfigureAwait(false);
        progress.Detail($"Title: {download.Title}");

        progress.Stage("Reading the properties of the video...");
        MediaInfo media = await new MediaProbe(tools.Ffprobe)
            .ProbeAsync(download.FilePath, cancellationToken)
            .ConfigureAwait(false);
        RequireUsableMedia(media);

        if (options.AutoCrop)
        {
            progress.Stage("Finding where the lyrics are...");
            CropMargins crop = await DetectLyricAreaAsync(download.FilePath, media, cancellationToken)
                .ConfigureAwait(false);
            options = options with { Crop = crop };
            if (crop.IsNone)
            {
                progress.Detail("The lyrics fill the whole picture, so nothing is cropped.");
            }
        }

        int sampleRate = options.Mp3SampleRate ?? Mp3SampleRate.SelectFrom(media.AudioSampleRate);
        int bitRateKbps = ResolveBitRate(options.Mp3BitRateKbps, sampleRate);
        ReportSource(media, options, sampleRate);

        string baseName = OutputNaming.ResolveBaseName(options.BaseName ?? download.Title);
        Directory.CreateDirectory(options.OutputDirectory);
        string cdgPath = Path.Combine(options.OutputDirectory, baseName + ".cdg");
        string mp3Path = Path.Combine(options.OutputDirectory, baseName + ".mp3");

        progress.Stage("Choosing the colors for the graphics...");
        CdgPalette palette = await BuildPaletteAsync(download.FilePath, options, cancellationToken)
            .ConfigureAwait(false);

        progress.Stage($"Writing {Path.GetFileName(cdgPath)}...");
        CdgEncoder encoder = await EncodeGraphicsAsync(
                download.FilePath,
                cdgPath,
                options,
                palette,
                media.Duration,
                cancellationToken)
            .ConfigureAwait(false);

        progress.Stage($"Writing {Path.GetFileName(mp3Path)}...");
        await new Mp3TrackEncoder(tools.Ffmpeg)
            .EncodeAsync(download.FilePath, mp3Path, sampleRate, bitRateKbps, cancellationToken)
            .ConfigureAwait(false);

        KaraokeResult result = new()
        {
            CdgPath = cdgPath,
            Mp3Path = mp3Path,
            CdgSizeBytes = new FileInfo(cdgPath).Length,
            Mp3SizeBytes = new FileInfo(mp3Path).Length,
            PacketCount = encoder.PacketsWritten,
            FramesWritten = encoder.FramesWritten,
            FramesDropped = encoder.FramesDropped,
            FramesHeldBack = encoder.FramesHeldBack,
            Duration = media.Duration,
            Mp3SampleRate = sampleRate,
            Mp3BitRateKbps = bitRateKbps,
        };

        ReportResult(result);
        return result;
    }

    private static void RequireUsableMedia(MediaInfo media)
    {
        if (!media.HasVideo)
        {
            throw new KaraokeException("The downloaded file has no video stream to draw.");
        }

        if (!media.HasAudio)
        {
            throw new KaraokeException("The downloaded file has no audio stream to make an MP3 from.");
        }

        if (media.Duration <= TimeSpan.Zero)
        {
            throw new KaraokeException("The downloaded file reports no length, so the graphics cannot be timed.");
        }
    }

    private void ReportSource(MediaInfo media, KaraokeOptions options, int sampleRate)
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
            progress.Detail("Mixing the two colors of each tile, so that gradients are drawn as a blend.");
        }

        progress.Detail(
            $"A whole screen is {CdgFormat.TileCount} of the {CdgFormat.PacketsPerSecond} packets a second, " +
            $"so a full redraw takes {(double)CdgFormat.TileCount / CdgFormat.PacketsPerSecond:0.#} seconds of playback.");
    }

    private int ResolveBitRate(int requestedKbps, int sampleRate)
    {
        int maximum = Mp3SampleRate.GetMaximumBitRateKbps(sampleRate);
        if (requestedKbps <= maximum)
        {
            return requestedKbps;
        }

        progress.Warning($"An MP3 at {sampleRate} Hz carries at most {maximum} kbps, so {requestedKbps} kbps is reduced to {maximum} kbps.");
        return maximum;
    }

    /// <summary>Samples the video and returns the margins that crop it down to the lyrics.</summary>
    private async Task<CropMargins> DetectLyricAreaAsync(
        string videoPath,
        MediaInfo media,
        CancellationToken cancellationToken)
    {
        FrameSize analysis = new(
            LyricAreaAnalysisWidth,
            Math.Max(2, (int)Math.Round(LyricAreaAnalysisWidth * (double)media.VideoHeight / media.VideoWidth / 2) * 2));
        LyricAreaDetector detector = new(analysis.Width, analysis.Height);
        await new VideoFrameReader(tools.Ffmpeg)
            .ReadScaledAsync(
                videoPath,
                PaletteFramesPerSecond,
                analysis,
                (_, rgbPixels) => detector.AddFrame(rgbPixels),
                cancellationToken)
            .ConfigureAwait(false);

        return detector.GetCropMargins();
    }

    private async Task<CdgPalette> BuildPaletteAsync(
        string videoPath,
        KaraokeOptions options,
        CancellationToken cancellationToken)
    {
        CdgColorHistogram histogram = new();
        int sampledFrames = await new VideoFrameReader(tools.Ffmpeg)
            .ReadAsync(
                videoPath,
                PaletteFramesPerSecond,
                options.UseSafeArea,
                options.Crop,
                (_, rgbPixels) => histogram.AddFrame(rgbPixels),
                cancellationToken)
            .ConfigureAwait(false);

        if (sampledFrames == 0)
        {
            throw new KaraokeException("The video produced no frames to take colors from.");
        }

        progress.Detail($"Sampled {sampledFrames} frames and {histogram.SampleCount} pixels.");
        return CdgPaletteBuilder.Build(histogram, [CdgColor.Black]);
    }

    private async Task<CdgEncoder> EncodeGraphicsAsync(
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

        await new VideoFrameReader(tools.Ffmpeg)
            .ReadAsync(
                videoPath,
                options.VideoFrameRate,
                options.UseSafeArea,
                options.Crop,
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

    private void ReportResult(KaraokeResult result)
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
