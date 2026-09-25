namespace CdgFromYoutube.Cdg;

/// <summary>
/// The graphics instructions that the second byte of a packet may hold, in the values the format assigns.
/// </summary>
/// <remarks>
/// Instruction list from "CD+G Revealed" by Jim Bumgardner (https://jbum.com/cdg_revealed.html). The
/// instructions this program does not use are listed so that the values stay meaningful in the code.
/// </remarks>
public enum CdgInstruction : byte
{
    /// <summary>Fills the whole screen with one color.</summary>
    MemoryPreset = 1,

    /// <summary>Fills the border with one color.</summary>
    BorderPreset = 2,

    /// <summary>Draws a tile over whatever is on screen.</summary>
    TileBlock = 6,

    /// <summary>Scrolls the screen, filling the newly revealed area with one color.</summary>
    ScrollPreset = 20,

    /// <summary>Scrolls the screen, wrapping the pixels that scroll off the far edge back in.</summary>
    ScrollCopy = 24,

    /// <summary>Marks one color as transparent.</summary>
    DefineTransparentColor = 28,

    /// <summary>Loads color table entries zero to seven.</summary>
    ColorTableLow = 30,

    /// <summary>Loads color table entries eight to fifteen.</summary>
    ColorTableHigh = 31,

    /// <summary>Draws a tile by exclusive-or-ing it with what is on screen.</summary>
    TileBlockXor = 38,
}
