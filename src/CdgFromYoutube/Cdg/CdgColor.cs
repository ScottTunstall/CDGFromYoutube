namespace CdgFromYoutube.Cdg;

/// <summary>
/// A color table entry: four bits each of red, green and blue.
/// </summary>
/// <remarks>
/// The sixteen entries travel as twelve significant bits laid out as <c>rrrr gggg bbbb</c> across two
/// bytes, with the top two bits of each byte reserved for the subchannel P and Q channels. See "CD+G
/// Revealed" by Jim Bumgardner (https://jbum.com/cdg_revealed.html); the bit layout is confirmed by VLC's
/// CDG decoder, which reads red from bits thirteen to ten, green from bits nine, eight, five and four, and
/// blue from bits three to zero.
/// </remarks>
public readonly record struct CdgColor(byte Red, byte Green, byte Blue)
{
    /// <summary>The largest value a single channel holds.</summary>
    public const byte MaxChannelValue = (1 << CdgFormat.ColorBitsPerChannel) - 1;

    /// <summary>The step between two adjacent channel levels once they are scaled to eight bits.</summary>
    public const byte EightBitChannelStep = byte.MaxValue / MaxChannelValue;

    /// <summary>The offset that makes the eight bit to four bit conversion round to the nearest level.</summary>
    private const byte EightBitRoundBias = EightBitChannelStep / 2;

    /// <summary>Black, which the palette builder always reserves for the screen and border presets.</summary>
    public static CdgColor Black => new(0, 0, 0);

    /// <summary>The highest saturation, in percent, at which a video pixel is treated as grey.</summary>
    /// <remarks>
    /// Compression tints the edges of white lettering faintly blue, green or yellow, and the saturation
    /// boost in the video filter strengthens those tints. The palette then gains several slightly different
    /// whites, each tile picks whichever is nearest, and white lyrics come out flecked with color. Measured
    /// on a karaoke track, the tints reached about 30%; real lyric colors are far above that (a highlight
    /// blue or orange is near 100%, and even a pale sky blue is about 46%).
    /// </remarks>
    public const int NearGreySaturationPercent = 30;

    /// <summary>Creates a color from eight bit channels, keeping the nearest four bit level of each.</summary>
    public static CdgColor FromEightBit(byte red, byte green, byte blue) =>
        new(ToNibble(red), ToNibble(green), ToNibble(blue));

    /// <summary>
    /// Creates a color from a pixel of video, as <see cref="FromEightBit"/> does, except that a pixel that
    /// is nearly grey becomes exactly grey. See <see cref="NearGreySaturationPercent"/>.
    /// </summary>
    /// <remarks>
    /// This works on the eight bit channels rather than on the rounded four bit color, because rounding
    /// can push a faint tint over the limit: (160, 192, 224) is 29% saturated, but rounds to (9, 11, 13),
    /// which is 31%.
    /// </remarks>
    public static CdgColor FromVideoPixel(byte red, byte green, byte blue)
    {
        if (!IsNearlyGrey(red, green, blue))
        {
            return FromEightBit(red, green, blue);
        }

        byte grey = (byte)((red + green + blue + 1) / 3);
        return FromEightBit(grey, grey, grey);
    }

    /// <summary>
    /// Returns this color as exactly grey when it is nearly grey. See <see cref="NearGreySaturationPercent"/>.
    /// </summary>
    public CdgColor ToGreyIfNearlyGrey()
    {
        if (!IsNearlyGrey(Red, Green, Blue))
        {
            return this;
        }

        byte grey = (byte)((Red + Green + Blue + 1) / 3);
        return new CdgColor(grey, grey, grey);
    }

    private static bool IsNearlyGrey(int red, int green, int blue)
    {
        int highest = Math.Max(red, Math.Max(green, blue));
        int lowest = Math.Min(red, Math.Min(green, blue));
        return (highest - lowest) * 100 <= highest * NearGreySaturationPercent;
    }

    /// <summary>Scales one eight bit channel to the four bit range.</summary>
    public static byte ToNibble(byte eightBitValue) =>
        (byte)((eightBitValue + EightBitRoundBias) / EightBitChannelStep);

    /// <summary>Spreads one four bit channel back over the eight bit range.</summary>
    public static byte ToEightBitChannel(byte fourBitValue) => (byte)(fourBitValue * EightBitChannelStep);

    /// <summary>Packs the color into the twelve bit red, green, blue layout of the color table.</summary>
    public ushort ToColorSpec() =>
        (ushort)((Red << 10) | ((Green & 0x0C) << 6) | ((Green & 0x03) << 4) | Blue);

    /// <summary>Packs the color into one integer, which the histogram uses as a bucket index.</summary>
    public int Pack() =>
        (Red << (2 * CdgFormat.ColorBitsPerChannel)) |
        (Green << CdgFormat.ColorBitsPerChannel) |
        Blue;

    /// <summary>Expands a value produced by <see cref="Pack"/>.</summary>
    public static CdgColor Unpack(int packed) => new(
        (byte)((packed >> (2 * CdgFormat.ColorBitsPerChannel)) & MaxChannelValue),
        (byte)((packed >> CdgFormat.ColorBitsPerChannel) & MaxChannelValue),
        (byte)(packed & MaxChannelValue));

    /// <summary>Returns the squared distance to another color in the four bit channel space.</summary>
    public int DistanceSquared(CdgColor other)
    {
        int redDelta = Red - other.Red;
        int greenDelta = Green - other.Green;
        int blueDelta = Blue - other.Blue;
        return (redDelta * redDelta) + (greenDelta * greenDelta) + (blueDelta * blueDelta);
    }
}
