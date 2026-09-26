using CdgFromYoutube.Imaging;

namespace CdgFromYoutube.Cdg;

/// <summary>
/// Counts how often each of the four thousand and ninety six possible four bit colors occurs in a track.
/// </summary>
/// <remarks>
/// The palette builder works from this histogram rather than from the frames themselves, so sampling a
/// whole video costs one integer per color rather than one entry per sampled pixel.
/// </remarks>
public sealed class CdgColorHistogram
{
    /// <summary>The number of possible four bit red, green and blue combinations.</summary>
    public const int BucketCount = 1 << CdgFormat.PackedColorBits;

    private readonly int[] _counts = new int[BucketCount];

    /// <summary>The number of samples that have been counted.</summary>
    public long SampleCount { get; private set; }

    /// <summary>Counts a single color once.</summary>
    public void Add(CdgColor color) => Add(color, 1);

    /// <summary>Counts a color the given number of times.</summary>
    public void Add(CdgColor color, int count)
    {
        _counts[color.Pack()] += count;
        SampleCount += count;
    }

    /// <summary>Counts every pixel of a frame of packed eight bit red, green and blue pixels.</summary>
    public void AddFrame(ReadOnlySpan<byte> rgbPixels)
    {
        for (int offset = 0; offset + RgbPixelFormat.BytesPerPixel <= rgbPixels.Length; offset += RgbPixelFormat.BytesPerPixel)
        {
            Add(CdgColor.FromVideoPixel(
                rgbPixels[offset],
                rgbPixels[offset + 1],
                rgbPixels[offset + 2]));
        }
    }

    /// <summary>Returns every color that was seen at least once, together with how often it was seen.</summary>
    public IEnumerable<Bucket> EnumerateBuckets()
    {
        for (int index = 0; index < BucketCount; index++)
        {
            int count = _counts[index];
            if (count > 0)
            {
                yield return new Bucket(CdgColor.Unpack(index), count);
            }
        }
    }

    /// <summary>One color of the four bit color space and the number of samples it holds.</summary>
    /// <param name="Color">The color of the bucket.</param>
    /// <param name="Count">The number of samples counted for the bucket.</param>
    public readonly record struct Bucket(CdgColor Color, int Count);
}
