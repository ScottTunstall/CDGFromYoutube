using System.Numerics;
using CdgFromYoutube.Cdg;
using CdgFromYoutube.Imaging;

namespace CdgFromYoutube.Tests;

public sealed class CdgTileEncoderTests
{
    private const byte BlackIndex = 0;
    private const byte WhiteIndex = 1;
    private const byte RedIndex = 2;
    private const byte BlueIndex = 3;

    /// <summary>
    /// Two dark reds two levels apart, which makes a pixel halfway between them a toss up. They sit above
    /// the background level, since anything darker is drawn as the background whatever the palette holds.
    /// </summary>
    private const byte DarkRedIndex = 4;

    private const byte LessDarkRedIndex = 5;

    /// <summary>Eight bit values that fall on the four bit levels seven and eight.</summary>
    private static readonly byte LevelSeven = CdgColor.ToEightBitChannel(7);

    private static readonly byte LevelEight = CdgColor.ToEightBitChannel(8);

    [Fact]
    public void ASingleColorFrameProducesFlatTiles()
    {
        CdgTileImage image = CreateEncoder(useDither: false).Encode(CreateFrame(255, 255, 255));

        for (int tile = 0; tile < CdgFormat.TileCount; tile++)
        {
            Assert.Equal(WhiteIndex, image.GetColor0(tile));
            Assert.Equal(WhiteIndex, image.GetColor1(tile));
            Assert.All(image.GetScanlines(tile).ToArray(), scanline => Assert.Equal((byte)0, scanline));
        }
    }

    [Fact]
    public void ColorsAreTakenFromTheAreaOfEachTile()
    {
        byte[] pixels = CreateFrame(0, 0, 0);
        FillRectangle(pixels, firstX: 0, firstY: 0, width: 150, height: CdgFormat.Height, red: 255, green: 0, blue: 0);
        FillRectangle(pixels, firstX: 150, firstY: 0, width: 150, height: CdgFormat.Height, red: 0, green: 0, blue: 255);

        CdgTileImage image = CreateEncoder(useDither: false).Encode(pixels);

        Assert.Equal(RedIndex, image.GetColor0(CdgFormat.GetTileIndex(row: 0, column: 0)));
        Assert.Equal(BlueIndex, image.GetColor0(CdgFormat.GetTileIndex(row: 0, column: CdgFormat.TileColumns - 1)));
    }

    [Fact]
    public void TheLeftmostPixelOfATileIsTheHighestBit()
    {
        byte[] pixels = CreateFrame(0, 0, 0);
        FillRectangle(pixels, firstX: 0, firstY: 0, width: 3, height: 1, red: 255, green: 255, blue: 255);

        CdgTileImage image = CreateEncoder(useDither: false).Encode(pixels);

        ReadOnlySpan<byte> scanlines = image.GetScanlines(CdgFormat.GetTileIndex(row: 0, column: 0));
        Assert.Equal(BlackIndex, image.GetColor0(0));
        Assert.Equal(WhiteIndex, image.GetColor1(0));

        // The three leftmost pixels of the top scanline take the set bit color, which is the top bit.
        Assert.Equal((byte)0b111000, scanlines[0]);
        Assert.All(scanlines[1..].ToArray(), scanline => Assert.Equal((byte)0, scanline));
    }

    [Fact]
    public void DitheringMixesTheTwoColorsOfATile()
    {
        // Every pixel sits exactly between two palette entries, so without dithering they all take the
        // nearer of the two and with dithering they are spread between them.
        CdgPalette palette = CreatePalette();
        byte[] pixels = CreateFrame(LevelSeven, 0, 0);
        SetPixel(pixels, x: 5, y: 0, red: LevelEight, green: 0, blue: 0);

        CdgTileImage plain = new CdgTileEncoder(palette, useDither: false).Encode(pixels);
        CdgTileImage dithered = new CdgTileEncoder(palette, useDither: true).Encode(pixels);

        ReadOnlySpan<byte> plainScanlines = plain.GetScanlines(CdgFormat.GetTileIndex(row: 0, column: 0));
        ReadOnlySpan<byte> ditheredScanlines = dithered.GetScanlines(CdgFormat.GetTileIndex(row: 0, column: 0));

        Assert.Equal(DarkRedIndex, plain.GetColor0(0));
        Assert.Equal(LessDarkRedIndex, plain.GetColor1(0));

        // Only the pixel that matches the second color exactly is drawn in that color without dithering.
        Assert.Equal((byte)0b000001, plainScanlines[0]);
        Assert.All(plainScanlines[1..].ToArray(), scanline => Assert.Equal((byte)0, scanline));

        // With dithering, the pixels that are equally distant from both colors are spread between them.
        Assert.True(BitOperations.PopCount(ditheredScanlines[0]) > BitOperations.PopCount(plainScanlines[0]));
    }

    [Fact]
    public void ATileWithThreeColorsKeepsAllThreeThroughAnXorPass()
    {
        // A word being highlighted: sung letters in red, letters still to sing in white, on black. Red is
        // nearer black than white, so with two colors the sung letters would be drawn as a black block.
        byte[] pixels = CreateFrame(0, 0, 0);
        FillRectangle(pixels, firstX: 0, firstY: 0, width: 2, height: CdgFormat.TileHeight, red: 255, green: 0, blue: 0);
        FillRectangle(pixels, firstX: 2, firstY: 0, width: 2, height: CdgFormat.TileHeight, red: 255, green: 255, blue: 255);

        CdgTileImage image = CreateEncoder(useDither: false).Encode(pixels);

        Assert.True(image.HasXorPass(0));
        for (int y = 0; y < CdgFormat.TileHeight; y++)
        {
            Assert.Equal(RedIndex, DecodePixel(image, tileIndex: 0, x: 0, y));
            Assert.Equal(RedIndex, DecodePixel(image, tileIndex: 0, x: 1, y));
            Assert.Equal(WhiteIndex, DecodePixel(image, tileIndex: 0, x: 2, y));
            Assert.Equal(WhiteIndex, DecodePixel(image, tileIndex: 0, x: 3, y));
            Assert.Equal(BlackIndex, DecodePixel(image, tileIndex: 0, x: 4, y));
            Assert.Equal(BlackIndex, DecodePixel(image, tileIndex: 0, x: 5, y));
        }
    }

    [Fact]
    public void TheFirstPassOfAThreeColorTileKeepsTheTwoColorsThatDrawItBest()
    {
        // Mostly white letters on black with a small patch of red: on its own, the first pass should still
        // show the letters, so the red is the color the second pass adds.
        byte[] pixels = CreateFrame(0, 0, 0);
        FillRectangle(pixels, firstX: 0, firstY: 0, width: 3, height: CdgFormat.TileHeight, red: 255, green: 255, blue: 255);
        FillRectangle(pixels, firstX: 3, firstY: 0, width: 1, height: 6, red: 255, green: 0, blue: 0);

        CdgTileImage image = CreateEncoder(useDither: false).Encode(pixels);

        Assert.True(image.HasXorPass(0));
        Assert.Equal(
            [BlackIndex, WhiteIndex],
            new[] { image.GetColor0(0), image.GetColor1(0) }.Order().ToArray());
    }

    [Fact]
    public void DimPixelsAreDrawnAsColorZeroEvenWhenThePaletteHoldsTheirExactColor()
    {
        // Players show their own backdrop through color zero only, so a dim texture drawn in any other
        // entry would come out as an opaque dark block.
        CdgColor[] colors = new CdgColor[CdgFormat.ColorCount];
        Array.Fill(colors, new CdgColor(15, 15, 15));
        colors[BlackIndex] = CdgColor.Black;
        colors[1] = new CdgColor(2, 0, 0);
        byte level = CdgColor.ToEightBitChannel(2);

        CdgTileImage image = new CdgTileEncoder(new CdgPalette(colors), useDither: false)
            .Encode(CreateFrame(level, 0, 0));

        Assert.Equal(BlackIndex, image.GetColor0(0));
        Assert.Equal(BlackIndex, image.GetColor1(0));
    }

    [Fact]
    public void AFewStrayPixelsOfAThirdColorDoNotCostASecondPacket()
    {
        byte[] pixels = CreateFrame(0, 0, 0);
        FillRectangle(pixels, firstX: 0, firstY: 0, width: 3, height: CdgFormat.TileHeight, red: 255, green: 255, blue: 255);
        SetPixel(pixels, x: 5, y: 0, red: 255, green: 0, blue: 0);

        CdgTileImage image = CreateEncoder(useDither: false).Encode(pixels);

        Assert.False(image.HasXorPass(0));
    }

    [Fact]
    public void AntialiasedEdgesAreDrawnInTheShadesOfARamp()
    {
        CdgPalette palette = CreateGreyRampPalette();
        byte[] pixels = CreateFrame(0, 0, 0);
        FillRectangle(pixels, firstX: 0, firstY: 0, width: 2, height: CdgFormat.TileHeight, red: 255, green: 255, blue: 255);
        FillRectangle(pixels, firstX: 2, firstY: 0, width: 1, height: CdgFormat.TileHeight, red: 170, green: 170, blue: 170);
        FillRectangle(pixels, firstX: 3, firstY: 0, width: 1, height: CdgFormat.TileHeight, red: 85, green: 85, blue: 85);

        CdgTileImage image = new CdgTileEncoder(palette, useDither: false).Encode(pixels);

        Assert.True(image.HasXorPass(0));
        Assert.Equal(new CdgColor(15, 15, 15), palette[DecodePixel(image, 0, x: 0, y: 0)]);
        Assert.Equal(new CdgColor(10, 10, 10), palette[DecodePixel(image, 0, x: 2, y: 0)]);
        Assert.Equal(new CdgColor(5, 5, 5), palette[DecodePixel(image, 0, x: 3, y: 0)]);
        Assert.Equal(CdgColor.Black, palette[DecodePixel(image, 0, x: 5, y: 0)]);
    }

    [Fact]
    public void TheFirstPassOfARampTileAloneShowsTheBrightPartOfTheLetters()
    {
        // The second pass can wait for a later frame, so the first must read as lettering by itself.
        CdgPalette palette = CreateGreyRampPalette();
        byte[] pixels = CreateFrame(0, 0, 0);
        FillRectangle(pixels, firstX: 0, firstY: 0, width: 2, height: CdgFormat.TileHeight, red: 255, green: 255, blue: 255);
        FillRectangle(pixels, firstX: 2, firstY: 0, width: 1, height: CdgFormat.TileHeight, red: 170, green: 170, blue: 170);
        FillRectangle(pixels, firstX: 3, firstY: 0, width: 1, height: CdgFormat.TileHeight, red: 85, green: 85, blue: 85);

        CdgTileImage image = new CdgTileEncoder(palette, useDither: false).Encode(pixels);

        Assert.Equal(CdgPaletteBuilder.BlackColorIndex, image.GetColor0(0));
        Assert.Equal(new CdgColor(15, 15, 15), palette[image.GetColor1(0)]);
        Assert.Equal(0b111000, image.GetScanlines(0)[0]);
    }

    [Fact]
    public void ASingleFaintEdgePixelDoesNotCostASecondPacket()
    {
        byte[] pixels = CreateFrame(0, 0, 0);
        FillRectangle(pixels, firstX: 0, firstY: 0, width: 3, height: CdgFormat.TileHeight, red: 255, green: 255, blue: 255);
        SetPixel(pixels, x: 4, y: 0, red: 85, green: 85, blue: 85);

        CdgTileImage image = new CdgTileEncoder(CreateGreyRampPalette(), useDither: false).Encode(pixels);

        Assert.False(image.HasXorPass(0));
    }

    [Fact]
    public void AFlatPaletteDrawsALetterAndItsDimEdgeInOneSolidColor()
    {
        CdgColorHistogram histogram = new();
        histogram.Add(new CdgColor(15, 15, 15), 100);
        histogram.Add(new CdgColor(11, 1, 1), 100);
        CdgPalette palette = CdgFlatPaletteBuilder.Build(histogram);
        byte[] pixels = CreateFrame(0, 0, 0);
        byte full = CdgColor.ToEightBitChannel(11);
        byte edge = CdgColor.ToEightBitChannel(6);
        FillRectangle(pixels, firstX: 0, firstY: 0, width: 2, height: CdgFormat.TileHeight, red: full, green: 17, blue: 17);
        FillRectangle(pixels, firstX: 2, firstY: 0, width: 1, height: CdgFormat.TileHeight, red: edge, green: 0, blue: 0);

        // Dithering is asked for, and must not put background pixels back into the lettering.
        CdgTileImage image = new CdgTileEncoder(palette, useDither: true).Encode(pixels);

        Assert.False(image.HasXorPass(0));
        for (int y = 0; y < CdgFormat.TileHeight; y++)
        {
            Assert.Equal(new CdgColor(11, 1, 1), palette[DecodePixel(image, 0, x: 0, y)]);
            Assert.Equal(new CdgColor(11, 1, 1), palette[DecodePixel(image, 0, x: 2, y)]);
            Assert.Equal(CdgColor.Black, palette[DecodePixel(image, 0, x: 3, y)]);
        }
    }

    [Fact]
    public void FramesThatAreTooShortAreRejected()
    {
        CdgTileEncoder encoder = CreateEncoder(useDither: false);

        Assert.Throws<ArgumentException>(() => encoder.Encode(new byte[100]));
    }

    private static CdgTileEncoder CreateEncoder(bool useDither) => new(CreatePalette(), useDither);

    private static CdgPalette CreateGreyRampPalette()
    {
        CdgColorHistogram histogram = new();
        histogram.Add(new CdgColor(15, 15, 15), 100);
        return CdgRampPaletteBuilder.Build(histogram);
    }

    private static CdgPalette CreatePalette()
    {
        CdgColor[] colors = new CdgColor[CdgFormat.ColorCount];
        Array.Fill(colors, CdgColor.Black);
        colors[BlackIndex] = new CdgColor(0, 0, 0);
        colors[WhiteIndex] = new CdgColor(15, 15, 15);
        colors[RedIndex] = new CdgColor(15, 0, 0);
        colors[BlueIndex] = new CdgColor(0, 0, 15);
        colors[DarkRedIndex] = new CdgColor(6, 0, 0);
        colors[LessDarkRedIndex] = new CdgColor(8, 0, 0);
        return new CdgPalette(colors);
    }

    /// <summary>Returns the palette index a player shows for a pixel once both passes of its tile are drawn.</summary>
    private static byte DecodePixel(CdgTileImage image, int tileIndex, int x, int y)
    {
        int bit = 1 << (CdgFormat.TileWidth - 1 - x);
        byte index = (image.GetScanlines(tileIndex)[y] & bit) != 0 ? image.GetColor1(tileIndex) : image.GetColor0(tileIndex);
        if ((image.GetXorScanlines(tileIndex)[y] & bit) != 0)
        {
            index ^= image.GetXorColor(tileIndex);
        }

        return index;
    }

    private static void SetPixel(byte[] pixels, int x, int y, byte red, byte green, byte blue)
    {
        int offset = ((y * CdgFormat.Width) + x) * RgbPixelFormat.BytesPerPixel;
        pixels[offset] = red;
        pixels[offset + 1] = green;
        pixels[offset + 2] = blue;
    }

    private static byte[] CreateFrame(byte red, byte green, byte blue)
    {
        byte[] pixels = new byte[RgbPixelFormat.GetFrameSizeBytes(CdgFormat.Width, CdgFormat.Height)];
        FillRectangle(pixels, 0, 0, CdgFormat.Width, CdgFormat.Height, red, green, blue);
        return pixels;
    }

    private static void FillRectangle(
        byte[] pixels,
        int firstX,
        int firstY,
        int width,
        int height,
        byte red,
        byte green,
        byte blue)
    {
        for (int y = firstY; y < firstY + height; y++)
        {
            for (int x = firstX; x < firstX + width; x++)
            {
                int offset = ((y * CdgFormat.Width) + x) * RgbPixelFormat.BytesPerPixel;
                pixels[offset] = red;
                pixels[offset + 1] = green;
                pixels[offset + 2] = blue;
            }
        }
    }
}
