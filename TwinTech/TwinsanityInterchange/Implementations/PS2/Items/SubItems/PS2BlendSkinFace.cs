using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems
{
    public class PS2BlendSkinFace : ITwinBlendSkinFace
    {
        Vector3 blendShape;
        Byte[] faceData;

        public UInt32 VertexesAmount { get; set; }
        public List<VertexBlendShape> Vertices { get; set; }

        public PS2BlendSkinFace(Vector3 blendShape)
        {
            this.blendShape = blendShape;
        }

        public Int32 GetLength()
        {
            return 4 + 4 + faceData.Length;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            var blobSize = reader.ReadInt32();
            VertexesAmount = reader.ReadUInt32();
            faceData = reader.ReadBytes(blobSize << 4);
        }

        // Every vertex of the model gets 4 signed bytes, the offset in blend shape units and an unused zero
        public void CalculateData()
        {
            Vertices = new((Int32)VertexesAmount);
            for (var i = 0; i < VertexesAmount; i++)
            {
                var x = (SByte)faceData[i * 4];
                var y = (SByte)faceData[i * 4 + 1];
                var z = (SByte)faceData[i * 4 + 2];
                Vertices.Add(new VertexBlendShape
                {
                    BlendShape = blendShape,
                    Offset = new Vector4(x * blendShape.X, y * blendShape.Y, z * blendShape.Z, 1.0f)
                });
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(faceData.Length >> 4);
            writer.Write(VertexesAmount);
            writer.Write(faceData);
        }

        public void Compile()
        {
            VertexesAmount = (UInt32)Vertices.Count;
            faceData = TwinVIFCompiler.CompileBlendFace(Vertices.Select(v => v.GetPackedOffset()).ToList());
        }
    }
}
