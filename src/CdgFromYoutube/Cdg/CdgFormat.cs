namespace CdgFromYoutube.Cdg;

/// <summary>
/// The fixed dimensions, colors and timing of the CD+G ("CD Graphics") format that karaoke players
/// draw on screen while an MP3 track plays.
/// </summary>
/// <remarks>
/// The numbers come from "CD+G Revealed" by Jim Bumgardner (https://jbum.com/cdg_revealed.html) and were
/// checked against the CD+G decoder in VLC (modules/codec/cdg.c), which agrees on the tile size, the
/// tile addressing and the color table layout.
/// </remarks>
public static class CdgFormat
{
    /// <summary>The width of the raster in pixels.</summary>
    public const int Width = 300;

    /// <summary>The height of the raster in pixels.</summary>
    public const int Height = 216;

    /// <summary>The number of pixels on the raster.</summary>
    public const int PixelCount = Width * Height;

    /// <summary>The width in pixels of one two color tile.</summary>
    public const int TileWidth = 6;

    /// <summary>The height in pixels of one two color tile.</summary>
    public const int TileHeight = 12;

    /// <summary>The number of tiles across the raster.</summary>
    public const int TileColumns = Width / TileWidth;

    /// <summary>The number of tiles down the raster.</summary>
    public const int TileRows = Height / TileHeight;

    /// <summary>The number of tiles that cover the whole raster.</summary>
    public const int TileCount = TileColumns * TileRows;

    /// <summary>The number of scanlines in a tile, each holding six one bit pixels.</summary>
    public const int TileScanlineCount = TileHeight;

    /// <summary>The number of colors a tile may use.</summary>
    public const int TileColorCount = 2;

    /// <summary>The number of entries in the color table.</summary>
    public const int ColorCount = 16;

    /// <summary>The number of bits used by each of the red, green and blue channels of a color table entry.</summary>
    public const int ColorBitsPerChannel = 4;

    /// <summary>The number of bits needed to name a color as packed red, green and blue nibbles.</summary>
    public const int PackedColorBits = ColorBitsPerChannel * RgbChannelCount;

    /// <summary>The number of color channels in a color table entry.</summary>
    public const int RgbChannelCount = 3;

    /// <summary>The size in bytes of a single packet.</summary>
    public const int PacketSizeBytes = 24;

    /// <summary>The number of packets a player consumes per second: seventy five sectors of four packets.</summary>
    public const int PacketsPerSecond = 300;

    /// <summary>The width of the border around the area that all players are guaranteed to show.</summary>
    public const int BorderWidth = 6;

    /// <summary>The height of the border around the area that all players are guaranteed to show.</summary>
    public const int BorderHeight = 12;

    /// <summary>The width of the area that all players are guaranteed to show.</summary>
    public const int SafeWidth = Width - (2 * BorderWidth);

    /// <summary>The height of the area that all players are guaranteed to show.</summary>
    public const int SafeHeight = Height - (2 * BorderHeight);

    /// <summary>Returns the index of a tile within the raster, counting left to right then top to bottom.</summary>
    public static int GetTileIndex(int row, int column) => (row * TileColumns) + column;
}
