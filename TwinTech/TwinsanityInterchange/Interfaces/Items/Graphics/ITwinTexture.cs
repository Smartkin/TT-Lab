using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items
{
    public interface ITwinTexture : ITwinItem
    {
        /// <summary>
        /// Color of each pixel
        /// </summary>
        List<Color> Colors { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        UInt32 HeaderSignature { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        UInt16 ImageWidthPower { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        UInt16 ImageHeightPower { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Byte MipLevels { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        TexturePixelFormat TextureFormat { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        TexturePixelFormat DestinationTextureFormat { get; set; }
        /// <summary>
        /// Whether RGB or RBGA is used for the texture's pixels
        /// </summary>
        TextureColorComponent ColorComponent { get; set; }
        /// <summary>
        /// The header's byte after the color component, 0 and never read by the game
        /// </summary>
        Byte Reserved1 { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        TextureFunction TexFun { get; set; }
        /// <summary>
        /// The two header bytes after the texture function, 0 and never read by the game
        /// </summary>
        Byte[] Reserved2 { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Int32 TextureBasePointer { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Int32[] MipLevelsTBP { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Int32 TextureBufferWidth { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Int32[] MipLevelsTBW { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Int32 ClutBufferBasePointer { get; set; }
        /// <summary>
        /// The word after the palette's buffer width: 0xE0 then the texture's size in blocks of 64 uploaded pixels, which the game
        /// reserves GS memory by (the Xbox version's textures keep 1 and the length of the PS2 texture they were made from
        /// instead). Set by FromBitmap
        /// </summary>
        Byte[] SizeWords { get; set; }
        /// <summary>
        /// The size in blocks once more, 0 on the textures every chunk has (Crash's) which don't reserve memory. Set by FromBitmap
        /// </summary>
        Byte[] ReservedBlocks { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Byte[] UnusedMetadata { get; set; }
        /// <summary>
        /// What the game's tools left in the header (the signature's low half, the reserved words and the unused metadata), for writing a texture back the way it was read
        /// </summary>
        TwinTextureLeftovers Leftovers { get; set; }
        /// <summary>
        /// Texture's raw compressed data. DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Byte[] TextureData { get; set; }

        /// <summary>
        /// Converts compressed texture data to its bitmap variant
        /// </summary>
        void CalculateData();
        /// <summary>
        /// The texture's levels the way the game draws them, each a list of its pixels: the picture, then the smaller versions the PS2's
        /// palette textures have for the distance (the mips), every level with the colors of the one palette. Other textures have one level
        /// </summary>
        List<List<Color>> DecodeLevels();
        /// <summary>
        /// Converts bitmap to compressed texture data
        /// </summary>
        /// <param name="image">Pixel colors</param>
        /// <param name="width">Width of the image (height is calculated automatically)</param>
        /// <param name="fun">GS function to use when rendering the texture</param>
        /// <param name="format">Texture's pixel format</param>
        /// <param name="generateMipmaps">Generate mipmaps for the texture</param>
        void FromBitmap(List<Color> image, Int32 width, TextureFunction fun, TexturePixelFormat format, bool generateMipmaps = false);

        #region Enums
        enum TexturePixelFormat
        {
            PSMCT32 = 0b000000,
            PSMCT24 = 0b000001,
            PSMCT16 = 0b000010,
            PSMCT16S = 0b001010,
            PSMT8 = 0b010011,
            PSMT4 = 0b010100,
            PSMT8H = 0b011011,
            PSMT4HL = 0b100100,
            PSMT4HH = 0b101100,
            PSMZ32 = 0b110000,
            PSMZ24 = 0b110001,
            PSMZ16 = 0b110010,
            PSMZ16S = 0b111010,
            // XBox specific
            DXT5 = 0xb111110,
            Raw = 0xb111111,
        }
        enum TextureColorComponent
        {
            RGB = 0,
            RGBA = 1
        }
        enum TextureFunction
        {
            MODULATE = 0b00,
            DECAL = 0b01,
            HIGHLIGHT = 0b10,
            HIGHLIGHT2 = 0b11
        }
        #endregion
    }
}
