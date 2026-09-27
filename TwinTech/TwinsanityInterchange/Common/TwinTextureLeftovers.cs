using System;

namespace Twinsanity.TwinsanityInterchange.Common;

/// <summary>
/// The parts of a texture's header the game's tools filled with whatever they had in memory
/// </summary>
/// <remarks>
/// Worked out from every texture of the PAL, NTSC and Xbox versions. Textures with other values show fine in game, TT Lab keeps
/// them to write textures back the way they were
/// </remarks>
public class TwinTextureLeftovers
{
    /// <summary>
    /// Default signature of the textures the tools made, the high half is the same for every texture
    /// </summary>
    public const UInt32 SignatureMagic = 0xBBCC0000;

    /// <summary>
    /// Low half of the header's signature, which the tools left uninitialized (0xCDCD where their debug heap had filled it)
    /// </summary>
    public UInt16 SignatureLeftover { get; set; } = 0xCDCD;
    /// <summary>
    /// Number of the texture in the tools' texture table, every texture of a chunk has another one. The textures of PSM files have
    /// addresses of the tools' memory there
    /// </summary>
    public UInt32 ToolSlot { get; set; }
    /// <summary>
    /// 0, 0xF in the Xbox version's chunks, where the tools' debug heap filled it 0xCDCDCDCD
    /// </summary>
    public UInt32 Reserved1 { get; set; }
    /// <summary>
    /// Always 0
    /// </summary>
    public UInt16 Reserved2 { get; set; }
    /// <summary>
    /// What the tools had in memory after the header: 0x1F and an address in the PS2 version's chunks, zeros in its PSM files, and
    /// leftover floats in the Xbox version's
    /// </summary>
    public UInt32[] ToolMemory { get; set; } = new UInt32[8];

    /// <summary>
    /// The leftovers the tools gave the textures of the PS2 version's chunks
    /// </summary>
    public static TwinTextureLeftovers ForChunks()
    {
        return new TwinTextureLeftovers { ToolMemory = new UInt32[] { 0x1F, 0, 0, 0, 0x2059F640, 0, 0, 0 } };
    }
}
