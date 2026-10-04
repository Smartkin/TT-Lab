using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    /// <summary>
    /// What a contact tells the agent it reaches (the game's ContactMessage, 0x20 bytes): a collision surface's is handed to what touches
    /// it when the surface's flags say so, and the damage the scripts' commands deal is one too. The agent keeps the message and runs its
    /// script's contact event when its <see cref="Damage"/> isn't 0, its conditions then test the <see cref="Kinds"/>
    /// </summary>
    public class TwinContactMessage : ITwinSerializable
    {
        public const Int32 LeftoverSize = 11;

        /// <summary>
        /// Where the contact was, its W the push strength a physical contact hands the object's node. The surfaces' are 0
        /// </summary>
        public Vector4 Point;
        /// <summary>
        /// A bit for every kind of hit the contact was (<see cref="Enums.ContactKinds"/>), which the scripts' conditions test (the HitBy*
        /// ones). The deadly surfaces have 0x4 falling through, 0x80 electric, 0x400 instant death, 0x800008 lava, 0x2800000 drowning;
        /// water 0x2000000. Rigid bodies touching the level's collision are told of a surface with any kind whatever its flags
        /// </summary>
        public UInt32 Kinds;
        /// <summary>
        /// The hit points the contact takes (100 on the deadly surfaces), 0 doesn't reach the agent's script
        /// </summary>
        public Byte Damage;
        /// <summary>
        /// The 11 bytes after the damage, the tools' memory (0xCD in the NTSC version)
        /// </summary>
        public Byte[] Leftover;

        public TwinContactMessage()
        {
            Point = new Vector4(0, 0, 0, 0);
            Leftover = new Byte[LeftoverSize];
        }

        public Int32 GetLength()
        {
            return Constants.SIZE_VECTOR4 + 4 + 1 + LeftoverSize;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Point.Read(reader, Constants.SIZE_VECTOR4);
            Kinds = reader.ReadUInt32();
            Damage = reader.ReadByte();
            Leftover = reader.ReadBytes(LeftoverSize);
        }

        public void Write(BinaryWriter writer)
        {
            Point.Write(writer);
            writer.Write(Kinds);
            writer.Write(Damage);
            writer.Write(Leftover, 0, LeftoverSize);
        }
    }
}
