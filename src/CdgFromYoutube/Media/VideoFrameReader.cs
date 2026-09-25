using System.Globalization;
using CdgFromYoutube.Cdg;
using CdgFromYoutube.Imaging;
using CdgFromYoutube.Tooling;

namespace CdgFromYoutube.Media;

/// <summary>Receives one decoded frame.</summary>
/// <param name="frameIndex">The number of the frame, counting from zero.</param>
/// <param name="rgbPixels">
/// The pixels of the frame as packed eight bit red, green and blue. The span points at a buffer that is
/// reused for the next frame, so it is only valid for the duration of the call.
/// </param>
public delegate void FrameHandler(int frameIndex, ReadOnlySpan<byte> rgbPixels);

/// <summary>Decodes a video into CD+G frames by streaming raw frames out of ffmpeg.</summary>
public sealed class VideoFrameReader(ExternalTool ffmpeg)
{
    /// <summary>Decodes a video, calling <paramref name="onFrame"/> once per frame.</summary>
    /// <param name="videoPath">The video to decode.</param>
    /// <param name="framesPerSecond">The rate frames are taken from the video at.</param>
    /// <param name="useSafeArea">Whether to stay inside the area that all players are guaranteed to show.</param>
    /// <param name="crop">The margins cut from the source before it is scaled.</param>
    /// <param name="onFrame">Called with each frame.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>The number of frames that were decoded.</returns>
    public async Task<int> ReadAsync(
        string videoPath,
        double framesPerSecond,
        bool useSafeArea,
        CropMargins crop,
        FrameHandler onFrame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onFrame);

        return await ReadCoreAsync(
                videoPath,
                CdgVideoFilter.Build(framesPerSecond, useSafeArea, crop),
                new FrameSize(CdgFormat.Width, CdgFormat.Height),
                onFrame,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Decodes a video at the given size with nothing but scaling, for looking at the picture as it is
    /// rather than as it will be drawn.
    /// </summary>
    /// <param name="videoPath">The video to decode.</param>
    /// <param name="framesPerSecond">The rate frames are taken from the video at.</param>
    /// <param name="size">The size every frame is scaled to.</param>
    /// <param name="onFrame">Called with each frame.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>The number of frames that were decoded.</returns>
    public async Task<int> ReadScaledAsync(
        string videoPath,
        double framesPerSecond,
        FrameSize size,
        FrameHandler onFrame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onFrame);

        string filter = string.Create(
            CultureInfo.InvariantCulture,
            $"fps={framesPerSecond:0.####},scale={size.Width}:{size.Height}:flags=area,format=rgb24");
        return await ReadCoreAsync(videoPath, filter, size, onFrame, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ReadCoreAsync(
        string videoPath,
        string filter,
        FrameSize size,
        FrameHandler onFrame,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[RgbPixelFormat.GetFrameSizeBytes(size.Width, size.Height)];
        List<string> arguments =
        [
            "-hide_banner", "-nostdin", "-v", "error",
            "-i", videoPath,
            "-an", "-sn",
            "-vf", filter,
            "-f", "rawvideo",
            "-pix_fmt", "rgb24",
            "-",
        ];

        int frameIndex = 0;
        await ffmpeg.RunAsync(
            arguments,
            async (standardOutput, token) =>
            {
                while (await TryReadFrameAsync(standardOutput, buffer, token).ConfigureAwait(false))
                {
                    onFrame(frameIndex, buffer);
                    frameIndex++;
                }
            },
            cancellationToken).ConfigureAwait(false);

        return frameIndex;
    }

    /// <summary>Reads one whole frame, and reports whether another frame was there to read.</summary>
    private static async Task<bool> TryReadFrameAsync(Stream source, byte[] buffer, CancellationToken cancellationToken)
    {
        int filled = 0;
        while (filled < buffer.Length)
        {
            int read = await source.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (filled == 0)
                {
                    return false;
                }

                throw new KaraokeException($"ffmpeg stopped part way through a frame ({filled} of {buffer.Length} bytes).");
            }

            filled += read;
        }

        return true;
    }
}
