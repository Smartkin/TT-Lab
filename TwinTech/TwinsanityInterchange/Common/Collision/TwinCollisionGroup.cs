using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Collision
{
    /// <summary>
    /// The triangles a leaf of the collision's tree has: how many, from which
    /// </summary>
    public class TwinCollisionGroup : ITwinSerializable
    {
        public UInt32 Count;
        public UInt32 FirstTriangle;

        public Int32 GetLength()
        {
            return 8;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Count = reader.ReadUInt32();
            FirstTriangle = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Count);
            writer.Write(FirstTriangle);
        }
    }
}
