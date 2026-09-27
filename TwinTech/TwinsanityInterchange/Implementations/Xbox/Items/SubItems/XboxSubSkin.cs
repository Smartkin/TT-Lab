using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SubItems
{
    public class XboxSubSkin : ITwinSubSkin, ITwinStripGroups
    {
        public XboxSkinnedGeometry Geometry { get; set; } = new();

        public UInt32 Material { get; set; }
        public List<Vector4> Vertexes { get => Geometry.Vertexes; set => Geometry.Vertexes = value; }
        public List<Vector4> UVW { get => Geometry.UVW; set => Geometry.UVW = value; }
        public List<Vector4> Colors { get => Geometry.Colors; set => Geometry.Colors = value; }
        public List<VertexJointInfo> SkinJoints { get => Geometry.SkinJoints; set => Geometry.SkinJoints = value; }
        /// <summary>
        /// Vertex amount of every strip
        /// </summary>
        public List<Int32> GroupSizes { get => Geometry.GroupSizes; set => Geometry.GroupSizes = value; }
        public List<List<Int32>> JointPalettes { get => Geometry.JointPalettes; set => Geometry.JointPalettes = value; }
        public List<Vector4> Normals { get => Geometry.Normals; set => Geometry.Normals = value; }
        public TwinSkinCompression Compression { get; set; }
        public TwinVifPadding Padding { get; set; }

        public void CalculateData()
        {
            // The vertexes are read as they are
        }

        public void Compile()
        {
        }

        public Int32 GetLength()
        {
            return 4 + Geometry.GetLength();
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Material = reader.ReadUInt32();
            Geometry.Read(reader);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Material);
            Geometry.Write(writer);
        }
    }
}
