using System.Globalization;
using CdgFromYoutube.Cdg;
using CdgFromYoutube.Imaging;

namespace CdgFromYoutube.Media;

/// <summary>Builds the ffmpeg filter chain that turns video frames into fixed size CD+G frames.</summary>
/// <remarks>
/// Frames are scaled with their shape preserved, so widescreen video gains black bars instead of being
/// stretched, and are then padded out to the full raster so that every frame is exactly
/// <see cref="CdgFormat.Width"/> by <see cref="CdgFormat.Height"/> pixels.
/// </remarks>
public static class CdgVideoFilter
{
    /// <summary>How much the scaled picture is sharpened before its colors are reduced.</summary>
    /// <remarks>
    /// Scaling down to 300x216 leaves the lettering as a wash of half shades, because a stroke that was
    /// three or four pixels thick in the source becomes about one pixel. A tile can only spend two colors
    /// on those pixels, so a washed edge has to be rounded to one of them and the lettering comes out
    /// chunky. Sharpening first pushes each edge pixel back towards the color it belongs to, which gives
    /// the tile reduction something decisive to work with.
    /// </remarks>
    private const double SharpenAmount = 0.8;

    /// <summary>The width and height of the sharpening kernel.</summary>
    private const int SharpenKernelSize = 5;

    /// <summary>How much the color of the scaled picture is strengthened before its colors are reduced.</summary>
    /// <remarks>
    /// Video stores color at a lower resolution than brightness, and compression smears it further, so the
    /// edge pixels of colored lettering keep their brightness but lose most of their color: an orange
    /// letter is ringed by dull browns and greys. The palette then spends entries on those browns and the
    /// tiles draw them as flat patches of an odd color beside the lettering, where in the video a dark
    /// outline hid them; the outline is folded into the background here, so nothing does. Strengthening
    /// the color pulls those pixels back towards the lettering they belong to, and leaves white lettering
    /// and its grey edges alone, since they have no color to strengthen. On a karaoke track this took the
    /// dull edge pixels from 8% of the lettering to 2%.
    /// </remarks>
    private const double Saturation = 1.5;

    /// <summary>Returns the area of the raster that the image is fitted into.</summary>
    /// <param name="useSafeArea">Whether to stay inside the area that all players are guaranteed to show.</param>
    public static FrameSize GetRasterSize(bool useSafeArea) => useSafeArea
        ? new FrameSize(CdgFormat.SafeWidth, CdgFormat.SafeHeight)
        : new FrameSize(CdgFormat.Width, CdgFormat.Height);

    /// <summary>Builds the filter chain that samples, scales and pads frames.</summary>
    /// <param name="framesPerSecond">The rate frames are taken from the video at.</param>
    /// <param name="useSafeArea">Whether to stay inside the area that all players are guaranteed to show.</param>
    /// <param name="crop">The margins cut from the source before it is scaled.</param>
    public static string Build(double framesPerSecond, bool useSafeArea, CropMargins crop = default)
    {
        FrameSize raster = GetRasterSize(useSafeArea);

        List<string> filters = [$"fps={framesPerSecond.ToString("0.####", CultureInfo.InvariantCulture)}"];
        if (!crop.IsNone)
        {
            filters.Add(crop.ToFfmpegFilter());
        }

        // The picture is centred, so that a picture narrower or shorter than the raster gains even bars.
        filters.AddRange(
        [
            $"scale={raster.Width}:{raster.Height}:force_original_aspect_ratio=decrease:force_divisible_by=2:flags=lanczos",
            $"unsharp=luma_msize_x={SharpenKernelSize}:luma_msize_y={SharpenKernelSize}:luma_amount={SharpenAmount.ToString(CultureInfo.InvariantCulture)}",
            $"eq=saturation={Saturation.ToString(CultureInfo.InvariantCulture)}",
            $"pad={CdgFormat.Width}:{CdgFormat.Height}:(ow-iw)/2:(oh-ih)/2:color=black",
            "format=rgb24",
        ]);

        return string.Join(',', filters);
    }
}
