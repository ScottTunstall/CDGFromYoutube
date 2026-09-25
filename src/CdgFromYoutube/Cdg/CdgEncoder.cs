namespace CdgFromYoutube.Cdg;

/// <summary>
/// Places tile images on a CD+G timeline, writing only the tiles that differ from what is already on screen.
/// </summary>
/// <remarks>
/// A player consumes exactly <see cref="CdgFormat.PacketsPerSecond"/> packets per second, so replacing the
/// whole screen of <see cref="CdgFormat.TileCount"/> tiles takes three seconds. A frame is written at the
/// packet index that matches its timestamp, padded with no-operation packets when it arrives early, and
/// dropped when the packets before its timestamp have already been spent on earlier frames.
/// </remarks>
public sealed class CdgEncoder
{
    private readonly CdgWriter _writer;
    private readonly CdgPalette _palette;
    private readonly CdgTileImage _screen = new();
    private readonly long _packetLimit;
    private bool _hasWrittenFirstFrame;
    private CdgTileImage? _pendingFrame;
    private TimeSpan _pendingTimestamp;
    private int _heldBackInARow;

    /// <summary>
    /// The fewest tiles a frame has to clear, with nothing taking their place, before it counts as a state
    /// the picture is passing through rather than the one it settles on.
    /// </summary>
    /// <remarks>
    /// Deliberately small. A single lyric line is only a handful of tiles, so a threshold measured in
    /// fractions of the screen would never see one, and it is exactly those wipes that leave a line's worth
    /// of black on screen for a second or more.
    /// </remarks>
    private const int ClearedTileCount = 8;

    /// <summary>
    /// How close to black a palette entry has to be before it counts as the background rather than as
    /// something the picture is showing.
    /// </summary>
    private const int DarkColorDistance = 9;

    /// <summary>
    /// How many frames may be passed over one after another before the picture is drawn regardless.
    /// </summary>
    /// <remarks>
    /// A scene that genuinely goes dark still has to be drawn, or the screen would freeze on whatever it
    /// showed last, but the limit has to clear the longest wipe a lyric area gets: this track leaves the
    /// lyric area empty for about a second between lines, which is fifteen frames, so a limit of six let
    /// every one of those wipes through and put the black on screen after all.
    /// </remarks>
    private const int MaximumHeldBackInARow = 30;

    /// <summary>Creates an encoder for a track of the given length.</summary>
    /// <param name="writer">The stream of packets to write to.</param>
    /// <param name="palette">The palette whose colors the tile images refer to.</param>
    /// <param name="duration">The length of the audio the graphics are played against.</param>
    public CdgEncoder(CdgWriter writer, CdgPalette palette, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        _writer = writer;
        _palette = palette;
        _packetLimit = (long)Math.Ceiling(duration.TotalSeconds * CdgFormat.PacketsPerSecond);
    }

    /// <summary>The number of packets the finished track holds.</summary>
    public long PacketLimit => _packetLimit;

    /// <summary>The number of packets written so far.</summary>
    public long PacketsWritten => _writer.PacketsWritten;

    /// <summary>The number of frames that were drawn.</summary>
    public int FramesWritten { get; private set; }

    /// <summary>The number of frames that were dropped because the screen could not be redrawn in time.</summary>
    public int FramesDropped { get; private set; }

    /// <summary>
    /// The number of frames that were passed over because the picture was only passing through them.
    /// </summary>
    public int FramesHeldBack { get; private set; }

    /// <summary>
    /// Clears the screen to black, loads the color table and clears the border to black. This must be the
    /// first thing written, because the tiles of the first frame refer to the colors it loads.
    /// </summary>
    public void WritePrologue()
    {
        _writer.WriteMemoryPreset(CdgPaletteBuilder.BlackColorIndex, repeatCount: 0);
        _writer.WriteMemoryPreset(CdgPaletteBuilder.BlackColorIndex, repeatCount: 1);
        _writer.WriteColorTableLow(_palette.Colors[..CdgWriter.ColorTableEntriesPerPacket]);
        _writer.WriteColorTableHigh(_palette.Colors[CdgWriter.ColorTableEntriesPerPacket..]);
        _writer.WriteBorderPreset(CdgPaletteBuilder.BlackColorIndex);
    }

    /// <summary>Offers a frame to the timeline, drawing the one held back before it.</summary>
    /// <returns><see langword="true"/> when a frame was drawn.</returns>
    /// <remarks>
    /// Each frame is held back for one step so that the next one can say whether it was a state the picture
    /// settled on. Karaoke videos clear the lyric area before drawing the next verse, and that cleared state
    /// exists for a fraction of a frame; drawing it would cost a whole screen of packets and leave the
    /// picture black for seconds afterwards, because nothing would be left to draw the verse with.
    /// </remarks>
    public bool WriteFrame(TimeSpan timestamp, CdgTileImage frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        bool drewPreviousFrame = false;
        if (_pendingFrame is not null)
        {
            if (IsMostlyClearing(_pendingFrame))
            {
                FramesHeldBack++;
                _heldBackInARow++;
            }
            else
            {
                drewPreviousFrame = DrawFrame(_pendingTimestamp, _pendingFrame, GetPacketIndex(timestamp));
                _heldBackInARow = 0;
            }
        }

        _pendingFrame = frame;
        _pendingTimestamp = timestamp;
        return drewPreviousFrame;
    }

    /// <summary>
    /// Returns whether the held back frame is a state the picture is only passing through, which is what a
    /// lyric line being wiped before the next one is drawn looks like: more of the screen is taken away than
    /// is put back.
    /// </summary>
    /// <remarks>
    /// Drawing such a frame spends packets on a state the picture is about to leave, which leaves the lyric
    /// area black until the budget has recovered enough to draw the next line. Passing it over keeps the
    /// line that is already on screen until the next one is ready, which is what a singer needs to see.
    /// </remarks>
    private bool IsMostlyClearing(CdgTileImage heldBack)
    {
        if (_heldBackInARow >= MaximumHeldBackInARow)
        {
            return false;
        }

        int cleared = 0;
        int written = 0;
        for (int tileIndex = 0; tileIndex < CdgFormat.TileCount; tileIndex++)
        {
            if (_screen.TileEquals(tileIndex, heldBack))
            {
                continue;
            }

            byte color0 = heldBack.GetColor0(tileIndex);
            byte color1 = heldBack.GetColor1(tileIndex);
            byte third = (byte)(color0 ^ heldBack.GetXorColor(tileIndex));
            if (IsDark(color0) && IsDark(color1) && IsDark(third))
            {
                cleared++;
            }
            else
            {
                written++;
            }
        }

        return cleared >= ClearedTileCount && cleared > written;
    }

    /// <summary>
    /// Returns whether a palette entry is dark enough to be the background rather than the lettering.
    /// </summary>
    /// <remarks>
    /// It cannot be a test for the reserved black alone. The background is drawn from whichever near black
    /// entry the palette holds, so a lyric line being wiped lands a shade or two away from pure black, and a
    /// test for pure black would count every wipe as the picture putting something back.
    /// </remarks>
    private bool IsDark(byte colorIndex) =>
        _palette[colorIndex].DistanceSquared(CdgColor.Black) <= DarkColorDistance;

    /// <summary>Draws a frame, if the packets before its timestamp can pay for the tiles it changes.</summary>
    /// <param name="timestamp">When the frame is shown.</param>
    /// <param name="frame">The frame to draw.</param>
    /// <param name="nextFramePacketIndex">
    /// Where the next frame is due, which is how far the optional third color passes may run.
    /// </param>
    private bool DrawFrame(TimeSpan timestamp, CdgTileImage frame, long nextFramePacketIndex)
    {
        long framePacketIndex = GetPacketIndex(timestamp);
        if (framePacketIndex >= _packetLimit)
        {
            FramesDropped++;
            return false;
        }

        // The prologue has already claimed the packets at the start of the track, so a strict budget test
        // would reject the very first frame and leave the screen blank. It is always allowed through.
        if (_hasWrittenFirstFrame && _writer.PacketsWritten > framePacketIndex)
        {
            FramesDropped++;
            return false;
        }

        _hasWrittenFirstFrame = true;
        PadUntil(framePacketIndex);
        WriteChangedTiles(frame, nextFramePacketIndex);
        FramesWritten++;
        return true;
    }

    /// <summary>Draws the frame that was still held back, then fills the rest of the track with packets
    /// that carry no command.</summary>
    public void WriteEpilogue()
    {
        if (_pendingFrame is not null)
        {
            DrawFrame(_pendingTimestamp, _pendingFrame, _packetLimit);
            _pendingFrame = null;
        }

        while (_writer.PacketsWritten < _packetLimit)
        {
            _writer.WriteNoOperation();
        }
    }

    private static long GetPacketIndex(TimeSpan timestamp) =>
        (long)(timestamp.TotalSeconds * CdgFormat.PacketsPerSecond);

    private void PadUntil(long packetIndex)
    {
        while (_writer.PacketsWritten < packetIndex && _writer.PacketsWritten < _packetLimit)
        {
            _writer.WriteNoOperation();
        }
    }

    /// <summary>
    /// Writes the tiles of a frame that differ from the screen: first the two color pass of every changed
    /// tile, then as many of the exclusive-or passes that add a third color as fit before the deadline.
    /// </summary>
    /// <remarks>
    /// A new page of lyrics has a third color along the antialiased edge of almost every letter, so drawing
    /// both passes tile by tile would nearly double what the page costs and keep it off screen for longer.
    /// Drawing every tile's first pass before any second pass gets the whole page up at the old cost; a
    /// second pass that misses the deadline is left off the screen, and a later frame that still wants it
    /// only has to pay the one packet it costs.
    /// </remarks>
    private void WriteChangedTiles(CdgTileImage frame, long deadline)
    {
        List<int> tilesWantingXorPass = [];
        for (int tileIndex = 0; tileIndex < CdgFormat.TileCount; tileIndex++)
        {
            if (_writer.PacketsWritten >= _packetLimit)
            {
                return;
            }

            if (_screen.TileEquals(tileIndex, frame))
            {
                continue;
            }

            bool hasFirstPass = !_screen.HasXorPass(tileIndex) &&
                _screen.TileEquals(
                    tileIndex,
                    frame.GetColor0(tileIndex),
                    frame.GetColor1(tileIndex),
                    frame.GetScanlines(tileIndex));
            if (!hasFirstPass)
            {
                (int row, int column) = Math.DivRem(tileIndex, CdgFormat.TileColumns);
                _writer.WriteTileBlock(
                    row,
                    column,
                    frame.GetColor0(tileIndex),
                    frame.GetColor1(tileIndex),
                    frame.GetScanlines(tileIndex));
                _screen.SetTile(
                    tileIndex,
                    frame.GetColor0(tileIndex),
                    frame.GetColor1(tileIndex),
                    frame.GetScanlines(tileIndex));
            }

            if (frame.HasXorPass(tileIndex))
            {
                tilesWantingXorPass.Add(tileIndex);
            }
        }

        // The second pass flips the pixels that take the tile's third color from the first color, which
        // the first pass drew them in, to the third.
        long xorDeadline = Math.Min(deadline, _packetLimit);
        foreach (int tileIndex in tilesWantingXorPass)
        {
            if (_writer.PacketsWritten >= xorDeadline)
            {
                return;
            }

            (int row, int column) = Math.DivRem(tileIndex, CdgFormat.TileColumns);
            _writer.WriteTileBlockXor(row, column, 0, frame.GetXorColor(tileIndex), frame.GetXorScanlines(tileIndex));
            _screen.CopyTileFrom(frame, tileIndex);
        }
    }
}
