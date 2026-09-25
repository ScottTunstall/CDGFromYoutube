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

    /// <summary>Creates a color from eight bit channels, keeping the nearest four bit level of each.</summary>
    public static CdgColor FromEightBit(byte red, byte green, byte blue) =>
        new(ToNibble(red), ToNibble(green), ToNibble(blue));

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
