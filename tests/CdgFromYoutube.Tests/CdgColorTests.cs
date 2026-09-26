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

    [Theory]
    [InlineData(160, 192, 224)] // the pale blue that flecked white lyrics
    [InlineData(192, 176, 160)] // the beige that flecked white lyrics
    [InlineData(221, 238, 221)] // a white with a green cast
    [InlineData(128, 128, 128)]
    public void FromVideoPixelTurnsANearlyGreyPixelGrey(byte red, byte green, byte blue)
    {
        CdgColor color = CdgColor.FromVideoPixel(red, green, blue);

        Assert.Equal(color.Red, color.Green);
        Assert.Equal(color.Green, color.Blue);
    }

    [Theory]
    [InlineData(0, 100, 255)]   // a highlight blue
    [InlineData(255, 128, 0)]   // orange
    [InlineData(135, 206, 250)] // a pale sky blue
    public void FromVideoPixelKeepsARealColor(byte red, byte green, byte blue) =>
        Assert.Equal(CdgColor.FromEightBit(red, green, blue), CdgColor.FromVideoPixel(red, green, blue));

    [Fact]
    public void FromVideoPixelKeepsTheBrightnessOfANearlyGreyPixel() =>
        Assert.Equal(new CdgColor(12, 12, 12), CdgColor.FromVideoPixel(192, 208, 224));

    [Fact]
    public void ToGreyIfNearlyGreyTurnsATintedWhiteGrey() =>
        Assert.Equal(new CdgColor(12, 12, 12), new CdgColor(11, 12, 13).ToGreyIfNearlyGrey());

    [Fact]
    public void ToGreyIfNearlyGreyKeepsARealColor() =>
        Assert.Equal(new CdgColor(0, 6, 15), new CdgColor(0, 6, 15).ToGreyIfNearlyGrey());

    [Fact]
    public void PackedColorsRoundTrip()
    {
        CdgColor color = new(3, 9, 15);

        Assert.Equal(color, CdgColor.Unpack(color.Pack()));
    }
}
