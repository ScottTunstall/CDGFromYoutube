namespace CdgFromYoutube.Cdg;

/// <summary>
/// Builds the palette for antialiased lettering: up to five lyric colors, each with the shades between it
/// and the black background.
/// </summary>
/// <remarks>
/// <see cref="CdgPaletteBuilder"/> spends its entries wherever the picture's pixels are, which suits a
/// picture, but a tile can only use two of them, so lettering loses its soft edges. This palette instead
/// finds the colors the lettering is drawn in, whatever the brightness of each pixel, and gives each one a
/// <see cref="CdgRamp"/>. It suits lyrics over a plain dark background; it does not try to keep the colors
/// of a picture behind the lyrics.
/// </remarks>
public static class CdgRampPaletteBuilder
{
    /// <summary>The five ramps that the fifteen entries after black divide into.</summary>
    /// <remarks>
    /// In each, the third entry is the exclusive-or of the first two: 1 ^ 2 = 3, 4 ^ 8 = 12, 5 ^ 10 = 15,
    /// 6 ^ 11 = 13 and 7 ^ 9 = 14. Together they use every index from 1 to 15 exactly once.
    /// </remarks>
    public static readonly IReadOnlyList<CdgRamp> Slots =
        [new(1, 2), new(4, 8), new(5, 10), new(6, 11), new(7, 9)];

    /// <summary>
    /// How far apart two lyric colors must be, as a squared distance once both are at full brightness,
    /// to be given ramps of their own. Closer ones are treated as one color.
    /// </summary>
    /// <remarks>
    /// Lettering is often outlined in a deeper shade of its own color. Given a ramp of its own, the outline
    /// makes neighbouring tiles pick different ramps and the lettering comes out blotchy. On a karaoke
    /// track, a light blue highlight (5, 8, 15) and its deep blue outline (0, 0, 15) are 89 apart, while
    /// white and the light blue are 149 apart, so this sits between the two.
    /// </remarks>
    private const int MinimumHueDistance = 100;

    /// <summary>The share of the lettering, in percent, that a color has to cover to be given a ramp.</summary>
    private const int MinimumHueSharePercent = 1;

    /// <summary>The share of a color's pixels, in percent, that are brighter than what counts as its full color.</summary>
    /// <remarks>
    /// Most pixels of lettering this small are edge pixels, so an average would come out too dim. The
    /// very brightest are few and may be noise, so the level near the top is taken instead.
    /// </remarks>
    private const int BrightestSharePercent = 10;

    private static readonly CdgColor White = new(CdgColor.MaxChannelValue, CdgColor.MaxChannelValue, CdgColor.MaxChannelValue);

    /// <summary>Builds a palette of lyric color ramps from the samples in a histogram.</summary>
    public static CdgPalette Build(CdgColorHistogram histogram)
    {
        ArgumentNullException.ThrowIfNull(histogram);

        CdgColorHistogram.Bucket[] lettering =
            [.. histogram.EnumerateBuckets().Where(bucket => !CdgPaletteBuilder.IsBackground(bucket.Color))];
        List<CdgColor> hues = FindHues(lettering);

        CdgColor[] colors = new CdgColor[CdgFormat.ColorCount];
        List<CdgRamp> ramps = [];
        for (int slot = 0; slot < Slots.Count; slot++)
        {
            // A slot with no lyric color of its own repeats the first one. The tile encoder never uses
            // those entries, because it only tries the ramps it is given, and every one of them has a
            // higher index than the matching entry of the first ramp, which wins any tie.
            bool isUsed = slot < hues.Count;
            CdgColor full = GetFullColor(isUsed ? hues[slot] : hues[0], hues, lettering);
            CdgRamp ramp = Slots[slot];
            colors[ramp.Full] = full;
            colors[ramp.Third] = Scale(full, 1, 3);
            colors[ramp.TwoThirds] = Scale(full, 2, 3);
            if (isUsed)
            {
                ramps.Add(ramp);
            }
        }

        colors[CdgPaletteBuilder.BlackColorIndex] = CdgColor.Black;
        return new CdgPalette(colors, ramps);
    }

    /// <summary>
    /// Returns the colors the lettering is drawn in, most common first, each at full brightness.
    /// </summary>
    private static List<CdgColor> FindHues(CdgColorHistogram.Bucket[] lettering)
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
            if (hues.Count == Slots.Count || hue.Count < minimumCount)
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

    private static int GetBrightness(CdgColor color) => Math.Max(color.Red, Math.Max(color.Green, color.Blue));

    private static CdgColor Scale(CdgColor color, int numerator, int denominator) => new(
        ScaleChannel(color.Red, numerator, denominator),
        ScaleChannel(color.Green, numerator, denominator),
        ScaleChannel(color.Blue, numerator, denominator));

    private static byte ScaleChannel(byte value, int numerator, int denominator) =>
        (byte)Math.Round((double)value * numerator / denominator, MidpointRounding.AwayFromZero);
}
