using CdgFromYoutube.Cdg;

namespace CdgFromYoutube.Tests;

public sealed class CdgColorTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(15, 15, 15)]
    [InlineData(10, 12, 3)]
    [InlineData(7, 1, 14)]
    [InlineData(1, 0, 15)]
    public void ToColorSpecMatchesTheLayoutThatPlayersDecode(int red, int green, int blue)
    {
        ushort colorSpec = new CdgColor((byte)red, (byte)green, (byte)blue).ToColorSpec();

        // A player reads red from bits thirteen to ten, green from bits nine, eight, five and four, and
        // blue from bits three to zero.
        Assert.Equal(red, (colorSpec >> 10) & 0x0F);
        Assert.Equal(green, ((colorSpec >> 6) & 0x0C) | ((colorSpec >> 4) & 0x03));
        Assert.Equal(blue, colorSpec & 0x0F);
    }

    [Fact]
    public void ToColorSpecLeavesTheUpperBitsOfBothBytesClear()
    {
        ushort colorSpec = new CdgColor(15, 15, 15).ToColorSpec();

        Assert.Equal((byte)0x3F, (byte)(colorSpec >> 8));
        Assert.Equal((byte)0x3F, (byte)(colorSpec & 0xFF));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(255, 15)]
    [InlineData(128, 8)]
    [InlineData(17, 1)]
    [InlineData(246, 14)]
    [InlineData(247, 15)]
    public void ToNibbleRoundsToTheNearestLevel(byte eightBit, byte expectedNibble) =>
        Assert.Equal(expectedNibble, CdgColor.ToNibble(eightBit));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(15)]
    public void ToEightBitChannelSpreadsTheLevelOverTheRange(byte nibble) =>
        Assert.Equal((byte)(nibble * 17), CdgColor.ToEightBitChannel(nibble));

    [Fact]
    public void PackedColorsRoundTrip()
    {
        CdgColor color = new(3, 9, 15);

        Assert.Equal(color, CdgColor.Unpack(color.Pack()));
    }
}
