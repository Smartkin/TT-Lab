using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics
{
    public class XboxAnyTexture : BaseTwinItem, ITwinTexture
    {
        static Dictionary<string, TextureDescriptor> TextureDescriptorHelper;

        UInt32 textureType;
        // Left over from the PS2 texture it was converted from, the size the PS2 one had (32 bits per pixel with a header of 0x84 or 0xA0)
        UInt32 sourceLength;
        // Runtime values the game ignores when loading
        UInt32 reserved1;
        UInt32 reserved2;
        UInt16 reserved3;
        const Int32 HeaderLength = 0x88;

        public List<Color> Colors { get; set; }
        public UInt32 HeaderSignature { get; set; }
        public UInt16 ImageWidthPower { get; set; }
        public UInt16 ImageHeightPower { get; set; }
        public Byte MipLevels { get; set; }

        private ITwinTexture.TexturePixelFormat ps2TextureFormat;
        public ITwinTexture.TexturePixelFormat TextureFormat
        {
            get
            {
                if (textureType == 2)
                {
                    return ITwinTexture.TexturePixelFormat.DXT5;
                }

                return ITwinTexture.TexturePixelFormat.Raw;
            }
            set
            {
                if (ITwinTexture.TexturePixelFormat.DXT5 == value)
                {
                    textureType = 2;
                    return;
                }

                textureType = 0;
            }
        }
        public ITwinTexture.TexturePixelFormat DestinationTextureFormat { get; set; }
        public ITwinTexture.TextureColorComponent ColorComponent { get; set; }
        public Byte Reserved1 { get; set; }
        public ITwinTexture.TextureFunction TexFun { get; set; }
        public Byte[] Reserved2 { get; set; }
        public Int32 TextureBasePointer { get; set; }
        public Int32[] MipLevelsTBP { get; set; }
        public Int32 TextureBufferWidth { get; set; }
        public Int32[] MipLevelsTBW { get; set; }
        public Int32 ClutBufferBasePointer { get; set; }
        public Byte[] SizeWords { get; set; }
        public Byte[] ReservedBlocks { get; set; }
        public Byte[] UnusedMetadata { get; set; }
        public Byte[] TextureData { get; set; }

        public TwinTextureLeftovers Leftovers
        {
            get
            {
                var toolMemory = new UInt32[UnusedMetadata.Length / 4];
                Buffer.BlockCopy(UnusedMetadata, 0, toolMemory, 0, toolMemory.Length * 4);
                return new TwinTextureLeftovers
                {
                    SignatureLeftover = (UInt16)HeaderSignature,
                    ToolSlot = reserved1,
                    Reserved1 = reserved2,
                    Reserved2 = reserved3,
                    ToolMemory = toolMemory
                };
            }
            set
            {
                HeaderSignature = (HeaderSignature & 0xFFFF0000) | value.SignatureLeftover;
                reserved1 = value.ToolSlot;
                reserved2 = value.Reserved1;
                reserved3 = value.Reserved2;
                Buffer.BlockCopy(value.ToolMemory, 0, UnusedMetadata, 0, Math.Min(UnusedMetadata.Length, value.ToolMemory.Length * 4));
            }
        }

        public XboxAnyTexture()
        {
            if (TextureDescriptorHelper == null)
            {
                TextureDescriptorHelper = JsonSerializer.Deserialize<Dictionary<string, TextureDescriptor>>(EmbeddedFiles.ReadText("TextureDescriptionHelper.json"));
            }
            UnusedMetadata = new byte[32];
            HeaderSignature = 0xbbcccdcd;
            DestinationTextureFormat = ITwinTexture.TexturePixelFormat.PSMCT32;
            ColorComponent = ITwinTexture.TextureColorComponent.RGBA;
            Reserved1 = 0;
            TextureBasePointer = 0;
            MipLevelsTBP = new int[6];
            TextureBufferWidth = 4;
            MipLevelsTBW = new int[6];
            ClutBufferBasePointer = 0;
            Reserved2 = new byte[2];
            SizeWords = new byte[8] { 0, 0, 0, 0, 224, 0, 2, 0 };
            ReservedBlocks = new byte[2] { 0, 2 };
            UnusedMetadata = new byte[32];
            UnusedMetadata[0] = 31;
            UnusedMetadata[16] = 64;
            UnusedMetadata[17] = 246;
            UnusedMetadata[18] = 89;
            UnusedMetadata[19] = 32;
            reserved2 = 0xF;
        }

        public override Int32 GetLength()
        {
            return HeaderLength + (TextureData != null ? TextureData.Length : 0);
        }

        public override String GetName()
        {
            return $"Texture {id:X}";
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            sourceLength = reader.ReadUInt32();
            HeaderSignature = reader.ReadUInt32();
            ImageWidthPower = reader.ReadUInt16();
            ImageHeightPower = reader.ReadUInt16();
            MipLevels = reader.ReadByte();
            ps2TextureFormat = (ITwinTexture.TexturePixelFormat)reader.ReadByte();
            DestinationTextureFormat = (ITwinTexture.TexturePixelFormat)reader.ReadByte();
            ColorComponent = (ITwinTexture.TextureColorComponent)reader.ReadByte();
            Reserved1 = reader.ReadByte();
            TexFun = (ITwinTexture.TextureFunction)reader.ReadByte();
            Reserved2 = reader.ReadBytes(2);
            TextureBasePointer = reader.ReadInt32();
            MipLevelsTBP = new int[6];
            for (var i = 0; i < 6; ++i)
            {
                MipLevelsTBP[i] = reader.ReadInt32();
            }
            TextureBufferWidth = reader.ReadInt32();
            MipLevelsTBW = new int[6];
            for (var i = 0; i < 6; ++i)
            {
                MipLevelsTBW[i] = reader.ReadInt32();
            }
            ClutBufferBasePointer = reader.ReadInt32();
            SizeWords = reader.ReadBytes(8);
            reserved1 = reader.ReadUInt32();
            reserved2 = reader.ReadUInt32();
            ReservedBlocks = reader.ReadBytes(2);
            reserved3 = reader.ReadUInt16();
            reader.Read(UnusedMetadata, 0, UnusedMetadata.Length);

            // XBox specific, only the largest mip is stored. Textures inside other files don't know their length
            textureType = reader.ReadUInt32();
            TextureData = reader.ReadBytes(length > 0 ? length - HeaderLength : GetDataLength());
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(sourceLength);
            writer.Write(HeaderSignature);
            writer.Write(ImageWidthPower);
            writer.Write(ImageHeightPower);
            writer.Write(MipLevels);
            writer.Write((Byte)ps2TextureFormat);
            writer.Write((Byte)DestinationTextureFormat);
            writer.Write((Byte)ColorComponent);
            writer.Write(Reserved1);
            writer.Write((Byte)TexFun);
            writer.Write(Reserved2);
            writer.Write(TextureBasePointer);
            for (var i = 0; i < 6; ++i)
            {
                writer.Write(MipLevelsTBP[i]);
            }
            writer.Write(TextureBufferWidth);
            for (var i = 0; i < 6; ++i)
            {
                writer.Write(MipLevelsTBW[i]);
            }
            writer.Write(ClutBufferBasePointer);
            writer.Write(SizeWords);
            writer.Write(reserved1);
            writer.Write(reserved2);
            writer.Write(ReservedBlocks);
            writer.Write(reserved3);
            writer.Write(UnusedMetadata);
            writer.Write(textureType);
            writer.Write(TextureData);
        }

        // Uncompressed textures can have 28 bytes after their pixels, the length of the PS2 texture they came from counts them
        private Int32 GetDataLength()
        {
            var width = 1 << ImageWidthPower;
            var height = 1 << ImageHeightPower;
            if (textureType != 0)
            {
                return Math.Max(1, width / 4) * Math.Max(1, height / 4) * 16;
            }

            return Math.Max(width * height * 4, (Int32)sourceLength - 0x84);
        }

        public void CalculateData()
        {
            var width = 1 << ImageWidthPower;
            var height = 1 << ImageHeightPower;
            if (textureType != 0)
            {
                Colors = Dxt5.Decode(TextureData, width, height);
                return;
            }

            // Uncompressed pixels are stored as BGRA
            Colors = new List<Color>(width * height);
            for (var i = 0; i < width * height && i * 4 + 3 < TextureData.Length; i++)
            {
                Colors.Add(new Color(TextureData[i * 4 + 2], TextureData[i * 4 + 1], TextureData[i * 4], TextureData[i * 4 + 3]));
            }
        }

        public void FromBitmap(List<Color> image, Int32 width, ITwinTexture.TextureFunction fun, ITwinTexture.TexturePixelFormat format, bool generateMipmaps = false)
        {
            int height = image.Count / width;
            TexFun = fun;
            // Textures of levels are always compressed, the ones of menus and fonts keep every pixel
            TextureFormat = format == ITwinTexture.TexturePixelFormat.Raw ? ITwinTexture.TexturePixelFormat.Raw : ITwinTexture.TexturePixelFormat.DXT5;
            ps2TextureFormat = format == ITwinTexture.TexturePixelFormat.PSMCT32 ? ITwinTexture.TexturePixelFormat.PSMCT32 : ITwinTexture.TexturePixelFormat.PSMT8;
            TextureBufferWidth = (int)Math.Ceiling(width / 64.0f);
            ImageWidthPower = (ushort)Math.Log2(width);
            ImageHeightPower = (ushort)Math.Log2(height);
            if (width != 256 && generateMipmaps && TextureDescriptorHelper.TryGetValue($"{width}x{height}", out var textureDescriptor))
            {
                ClutBufferBasePointer = textureDescriptor.CBP;
                MipLevelsTBP = textureDescriptor.MipTBP;
                MipLevelsTBW = textureDescriptor.MipTBW;
                MipLevels = (byte)textureDescriptor.MipLevels;
            }
            else
            {
                ClutBufferBasePointer = 0;
                MipLevelsTBP = new Int32[6];
                MipLevelsTBW = new Int32[6];
                MipLevels = 1;
            }
            // The game's textures keep the size of the PS2 texture they were made from, it's written the same way for new ones
            sourceLength = (UInt32)(width * height * 4 + 0x84);
            ReservedBlocks = new Byte[2];
            BitConverter.TryWriteBytes(SizeWords.AsSpan(0, 4), 1U);
            BitConverter.TryWriteBytes(SizeWords.AsSpan(4, 4), sourceLength);
            if (textureType != 0)
            {
                TextureData = Dxt5.Encode(image, width, height);
                return;
            }

            TextureData = new Byte[width * height * 4];
            for (var i = 0; i < width * height; i++)
            {
                TextureData[i * 4] = image[i].B;
                TextureData[i * 4 + 1] = image[i].G;
                TextureData[i * 4 + 2] = image[i].R;
                TextureData[i * 4 + 3] = image[i].A;
            }
        }

    }
}
