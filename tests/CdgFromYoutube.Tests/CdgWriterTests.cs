using CdgFromYoutube.Cdg;

namespace CdgFromYoutube.Tests;

public sealed class CdgWriterTests
{
    [Fact]
    public void TileBlockPacketCarriesTheCommandInstructionAndTile()
    {
        using MemoryStream stream = new();
        CdgWriter writer = new(stream);
        byte[] scanlines = [0x20, 0x10, 0x08, 0x04, 0x02, 0x01, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00];

        writer.WriteTileBlock(row: 2, column: 3, color0: 4, color1: 5, scanlines);

        byte[] packet = stream.ToArray();
        Assert.Equal(CdgFormat.PacketSizeBytes, packet.Length);
        Assert.Equal(CdgPacketLayout.GraphicsCommand, packet[CdgPacketLayout.CommandOffset]);
        Assert.Equal((byte)CdgInstruction.TileBlock, packet[CdgPacketLayout.InstructionOffset]);
        Assert.Equal((byte)0x00, packet[2]);
        Assert.Equal((byte)0x00, packet[3]);
        Assert.Equal((byte)4, packet[4]);
        Assert.Equal((byte)5, packet[5]);
        Assert.Equal((byte)2, packet[6]);
        Assert.Equal((byte)3, packet[7]);
        Assert.Equal(scanlines, packet[8..20]);
        Assert.All(packet[20..], value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void TileBlockXorPacketCarriesTheXorInstructionAndTile()
    {
        using MemoryStream stream = new();
        CdgWriter writer = new(stream);
        byte[] scanlines = [0x3F, 0x00, 0x21, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C];

        writer.WriteTileBlockXor(row: 17, column: 49, color0: 0, color1: 7, scanlines);

        byte[] packet = stream.ToArray();
        Assert.Equal(CdgPacketLayout.GraphicsCommand, packet[CdgPacketLayout.CommandOffset]);
        Assert.Equal((byte)CdgInstruction.TileBlockXor, packet[CdgPacketLayout.InstructionOffset]);
        Assert.Equal((byte)0, packet[4]);
        Assert.Equal((byte)7, packet[5]);
        Assert.Equal((byte)17, packet[6]);
        Assert.Equal((byte)49, packet[7]);
        Assert.Equal(scanlines, packet[8..20]);
    }

    [Fact]
    public void MemoryPresetPacketCarriesTheColorAndRepeatCount()
    {
        using MemoryStream stream = new();
        CdgWriter writer = new(stream);

        writer.WriteMemoryPreset(colorIndex: 6, repeatCount: 2);

        byte[] packet = stream.ToArray();
        Assert.Equal((byte)CdgInstruction.MemoryPreset, packet[CdgPacketLayout.InstructionOffset]);
        Assert.Equal((byte)6, packet[CdgPacketLayout.DataOffset]);
        Assert.Equal((byte)2, packet[CdgPacketLayout.DataOffset + 1]);
    }

    [Fact]
    public void BorderPresetPacketCarriesTheColor()
    {
        using MemoryStream stream = new();
        CdgWriter writer = new(stream);

        writer.WriteBorderPreset(colorIndex: 9);

        byte[] packet = stream.ToArray();
        Assert.Equal((byte)CdgInstruction.BorderPreset, packet[CdgPacketLayout.InstructionOffset]);
        Assert.Equal((byte)9, packet[CdgPacketLayout.DataOffset]);
    }

    [Fact]
    public void ColorTablePacketWritesEachColorHighByteFirst()
    {
        using MemoryStream stream = new();
        CdgWriter writer = new(stream);
        CdgColor[] colors =
        [
            new(1, 2, 3), new(4, 5, 6), new(7, 8, 9), new(10, 11, 12),
            new(13, 14, 15), new(0, 0, 0), new(15, 0, 0), new(0, 15, 0),
        ];

        writer.WriteColorTableLow(colors);

        byte[] packet = stream.ToArray();
        Assert.Equal((byte)CdgInstruction.ColorTableLow, packet[CdgPacketLayout.InstructionOffset]);
        for (int index = 0; index < colors.Length; index++)
        {
            ushort colorSpec = colors[index].ToColorSpec();
            Assert.Equal((byte)(colorSpec >> 8), packet[CdgPacketLayout.DataOffset + (index * 2)]);
            Assert.Equal((byte)(colorSpec & 0xFF), packet[CdgPacketLayout.DataOffset + (index * 2) + 1]);
        }
    }

    [Fact]
    public void TheHighColorTablePacketNamesTheHigherInstruction()
    {
        using MemoryStream stream = new();
        CdgWriter writer = new(stream);

        writer.WriteColorTableHigh(new CdgColor[CdgWriter.ColorTableEntriesPerPacket]);

        Assert.Equal((byte)CdgInstruction.ColorTableHigh, stream.ToArray()[CdgPacketLayout.InstructionOffset]);
    }

    [Fact]
    public void NoOperationPacketsCarryNoCommandAndAreCounted()
    {
        using MemoryStream stream = new();
        CdgWriter writer = new(stream);

        writer.WriteNoOperation();

        Assert.Equal(1L, writer.PacketsWritten);
        Assert.All(stream.ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void TileBlocksOutsideTheRasterAreRejected()
    {
        using MemoryStream stream = new();
        CdgWriter writer = new(stream);
        byte[] scanlines = new byte[CdgWriter.TileScanlineBytes];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => writer.WriteTileBlock(CdgFormat.TileRows, column: 0, color0: 0, color1: 0, scanlines));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => writer.WriteTileBlock(row: 0, column: CdgFormat.TileColumns, color0: 0, color1: 0, scanlines));
    }

    [Fact]
    public void TilesWithTheWrongNumberOfScanlinesAreRejected()
    {
        using MemoryStream stream = new();
        CdgWriter writer = new(stream);

        Assert.Throws<ArgumentException>(
            () => writer.WriteTileBlock(row: 0, column: 0, color0: 0, color1: 0, new byte[3]));
    }

    [Fact]
    public void ColorsOutsideTheTableAreRejected()
    {
        using MemoryStream stream = new();
        CdgWriter writer = new(stream);

        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteMemoryPreset(CdgFormat.ColorCount, repeatCount: 0));
    }
}
