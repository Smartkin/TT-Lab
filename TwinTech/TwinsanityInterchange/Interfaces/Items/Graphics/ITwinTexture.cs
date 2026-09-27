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
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Byte UnkByte { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        TextureFunction TexFun { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Byte[] UnkBytes1 { get; set; }
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
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Byte[] UnkBytes2 { get; set; }
        /// <summary>
        /// DO NOT EDIT. Prefer using FromBitmap function
        /// </summary>
        Byte[] UnkBytes3 { get; set; }
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
