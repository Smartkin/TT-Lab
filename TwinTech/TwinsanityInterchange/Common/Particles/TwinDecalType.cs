using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Particles
{
    /// <summary>
    /// A kind of decal the game stamps onto the ground (ripples, footfalls): the default chunk's particle data holds up to 16
    /// of them, each a 0x890 byte block the game DMAs into VU0 memory as it is (the update at 0x1ad8e8 uploads
    /// <see cref="VariantCount"/> × 8 vectors of it after the tag). A decal names one of the block's variants when it's made
    /// (0x1ae3a8 reads the variant's lifetime from its first size key). The variants' names sit in a table at the end
    /// </summary>
    public class TwinDecalType : ITwinSerializable
    {
        public const Int32 Length = 0x890;
        public const Int32 MaxVariants = 15;

        /// <summary>
        /// 0x000, the DMA tag the game writes over before every upload, kept as the tools left it
        /// </summary>
        public Byte[] DmaTag;
        /// <summary>
        /// 0x010, the variants, 8 vectors each. Only the first <see cref="VariantCount"/> are used
        /// </summary>
        public TwinDecalVariant[] Variants;
        /// <summary>
        /// 0x790, how many of the variants are defined
        /// </summary>
        public Int32 VariantCount;
        /// <summary>
        /// 0x794 and 0x798, leftovers of the tools' memory
        /// </summary>
        public Int32 Leftover1;
        public Int32 Leftover2;
        /// <summary>
        /// 0x79c, a name for each variant with 8 bytes of leftovers after it
        /// </summary>
        public TwinDecalVariantName[] Names;
        /// <summary>
        /// 0x88c, a leftover of the tools' memory
        /// </summary>
        public Int32 Leftover3;

        public TwinDecalType()
        {
            DmaTag = new Byte[16];
            Variants = new TwinDecalVariant[MaxVariants];
            Names = new TwinDecalVariantName[MaxVariants];
            for (var i = 0; i < MaxVariants; i++)
            {
                Variants[i] = new TwinDecalVariant();
                Names[i] = new TwinDecalVariantName();
            }
        }

        public Int32 GetLength()
        {
            return Length;
        }

        public void Compile()
        {
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            DmaTag = reader.ReadBytes(16);
            for (var i = 0; i < MaxVariants; i++)
            {
                Variants[i].Read(reader, TwinDecalVariant.Length);
            }
            VariantCount = reader.ReadInt32();
            Leftover1 = reader.ReadInt32();
            Leftover2 = reader.ReadInt32();
            for (var i = 0; i < MaxVariants; i++)
            {
                Names[i].Read(reader, TwinDecalVariantName.Length);
            }
            Leftover3 = reader.ReadInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(DmaTag, 0, 16);
            for (var i = 0; i < MaxVariants; i++)
            {
                Variants[i].Write(writer);
            }
            writer.Write(VariantCount);
            writer.Write(Leftover1);
            writer.Write(Leftover2);
            for (var i = 0; i < MaxVariants; i++)
            {
                Names[i].Write(writer);
            }
            writer.Write(Leftover3);
        }
    }

    /// <summary>
    /// One decal of a type: 4 colour keys and 4 size keys the VU0 program runs through over the decal's life
    /// </summary>
    public class TwinDecalVariant : ITwinSerializable
    {
        public const Int32 Length = 0x80;

        /// <summary>
        /// Red, green and blue 0 to 255 (127 leaves the texture as it is) with the alpha in W, spread over the life
        /// </summary>
        public Vector4[] Colors;
        /// <summary>
        /// The decal's scale along X, Y and Z with the key's time in the life (0 to 1) in W. The first key's W is the
        /// lifetime in seconds instead, which the game reads when it makes the decal
        /// </summary>
        public Vector4[] Sizes;

        public TwinDecalVariant()
        {
            Colors = new Vector4[4];
            Sizes = new Vector4[4];
            for (var i = 0; i < 4; i++)
            {
                Colors[i] = new Vector4();
                Sizes[i] = new Vector4();
            }
        }

        public Int32 GetLength()
        {
            return Length;
        }

        public void Compile()
        {
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            for (var i = 0; i < 4; i++)
            {
                Colors[i].Read(reader, Constants.SIZE_VECTOR4);
            }
            for (var i = 0; i < 4; i++)
            {
                Sizes[i].Read(reader, Constants.SIZE_VECTOR4);
            }
        }

        public void Write(BinaryWriter writer)
        {
            for (var i = 0; i < 4; i++)
            {
                Colors[i].Write(writer);
            }
            for (var i = 0; i < 4; i++)
            {
                Sizes[i].Write(writer);
            }
        }
    }

    /// <summary>
    /// A variant's name (8 characters, "NULL" for the unused ones) followed by 8 bytes of the tools' memory
    /// </summary>
    public class TwinDecalVariantName : ITwinSerializable
    {
        public const Int32 Length = 0x10;

        public Char[] Name;
        public UInt32 Leftover1;
        public UInt32 Leftover2;

        public TwinDecalVariantName()
        {
            Name = new Char[8];
        }

        public Int32 GetLength()
        {
            return Length;
        }

        public void Compile()
        {
        }

        // The bytes after the name's terminator are leftovers of the tools' memory, read as text they'd get decoded
        public void Read(BinaryReader reader, Int32 length)
        {
            var bytes = reader.ReadBytes(8);
            for (var i = 0; i < 8; i++)
            {
                Name[i] = (Char)bytes[i];
            }

            Leftover1 = reader.ReadUInt32();
            Leftover2 = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer)
        {
            for (var i = 0; i < 8; i++)
            {
                writer.Write((Byte)Name[i]);
            }

            writer.Write(Leftover1);
            writer.Write(Leftover2);
        }
    }

    /// <summary>
    /// The VU1 packet that sets the decals up for drawing: a GIF tag (the game writes its own over it), texture coordinate
    /// vectors and how many pairs of them are used, 0x420 bytes
    /// </summary>
    public class TwinDecalUvPacket : ITwinSerializable
    {
        public const Int32 Length = 0x420;
        public const Int32 MaxUvs = 64;

        /// <summary>
        /// 0x000, written over by the game before every draw
        /// </summary>
        public Byte[] GifTag;
        /// <summary>
        /// 0x010, texture coordinates (u, v, u, v) of the decal images on the decal texture
        /// </summary>
        public Vector4[] Uvs;
        /// <summary>
        /// 0x410, the game uploads (this + 1) × 2 of the vectors
        /// </summary>
        public Int32 UvCount;
        /// <summary>
        /// 0x414, padding
        /// </summary>
        public Byte[] Tail;

        public TwinDecalUvPacket()
        {
            GifTag = new Byte[16];
            Uvs = new Vector4[MaxUvs];
            for (var i = 0; i < MaxUvs; i++)
            {
                Uvs[i] = new Vector4();
            }
            Tail = new Byte[12];
        }

        public Int32 GetLength()
        {
            return Length;
        }

        public void Compile()
        {
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            GifTag = reader.ReadBytes(16);
            for (var i = 0; i < MaxUvs; i++)
            {
                Uvs[i].Read(reader, Constants.SIZE_VECTOR4);
            }
            UvCount = reader.ReadInt32();
            Tail = reader.ReadBytes(12);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(GifTag, 0, 16);
            for (var i = 0; i < MaxUvs; i++)
            {
                Uvs[i].Write(writer);
            }
            writer.Write(UvCount);
            writer.Write(Tail, 0, 12);
        }
    }
}
