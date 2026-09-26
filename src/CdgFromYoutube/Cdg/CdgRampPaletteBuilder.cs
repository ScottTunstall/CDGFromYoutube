namespace CdgFromYoutube.Cdg;

/// <summary>
/// Builds the palette for antialiased lettering: up to five lyric colors, each with the shades between it
/// and the black background.
/// </summary>
/// <remarks>
/// <see cref="CdgPaletteBuilder"/> spends its entries wherever the picture's pixels are, which suits a
/// picture, but a tile can only use two of them, so lettering loses its soft edges. This palette instead
/// finds the colors the lettering is drawn in (see <see cref="CdgLyricColors"/>) and gives each one a
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

    /// <summary>Builds a palette of lyric color ramps from the samples in a histogram.</summary>
    public static CdgPalette Build(CdgColorHistogram histogram)
    {
        ArgumentNullException.ThrowIfNull(histogram);

        IReadOnlyList<CdgLyricColors.LyricColor> lyricColors = CdgLyricColors.Find(histogram, Slots.Count);

        CdgColor[] colors = new CdgColor[CdgFormat.ColorCount];
        List<CdgRamp> ramps = [];
        for (int slot = 0; slot < Slots.Count; slot++)
        {
            // A slot with no lyric color of its own repeats the first one. The tile encoder never uses
            // those entries, because it only tries the ramps it is given, and every one of them has a
            // higher index than the matching entry of the first ramp, which wins any tie.
            bool isUsed = slot < lyricColors.Count;
            CdgColor full = (isUsed ? lyricColors[slot] : lyricColors[0]).Full;
            CdgRamp ramp = Slots[slot];
            colors[ramp.Full] = full;
            colors[ramp.Third] = CdgLyricColors.Scale(full, 1, 3);
            colors[ramp.TwoThirds] = CdgLyricColors.Scale(full, 2, 3);
            if (isUsed)
            {
                ramps.Add(ramp);
            }
        }

        colors[CdgPaletteBuilder.BlackColorIndex] = CdgColor.Black;
        return new CdgPalette(colors, ramps);
    }
}
