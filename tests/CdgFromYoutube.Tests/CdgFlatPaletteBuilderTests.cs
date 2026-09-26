using CdgFromYoutube.Cdg;

namespace CdgFromYoutube.Tests;

public sealed class CdgFlatPaletteBuilderTests
{
    private static readonly CdgColor White = new(15, 15, 15);
    private static readonly CdgColor DarkRed = new(11, 1, 1);

    [Fact]
    public void EachLyricColorGetsExactlyOneEntry()
    {
        CdgPalette palette = Build();

        Assert.True(palette.HasColorMap);
        Assert.Equal(CdgColor.Black, palette[CdgPaletteBuilder.BlackColorIndex]);
        Assert.Equal([White, DarkRed], palette.Colors[1..3].ToArray());
        Assert.All(palette.Colors[3..].ToArray(), color => Assert.Equal(CdgColor.Black, color));
    }

    [Fact]
    public void TheDimEdgesOfALetterAreDrawnInItsFullColor()
    {
        CdgPalette palette = Build();

        Assert.Equal(DarkRed, palette[palette.MapColor(new CdgColor(8, 1, 1))]);
        Assert.Equal(DarkRed, palette[palette.MapColor(new CdgColor(6, 0, 0))]);
        Assert.Equal(White, palette[palette.MapColor(new CdgColor(9, 9, 9))]);
    }

    [Fact]
    public void AnEdgeLessThanHalfAsBrightAsItsLetterIsBackground()
    {
        CdgPalette palette = Build();

        Assert.Equal(CdgPaletteBuilder.BlackColorIndex, palette.MapColor(new CdgColor(5, 1, 1)));
        Assert.Equal(CdgPaletteBuilder.BlackColorIndex, palette.MapColor(new CdgColor(7, 7, 7)));
    }

    [Fact]
    public void TheGreyEdgesOfWhiteLetteringAreNeverDrawnInAnotherLyricColor()
    {
        // (7, 7, 7) is nearer the dark red than it is to white or to black, so the nearest entry would be red.
        CdgPalette palette = Build();

        foreach (byte level in (byte[])[5, 6, 7, 8, 9, 10])
        {
            Assert.NotEqual(DarkRed, palette[palette.MapColor(new CdgColor(level, level, level))]);
        }
    }

    [Fact]
    public void TheBackgroundIsColorZero()
    {
        CdgPalette palette = Build();

        Assert.Equal(CdgPaletteBuilder.BlackColorIndex, palette.MapColor(new CdgColor(4, 0, 0)));
        Assert.Equal(CdgPaletteBuilder.BlackColorIndex, palette.MapColor(CdgColor.Black));
    }

    [Fact]
    public void ARedLetterOnlyTouchedByAHintOfColorStaysRed()
    {
        // Compression dulls the edges of colored lettering towards brown; they belong to the red, not to white.
        CdgPalette palette = Build();

        Assert.Equal(DarkRed, palette[palette.MapColor(new CdgColor(9, 4, 3))]);
    }

    private static CdgPalette Build()
    {
        CdgColorHistogram histogram = new();
        histogram.Add(CdgColor.Black, 5000);
        histogram.Add(White, 300);
        histogram.Add(new CdgColor(8, 8, 8), 100);
        histogram.Add(DarkRed, 200);
        histogram.Add(new CdgColor(6, 1, 1), 100);
        return CdgFlatPaletteBuilder.Build(histogram);
    }
}
