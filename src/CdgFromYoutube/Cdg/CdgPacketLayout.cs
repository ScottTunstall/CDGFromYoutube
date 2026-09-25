namespace CdgFromYoutube.Cdg;

/// <summary>
/// The byte offsets inside the twenty four byte packet that carries CD+G graphics data.
/// </summary>
/// <remarks>
/// A packet is laid out as command, instruction, two parity Q bytes, sixteen data bytes and four parity P
/// bytes. See "CD+G Revealed" by Jim Bumgardner (https://jbum.com/cdg_revealed.html); the layout matches
/// VLC's CDG decoder, which reads the instruction from byte one and the data from byte four.
/// </remarks>
public static class CdgPacketLayout
{
    /// <summary>The value the command byte holds when the packet carries graphics instructions.</summary>
    public const byte GraphicsCommand = 0x09;

    /// <summary>The offset of the command byte.</summary>
    public const int CommandOffset = 0;

    /// <summary>The offset of the instruction byte.</summary>
    public const int InstructionOffset = 1;

    /// <summary>The offset of the first of the two parity Q bytes.</summary>
    public const int ParityQOffset = 2;

    /// <summary>The number of parity Q bytes.</summary>
    public const int ParityQBytes = 2;

    /// <summary>The offset of the sixteen byte data field.</summary>
    public const int DataOffset = ParityQOffset + ParityQBytes;

    /// <summary>The number of bytes in the data field.</summary>
    public const int DataSizeBytes = 16;

    /// <summary>The offset of the first of the four parity P bytes.</summary>
    public const int ParityPOffset = DataOffset + DataSizeBytes;

    /// <summary>The number of parity P bytes.</summary>
    public const int ParityPBytes = 4;
}
