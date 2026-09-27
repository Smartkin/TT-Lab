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
    /// Skinned strips of a blend skin's material with the offset of every vertex for every shape
    /// </summary>
    public class XboxBlendSkinModel : ITwinBlendSkinModel, ITwinStripGroups
    {
        readonly Int32 blendsAmount;

        public XboxSkinnedGeometry Geometry { get; set; } = new();

        public TwinSkinCompression Compression { get; set; }
        public TwinVifPadding Padding { get; set; }
        public Int32 VertexesAmount { get => Geometry.Vertexes.Count; set { } }
        // Offsets are stored as floats, there's no scale to pack them with
        public Vector3 BlendShape { get; set; } = new();
        public List<ITwinBlendSkinFace> Faces { get; set; } = new();
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

        public XboxBlendSkinModel(Int32 blendsAmount)
        {
            this.blendsAmount = blendsAmount;
        }

        public void CalculateData()
        {
            // The vertexes and offsets are read as they are
        }

        public void Compile()
        {
        }

        public Int32 GetLength()
        {
            return Geometry.GetLength() + Faces.Sum(f => f.GetLength());
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Geometry.Read(reader);
            Faces = new List<ITwinBlendSkinFace>(blendsAmount);
            for (var i = 0; i < blendsAmount; i++)
            {
                var face = new XboxBlendSkinFace((UInt32)Geometry.Vertexes.Count);
                face.Read(reader, length);
                Faces.Add(face);
            }
        }

        public void Write(BinaryWriter writer)
        {
            Geometry.Write(writer);
            foreach (var face in Faces)
            {
                face.Write(writer);
            }
        }
    }
}
