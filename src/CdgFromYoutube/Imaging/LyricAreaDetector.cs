namespace CdgFromYoutube.Imaging;

/// <summary>
/// Finds the part of a karaoke video where the lyrics are, so that the margins around it can be cropped.
/// </summary>
/// <remarks>
/// The lyrics are the part of the picture that keeps changing: every line is drawn, highlighted as it is
/// sung and cleared again, page after page. So each pixel counts the samples in which it changed, and the
/// area is the box around the pixels that changed often. A logo that stays on screen never changes, and a
/// title card changes only for the few seconds it is shown, so neither of them widens the box.
/// </remarks>
public sealed class LyricAreaDetector
{
    /// <summary>The fewest samples a pixel has to change in before it counts as part of the lyrics.</summary>
    private const int MinimumChangeCount = 4;

    /// <summary>The fraction of all samples a pixel has to change in, when that is more than the minimum.</summary>
    private const double MinimumChangeFraction = 0.02;

    /// <summary>How far a channel has to move between two samples before the pixel counts as changed.</summary>
    private const int ChangeThreshold = 48;

    /// <summary>The margin kept around the lyrics, in percent of the frame, so letters do not touch the edge.</summary>
    private const int MarginPercent = 2;

    private readonly int _width;
    private readonly int _height;
    private readonly int[] _changeCounts;
    private readonly byte[] _previous;
    private int _frameCount;

    /// <summary>Creates a detector for frames of the given size.</summary>
    public LyricAreaDetector(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        _width = width;
        _height = height;
        _changeCounts = new int[width * height];
        _previous = new byte[RgbPixelFormat.GetFrameSizeBytes(width, height)];
    }

    /// <summary>The number of frames that have been looked at.</summary>
    public int FrameCount => _frameCount;

    /// <summary>Looks at the next sampled frame of packed eight bit red, green and blue pixels.</summary>
    public void AddFrame(ReadOnlySpan<byte> rgbPixels)
    {
        if (rgbPixels.Length < _previous.Length)
        {
            throw new ArgumentException(
                $"A frame holds {_previous.Length} bytes, but {rgbPixels.Length} were given.",
                nameof(rgbPixels));
        }

        if (_frameCount > 0)
        {
            for (int pixel = 0; pixel < _changeCounts.Length; pixel++)
            {
                int offset = pixel * RgbPixelFormat.BytesPerPixel;
                if (HasChanged(rgbPixels, offset))
                {
                    _changeCounts[pixel]++;
                }
            }
        }

        rgbPixels[.._previous.Length].CopyTo(_previous);
        _frameCount++;
    }

    /// <summary>
    /// Returns the margins that crop the frame down to the lyrics and a small border, or no cropping when
    /// too little changed to tell where the lyrics are.
    /// </summary>
    public CropMargins GetCropMargins()
    {
        int threshold = Math.Max(MinimumChangeCount, (int)Math.Ceiling(_frameCount * MinimumChangeFraction));
        int left = _width;
        int right = -1;
        int top = _height;
        int bottom = -1;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (_changeCounts[(y * _width) + x] < threshold)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < 0)
        {
            return CropMargins.None;
        }

        CropMargins crop = new(
            ToMarginPercent(left, _width),
            ToMarginPercent(top, _height),
            ToMarginPercent(_width - 1 - right, _width),
            ToMarginPercent(_height - 1 - bottom, _height));
        return crop.IsValid ? crop : CropMargins.None;
    }

    private bool HasChanged(ReadOnlySpan<byte> current, int offset)
    {
        for (int channel = 0; channel < RgbPixelFormat.BytesPerPixel; channel++)
        {
            if (Math.Abs(current[offset + channel] - _previous[offset + channel]) > ChangeThreshold)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Turns an unused strip of pixels into a whole percentage, less the margin that is kept.</summary>
    private static int ToMarginPercent(int unusedPixels, int size) =>
        Math.Max(0, (int)Math.Floor(unusedPixels * 100.0 / size) - MarginPercent);
}
