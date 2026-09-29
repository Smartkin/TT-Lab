using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    /// <summary>
    /// A collision hull of a chunk link, bit 0 of its type says another one follows
    /// </summary>
    public class TwinChunkLinkHull : ITwinSerializable
    {
        public Int32 Type { get; set; }
        public TwinCollisionHull Hull { get; set; }

        public TwinChunkLinkHull()
        {
            Hull = new TwinCollisionHull();
        }

        public Int32 GetLength()
        {
            return 4 + Hull.GetLength();
        }

        public void Compile()
        {
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Type = reader.ReadInt32();
            Hull.Read(reader, length);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Type);
            Hull.Write(writer);
        }
    }
}
