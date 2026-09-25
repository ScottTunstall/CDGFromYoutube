using CdgFromYoutube.Cdg;

namespace CdgFromYoutube.Tests;

public sealed class CdgPaletteBuilderTests
{
    [Fact]
    public void ThePaletteAlwaysHoldsTheFullColorCountAndStartsWithTheReservedColor()
    {
        CdgColorHistogram histogram = new();
        histogram.Add(new CdgColor(15, 0, 0));

        CdgPalette palette = CdgPaletteBuilder.Build(histogram, [CdgColor.Black]);

        Assert.Equal(CdgFormat.ColorCount, palette.Colors.Length);
        Assert.Equal(CdgColor.Black, palette[CdgPaletteBuilder.BlackColorIndex]);
    }

    [Fact]
    public void DarkColorsAreLeftToTheReservedBlackRatherThanGivenEntries()
    {
        // A dim texture behind the lyrics: if it had entries of its own, players would draw it as opaque
        // dark blocks instead of showing their backdrop through color zero.
        CdgColorHistogram histogram = new();
        for (int sample = 0; sample < 1000; sample++)
        {
            histogram.Add(new CdgColor((byte)(1 + (sample % CdgPaletteBuilder.BackgroundLevel)), 0, 0));
        }

        histogram.Add(new CdgColor(15, 15, 15));

        CdgPalette palette = CdgPaletteBuilder.Build(histogram, [CdgColor.Black]);

        for (int index = 1; index < CdgFormat.ColorCount; index++)
        {
            Assert.False(CdgPaletteBuilder.IsBackground(palette[index]));
        }
    }

    [Fact]
    public void AnEmptyHistogramStillProducesAPalette()
    {
        CdgPalette palette = CdgPaletteBuilder.Build(new CdgColorHistogram(), [CdgColor.Black]);

        Assert.Equal(CdgFormat.ColorCount, palette.Colors.Length);
    }

    [Fact]
    public void ColorsThatDominateTheSamplesSurviveTheReduction()
    {
        CdgColor red = new(15, 0, 0);
        CdgColor blue = new(0, 0, 15);
        CdgColorHistogram histogram = new();
        for (int sample = 0; sample < 900; sample++)
        {
            histogram.Add(red);
        }

        for (int sample = 0; sample < 100; sample++)
        {
            histogram.Add(blue);
        }

        CdgPalette palette = CdgPaletteBuilder.Build(histogram, [CdgColor.Black]);

        Assert.Equal(red, palette[palette.FindNearestIndex(red)]);
        Assert.Equal(blue, palette[palette.FindNearestIndex(blue)]);
    }

    [Fact]
    public void MoreReservedColorsThanTheTableHoldsAreRejected()
    {
        CdgColor[] reserved = [.. Enumerable.Repeat(CdgColor.Black, CdgFormat.ColorCount + 1)];

        Assert.Throws<ArgumentException>(
            () => CdgPaletteBuilder.Build(new CdgColorHistogram(), reserved));
    }
}
