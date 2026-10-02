using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Collision
{
    public class TwinCollisionTriangle : ITwinSerializable
    {
        public Int32 Vertex1Index;
        public Int32 Vertex2Index;
        public Int32 Vertex3Index;
        public Int32 SurfaceIndex;

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
            UInt32 mask = 0x3FFFF;
            UInt64 triangle = reader.ReadUInt64();
            Vertex1Index = (Int32)(triangle & mask);
            Vertex2Index = (Int32)((triangle >> 0x12) & mask);
            Vertex3Index = (Int32)((triangle >> 0x24) & mask);
            SurfaceIndex = (Int32)((triangle >> 0x36) & mask);
        }

        public void Write(BinaryWriter writer)
        {
            UInt32 mask = 0x3FFFF;
            // Three 18 bit vertex indexes and the surface's index in the 10 bits left
            if (Vertex1Index > mask || Vertex2Index > mask || Vertex3Index > mask)
            {
                throw new InvalidOperationException($"A collision triangle uses vertex {Math.Max(Vertex1Index, Math.Max(Vertex2Index, Vertex3Index))}, the game's collision indexes {mask + 1} vertexes");
            }

            if (SurfaceIndex > 0x3FF)
            {
                throw new InvalidOperationException($"A collision triangle uses surface {SurfaceIndex}, the game's collision indexes 1024 surfaces");
            }

            UInt64 packedTriangle = (UInt64)Vertex1Index & mask;
            packedTriangle |= (UInt64)(Vertex2Index & mask) << 0x12;
            packedTriangle |= (UInt64)(Vertex3Index & mask) << 0x24;
            packedTriangle |= (UInt64)(SurfaceIndex & mask) << 0x36;
            writer.Write(packedTriangle);
        }
    }
}
