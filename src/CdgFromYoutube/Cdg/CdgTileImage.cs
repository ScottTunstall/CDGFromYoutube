namespace CdgFromYoutube.Cdg;

/// <summary>
/// A screen reduced to two color tiles: for each tile the two palette entries it uses and the twelve
/// scanlines of six one bit pixels that say which of the two colors each pixel takes, plus an optional
/// exclusive-or pass that gives a tile a third color.
/// </summary>
/// <remarks>
/// A tile block can only draw two colors, but a lyric being highlighted as it is sung puts three in one
/// tile: the background, the letters still to sing and the letters already sung. The exclusive-or tile
/// block flips the palette index of the pixels whose bit is set, so drawing the tile with two colors and
/// then flipping some of its pixels by <c>first ^ third</c> turns those pixels into the third color. An
/// exclusive-or color of zero means the tile has no such pass.
/// </remarks>
public sealed class CdgTileImage
{
    private readonly byte[] _tileColors = new byte[CdgFormat.TileCount * CdgFormat.TileColorCount];
    private readonly byte[] _tileScanlines = new byte[CdgFormat.TileCount * CdgFormat.TileScanlineCount];
    private readonly byte[] _xorColors = new byte[CdgFormat.TileCount];
    private readonly byte[] _xorScanlines = new byte[CdgFormat.TileCount * CdgFormat.TileScanlineCount];

    /// <summary>Returns the palette index of the color that pixels with a clear bit take.</summary>
    public byte GetColor0(int tileIndex) => _tileColors[tileIndex * CdgFormat.TileColorCount];

    /// <summary>Returns the palette index of the color that pixels with a set bit take.</summary>
    public byte GetColor1(int tileIndex) => _tileColors[(tileIndex * CdgFormat.TileColorCount) + 1];

    /// <summary>Returns the twelve scanlines of a tile, from the top of the tile downwards.</summary>
    public ReadOnlySpan<byte> GetScanlines(int tileIndex) => _tileScanlines.AsSpan(
        tileIndex * CdgFormat.TileScanlineCount,
        CdgFormat.TileScanlineCount);

    /// <summary>
    /// Returns the value the exclusive-or pass flips the palette index of its set pixels by, or zero when
    /// the tile has no exclusive-or pass.
    /// </summary>
    public byte GetXorColor(int tileIndex) => _xorColors[tileIndex];

    /// <summary>Returns the twelve scanlines of the exclusive-or pass, which mark the pixels it flips.</summary>
    public ReadOnlySpan<byte> GetXorScanlines(int tileIndex) => _xorScanlines.AsSpan(
        tileIndex * CdgFormat.TileScanlineCount,
        CdgFormat.TileScanlineCount);

    /// <summary>Returns whether a tile is drawn in two passes.</summary>
    public bool HasXorPass(int tileIndex) => _xorColors[tileIndex] != 0;

    /// <summary>Replaces one tile with a tile of two colors.</summary>
    /// <param name="tileIndex">The index of the tile, as returned by <see cref="CdgFormat.GetTileIndex"/>.</param>
    /// <param name="color0">The palette index used where a pixel bit is clear.</param>
    /// <param name="color1">The palette index used where a pixel bit is set.</param>
    /// <param name="scanlines">Twelve scanlines; in each, the top bit is the leftmost pixel.</param>
    public void SetTile(int tileIndex, byte color0, byte color1, ReadOnlySpan<byte> scanlines) =>
        SetTile(tileIndex, color0, color1, scanlines, xorColor: 0, scanlines);

    /// <summary>Replaces one tile, including the exclusive-or pass that gives it a third color.</summary>
    /// <param name="tileIndex">The index of the tile, as returned by <see cref="CdgFormat.GetTileIndex"/>.</param>
    /// <param name="color0">The palette index used where a pixel bit is clear.</param>
    /// <param name="color1">The palette index used where a pixel bit is set.</param>
    /// <param name="scanlines">Twelve scanlines; in each, the top bit is the leftmost pixel.</param>
    /// <param name="xorColor">The value set pixels of the second pass are flipped by, or zero for none.</param>
    /// <param name="xorScanlines">Twelve scanlines marking the pixels the second pass flips.</param>
    public void SetTile(
        int tileIndex,
        byte color0,
        byte color1,
        ReadOnlySpan<byte> scanlines,
        byte xorColor,
        ReadOnlySpan<byte> xorScanlines)
    {
        ValidateTileIndex(tileIndex);
        ValidateScanlines(scanlines, nameof(scanlines));
        ValidateScanlines(xorScanlines, nameof(xorScanlines));

        _tileColors[tileIndex * CdgFormat.TileColorCount] = color0;
        _tileColors[(tileIndex * CdgFormat.TileColorCount) + 1] = color1;
        scanlines.CopyTo(_tileScanlines.AsSpan(tileIndex * CdgFormat.TileScanlineCount));

        _xorColors[tileIndex] = xorColor;
        Span<byte> storedXorScanlines = _xorScanlines.AsSpan(
            tileIndex * CdgFormat.TileScanlineCount,
            CdgFormat.TileScanlineCount);
        if (xorColor == 0)
        {
            storedXorScanlines.Clear();
        }
        else
        {
            xorScanlines.CopyTo(storedXorScanlines);
        }
    }

    /// <summary>Copies one tile from another image.</summary>
    public void CopyTileFrom(CdgTileImage source, int tileIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        SetTile(
            tileIndex,
            source.GetColor0(tileIndex),
            source.GetColor1(tileIndex),
            source.GetScanlines(tileIndex),
            source.GetXorColor(tileIndex),
            source.GetXorScanlines(tileIndex));
    }

    /// <summary>Returns whether a tile already holds the given two colors and scanlines, with no second pass.</summary>
    public bool TileEquals(int tileIndex, byte color0, byte color1, ReadOnlySpan<byte> scanlines)
    {
        ValidateTileIndex(tileIndex);
        return GetColor0(tileIndex) == color0 &&
            GetColor1(tileIndex) == color1 &&
            GetScanlines(tileIndex).SequenceEqual(scanlines) &&
            !HasXorPass(tileIndex);
    }

    /// <summary>Returns whether a tile is drawn exactly as the same tile of another image.</summary>
    public bool TileEquals(int tileIndex, CdgTileImage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        ValidateTileIndex(tileIndex);
        return GetColor0(tileIndex) == other.GetColor0(tileIndex) &&
            GetColor1(tileIndex) == other.GetColor1(tileIndex) &&
            GetScanlines(tileIndex).SequenceEqual(other.GetScanlines(tileIndex)) &&
            GetXorColor(tileIndex) == other.GetXorColor(tileIndex) &&
            GetXorScanlines(tileIndex).SequenceEqual(other.GetXorScanlines(tileIndex));
    }

    private static void ValidateTileIndex(int tileIndex) =>
        ArgumentOutOfRangeException.ThrowIfNegative(tileIndex);

    private static void ValidateScanlines(ReadOnlySpan<byte> scanlines, string parameterName)
    {
        if (scanlines.Length != CdgFormat.TileScanlineCount)
        {
            throw new ArgumentException(
                $"A tile is made of {CdgFormat.TileScanlineCount} scanlines, but {scanlines.Length} were given.",
                parameterName);
        }
    }
}
