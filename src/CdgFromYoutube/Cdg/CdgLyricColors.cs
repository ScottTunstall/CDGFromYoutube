namespace CdgFromYoutube.Cdg;

/// <summary>
/// Finds the colors that lettering is drawn in, whatever the brightness of each pixel, for palettes that
/// draw lyrics over a plain dark background.
/// </summary>
/// <remarks>
/// A letter and its dimmer edges are one color at different brightnesses, so colors are compared by hue:
/// each is scaled up to full brightness first. That keeps the edges of a red letter with the red, rather
/// than letting them become a color of their own.
/// </remarks>
public static class CdgLyricColors
{
    /// <summary>
    /// How far apart two lyric colors must be, as a squared distance once both are at full brightness,
    /// to count as different colors. Closer ones are treated as one color.
    /// </summary>
    /// <remarks>
    /// Lettering is often outlined in a deeper shade of its own color. Counted as a color of its own, the
    /// outline makes neighbouring tiles pick different colors and the lettering comes out blotchy. On a
    /// karaoke track, a light blue highlight (5, 8, 15) and its deep blue outline (0, 0, 15) are 89 apart,
    /// while white and the light blue are 149 apart, so this sits between the two.
    /// </remarks>
    private const int MinimumHueDistance = 100;

    /// <summary>The share of the lettering, in percent, that a color has to cover to count.</summary>
    private const int MinimumHueSharePercent = 1;

    /// <summary>The share of a color's pixels, in percent, that are brighter than what counts as its full color.</summary>
    /// <remarks>
    /// Most pixels of lettering this small are edge pixels, so an average would come out too dim. The
    /// very brightest are few and may be noise, so the level near the top is taken instead.
    /// </remarks>
    private const int BrightestSharePercent = 10;

    private static readonly CdgColor White = new(CdgColor.MaxChannelValue, CdgColor.MaxChannelValue, CdgColor.MaxChannelValue);

    /// <summary>One color that lettering is drawn in.</summary>
    /// <param name="Hue">The color at full brightness, which pixels are compared against to find their color.</param>
    /// <param name="Full">The color at the brightness the lettering actually reaches.</param>
    public readonly record struct LyricColor(CdgColor Hue, CdgColor Full);

    /// <summary>Returns the colors the lettering in a histogram is drawn in, most common first.</summary>
    /// <param name="histogram">The colors seen while sampling the track.</param>
    /// <param name="maximumCount">The most colors to return. White is returned when nothing else is found.</param>
    public static IReadOnlyList<LyricColor> Find(CdgColorHistogram histogram, int maximumCount)
    {
        ArgumentNullException.ThrowIfNull(histogram);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumCount, 1);

        CdgColorHistogram.Bucket[] lettering =
            [.. histogram.EnumerateBuckets().Where(bucket => !CdgPaletteBuilder.IsBackground(bucket.Color))];
        List<CdgColor> hues = FindHues(lettering, maximumCount);
        return [.. hues.Select(hue => new LyricColor(hue, GetFullColor(hue, hues, lettering)))];
    }

    /// <summary>Returns the index of the lyric color whose hue is nearest to a color's.</summary>
    public static int FindNearest(CdgColor color, IReadOnlyList<LyricColor> lyricColors)
    {
        ArgumentNullException.ThrowIfNull(lyricColors);

        CdgColor hue = GetHue(color);
        int nearest = 0;
        for (int index = 1; index < lyricColors.Count; index++)
        {
            if (lyricColors[index].Hue.DistanceSquared(hue) < lyricColors[nearest].Hue.DistanceSquared(hue))
            {
                nearest = index;
            }
        }

        return nearest;
    }

    /// <summary>The brightness of a color: the level of its brightest channel.</summary>
    public static int GetBrightness(CdgColor color) => Math.Max(color.Red, Math.Max(color.Green, color.Blue));

    /// <summary>Scales every channel of a color by a fraction, rounding to the nearest level.</summary>
    public static CdgColor Scale(CdgColor color, int numerator, int denominator) => new(
        ScaleChannel(color.Red, numerator, denominator),
        ScaleChannel(color.Green, numerator, denominator),
        ScaleChannel(color.Blue, numerator, denominator));

    /// <summary>Returns the colors the lettering is drawn in, most common first, each at full brightness.</summary>
    private static List<CdgColor> FindHues(CdgColorHistogram.Bucket[] lettering, int maximumCount)
    {
        CdgColorHistogram hueHistogram = new();
        foreach (CdgColorHistogram.Bucket bucket in lettering)
        {
            hueHistogram.Add(GetHue(bucket.Color), bucket.Count);
        }

        long minimumCount = hueHistogram.SampleCount * MinimumHueSharePercent / 100;
        List<CdgColor> hues = [];
        foreach (CdgColorHistogram.Bucket hue in hueHistogram.EnumerateBuckets().OrderByDescending(bucket => bucket.Count))
        {
            if (hues.Count == maximumCount || hue.Count < minimumCount)
            {
                break;
            }

            if (hues.TrueForAll(chosen => chosen.DistanceSquared(hue.Color) >= MinimumHueDistance))
            {
                hues.Add(hue.Color);
            }
        }

        if (hues.Count == 0)
        {
            hues.Add(White);
        }

        return hues;
    }

    /// <summary>Returns a hue at the brightness the lettering drawn in it actually reaches.</summary>
    private static CdgColor GetFullColor(CdgColor hue, List<CdgColor> hues, CdgColorHistogram.Bucket[] lettering)
    {
        CdgColorHistogram.Bucket[] pixelsOfHue =
        [
            .. lettering
                .Where(bucket => FindNearestHue(bucket.Color, hues) == hue)
                .OrderByDescending(bucket => GetBrightness(bucket.Color)),
        ];

        long total = pixelsOfHue.Sum(bucket => (long)bucket.Count);
        long brightest = total * BrightestSharePercent / 100;
        long seen = 0;
        int level = CdgColor.MaxChannelValue;
        foreach (CdgColorHistogram.Bucket bucket in pixelsOfHue)
        {
            level = GetBrightness(bucket.Color);
            seen += bucket.Count;
            if (seen > brightest)
            {
                break;
            }
        }

        return Scale(hue, level, CdgColor.MaxChannelValue);
    }

    private static CdgColor FindNearestHue(CdgColor color, List<CdgColor> hues)
    {
        CdgColor hue = GetHue(color);
        return hues.MinBy(candidate => candidate.DistanceSquared(hue));
    }

    /// <summary>
    /// Returns a color at full brightness, so that a letter and its dimmer edges come out as the same hue.
    /// A color that is nearly grey becomes grey first, because the rounding of dim pixels would otherwise
    /// scale up into a strong tint.
    /// </summary>
    private static CdgColor GetHue(CdgColor color)
    {
        CdgColor grey = color.ToGreyIfNearlyGrey();
        return Scale(grey, CdgColor.MaxChannelValue, GetBrightness(grey));
    }

    private static byte ScaleChannel(byte value, int numerator, int denominator) =>
        (byte)Math.Round((double)value * numerator / denominator, MidpointRounding.AwayFromZero);
}
