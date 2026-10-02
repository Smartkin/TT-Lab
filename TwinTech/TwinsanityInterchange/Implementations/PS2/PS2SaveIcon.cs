using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Implementations.Base;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2
{
    /// <summary>
    /// A corner of a save icon's triangle: its position in every shape and its normal (X, Y, Z in 4096ths with Y going down, and a word the
    /// tools wrote 0 into), its UV in 4096ths and its color, RGBA from the lowest byte
    /// </summary>
    public class SaveIconVertex
    {
        public SaveIconVertex()
        {
            Positions = new Int16[4];
            Normal = new Int16[4];
        }

        /// <summary>
        /// 4 values for every shape
        /// </summary>
        public Int16[] Positions { get; set; }
        public Int16[] Normal { get; set; }
        public Int16 U { get; set; }
        public Int16 V { get; set; }
        public UInt32 Color { get; set; }
    }

    /// <summary>
    /// A key of a shape's weight over the animation
    /// </summary>
    public struct SaveIconKey : IEquatable<SaveIconKey>
    {
        public SaveIconKey(Single time, Single value)
        {
            Time = time;
            Value = value;
        }

        public Single Time { get; set; }
        public Single Value { get; set; }

        public Boolean Equals(SaveIconKey other)
        {
            return BitConverter.SingleToInt32Bits(Time) == BitConverter.SingleToInt32Bits(other.Time) && BitConverter.SingleToInt32Bits(Value) == BitConverter.SingleToInt32Bits(other.Value);
        }

        public override Boolean Equals(Object obj) => obj is SaveIconKey other && Equals(other);

        public override Int32 GetHashCode() => HashCode.Combine(BitConverter.SingleToInt32Bits(Time), BitConverter.SingleToInt32Bits(Value));
    }

    /// <summary>
    /// One shape's weight over the animation, which blends the shapes by their weights
    /// </summary>
    public class SaveIconFrame
    {
        public SaveIconFrame()
        {
            Keys = new List<SaveIconKey>();
        }

        public UInt32 Shape { get; set; }
        public List<SaveIconKey> Keys { get; set; }
    }

    /// <summary>
    /// A PlayStation 2 memory card icon (the disc's Startup\Crash.ico, which saves get), the format the console's browser draws. Its
    /// triangles are 3 corners each, every corner has a position in each of the shapes the animation blends, a normal, a UV and a color.
    /// Then come the animation and a 128x128 texture of 16 bit texels (5 bits of red, green and blue from the lowest and the alpha bit), run
    /// length encoded when the texture type's bit 3 is set
    /// </summary>
    public class PS2SaveIcon : BaseTwinItem
    {
        public const Int32 TextureSize = 128;
        public const Int32 TexelCount = TextureSize * TextureSize;
        public const UInt32 CompressedTextureBit = 0x8;
        // A count in the encoded texture up to this many repeats the texel after it, the larger ones are 65536 - n texels as they are
        private const Int32 LongestRun = 0xFEFF;
        private const Int32 LongestCopy = 0x100;

        public PS2SaveIcon()
        {
            FileId = 0x10000;
            ShapeCount = 1;
            TextureType = 0x7;
            HeaderValue = 0x3F800000;
            Vertexes = new List<SaveIconVertex>();
            AnimationTag = 1;
            FrameLength = 1;
            AnimationSpeed = 1.0f;
            Frames = new List<SaveIconFrame>();
            Texture = new UInt16[TexelCount];
            Trailing = Array.Empty<Byte>();
        }

        /// <summary>
        /// 0x10000 in the game's icon
        /// </summary>
        public UInt32 FileId { get; set; }
        /// <summary>
        /// How many positions every vertex has, one for each shape
        /// </summary>
        public Int32 ShapeCount { get; set; }
        /// <summary>
        /// The texture's kind: bit 3 run length encodes it. 6 in the game's icon
        /// </summary>
        public UInt32 TextureType { get; set; }
        /// <summary>
        /// The bits of 1.0f in the game's icon
        /// </summary>
        public UInt32 HeaderValue { get; set; }
        public List<SaveIconVertex> Vertexes { get; set; }
        /// <summary>
        /// 1 in the game's icon
        /// </summary>
        public UInt32 AnimationTag { get; set; }
        public UInt32 FrameLength { get; set; }
        public Single AnimationSpeed { get; set; }
        public UInt32 PlayOffset { get; set; }
        public List<SaveIconFrame> Frames { get; set; }
        /// <summary>
        /// The texels, the top row first
        /// </summary>
        public UInt16[] Texture { get; set; }
        /// <summary>
        /// The encoded texture as it was read, written back while it still decodes to the texels: other encoders split the runs otherwise
        /// </summary>
        public Byte[] CompressedTexture { get; set; }
        /// <summary>
        /// Whatever followed the texture
        /// </summary>
        public Byte[] Trailing { get; set; }

        public Boolean IsTextureCompressed => (TextureType & CompressedTextureBit) != 0;

        public override Int32 GetLength()
        {
            return 20 + Vertexes.Count * (ShapeCount * 8 + 16) + 20 + Frames.Sum(frame => 8 + frame.Keys.Count * 8)
                   + (IsTextureCompressed ? 4 + GetCompressedTexture().Length : TexelCount * 2) + Trailing.Length;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            var start = reader.BaseStream.Position;
            FileId = reader.ReadUInt32();
            ShapeCount = reader.ReadInt32();
            TextureType = reader.ReadUInt32();
            HeaderValue = reader.ReadUInt32();
            var vertexCount = reader.ReadInt32();
            if (ShapeCount < 0 || vertexCount < 0 || (Int64)vertexCount * ((Int64)ShapeCount * 8 + 16) > length)
            {
                throw new InvalidDataException($"A save icon of {vertexCount} vertexes in {ShapeCount} shapes doesn't fit in {length} bytes");
            }

            Vertexes = new List<SaveIconVertex>(vertexCount);
            for (var i = 0; i < vertexCount; i++)
            {
                var vertex = new SaveIconVertex { Positions = new Int16[ShapeCount * 4] };
                for (var j = 0; j < vertex.Positions.Length; j++)
                {
                    vertex.Positions[j] = reader.ReadInt16();
                }

                for (var j = 0; j < 4; j++)
                {
                    vertex.Normal[j] = reader.ReadInt16();
                }

                vertex.U = reader.ReadInt16();
                vertex.V = reader.ReadInt16();
                vertex.Color = reader.ReadUInt32();
                Vertexes.Add(vertex);
            }

            AnimationTag = reader.ReadUInt32();
            FrameLength = reader.ReadUInt32();
            AnimationSpeed = reader.ReadSingle();
            PlayOffset = reader.ReadUInt32();
            var frameCount = reader.ReadInt32();
            Frames = new List<SaveIconFrame>();
            for (var i = 0; i < frameCount; i++)
            {
                var frame = new SaveIconFrame { Shape = reader.ReadUInt32() };
                var keyCount = reader.ReadInt32();
                for (var j = 0; j < keyCount; j++)
                {
                    frame.Keys.Add(new SaveIconKey(reader.ReadSingle(), reader.ReadSingle()));
                }

                Frames.Add(frame);
            }

            CompressedTexture = null;
            if (IsTextureCompressed)
            {
                var size = reader.ReadInt32();
                CompressedTexture = reader.ReadBytes(size);
                Texture = DecompressTexture(CompressedTexture);
            }
            else
            {
                Texture = new UInt16[TexelCount];
                for (var i = 0; i < TexelCount; i++)
                {
                    Texture[i] = reader.ReadUInt16();
                }
            }

            var left = start + length - reader.BaseStream.Position;
            Trailing = left > 0 ? reader.ReadBytes((Int32)left) : Array.Empty<Byte>();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(FileId);
            writer.Write(ShapeCount);
            writer.Write(TextureType);
            writer.Write(HeaderValue);
            writer.Write(Vertexes.Count);
            foreach (var vertex in Vertexes)
            {
                for (var j = 0; j < ShapeCount * 4; j++)
                {
                    writer.Write(j < vertex.Positions.Length ? vertex.Positions[j] : (Int16)0);
                }

                for (var j = 0; j < 4; j++)
                {
                    writer.Write(j < vertex.Normal.Length ? vertex.Normal[j] : (Int16)0);
                }

                writer.Write(vertex.U);
                writer.Write(vertex.V);
                writer.Write(vertex.Color);
            }

            writer.Write(AnimationTag);
            writer.Write(FrameLength);
            writer.Write(AnimationSpeed);
            writer.Write(PlayOffset);
            writer.Write(Frames.Count);
            foreach (var frame in Frames)
            {
                writer.Write(frame.Shape);
                writer.Write(frame.Keys.Count);
                foreach (var key in frame.Keys)
                {
                    writer.Write(key.Time);
                    writer.Write(key.Value);
                }
            }

            if (IsTextureCompressed)
            {
                var compressed = GetCompressedTexture();
                writer.Write(compressed.Length);
                writer.Write(compressed);
            }
            else
            {
                for (var i = 0; i < TexelCount; i++)
                {
                    writer.Write(i < Texture.Length ? Texture[i] : (UInt16)0);
                }
            }

            writer.Write(Trailing);
        }

        public override String GetName()
        {
            return "Save icon";
        }

        private Byte[] GetCompressedTexture()
        {
            if (CompressedTexture != null)
            {
                try
                {
                    if (DecompressTexture(CompressedTexture).AsSpan().SequenceEqual(Texture))
                    {
                        return CompressedTexture;
                    }
                }
                catch (InvalidDataException)
                {
                    // Encoded otherwise, the texels get encoded again
                }
            }

            return CompressTexture(Texture);
        }

        /// <summary>
        /// The texels of an encoded texture: a count below 0xFF00 repeats the texel after it that many times, a larger one is followed by
        /// 65536 minus it texels as they are
        /// </summary>
        public static UInt16[] DecompressTexture(Byte[] data)
        {
            var texels = new UInt16[TexelCount];
            var index = 0;
            var position = 0;
            UInt16 Next()
            {
                if (position + 2 > data.Length)
                {
                    throw new InvalidDataException("The save icon's texture ends in the middle of a run");
                }

                var value = (UInt16)(data[position] | data[position + 1] << 8);
                position += 2;
                return value;
            }

            while (position < data.Length)
            {
                var count = Next();
                var run = count < 0xFF00;
                var texelCount = run ? count : 0x10000 - count;
                var texel = run ? Next() : (UInt16)0;
                for (var i = 0; i < texelCount; i++)
                {
                    if (index >= TexelCount)
                    {
                        throw new InvalidDataException("The save icon's texture has more than 128x128 texels");
                    }

                    texels[index++] = run ? texel : Next();
                }
            }

            return texels;
        }

        public static Byte[] CompressTexture(UInt16[] texels)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            var count = Math.Min(texels.Length, TexelCount);
            var i = 0;
            while (i < count)
            {
                var run = 1;
                while (i + run < count && run < LongestRun && texels[i + run] == texels[i])
                {
                    run++;
                }

                if (run > 1)
                {
                    writer.Write((UInt16)run);
                    writer.Write(texels[i]);
                    i += run;
                    continue;
                }

                // Texels as they are up to where two of a kind start a run
                var copy = 1;
                while (i + copy < count && copy < LongestCopy && !(i + copy + 1 < count && texels[i + copy] == texels[i + copy + 1]))
                {
                    copy++;
                }

                writer.Write((UInt16)(0x10000 - copy));
                for (var j = 0; j < copy; j++)
                {
                    writer.Write(texels[i + j]);
                }

                i += copy;
            }

            writer.Flush();
            return stream.ToArray();
        }

        /// <summary>
        /// A texel as 8 bit RGBA, its 5 bits spread over the 8 and the alpha bit opaque or not
        /// </summary>
        public static (Byte R, Byte G, Byte B, Byte A) ToRgba(UInt16 texel)
        {
            static Byte Spread(Int32 value) => (Byte)(value << 3 | value >> 2);
            return (Spread(texel & 0x1F), Spread(texel >> 5 & 0x1F), Spread(texel >> 10 & 0x1F), (Byte)((texel & 0x8000) != 0 ? 0xFF : 0));
        }

        public static UInt16 FromRgba(Byte r, Byte g, Byte b, Byte a)
        {
            return (UInt16)(r >> 3 | (g >> 3) << 5 | (b >> 3) << 10 | (a >= 0x80 ? 0x8000 : 0));
        }
    }
}
