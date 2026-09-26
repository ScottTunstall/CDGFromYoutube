using CdgFromYoutube.Cdg;
using CdgFromYoutube.CommandLine;
using CdgFromYoutube.Imaging;
using CdgFromYoutube.Media;
using CdgFromYoutube.Tooling;

namespace CdgFromYoutube.Pipeline;

/// <summary>Turns a YouTube URL into a CD+G file and the MP3 that goes with it.</summary>
public sealed class KaraokePipeline(ToolPaths tools, IProgressSink progress)
{
    /// <summary>Converts one video.</summary>
    /// <param name="options">What to convert and how.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    public async Task<KaraokeResult> RunAsync(KaraokeOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        KaraokeProgressReporter reporter = new(progress);
        KaraokeGraphicsBuilder graphics = new(tools.Ffmpeg, progress);

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
            CropMargins crop = await graphics.DetectLyricAreaAsync(download.FilePath, media, cancellationToken)
                .ConfigureAwait(false);
            options = options with { Crop = crop };
            if (crop.IsNone)
            {
                progress.Detail("The lyrics fill the whole picture, so nothing is cropped.");
            }
        }

        int sampleRate = options.Mp3SampleRate ?? Mp3SampleRate.SelectFrom(media.AudioSampleRate);
        int bitRateKbps = reporter.ResolveBitRate(options.Mp3BitRateKbps, sampleRate);
        reporter.ReportSource(media, options, sampleRate);

        string baseName = OutputNaming.ResolveBaseName(options.BaseName ?? download.Title);
        Directory.CreateDirectory(options.OutputDirectory);
        string cdgPath = Path.Combine(options.OutputDirectory, baseName + ".cdg");
        string mp3Path = Path.Combine(options.OutputDirectory, baseName + ".mp3");

        progress.Stage("Choosing the colours for the graphics...");
        CdgPalette palette = await graphics.BuildPaletteAsync(download.FilePath, options, cancellationToken)
            .ConfigureAwait(false);

        progress.Stage($"Writing {Path.GetFileName(cdgPath)}...");
        CdgEncoder encoder = await graphics.EncodeAsync(
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

        reporter.ReportResult(result);
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
}
