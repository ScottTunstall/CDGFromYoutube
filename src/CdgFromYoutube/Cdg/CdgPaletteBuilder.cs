using CdgFromYoutube.Imaging;

namespace CdgFromYoutube.Cdg;

/// <summary>
/// Chooses the sixteen colors of a track from a histogram of its frames, using median cut.
/// </summary>
/// <remarks>
/// The colors that occur in the track are repeatedly split into two groups of roughly equal sample counts
/// along their widest channel, and each resulting group contributes its average color. Unlike taking the
/// most common colors, this keeps enough entries for the colors that are rare but visually important.
/// </remarks>
public static class CdgPaletteBuilder
{
    /// <summary>The index of the black entry, which the presets that clear the screen refer to.</summary>
    public const int BlackColorIndex = 0;

    /// <summary>
    /// The brightest four bit level every channel of a color may reach and still count as the background.
    /// </summary>
    /// <remarks>
    /// Karaoke players such as KaraFun show their own backdrop wherever the screen holds color zero, so
    /// the background has to be color zero exactly. A near black that has an entry of its own is drawn as
    /// an opaque dark block, which is what put black squares behind the lyrics. Karaoke videos often lay
    /// the lyrics over a dim texture that reaches level four or five, so everything up to this level is
    /// folded into black rather than given entries of its own.
    /// </remarks>
    public const int BackgroundLevel = 4;

    /// <summary>Returns whether a color is dark enough to be drawn as the background, color zero.</summary>
    public static bool IsBackground(CdgColor color) =>
        color.Red <= BackgroundLevel && color.Green <= BackgroundLevel && color.Blue <= BackgroundLevel;

    /// <summary>Builds a palette of <see cref="CdgFormat.ColorCount"/> colors from the samples in a histogram.</summary>
    /// <param name="histogram">The colors seen while sampling the track.</param>
    /// <param name="reservedColors">Colors that must appear first, in the given order.</param>
    public static CdgPalette Build(CdgColorHistogram histogram, IReadOnlyList<CdgColor> reservedColors)
    {
        ArgumentNullException.ThrowIfNull(histogram);
        ArgumentNullException.ThrowIfNull(reservedColors);
        if (reservedColors.Count > CdgFormat.ColorCount)
        {
            throw new ArgumentException(
                $"A CD+G palette holds {CdgFormat.ColorCount} colours, so fewer can be reserved.",
                nameof(reservedColors));
        }

        // When black is reserved it stands for the whole background, so the dark colors it covers do not
        // take entries of their own.
        bool blackIsReserved = reservedColors.Contains(CdgColor.Black);
        CdgColorHistogram.Bucket[] buckets = [.. histogram
            .EnumerateBuckets()
            .Where(bucket => !blackIsReserved || !IsBackground(bucket.Color))];
        List<ColorBox> boxes = SplitIntoBoxes(buckets, CdgFormat.ColorCount - reservedColors.Count);

        // A box can hold grey lettering together with a few colored pixels, and its average is then a tinted
        // white that the lettering gets drawn with. Snapping such an average back to grey keeps white white.
        List<CdgColor> colors = [.. reservedColors];
        colors.AddRange(boxes
            .OrderByDescending(box => box.Weight)
            .Select(box => GetAverageColor(buckets, box).ToGreyIfNearlyGrey()));

        while (colors.Count < CdgFormat.ColorCount)
        {
            colors.Add(colors.Count == 0 ? CdgColor.Black : colors[^1]);
        }

        return new CdgPalette(colors);
    }

    /// <summary>The channel a group of colors is widest in, which is the channel it gets split along.</summary>
    private enum ColorChannel
    {
        /// <summary>The red channel.</summary>
        Red,

        /// <summary>The green channel.</summary>
        Green,

        /// <summary>The blue channel.</summary>
        Blue,
    }

    private static readonly IComparer<CdgColorHistogram.Bucket> RedComparer =
        Comparer<CdgColorHistogram.Bucket>.Create((left, right) => left.Color.Red.CompareTo(right.Color.Red));

    private static readonly IComparer<CdgColorHistogram.Bucket> GreenComparer =
        Comparer<CdgColorHistogram.Bucket>.Create((left, right) => left.Color.Green.CompareTo(right.Color.Green));

    private static readonly IComparer<CdgColorHistogram.Bucket> BlueComparer =
        Comparer<CdgColorHistogram.Bucket>.Create((left, right) => left.Color.Blue.CompareTo(right.Color.Blue));

    /// <summary>A run of histogram buckets that will become one palette entry.</summary>
    private readonly record struct ColorBox(int Start, int Length, int Weight, ColorChannel WidestChannel)
    {
        /// <summary>The index just past the last bucket of the box.</summary>
        public int End => Start + Length;
    }

    private static List<ColorBox> SplitIntoBoxes(CdgColorHistogram.Bucket[] buckets, int boxCount)
    {
        List<ColorBox> boxes = [];
        if (buckets.Length == 0 || boxCount <= 0)
        {
            return boxes;
        }

        boxes.Add(CreateBox(buckets, 0, buckets.Length));
        while (boxes.Count < boxCount)
        {
            int target = FindBoxToSplit(boxes);
            if (target < 0)
            {
                break;
            }

            (ColorBox lower, ColorBox upper) = Split(buckets, boxes[target]);
            boxes[target] = lower;
            boxes.Add(upper);
        }

        return boxes;
    }

    /// <summary>Returns the index of the heaviest box that still holds enough colors to split, or -1.</summary>
    /// <remarks>
    /// Splitting the box that holds the most pixels puts the entries where the pixels are, which is what
    /// keeps the colors the picture is actually made of close to the originals.
    /// </remarks>
    private static int FindBoxToSplit(List<ColorBox> boxes)
    {
        int target = -1;
        for (int index = 0; index < boxes.Count; index++)
        {
            if (boxes[index].Length < 2)
            {
                continue;
            }

            if (target < 0 || boxes[index].Weight > boxes[target].Weight)
            {
                target = index;
            }
        }

        return target;
    }

    private static (ColorBox Lower, ColorBox Upper) Split(CdgColorHistogram.Bucket[] buckets, ColorBox box)
    {
        Array.Sort(buckets, box.Start, box.Length, GetComparer(box.WidestChannel));

        int halfWeight = box.Weight / 2;
        int split = 1;
        int accumulated = 0;
        for (int offset = 0; offset < box.Length; offset++)
        {
            accumulated += buckets[box.Start + offset].Count;
            if (accumulated >= halfWeight)
            {
                split = Math.Clamp(offset + 1, 1, box.Length - 1);
                break;
            }
        }

        return (CreateBox(buckets, box.Start, split), CreateBox(buckets, box.Start + split, box.Length - split));
    }

    private static ColorBox CreateBox(CdgColorHistogram.Bucket[] buckets, int start, int length)
    {
        CdgColor lowest = new(byte.MaxValue, byte.MaxValue, byte.MaxValue);
        CdgColor highest = new(byte.MinValue, byte.MinValue, byte.MinValue);
        int weight = 0;

        for (int index = start; index < start + length; index++)
        {
            CdgColor color = buckets[index].Color;
            weight += buckets[index].Count;
            lowest = new CdgColor(
                Math.Min(lowest.Red, color.Red),
                Math.Min(lowest.Green, color.Green),
                Math.Min(lowest.Blue, color.Blue));
            highest = new CdgColor(
                Math.Max(highest.Red, color.Red),
                Math.Max(highest.Green, color.Green),
                Math.Max(highest.Blue, color.Blue));
        }

        int redRange = highest.Red - lowest.Red;
        int greenRange = highest.Green - lowest.Green;
        int blueRange = highest.Blue - lowest.Blue;
        ColorChannel widest = redRange >= greenRange && redRange >= blueRange
            ? ColorChannel.Red
            : greenRange >= blueRange
                ? ColorChannel.Green
                : ColorChannel.Blue;

        return new ColorBox(start, length, weight, widest);
    }

    private static CdgColor GetAverageColor(CdgColorHistogram.Bucket[] buckets, ColorBox box)
    {
        long red = 0;
        long green = 0;
        long blue = 0;
        long weight = 0;

        for (int index = box.Start; index < box.End; index++)
        {
            CdgColorHistogram.Bucket bucket = buckets[index];
            red += (long)bucket.Color.Red * bucket.Count;
            green += (long)bucket.Color.Green * bucket.Count;
            blue += (long)bucket.Color.Blue * bucket.Count;
            weight += bucket.Count;
        }

        if (weight == 0)
        {
            return CdgColor.Black;
        }

        return new CdgColor(
            (byte)Math.Round((double)red / weight),
            (byte)Math.Round((double)green / weight),
            (byte)Math.Round((double)blue / weight));
    }

    private static IComparer<CdgColorHistogram.Bucket> GetComparer(ColorChannel channel) => channel switch
    {
        ColorChannel.Red => RedComparer,
        ColorChannel.Green => GreenComparer,
        _ => BlueComparer,
    };
}
