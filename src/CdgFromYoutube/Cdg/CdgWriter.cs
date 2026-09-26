namespace CdgFromYoutube.Cdg;

/// <summary>
/// Writes CD+G packets to a stream in the order a player expects to consume them.
/// </summary>
/// <remarks>
/// Every packet is twenty four bytes: the graphics command, the instruction, unused parity bytes, the
/// instruction's data and four more unused parity bytes. See "CD+G Revealed" by Jim Bumgardner
/// (https://jbum.com/cdg_revealed.html) for the instructions themselves.
/// </remarks>
public sealed class CdgWriter(Stream stream)
{
    /// <summary>The number of color table entries a single packet carries.</summary>
    public const int ColorTableEntriesPerPacket = CdgFormat.ColorCount / 2;

    /// <summary>The number of scanline bytes a tile packet carries.</summary>
    public const int TileScanlineBytes = CdgFormat.TileScanlineCount;

    private readonly byte[] _packet = new byte[CdgFormat.PacketSizeBytes];

    /// <summary>The number of packets written so far, which is also the packet index of the next one.</summary>
    public long PacketsWritten { get; private set; }

    /// <summary>Clears the whole screen to one color.</summary>
    /// <param name="colorIndex">The color table entry to fill with.</param>
    /// <param name="repeatCount">The repeat number, which players use to recognise a repeated command.</param>
    public void WriteMemoryPreset(byte colorIndex, byte repeatCount)
    {
        ValidateColorIndex(colorIndex);
        BeginPacket(CdgInstruction.MemoryPreset);
        _packet[CdgPacketLayout.DataOffset] = colorIndex;
        _packet[CdgPacketLayout.DataOffset + 1] = repeatCount;
        WritePacket();
    }

    /// <summary>Clears the border to one color.</summary>
    public void WriteBorderPreset(byte colorIndex)
    {
        ValidateColorIndex(colorIndex);
        BeginPacket(CdgInstruction.BorderPreset);
        _packet[CdgPacketLayout.DataOffset] = colorIndex;
        WritePacket();
    }

    /// <summary>Draws one tile of the screen.</summary>
    /// <param name="row">The tile row, counted down from the top of the screen.</param>
    /// <param name="column">The tile column, counted across from the left of the screen.</param>
    /// <param name="color0">The palette index used where a pixel bit is clear.</param>
    /// <param name="color1">The palette index used where a pixel bit is set.</param>
    /// <param name="scanlines">Twelve scanlines; in each, the top bit is the leftmost pixel.</param>
    public void WriteTileBlock(int row, int column, byte color0, byte color1, ReadOnlySpan<byte> scanlines) =>
        WriteTile(CdgInstruction.TileBlock, row, column, color0, color1, scanlines);

    /// <summary>
    /// Flips the palette index of the pixels of one tile: pixels with a clear bit are exclusive-or-ed with
    /// <paramref name="color0"/> and pixels with a set bit with <paramref name="color1"/>.
    /// </summary>
    /// <param name="row">The tile row, counted down from the top of the screen.</param>
    /// <param name="column">The tile column, counted across from the left of the screen.</param>
    /// <param name="color0">The value clear pixels are flipped by; zero leaves them alone.</param>
    /// <param name="color1">The value set pixels are flipped by.</param>
    /// <param name="scanlines">Twelve scanlines; in each, the top bit is the leftmost pixel.</param>
    public void WriteTileBlockXor(int row, int column, byte color0, byte color1, ReadOnlySpan<byte> scanlines) =>
        WriteTile(CdgInstruction.TileBlockXor, row, column, color0, color1, scanlines);

    private void WriteTile(
        CdgInstruction instruction,
        int row,
        int column,
        byte color0,
        byte color1,
        ReadOnlySpan<byte> scanlines)
    {
        ValidateTilePosition(row, column);
        ValidateColorIndex(color0);
        ValidateColorIndex(color1);
        if (scanlines.Length != TileScanlineBytes)
        {
            throw new ArgumentException(
                $"A tile is made of {TileScanlineBytes} scanlines, but {scanlines.Length} were given.",
                nameof(scanlines));
        }

        BeginPacket(instruction);
        int data = CdgPacketLayout.DataOffset;
        _packet[data] = color0;
        _packet[data + 1] = color1;
        _packet[data + 2] = (byte)row;
        _packet[data + 3] = (byte)column;
        scanlines.CopyTo(_packet.AsSpan(data + 4));
        WritePacket();
    }

    /// <summary>Loads color table entries zero to seven.</summary>
    public void WriteColorTableLow(ReadOnlySpan<CdgColor> colors) =>
        WriteColorTable(CdgInstruction.ColorTableLow, colors);

    /// <summary>Loads color table entries eight to fifteen.</summary>
    public void WriteColorTableHigh(ReadOnlySpan<CdgColor> colors) =>
        WriteColorTable(CdgInstruction.ColorTableHigh, colors);

    /// <summary>Writes a packet that carries no graphics command, which players ignore.</summary>
    public void WriteNoOperation()
    {
        Array.Clear(_packet);
        WritePacket();
    }

    private void WriteColorTable(CdgInstruction instruction, ReadOnlySpan<CdgColor> colors)
    {
        if (colors.Length != ColorTableEntriesPerPacket)
        {
            throw new ArgumentException(
                $"A colour table packet carries {ColorTableEntriesPerPacket} entries, but {colors.Length} were given.",
                nameof(colors));
        }

        BeginPacket(instruction);
        int data = CdgPacketLayout.DataOffset;
        for (int index = 0; index < colors.Length; index++)
        {
            ushort colorSpec = colors[index].ToColorSpec();
            _packet[data + (index * 2)] = (byte)(colorSpec >> 8);
            _packet[data + (index * 2) + 1] = (byte)(colorSpec & 0xFF);
        }

        WritePacket();
    }

    private void BeginPacket(CdgInstruction instruction)
    {
        Array.Clear(_packet);
        _packet[CdgPacketLayout.CommandOffset] = CdgPacketLayout.GraphicsCommand;
        _packet[CdgPacketLayout.InstructionOffset] = (byte)instruction;
    }

    private void WritePacket()
    {
        stream.Write(_packet, 0, _packet.Length);
        PacketsWritten++;
    }

    private static void ValidateColorIndex(byte colorIndex)
    {
        if (colorIndex >= CdgFormat.ColorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(colorIndex),
                colorIndex,
                $"A colour table holds {CdgFormat.ColorCount} entries.");
        }
    }

    private static void ValidateTilePosition(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, CdgFormat.TileRows);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, CdgFormat.TileColumns);
    }
}
