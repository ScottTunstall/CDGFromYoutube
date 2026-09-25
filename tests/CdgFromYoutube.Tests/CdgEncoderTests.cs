using CdgFromYoutube.Cdg;

namespace CdgFromYoutube.Tests;

public sealed class CdgEncoderTests : IDisposable
{
    /// <summary>Two memory presets, two color table packets and a border preset.</summary>
    private const int ProloguePacketCount = 5;

    private readonly MemoryStream _stream = new();

    /// <inheritdoc />
    public void Dispose() => _stream.Dispose();

    [Fact]
    public void WritePrologueClearsTheScreenAndLoadsTheColorTable()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(1));

        encoder.WritePrologue();

        Assert.Equal(ProloguePacketCount, encoder.PacketsWritten);
    }

    [Fact]
    public void UnchangedFramesOnlyPadTheTimeline()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(2));
        encoder.WritePrologue();
        CdgTileImage blank = new();

        encoder.WriteFrame(TimeSpan.Zero, blank);
        Assert.True(encoder.WriteFrame(TimeSpan.FromSeconds(1), blank));
        encoder.WriteEpilogue();

        Assert.Equal((long)(2 * CdgFormat.PacketsPerSecond), encoder.PacketsWritten);
        Assert.Equal(2, encoder.FramesWritten);
        Assert.Equal(0, encoder.FramesDropped);
    }

    [Fact]
    public void ChangedTilesAreWrittenAtTheFrameTimestamp()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(2));
        encoder.WritePrologue();
        CdgTileImage image = new();
        byte[] scanlines = new byte[CdgFormat.TileScanlineCount];
        image.SetTile(CdgFormat.GetTileIndex(0, 0), 1, 1, scanlines);
        image.SetTile(CdgFormat.GetTileIndex(0, 1), 1, 1, scanlines);
        image.SetTile(CdgFormat.GetTileIndex(5, 7), 1, 1, scanlines);

        encoder.WriteFrame(TimeSpan.FromSeconds(1), image);

        // The frame is held back for one step, so the next frame is what releases it.
        encoder.WriteFrame(TimeSpan.FromSeconds(2), image);

        // The frame's three tiles land after the three hundred packets that the first second holds.
        Assert.Equal(CdgFormat.PacketsPerSecond + 3L, encoder.PacketsWritten);
        Assert.Equal(1, encoder.FramesWritten);
    }

    [Fact]
    public void TilesThatDidNotChangeAreNotWrittenAgain()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(4));
        encoder.WritePrologue();
        CdgTileImage image = new();
        image.SetTile(CdgFormat.GetTileIndex(0, 0), 1, 1, new byte[CdgFormat.TileScanlineCount]);

        encoder.WriteFrame(TimeSpan.Zero, image);
        encoder.WriteFrame(TimeSpan.FromSeconds(1), image);
        long afterFirstFrame = encoder.PacketsWritten;
        encoder.WriteFrame(TimeSpan.FromSeconds(2), image);

        Assert.Equal(ProloguePacketCount + 1L, afterFirstFrame);
        Assert.Equal(CdgFormat.PacketsPerSecond, encoder.PacketsWritten);
    }

    [Fact]
    public void FramesThatCostMoreThanThePacketsBeforeThemAreDropped()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(10));
        encoder.WritePrologue();
        CdgTileImage fullScreen = CreateFullScreenChange();

        encoder.WriteFrame(TimeSpan.Zero, fullScreen);
        Assert.True(encoder.WriteFrame(TimeSpan.FromSeconds(1), fullScreen));
        Assert.Equal(ProloguePacketCount + (long)CdgFormat.TileCount, encoder.PacketsWritten);

        // A whole screen costs three seconds of packets, so a frame one second in has to be dropped.
        Assert.False(encoder.WriteFrame(TimeSpan.FromSeconds(2), fullScreen));
        Assert.Equal(1, encoder.FramesDropped);
        Assert.Equal(1, encoder.FramesWritten);
    }

    [Fact]
    public void AClearedLyricAreaIsPassedOver()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(30));
        encoder.WritePrologue();
        CdgTileImage verse = CreateFullScreenChange(color: 1);

        encoder.WriteFrame(TimeSpan.Zero, verse);
        encoder.WriteFrame(TimeSpan.FromSeconds(1), verse);
        long afterVerse = encoder.PacketsWritten;

        // Then the video clears the lyric area before the next verse arrives.
        encoder.WriteFrame(TimeSpan.FromSeconds(2), new CdgTileImage());
        encoder.WriteFrame(TimeSpan.FromSeconds(3), verse);

        // The cleared state is passed over, so the verse stays on screen and keeps its packets.
        Assert.Equal(1, encoder.FramesHeldBack);
        Assert.Equal(afterVerse, encoder.PacketsWritten);
    }

    [Fact]
    public void ASmallClearingIsNotPassedOver()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(30));
        encoder.WritePrologue();
        CdgTileImage verse = CreateFullScreenChange(color: 1);
        encoder.WriteFrame(TimeSpan.Zero, verse);
        encoder.WriteFrame(TimeSpan.FromSeconds(1), verse);

        // A handful of tiles going black is a highlight moving, not the lyric area being cleared.
        CdgTileImage nearlyAllVerse = CreateFullScreenChange(color: 1);
        byte[] scanlines = new byte[CdgFormat.TileScanlineCount];
        nearlyAllVerse.SetTile(CdgFormat.GetTileIndex(0, 0), 0, 0, scanlines);
        nearlyAllVerse.SetTile(CdgFormat.GetTileIndex(1, 0), 0, 0, scanlines);
        nearlyAllVerse.SetTile(CdgFormat.GetTileIndex(2, 0), 0, 0, scanlines);
        encoder.WriteFrame(TimeSpan.FromSeconds(2), nearlyAllVerse);
        encoder.WriteFrame(TimeSpan.FromSeconds(3), nearlyAllVerse);

        Assert.Equal(0, encoder.FramesHeldBack);
    }

    [Fact]
    public void ALyricLineBeingClearedIsPassedOver()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(30));
        encoder.WritePrologue();
        CdgTileImage verse = CreateFullScreenChange(color: 1);
        encoder.WriteFrame(TimeSpan.Zero, verse);
        encoder.WriteFrame(TimeSpan.FromSeconds(1), verse);

        // One lyric line is a handful of tiles: enough to matter, far too few to be a fraction of the screen.
        CdgTileImage withoutOneLine = CreateFullScreenChange(color: 1);
        byte[] scanlines = new byte[CdgFormat.TileScanlineCount];
        for (int row = 4; row < 6; row++)
        {
            for (int column = 0; column < CdgFormat.TileColumns; column++)
            {
                withoutOneLine.SetTile(CdgFormat.GetTileIndex(row, column), 0, 0, scanlines);
            }
        }

        encoder.WriteFrame(TimeSpan.FromSeconds(2), withoutOneLine);
        encoder.WriteFrame(TimeSpan.FromSeconds(3), withoutOneLine);

        // The line stays on screen instead of going black for as long as the wipe would take to draw.
        Assert.Equal(1, encoder.FramesHeldBack);
    }

    [Fact]
    public void ATileWithAThirdColorIsWrittenAsATileAndAnXorPacket()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(2));
        encoder.WritePrologue();
        CdgTileImage image = new();
        byte[] scanlines = new byte[CdgFormat.TileScanlineCount];
        image.SetTile(CdgFormat.GetTileIndex(0, 0), 0, 1, scanlines, xorColor: 3, scanlines);

        encoder.WriteFrame(TimeSpan.Zero, image);
        encoder.WriteFrame(TimeSpan.FromSeconds(1), image);

        Assert.Equal(ProloguePacketCount + 2L, encoder.PacketsWritten);
        byte[] written = _stream.ToArray();
        Assert.Equal((byte)CdgInstruction.TileBlock, written[(ProloguePacketCount * CdgFormat.PacketSizeBytes) + 1]);
        Assert.Equal((byte)CdgInstruction.TileBlockXor, written[((ProloguePacketCount + 1) * CdgFormat.PacketSizeBytes) + 1]);
    }

    [Fact]
    public void XorPassesThatMissTheNextFrameAreFinishedLaterWithoutRedrawingTheTile()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(10));
        encoder.WritePrologue();
        CdgTileImage page = CreateFullScreenChange(color: 1, xorColor: 3);

        // The whole page's first passes run past the next frame at one second, so none of the second
        // passes are written with it: the page goes up at the cost of a two color page.
        encoder.WriteFrame(TimeSpan.Zero, page);
        encoder.WriteFrame(TimeSpan.FromSeconds(1), page);
        Assert.Equal(ProloguePacketCount + (long)CdgFormat.TileCount, encoder.PacketsWritten);

        // The frame at one second cannot be paid for; the one at four seconds adds only the second passes.
        encoder.WriteFrame(TimeSpan.FromSeconds(4), page);
        encoder.WriteFrame(TimeSpan.FromSeconds(8), page);
        Assert.Equal((4L * CdgFormat.PacketsPerSecond) + CdgFormat.TileCount, encoder.PacketsWritten);
    }

    [Fact]
    public void FramesPastTheEndOfTheTrackAreDropped()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(2));
        encoder.WritePrologue();

        encoder.WriteFrame(TimeSpan.FromSeconds(5), new CdgTileImage());
        Assert.False(encoder.WriteFrame(TimeSpan.FromSeconds(6), new CdgTileImage()));
        Assert.Equal(1, encoder.FramesDropped);
    }
    [Fact]
    public void TheEpilogueFillsTheRestOfTheTrackWithNoOperationPackets()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(3));
        encoder.WritePrologue();

        encoder.WriteEpilogue();

        Assert.Equal((long)(3 * CdgFormat.PacketsPerSecond), encoder.PacketsWritten);
        Assert.Equal(encoder.PacketLimit, encoder.PacketsWritten);
    }

    [Fact]
    public void ThePacketLimitMatchesTheLengthOfTheAudio()
    {
        CdgEncoder encoder = CreateEncoder(TimeSpan.FromSeconds(213.5));

        Assert.Equal(64050L, encoder.PacketLimit);
    }

    private CdgEncoder CreateEncoder(TimeSpan duration) => new(new CdgWriter(_stream), CreatePalette(), duration);

    private static CdgPalette CreatePalette()
    {
        CdgColor[] colors = new CdgColor[CdgFormat.ColorCount];
        Array.Fill(colors, CdgColor.Black);
        colors[1] = new CdgColor(15, 15, 15);
        return new CdgPalette(colors);
    }

    private static CdgTileImage CreateFullScreenChange(byte color = 1, byte xorColor = 0)
    {
        CdgTileImage image = new();
        byte[] scanlines = new byte[CdgFormat.TileScanlineCount];
        for (int row = 0; row < CdgFormat.TileRows; row++)
        {
            for (int column = 0; column < CdgFormat.TileColumns; column++)
            {
                image.SetTile(CdgFormat.GetTileIndex(row, column), color, color, scanlines, xorColor, scanlines);
            }
        }

        return image;
    }
}
