namespace CdgFromYoutube.Cdg;

/// <summary>
/// Builds the palette for flat lettering: black, and one entry for each color the lyrics are drawn in,
/// with no shades between them.
/// </summary>
/// <remarks>
/// <see cref="CdgPaletteBuilder"/> spends its entries wherever the picture's pixels are, and the edges of
/// lettering are a large share of those, so a dark red lyric gets three or four reds of its own. A tile can
/// only use two entries, so neighbouring tiles pick different reds, and the edge pixels that fall nearest
/// black leave holes; the lettering comes out blotchy. Here each lyric color has exactly one entry, and
/// every pixel is decided as either that color or the background, so a letter is drawn in one solid color.
/// It suits lyrics over a plain dark background; it does not try to keep the colors of a picture.
/// </remarks>
public static class CdgFlatPaletteBuilder
{
    /// <summary>
    /// The share of a lyric color's full brightness, in percent, that a pixel of that color has to reach to
    /// be drawn as lettering rather than as the background.
    /// </summary>
    /// <remarks>
    /// Scaling down turns each edge pixel into a mix of lettering and background in proportion to how much
    /// of it the letter covered, so half brightness is half covered. Drawing every pixel that is at least
    /// half covered keeps strokes their true thickness, where a higher cut would thin them.
    /// </remarks>
    public const int LetteringThresholdPercent = 50;

    /// <summary>Builds a palette of flat lyric colors from the samples in a histogram.</summary>
    public static CdgPalette Build(CdgColorHistogram histogram)
    {
        ArgumentNullException.ThrowIfNull(histogram);

        IReadOnlyList<CdgLyricColors.LyricColor> lyricColors =
            CdgLyricColors.Find(histogram, CdgFormat.ColorCount - 1);

        // Unused entries are black too. The color map never points at them, so they are never drawn.
        CdgColor[] colors = new CdgColor[CdgFormat.ColorCount];
        for (int index = 0; index < lyricColors.Count; index++)
        {
            colors[index + 1] = lyricColors[index].Full;
        }

        byte[] colorMap = new byte[CdgColorHistogram.BucketCount];
        for (int packed = 0; packed < colorMap.Length; packed++)
        {
            colorMap[packed] = MapColor(CdgColor.Unpack(packed), lyricColors);
        }

        return CdgPalette.WithColorMap(colors, colorMap);
    }

    /// <summary>
    /// Returns the entry a color is drawn as: the lyric color whose hue it is nearest to, when it is bright
    /// enough to be lettering of that color, and the background otherwise.
    /// </summary>
    /// <remarks>
    /// The hue is decided first, because brightness alone would be judged against the wrong color: the grey
    /// edge of a white letter, (7, 7, 7), is nearer a dark red (11, 1, 1) than it is to white or to black,
    /// so drawing it as the nearest entry would ring white lettering with red.
    /// </remarks>
    private static byte MapColor(CdgColor color, IReadOnlyList<CdgLyricColors.LyricColor> lyricColors)
    {
        if (CdgPaletteBuilder.IsBackground(color))
        {
            return CdgPaletteBuilder.BlackColorIndex;
        }

        int nearest = CdgLyricColors.FindNearest(color, lyricColors);
        int fullBrightness = CdgLyricColors.GetBrightness(lyricColors[nearest].Full);
        bool isLettering = CdgLyricColors.GetBrightness(color) * 100 >= fullBrightness * LetteringThresholdPercent;
        return isLettering ? (byte)(nearest + 1) : (byte)CdgPaletteBuilder.BlackColorIndex;
    }
}
