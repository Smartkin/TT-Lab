using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Collision
{
    public class TwinCollisionTriangle : ITwinSerializable
    {
        public Int32 Vector1Index;
        public Int32 Vector2Index;
        public Int32 Vector3Index;
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
            Vector1Index = (Int32)(triangle & mask);
            Vector2Index = (Int32)((triangle >> 0x12) & mask);
            Vector3Index = (Int32)((triangle >> 0x24) & mask);
            SurfaceIndex = (Int32)((triangle >> 0x36) & mask);
        }

        public void Write(BinaryWriter writer)
        {
            UInt32 mask = 0x3FFFF;
            // Three 18 bit vertex indexes and the surface's index in the 10 bits left
            if (Vector1Index > mask || Vector2Index > mask || Vector3Index > mask)
            {
                throw new InvalidOperationException($"A collision triangle uses vertex {Math.Max(Vector1Index, Math.Max(Vector2Index, Vector3Index))}, the game's collision indexes {mask + 1} vertexes");
            }

            if (SurfaceIndex > 0x3FF)
            {
                throw new InvalidOperationException($"A collision triangle uses surface {SurfaceIndex}, the game's collision indexes 1024 surfaces");
            }

            UInt64 packedTriangle = (UInt64)Vector1Index & mask;
            packedTriangle |= (UInt64)(Vector2Index & mask) << 0x12;
            packedTriangle |= (UInt64)(Vector3Index & mask) << 0x24;
            packedTriangle |= (UInt64)(SurfaceIndex & mask) << 0x36;
            writer.Write(packedTriangle);
        }
    }
}
