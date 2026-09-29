using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Twinsanity.Libraries;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics
{
    public class PS2AnyTexture : BaseTwinItem, ITwinTexture
    {
        static Dictionary<string, TextureDescriptor> TextureDescriptorHelper;
        public List<Color> Colors { get; set; } = new List<Color>();
        public UInt32 HeaderSignature { get; set; }
        public UInt16 ImageWidthPower { get; set; }
        public UInt16 ImageHeightPower { get; set; }
        public Byte MipLevels { get; set; }
        public ITwinTexture.TexturePixelFormat TextureFormat { get; set; }
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
        // Left over from the tools like the unused metadata, see TwinTextureLeftovers
        UInt32 reserved1;
        UInt32 reserved2;
        UInt16 reserved3;

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

        public PS2AnyTexture()
        {
            LoadTextureDescriptors();
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
            SizeWords = new byte[4] { 224, 0, 2, 0 };
            ReservedBlocks = new byte[2] { 0, 2 };
            UnusedMetadata = new byte[32];
            UnusedMetadata[0] = 31;
            UnusedMetadata[16] = 64;
            UnusedMetadata[17] = 246;
            UnusedMetadata[18] = 89;
            UnusedMetadata[19] = 32;
        }

        public override Int32 GetLength()
        {
            return 4 + 96 + UnusedMetadata.Length + (TextureData != null ? TextureData.Length : 0);
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            int dataLen = reader.ReadInt32();
            HeaderSignature = reader.ReadUInt32();
            ImageWidthPower = reader.ReadUInt16();
            ImageHeightPower = reader.ReadUInt16();
            MipLevels = reader.ReadByte();
            TextureFormat = (ITwinTexture.TexturePixelFormat)reader.ReadByte();
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
            reader.ReadInt32(); // CLUT buffer width, always 1 meaning always 64
            SizeWords = reader.ReadBytes(4);
            reserved1 = reader.ReadUInt32();
            reserved2 = reader.ReadUInt32();
            ReservedBlocks = reader.ReadBytes(2);
            reserved3 = reader.ReadUInt16();
            reader.Read(UnusedMetadata, 0, UnusedMetadata.Length);
            TextureData = reader.ReadBytes(dataLen - 96 - UnusedMetadata.Length);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(GetLength() - 4);
            writer.Write(HeaderSignature);
            writer.Write(ImageWidthPower);
            writer.Write(ImageHeightPower);
            writer.Write(MipLevels);
            writer.Write((Byte)TextureFormat);
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
            writer.Write(1); // CLUT buffer width
            writer.Write(SizeWords);
            writer.Write(reserved1);
            writer.Write(reserved2);
            writer.Write(ReservedBlocks);
            writer.Write(reserved3);
            writer.Write(UnusedMetadata);
            writer.Write(TextureData);
        }

        public void CalculateData()
        {
            var interpreter = VIFInterpreter.InterpretCode(TextureData);
            var data = interpreter.GetGifMem();
            Colors.Clear();
            switch (TextureFormat)
            {
                case ITwinTexture.TexturePixelFormat.PSMCT32:
                    EzSwizzle.TagToColors(data[1], Colors);
                    foreach (var c in Colors)
                    {
                        c.ScaleAlphaUp();
                    }
                    break;
                case ITwinTexture.TexturePixelFormat.PSMT8:
                    var gifData = EzSwizzle.TagToBytes(data[1]);
                    var rrw = (int)((data[0].Data[1].Output >> 0) & 0xFFFFFFFF);
                    var rrh = (int)((data[0].Data[1].Output >> 32) & 0xFFFFFFFF);
                    var width = (int)(Math.Pow(2, ImageWidthPower));
                    var height = (int)(Math.Pow(2, ImageHeightPower));
                    var rawTextureData = EzSwizzle.writeTexPSMCT32(0, 1, 0, 0, rrw, rrh, gifData);
                    var texData = EzSwizzle.readTexPSMT8(0, TextureBufferWidth, 0, 0, width, height, rawTextureData, false);
                    var paletteData = EzSwizzle.readTexPSMCT32(ClutBufferBasePointer, 1, 0, 0, 16, 16, rawTextureData, false);
                    var palette = EzSwizzle.BytesToColors(paletteData);
                    for (var i = 0; i < 8; i++)
                    {
                        for (var j = 8; j < 16; j++)
                        {
                            Color tmp = palette[j + i * 32];
                            palette[j + i * 32] = palette[j + i * 32 + 8];
                            palette[j + i * 32 + 8] = tmp;
                        }
                    }
                    foreach (var c in palette)
                    {
                        c.ScaleAlphaUp();
                    }
                    
                    var pixels = width * height;
                    for (var i = 0; i < pixels; ++i)
                    {
                        Colors.Add(palette[texData[i]]);
                    }
                    break;
            }
        }

        private static void LoadTextureDescriptors()
        {
            if (TextureDescriptorHelper != null)
            {
                return;
            }

            string codeBase = Assembly.GetExecutingAssembly().Location;
            UriBuilder uri = new($"file://{codeBase}");
            string path = Uri.UnescapeDataString(uri.Path);
            using FileStream stream = new(Path.Combine(Path.GetDirectoryName(path), "TextureDescriptionHelper.json"), FileMode.Open, FileAccess.Read);
            using StreamReader reader = new(stream);
            TextureDescriptorHelper = JsonSerializer.Deserialize<Dictionary<string, TextureDescriptor>>(reader.ReadToEnd());
        }

        /// <summary>
        /// Whether the game's tools laid out palette textures of the size, with their mips. Textures of other sizes only go in with
        /// every color and without mips
        /// </summary>
        public static Boolean HasPaletteLayout(Int32 width, Int32 height)
        {
            LoadTextureDescriptors();
            return TextureDescriptorHelper.ContainsKey($"{width}x{height}");
        }

        public void FromBitmap(List<Color> image, Int32 width, ITwinTexture.TextureFunction fun, ITwinTexture.TexturePixelFormat format, bool generateMipmaps = false)
        {
            int height = image.Count / width;
            TexFun = fun;
            TextureFormat = format;
            TextureBufferWidth = (int)Math.Ceiling(width / 64.0f);
            ImageWidthPower = (ushort)Math.Log2(width);
            ImageHeightPower = (ushort)Math.Log2(height);
            if (width != 256 && generateMipmaps)
            {
                TextureDescriptor textureDescriptor = TextureDescriptorHelper[$"{width}x{height}"];
                ClutBufferBasePointer = textureDescriptor.CBP;
                MipLevelsTBP = (Int32[])textureDescriptor.MipTBP.Clone();
                MipLevelsTBW = (Int32[])textureDescriptor.MipTBW.Clone();
                MipLevels = (byte)textureDescriptor.MipLevels;
            }
            else
            {
                ClutBufferBasePointer = 0;
                MipLevelsTBP = new Int32[6];
                MipLevelsTBW = new Int32[6];
                MipLevels = 1;
            }

            if (format == ITwinTexture.TexturePixelFormat.PSMT8 && MipLevels == 1)
            {
                // Without mips the palette goes where the game's textures of the size have it, the descriptors lay textures out with their mips
                ClutBufferBasePointer = UnmippedClutPointers.TryGetValue($"{width}x{height}", out var pointer) ? pointer : TextureDescriptorHelper[$"{width}x{height}"].CBP;
            }

            GIFTag headerTag = new GIFTag();
            headerTag.REGS = new REGSEnum[16];
            headerTag.REGS[0] = REGSEnum.ApD;
            headerTag.NLOOP = 3;
            headerTag.NREG = 1;
            headerTag.FLG = GIFModeEnum.PACKED;
            headerTag.Data = new List<RegOutput>();
            RegOutput head1 = new RegOutput();
            head1.REG = REGSEnum.ApD;
            head1.Address = 81;
            RegOutput head2 = new RegOutput();
            head2.REG = REGSEnum.ApD;
            head2.Address = 82;
            RegOutput head3 = new RegOutput();
            head3.REG = REGSEnum.ApD;
            head3.Address = 83;
            headerTag.Data.Add(head1);
            headerTag.Data.Add(head2);
            headerTag.Data.Add(head3);
            GIFTag tag;
            if (format == ITwinTexture.TexturePixelFormat.PSMCT32)
            {
                foreach (var c in image)
                {
                    c.ScaleAlphaDown();
                }
                tag = EzSwizzle.ColorsToTag(image);
                // The pixels get uploaded as they are, the transfer covers the whole image
                head2.Output = ((UInt64)height << 32) | (UInt64)width;
                SetMemorySize(width * height);
            }
            else
            {
                var textureData = new byte[width * height];
                var paletteData = new byte[256 * 4];
                var palette = new List<Color>(256);
                // Colors are equal by their ARGB value, the first occurrence decides a color's index in the palette
                var paletteIndices = new Dictionary<UInt32, Int32>(256);
                
                foreach (var c in image)
                {
                    var argb = c.ToARGB();
                    if (!paletteIndices.ContainsKey(argb))
                    {
                        paletteIndices.Add(argb, palette.Count);
                        palette.Add(c);
                    }
                }
                while (palette.Count < 256)
                {
                    palette.Add(new Color());
                }

                var useQuantizer = palette.Count > 256;
                if (useQuantizer)
                {
                    palette = ImageQuantizer.Quantize(image);
                }
                
                var index = 0;
                foreach (var c in image)
                {
                    textureData[index] = useQuantizer ? ImageQuantizer.PaletteIndex(c, palette) : (byte)paletteIndices[c.ToARGB()];
                    ++index;
                }
                foreach (var c in palette)
                {
                    c.ScaleAlphaDown();
                }
                for (int i = 0; i < 8; i++)
                {
                    for (int j = 8; j < 16; j++)
                    {
                        var srcIndex = j + i * 32 + 8;
                        var dstIndex = j + i * 32;
                        Color tmp = palette[srcIndex];
                        palette[srcIndex] = palette[dstIndex];
                        palette[dstIndex] = tmp;
                    }
                }
                index = 0;
                foreach (var c in palette)
                {
                    EzSwizzle.ColorsToByte(c, paletteData, index);
                    ++index;
                }

                TextureDescriptor textureDescriptor = TextureDescriptorHelper[$"{width}x{height}"];
                ulong high = (ulong)textureDescriptor.RRH;
                ulong low = (ulong)textureDescriptor.RRW;
                head2.Output = (high << 32) | (low);
                SetMemorySize(textureDescriptor.RRW * textureDescriptor.RRH);
                byte[] rawTextureData = new byte[textureDescriptor.RRH * 256];
                Array.Fill<byte>(rawTextureData, 0xFF);

                EzSwizzle.writeTexPSMT8To(0, TextureBufferWidth, 0, 0, width, height, textureData, rawTextureData);
                var prevData = textureData;
                var mipWidth = width;
                var mipHeight = height;
                for (var i = 1; i < MipLevels; ++i)
                {
                    mipWidth /= 2;
                    mipHeight /= 2;
                    var mipData = new byte[mipWidth * mipHeight];
                    for (var y = 0; y < mipHeight; ++y)
                    {
                        for (var x = 0; x < mipWidth; ++x)
                        {
                            var prevWidth = mipWidth * 2;
                            var srcX = x * 2;
                            var srcY = y * 2;
                            mipData[x + y * mipWidth] = prevData[srcX + srcY * prevWidth];
                        }
                    }
                    EzSwizzle.writeTexPSMT8To(MipLevelsTBP[i - 1], MipLevelsTBW[i - 1], 0, 0, mipWidth, mipHeight, mipData, rawTextureData);
                    prevData = mipData;
                }
                EzSwizzle.writeTexPSMCT32To(ClutBufferBasePointer, 1, 0, 0, 16, 16, paletteData, rawTextureData);
                byte[] gifData = EzSwizzle.readTexPSMCT32(0, 1, 0, 0, textureDescriptor.RRW, textureDescriptor.RRH, rawTextureData);
                tag = EzSwizzle.ColorsToTag(EzSwizzle.BytesToColors(gifData));
            }

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            {
                var QWC = (UInt64)headerTag.GetLength() + (UInt64)tag.GetLength() + 2;
                UInt64 low = QWC;
                low |= (UInt64)6 << 28;
                writer.Write(low);
                VIFCode code1 = new VIFCode();
                code1.OP = VIFCodeEnum.NOP;
                code1.Write(writer);
                VIFCode code2 = new VIFCode();
                code2.OP = VIFCodeEnum.DIRECT;
                code2.Immediate = (ushort)QWC;
                code2.Write(writer);
                headerTag.Write(writer);
                tag.Write(writer);
                writer.Flush();
                TextureData = stream.ToArray();
            }
        }

        // The game's textures without mips put their palette right after their pixels
        private static readonly Dictionary<String, Int32> UnmippedClutPointers = new()
        {
            { "32x8", 8 },
            { "32x32", 8 },
            { "32x64", 4 },
            { "64x64", 16 },
            { "128x128", 64 },
            { "128x256", 128 }
        };

        // The header has the texture's size twice, in blocks of 64 of the 32 bit pixels it uploads
        private void SetMemorySize(Int32 uploadedPixels)
        {
            var blocks = uploadedPixels / 64;
            SizeWords = new Byte[] { 0xE0, (Byte)blocks, (Byte)(blocks >> 8), 0 };
            ReservedBlocks = new Byte[] { (Byte)blocks, (Byte)(blocks >> 8) };
        }

        public override String GetName()
        {
            return $"Texture {id:X}";
        }

        public struct TextureDescriptor
        {
            public Int32 MipLevels { get; set; }
            public Int32 CBP { get; set; }
            public Int32 RRW { get; set; }
            public Int32 RRH { get; set; }
            public Int32[] MipTBP { get; set; }
            public Int32[] MipTBW { get; set; }

        }
    }
}
