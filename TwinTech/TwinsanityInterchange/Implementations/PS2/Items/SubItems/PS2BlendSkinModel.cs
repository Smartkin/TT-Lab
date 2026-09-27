using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems
{
    public class PS2BlendSkinModel : ITwinBlendSkinModel
    {
        Byte[] vifCode;
        Int32 blendsAmount;

        public TwinSkinCompression Compression { get; set; }
        public TwinVifPadding Padding { get; set; }
        public Int32 VertexesAmount { get; set; }
        public Vector3 BlendShape { get; set; }
        public List<ITwinBlendSkinFace> Faces { get; set; }
        public List<Vector4> Vertexes { get; set; }
        public List<Vector4> UVW { get; set; }
        public List<Vector4> Colors { get; set; }
        public List<VertexJointInfo> SkinJoints { get; set; }
        public List<Int32> GroupSizes { get; set; }

        public PS2BlendSkinModel(Int32 blendsAmount)
        {
            this.blendsAmount = blendsAmount;
        }

        public int GetLength()
        {
            return 20 + vifCode.Length + Faces.Sum(f => f.GetLength());
        }

        public void Read(BinaryReader reader, int length)
        {
            var blobLen = reader.ReadInt32();
            VertexesAmount = reader.ReadInt32();
            vifCode = reader.ReadBytes(blobLen);
            BlendShape = new();
            BlendShape.Read(reader, Constants.SIZE_VECTOR3);

            Faces = new();
            for (int i = 0; i < blendsAmount; ++i)
            {
                var face = new PS2BlendSkinFace(BlendShape);
                face.Read(reader, length);
                Faces.Add(face);
            }
        }

        public void CalculateData()
        {
            var decoded = TwinSkinPacketDecoder.Decode(vifCode);
            Vertexes = decoded.Positions;
            UVW = decoded.Uvs;
            Colors = decoded.Colors;
            SkinJoints = decoded.Joints;
            GroupSizes = decoded.GroupSizes;
            Compression = decoded.Compression;
            Padding = TwinVifPacket.DetectPadding(vifCode);

            foreach (var face in Faces)
            {
                face.CalculateData();
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(vifCode.Length);
            writer.Write(VertexesAmount);
            writer.Write(vifCode);
            BlendShape.Write(writer);
            foreach (var face in Faces)
            {
                face.Write(writer);
            }
        }

        public void Compile()
        {
            Compression ??= TwinSkinCompression.FitTo(Vertexes);
            VertexesAmount = Vertexes.Count;
            // The game draws a blend skin model as a single batch
            var batches = TwinSkinPacketDecoder.ToBatches(new List<Int32> { Vertexes.Count }, Vertexes, UVW, Colors, SkinJoints, Compression);
            vifCode = TwinVIFCompiler.CompileSkin(batches, Compression, true, Padding);
            foreach (var face in Faces)
            {
                face.Compile();
            }
        }
    }
}
