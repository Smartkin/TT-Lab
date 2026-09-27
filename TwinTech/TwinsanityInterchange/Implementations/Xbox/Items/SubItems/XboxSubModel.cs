using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SubItems
{
    /// <summary>
    /// Plain vertexes the Xbox draws as triangle strips, one strip per group
    /// </summary>
    public class XboxSubModel : ITwinSubModel, ITwinStripGroups
    {
        private const Int32 VertexLength = 0x1C;

        UInt32 trailer;

        public List<Vector4> Vertexes { get; set; } = new();
        public List<Vector4> UVW { get; set; } = new();
        public List<Vector4> Colors { get; set; } = new();
        public List<Vector4> EmitColor { get; set; } = new();
        public List<Vector4> Normals { get; set; } = new();
        public List<Boolean> Connection { get; set; } = new();
        /// <summary>
        /// Vertex amount of every strip
        /// </summary>
        public List<Int32> GroupSizes { get; set; } = new();
        public TwinVifPadding Padding { get; set; }
        public List<List<Int32>> JointPalettes { get; set; } = new();

        public void CalculateData()
        {
            Connection = GetConnections(GroupSizes, Vertexes);
        }

        /// <summary>
        /// Whether every strip vertex ends a triangle, which every one but the strip's first two does. Strips are joined with
        /// repeated vertexes, the triangles they end have no area
        /// </summary>
        public static List<Boolean> GetConnections(List<Int32> groupSizes, List<Vector4> positions)
        {
            var connections = new List<Boolean>(positions.Count);
            var start = 0;
            foreach (var size in groupSizes)
            {
                for (var i = 0; i < size; i++)
                {
                    var position = start + i;
                    connections.Add(i >= 2 && position < positions.Count && !IsSame(positions[position], positions[position - 1]) &&
                                    !IsSame(positions[position], positions[position - 2]) && !IsSame(positions[position - 1], positions[position - 2]));
                }

                start += size;
            }

            return connections;
        }

        private static Boolean IsSame(Vector4 first, Vector4 second)
        {
            return first.X == second.X && first.Y == second.Y && first.Z == second.Z;
        }

        public Int32 GetLength()
        {
            return 12 + GroupSizes.Count * 4 + Vertexes.Count * VertexLength + 4;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            var vertexAmount = reader.ReadInt32();
            reader.ReadInt32(); // Length of the vertexes
            var groupAmount = reader.ReadInt32();
            GroupSizes = new List<Int32>(groupAmount);
            for (var i = 0; i < groupAmount; i++)
            {
                GroupSizes.Add(reader.ReadInt32());
            }

            Vertexes = new List<Vector4>(vertexAmount);
            UVW = new List<Vector4>(vertexAmount);
            Colors = new List<Vector4>(vertexAmount);
            EmitColor = new List<Vector4>();
            Normals = new List<Vector4>(vertexAmount);
            for (var i = 0; i < vertexAmount; i++)
            {
                Vertexes.Add(new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 0.0f));
                Normals.Add(UnpackNormal(reader.ReadUInt32()));
                Colors.Add(new Vector4(reader.ReadByte() / 255f, reader.ReadByte() / 255f, reader.ReadByte() / 255f, reader.ReadByte() / 255f));
                UVW.Add(new Vector4(reader.ReadSingle(), reader.ReadSingle(), 1.0f, 0.0f));
            }

            trailer = reader.ReadUInt32();
            CalculateData();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Vertexes.Count);
            writer.Write(Vertexes.Count * VertexLength);
            writer.Write(GroupSizes.Count);
            foreach (var size in GroupSizes)
            {
                writer.Write(size);
            }

            for (var i = 0; i < Vertexes.Count; i++)
            {
                writer.Write(Vertexes[i].X);
                writer.Write(Vertexes[i].Y);
                writer.Write(Vertexes[i].Z);
                writer.Write(PackNormal(i < Normals.Count ? Normals[i] : new Vector4(0, 1, 0, 0)));
                var color = i < Colors.Count ? Colors[i] : new Vector4(1, 1, 1, 1);
                writer.Write(ToByte(color.X));
                writer.Write(ToByte(color.Y));
                writer.Write(ToByte(color.Z));
                writer.Write(ToByte(color.W));
                var uv = i < UVW.Count ? UVW[i] : new Vector4();
                writer.Write(uv.X);
                writer.Write(uv.Y);
            }

            writer.Write(trailer);
        }

        public void Compile()
        {
        }

        /// <summary>
        /// Normals are packed as signed 11, 11 and 10 bit fractions
        /// </summary>
        public static Vector4 UnpackNormal(UInt32 packed)
        {
            return new Vector4(ToSigned(packed & 0x7FF, 11) / 1023f, ToSigned(packed >> 11 & 0x7FF, 11) / 1023f, ToSigned(packed >> 22 & 0x3FF, 10) / 511f, 0.0f);
        }

        public static UInt32 PackNormal(Vector4 normal)
        {
            var x = (UInt32)Math.Clamp((Int32)Math.Round(normal.X * 1023f), -1024, 1023) & 0x7FF;
            var y = (UInt32)Math.Clamp((Int32)Math.Round(normal.Y * 1023f), -1024, 1023) & 0x7FF;
            var z = (UInt32)Math.Clamp((Int32)Math.Round(normal.Z * 511f), -512, 511) & 0x3FF;
            return x | y << 11 | z << 22;
        }

        private static Int32 ToSigned(UInt32 value, Int32 bits)
        {
            return value >= 1U << (bits - 1) ? (Int32)value - (1 << bits) : (Int32)value;
        }

        private static Byte ToByte(Single value)
        {
            return (Byte)Math.Clamp(Math.Round(value * 255.0f), 0, 255);
        }
    }
}
