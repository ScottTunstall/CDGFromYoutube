using CdgFromYoutube.Imaging;

namespace CdgFromYoutube.Cdg;

/// <summary>
/// Reduces frames of packed eight bit color into the two color tiles that a CD+G screen is built from.
/// </summary>
/// <remarks>
/// A tile may only use two of the palette entries, so each tile is rebuilt from the pair that comes closest
/// to its colors. Every pair is scored against the tile's color counts, which costs almost nothing and
/// finds the two entries that span the tile.
/// </remarks>
public sealed class CdgTileEncoder
{
    private readonly CdgPalette _palette;
    private readonly bool _useDither;
    private readonly byte[] _paletteIndices = new byte[CdgFormat.PixelCount];
    private readonly int[] _tileHistogram = new int[CdgFormat.ColorCount];

    /// <summary>
    /// How much a third color has to reduce a tile's error, in squared four bit channel steps summed over
    /// its pixels, before the tile is drawn with a second, exclusive-or packet.
    /// </summary>
    /// <remarks>
    /// Every tile that takes a third color costs two packets instead of one, so the third color is kept
    /// for tiles where a whole group of pixels would otherwise be drawn in the wrong color, such as the
    /// highlighted part of a word, and not spent on a few antialiased edge pixels.
    /// </remarks>
    private const int MinimumThirdColorGain = 400;

    /// <summary>
    /// How much drawing a tile with a ramp of shades has to reduce its error, over the best pair, before
    /// the second packet the ramp costs is paid. Only used when the palette has ramps.
    /// </summary>
    /// <remarks>
    /// Far lower than <see cref="MinimumThirdColorGain"/>, because smoothing edge pixels is the whole point
    /// of a ramp palette. One edge pixel a third of the way to white is 75 away from black, so this spends
    /// the packet on any tile with at least two such pixels, but not on a single stray one.
    /// </remarks>
    private const int MinimumRampGain = 150;

    /// <summary>Creates an encoder that reduces frames to tiles of the given palette.</summary>
    /// <param name="palette">The palette that every tile's two colors are taken from.</param>
    /// <param name="useDither">Whether to mix the two colors of a tile to soften gradients.</param>
    public CdgTileEncoder(CdgPalette palette, bool useDither)
    {
        ArgumentNullException.ThrowIfNull(palette);
        _palette = palette;
        _useDither = useDither;
    }

    /// <summary>Reduces one frame of packed eight bit red, green and blue pixels to tiles.</summary>
    public CdgTileImage Encode(ReadOnlySpan<byte> rgbPixels)
    {
        int requiredBytes = RgbPixelFormat.GetFrameSizeBytes(CdgFormat.Width, CdgFormat.Height);
        if (rgbPixels.Length < requiredBytes)
        {
            throw new ArgumentException(
                $"A frame holds {requiredBytes} bytes, but {rgbPixels.Length} were given.",
                nameof(rgbPixels));
        }

        MapPixelsToPaletteIndices(rgbPixels);

        CdgTileImage image = new();
        image.SetSource(_paletteIndices);
        Span<byte> scanlines = stackalloc byte[CdgFormat.TileScanlineCount];
        Span<byte> xorScanlines = stackalloc byte[CdgFormat.TileScanlineCount];
        for (int row = 0; row < CdgFormat.TileRows; row++)
        {
            for (int column = 0; column < CdgFormat.TileColumns; column++)
            {
                EncodeTile(row, column, scanlines, xorScanlines, out byte color0, out byte color1, out byte xorColor);
                image.SetTile(CdgFormat.GetTileIndex(row, column), color0, color1, scanlines, xorColor, xorScanlines);
            }
        }

        return image;
    }

    private void MapPixelsToPaletteIndices(ReadOnlySpan<byte> rgbPixels)
    {
        int pixel = 0;
        for (int y = 0; y < CdgFormat.Height; y++)
        {
            for (int x = 0; x < CdgFormat.Width; x++)
            {
                int offset = pixel * RgbPixelFormat.BytesPerPixel;
                CdgColor color = CdgColor.FromVideoPixel(
                    rgbPixels[offset],
                    rgbPixels[offset + 1],
                    rgbPixels[offset + 2]);
                if (CdgPaletteBuilder.IsBackground(color))
                {
                    // The background has to be color zero exactly, which players draw as transparent.
                    _paletteIndices[pixel] = CdgPaletteBuilder.BlackColorIndex;
                    pixel++;
                    continue;
                }

                CdgPalette.NearestColors nearest = _palette.FindTwoNearest(color);
                _paletteIndices[pixel] = _useDither
                    ? ChooseDitheredIndex(nearest, x, y)
                    : nearest.NearestIndex;
                pixel++;
            }
        }
    }

    /// <summary>
    /// Chooses between the two entries closest to a color, using the ordered dithering matrix so that a
    /// color between them is drawn as a mix rather than as blocks of the nearer one.
    /// </summary>
    /// <remarks>
    /// The mix follows how much of the way the color lies from one entry to the other: a color that is
    /// exactly one of them never mixes, and a color halfway between the two is split evenly. The distances
    /// are squared distances, which is an approximation that keeps the mix even enough for a gradient.
    /// </remarks>
    private static byte ChooseDitheredIndex(CdgPalette.NearestColors nearest, int x, int y)
    {
        if (nearest.NearestDistance == 0)
        {
            return nearest.NearestIndex;
        }

        int threshold = OrderedDither.GetThreshold(x, y);
        return threshold * (nearest.NearestDistance + nearest.SecondDistance) <
            nearest.NearestDistance * OrderedDither.ThresholdCount
                ? nearest.SecondIndex
                : nearest.NearestIndex;
    }

    private void EncodeTile(
        int row,
        int column,
        Span<byte> scanlines,
        Span<byte> xorScanlines,
        out byte color0,
        out byte color1,
        out byte xorColor)
    {
        int firstX = column * CdgFormat.TileWidth;
        int firstY = row * CdgFormat.TileHeight;
        CountTileColors(firstX, firstY);

        (byte bestFirst, byte bestSecond, long pairError) = FindBestColorPair();
        bool hasThirdColor = TryFindThirdColor(
            pairError,
            out byte first,
            out byte second,
            out byte third,
            out long thirdColorError);

        // A ramp costs the same two packets as a third color, so it only has to beat that on error; against
        // a plain pair it has to earn its second packet.
        long errorToBeat = hasThirdColor ? thirdColorError : pairError - MinimumRampGain;
        if (TryFindRamp(errorToBeat, out CdgRamp ramp))
        {
            color0 = CdgPaletteBuilder.BlackColorIndex;
            color1 = ramp.Full;
            xorColor = ramp.Third;
            BuildRampScanlines(ramp, firstX, firstY, scanlines, xorScanlines);
            return;
        }

        if (hasThirdColor)
        {
            // The pixels of the third color are drawn by the first pass in whichever of the other two is
            // nearer to it, so that the tile still looks right before the second pass flips them.
            bool thirdStartsAsSecond =
                _palette.GetDistanceSquared(third, second) < _palette.GetDistanceSquared(third, first);
            color0 = first;
            color1 = second;
            xorColor = (byte)((thirdStartsAsSecond ? second : first) ^ third);
            BuildScanlines(first, second, third, thirdStartsAsSecond, firstX, firstY, scanlines, xorScanlines);
            return;
        }

        color0 = bestFirst;
        color1 = bestSecond;
        xorColor = 0;
        xorScanlines.Clear();
        BuildScanlines(color0, color1, firstX, firstY, scanlines);
    }

    /// <summary>
    /// Looks for three palette entries that draw the tile so much better than the best pair that the
    /// second packet an exclusive-or pass costs is worth paying.
    /// </summary>
    /// <remarks>
    /// This is what keeps a lyric readable while it is being highlighted. The tile where the highlight
    /// has reached holds the background, the letters not yet sung and the letters already sung; with only
    /// two colors one of those has to go, and it is the highlight, which is nearer the black background
    /// than the white lettering is, so the letters being sung would be drawn as a black block. Only the
    /// entries the tile actually uses are tried, which is a handful, so this costs little.
    /// </remarks>
    private bool TryFindThirdColor(
        long pairError,
        out byte first,
        out byte second,
        out byte third,
        out long error)
    {
        first = 0;
        second = 0;
        third = 0;
        error = long.MaxValue;

        Span<byte> used = stackalloc byte[CdgFormat.ColorCount];
        int usedCount = 0;
        for (int index = 0; index < CdgFormat.ColorCount; index++)
        {
            if (_tileHistogram[index] > 0)
            {
                used[usedCount++] = (byte)index;
            }
        }

        if (usedCount < 3)
        {
            return false;
        }

        long bestError = pairError - MinimumThirdColorGain;
        bool found = false;
        for (int a = 0; a < usedCount; a++)
        {
            for (int b = a + 1; b < usedCount; b++)
            {
                for (int c = b + 1; c < usedCount; c++)
                {
                    long tripleError = GetTripleError(used[a], used[b], used[c]);
                    if (tripleError < bestError)
                    {
                        bestError = tripleError;
                        (first, second, third) = ChooseThirdColor(used[a], used[b], used[c]);
                        found = true;
                    }
                }
            }
        }

        if (found)
        {
            error = bestError;
        }

        return found;
    }

    /// <summary>Looks for the ramp of shades that draws the tile with less error than <paramref name="errorToBeat"/>.</summary>
    private bool TryFindRamp(long errorToBeat, out CdgRamp bestRamp)
    {
        bestRamp = default;
        long bestError = errorToBeat;
        bool found = false;
        foreach (CdgRamp ramp in _palette.Ramps)
        {
            long error = GetRampError(ramp);
            if (error < bestError)
            {
                bestError = error;
                bestRamp = ramp;
                found = true;
            }
        }

        return found;
    }

    /// <summary>The error the tile suffers when it is drawn with black and the three shades of a ramp.</summary>
    private long GetRampError(CdgRamp ramp)
    {
        long error = 0;
        for (int index = 0; index < CdgFormat.ColorCount; index++)
        {
            int count = _tileHistogram[index];
            if (count > 0)
            {
                error += (long)count * GetRampDistance((byte)index, ramp, out _);
            }
        }

        return error;
    }

    /// <summary>Returns how far a palette entry is from the nearest of black and a ramp's shades, and which that is.</summary>
    private int GetRampDistance(byte index, CdgRamp ramp, out byte nearest)
    {
        nearest = CdgPaletteBuilder.BlackColorIndex;
        int best = _palette.GetDistanceSquared(index, nearest);
        foreach (byte shade in (ReadOnlySpan<byte>)[ramp.Full, ramp.TwoThirds, ramp.Third])
        {
            int distance = _palette.GetDistanceSquared(index, shade);
            if (distance < best)
            {
                best = distance;
                nearest = shade;
            }
        }

        return best;
    }

    /// <summary>
    /// Decides which of three entries the exclusive-or pass adds, leaving the other two for the first pass.
    /// </summary>
    /// <remarks>
    /// The first pass can be on screen by itself for a while: when a frame changes more tiles than there
    /// are packets for, the second passes wait for a later frame. So the two entries of the first pass are
    /// the pair that draws the tile best on its own, which keeps a new page of lyrics readable while its
    /// third colors are still being filled in.
    /// </remarks>
    private (byte First, byte Second, byte Third) ChooseThirdColor(byte a, byte b, byte c)
    {
        long withoutA = GetPairError(b, c);
        long withoutB = GetPairError(a, c);
        long withoutC = GetPairError(a, b);
        if (withoutA <= withoutB && withoutA <= withoutC)
        {
            return (b, c, a);
        }

        return withoutB <= withoutC ? (a, c, b) : (a, b, c);
    }

    /// <summary>The error the tile suffers when it is drawn with the given three entries.</summary>
    private long GetTripleError(byte first, byte second, byte third)
    {
        long error = 0;
        for (int index = 0; index < CdgFormat.ColorCount; index++)
        {
            int count = _tileHistogram[index];
            if (count == 0)
            {
                continue;
            }

            int distance = Math.Min(
                _palette.GetDistanceSquared((byte)index, first),
                Math.Min(
                    _palette.GetDistanceSquared((byte)index, second),
                    _palette.GetDistanceSquared((byte)index, third)));
            error += (long)count * distance;
        }

        return error;
    }

    /// <summary>Returns the two palette entries that together come closest to the colors of the tile.</summary>
    /// <remarks>
    /// Choosing the pair by how often an entry occurs takes the middle colors of a tile that covers a
    /// gradient, which is what leaves a flat patch where the picture is smooth. Scoring whole pairs from
    /// the tile's color counts instead finds the two entries that span it, for about the same work.
    /// </remarks>
    private (byte First, byte Second, long Error) FindBestColorPair()
    {
        byte bestFirst = 0;
        byte bestSecond = 0;
        long bestError = long.MaxValue;

        for (int first = 0; first < CdgFormat.ColorCount; first++)
        {
            if (_tileHistogram[first] == 0)
            {
                continue;
            }

            for (int second = first; second < CdgFormat.ColorCount; second++)
            {
                long error = GetPairError((byte)first, (byte)second);
                if (error < bestError)
                {
                    bestError = error;
                    bestFirst = (byte)first;
                    bestSecond = (byte)second;
                }
            }
        }

        return (bestFirst, bestSecond, bestError);
    }

    /// <summary>The error the tile suffers when it is drawn with the given pair of entries.</summary>
    private long GetPairError(byte first, byte second)
    {
        long error = 0;
        for (int index = 0; index < CdgFormat.ColorCount; index++)
        {
            int count = _tileHistogram[index];
            if (count == 0)
            {
                continue;
            }

            int distanceToFirst = _palette.GetDistanceSquared((byte)index, first);
            int distanceToSecond = _palette.GetDistanceSquared((byte)index, second);
            error += (long)count * Math.Min(distanceToFirst, distanceToSecond);
        }

        return error;
    }

    private void CountTileColors(int firstX, int firstY)
    {
        Array.Clear(_tileHistogram);
        for (int y = 0; y < CdgFormat.TileHeight; y++)
        {
            int rowStart = ((firstY + y) * CdgFormat.Width) + firstX;
            for (int x = 0; x < CdgFormat.TileWidth; x++)
            {
                _tileHistogram[_paletteIndices[rowStart + x]]++;
            }
        }
    }

    /// <summary>Writes the twelve scanlines that draw a tile from two palette entries.</summary>
    private void BuildScanlines(byte color0, byte color1, int firstX, int firstY, Span<byte> scanlines)
    {
        for (int y = 0; y < CdgFormat.TileHeight; y++)
        {
            int rowStart = ((firstY + y) * CdgFormat.Width) + firstX;
            byte bits = 0;
            for (int x = 0; x < CdgFormat.TileWidth; x++)
            {
                byte index = _paletteIndices[rowStart + x];
                if (_palette.GetDistanceSquared(index, color1) < _palette.GetDistanceSquared(index, color0))
                {
                    bits |= (byte)(1 << (CdgFormat.TileWidth - 1 - x));
                }
            }

            scanlines[y] = bits;
        }
    }

    /// <summary>
    /// Writes the scanlines that draw a tile from three palette entries: the first pass draws the first and
    /// second, with the pixels of the third drawn as whichever of them it starts as, and the exclusive-or
    /// pass marks the pixels that are flipped from there to the third.
    /// </summary>
    private void BuildScanlines(
        byte first,
        byte second,
        byte third,
        bool thirdStartsAsSecond,
        int firstX,
        int firstY,
        Span<byte> scanlines,
        Span<byte> xorScanlines)
    {
        for (int y = 0; y < CdgFormat.TileHeight; y++)
        {
            int rowStart = ((firstY + y) * CdgFormat.Width) + firstX;
            byte bits = 0;
            byte xorBits = 0;
            for (int x = 0; x < CdgFormat.TileWidth; x++)
            {
                byte index = _paletteIndices[rowStart + x];
                int toFirst = _palette.GetDistanceSquared(index, first);
                int toSecond = _palette.GetDistanceSquared(index, second);
                int toThird = _palette.GetDistanceSquared(index, third);
                byte bit = (byte)(1 << (CdgFormat.TileWidth - 1 - x));
                if (toThird < toFirst && toThird < toSecond)
                {
                    xorBits |= bit;
                    if (thirdStartsAsSecond)
                    {
                        bits |= bit;
                    }
                }
                else if (toSecond < toFirst)
                {
                    bits |= bit;
                }
            }

            scanlines[y] = bits;
            xorScanlines[y] = xorBits;
        }
    }

    /// <summary>
    /// Writes the scanlines that draw a tile in black and the three shades of a ramp. The first pass draws
    /// the full color wherever the pixel is two thirds or full, and black elsewhere; the exclusive-or pass
    /// then turns black into a third, and the full color into two thirds, wherever those are wanted.
    /// </summary>
    private void BuildRampScanlines(
        CdgRamp ramp,
        int firstX,
        int firstY,
        Span<byte> scanlines,
        Span<byte> xorScanlines)
    {
        for (int y = 0; y < CdgFormat.TileHeight; y++)
        {
            int rowStart = ((firstY + y) * CdgFormat.Width) + firstX;
            byte bits = 0;
            byte xorBits = 0;
            for (int x = 0; x < CdgFormat.TileWidth; x++)
            {
                GetRampDistance(_paletteIndices[rowStart + x], ramp, out byte shade);
                byte bit = (byte)(1 << (CdgFormat.TileWidth - 1 - x));
                if (shade == ramp.Full || shade == ramp.TwoThirds)
                {
                    bits |= bit;
                }

                if (shade == ramp.Third || shade == ramp.TwoThirds)
                {
                    xorBits |= bit;
                }
            }

            scanlines[y] = bits;
            xorScanlines[y] = xorBits;
        }
    }
}
