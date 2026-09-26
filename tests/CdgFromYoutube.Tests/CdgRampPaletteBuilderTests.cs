using CdgFromYoutube.Cdg;

namespace CdgFromYoutube.Tests;

public sealed class CdgRampPaletteBuilderTests
{
    private static readonly CdgColor White = new(15, 15, 15);

    [Fact]
    public void TheSlotsUseEveryEntryAfterBlackExactlyOnce()
    {
        int[] indices = [.. CdgRampPaletteBuilder.Slots.SelectMany(ramp => new int[] { ramp.Full, ramp.Third, ramp.TwoThirds })];

        Assert.Equal(Enumerable.Range(1, CdgFormat.ColorCount - 1), indices.Order());
    }

    [Fact]
    public void WhiteLetteringGetsARampOfGreys()
    {
        CdgColorHistogram histogram = new();
        histogram.Add(White, 100);
        histogram.Add(new CdgColor(10, 10, 10), 50);
        histogram.Add(new CdgColor(5, 5, 5), 50);

        CdgPalette palette = CdgRampPaletteBuilder.Build(histogram);

        CdgRamp ramp = Assert.Single(palette.Ramps);
        Assert.Equal(CdgColor.Black, palette[CdgPaletteBuilder.BlackColorIndex]);
        Assert.Equal(White, palette[ramp.Full]);
        Assert.Equal(new CdgColor(10, 10, 10), palette[ramp.TwoThirds]);
        Assert.Equal(new CdgColor(5, 5, 5), palette[ramp.Third]);
    }

    [Fact]
    public void DimEdgesOfALetterCountAsTheSameColorAsTheLetter()
    {
        // The edges of blue lettering are darker blues. They must not be given a ramp of their own.
        CdgColorHistogram histogram = new();
        histogram.Add(White, 200);
        histogram.Add(new CdgColor(0, 6, 15), 60);
        histogram.Add(new CdgColor(0, 4, 10), 30);
        histogram.Add(new CdgColor(0, 2, 5), 30);

        CdgPalette palette = CdgRampPaletteBuilder.Build(histogram);

        Assert.Equal(2, palette.Ramps.Count);
        Assert.Equal(White, palette[palette.Ramps[0].Full]);
        Assert.Equal(new CdgColor(0, 6, 15), palette[palette.Ramps[1].Full]);
    }

    [Fact]
    public void AnOutlineInADeeperShadeOfTheLetteringDoesNotGetARampOfItsOwn()
    {
        CdgColorHistogram histogram = new();
        histogram.Add(White, 200);
        histogram.Add(new CdgColor(5, 8, 15), 100);
        histogram.Add(new CdgColor(0, 0, 11), 50);

        CdgPalette palette = CdgRampPaletteBuilder.Build(histogram);

        Assert.Equal(2, palette.Ramps.Count);
    }

    [Fact]
    public void AFewOverBrightPixelsDoNotSetTheFullColor()
    {
        CdgColorHistogram histogram = new();
        histogram.Add(new CdgColor(13, 13, 13), 95);
        histogram.Add(White, 5);

        CdgPalette palette = CdgRampPaletteBuilder.Build(histogram);

        Assert.Equal(new CdgColor(13, 13, 13), palette[palette.Ramps[0].Full]);
    }

    [Fact]
    public void AnEmptyHistogramStillProducesAWhiteRamp()
    {
        CdgPalette palette = CdgRampPaletteBuilder.Build(new CdgColorHistogram());

        Assert.Equal(White, palette[Assert.Single(palette.Ramps).Full]);
    }
}
