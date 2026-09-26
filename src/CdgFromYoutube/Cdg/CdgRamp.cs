namespace CdgFromYoutube.Cdg;

/// <summary>
/// Three palette entries that shade one lyric color over the black background: the full color, a third of
/// it and two thirds of it, which is what lets the edges of a letter be drawn smooth.
/// </summary>
/// <remarks>
/// The entries sit at palette indices chosen so that the index of two thirds is the exclusive-or of the
/// other two. A tile block of black and the full color, followed by an exclusive-or block of the index of a
/// third, then gives every pixel one of four shades: black stays black or becomes a third, and the full
/// color stays full or becomes two thirds. The first block on its own already shows the letters, a little
/// bolder, so a tile whose second block has to wait for a later frame still reads correctly.
/// </remarks>
/// <param name="Full">The index of the full color.</param>
/// <param name="Third">The index of a third of the full color.</param>
public readonly record struct CdgRamp(byte Full, byte Third)
{
    /// <summary>The index of two thirds of the full color.</summary>
    public byte TwoThirds => (byte)(Full ^ Third);
}
