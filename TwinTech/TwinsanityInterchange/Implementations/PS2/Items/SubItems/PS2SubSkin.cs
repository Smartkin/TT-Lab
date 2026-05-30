using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;
using static Twinsanity.PS2Hardware.TwinVIFCompiler;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems
{
    public class PS2SubSkin : ITwinSubSkin
    {
        private Int32 vifCodeSize;
        private Int32 vertexAmount;
        private Byte[] vifCode;

        public UInt32 Material { get; set; }
        public List<Vector4> Vertexes { get; set; }
        public List<Vector4> UVW { get; set; }
        public List<Vector4> Colors { get; set; }
        public List<VertexJointInfo> SkinJoints { get; set; }
        public List<Int32> GroupSizes { get; set; }

        public int GetLength()
        {
            return 12 + vifCode.Length;
        }

        public void Read(BinaryReader reader, int length)
        {
            Material = reader.ReadUInt32();
            vifCodeSize = reader.ReadInt32();
            vertexAmount = reader.ReadInt32();
            vifCode = reader.ReadBytes(vifCodeSize);
        }

        public void CalculateData()
        {
            var interpreter = VIFInterpreter.InterpretCode(vifCode);
            var data = interpreter.GetMem();

            Vertexes = new List<Vector4>();
            UVW = new List<Vector4>();
            Colors = new List<Vector4>();
            SkinJoints = new List<VertexJointInfo>();
            GroupSizes = new List<Int32>();

            const Int32 VERT_DATA_INDEX = 3;
            for (Int32 i = 0; i < data.Count;)
            {
                var verts = data[i][0].GetBinaryX() & 0xFF;
                var fields = (data[i + 1][0].GetBinaryX() & 0xFF) / verts;
                var scaleVec = data[i + 2][0];

                var positionVertexBatch = data[i + VERT_DATA_INDEX]; // Position vectors and some unknown W component
                var uvVertexBatch = data[i + VERT_DATA_INDEX + 1]; // UV vectors and something related to calculating lighting
                var jointInfosVertexBatch = data[i + VERT_DATA_INDEX + 3]; // Weights and joint indices
                var colorsVertexBatch = data[i + VERT_DATA_INDEX + 2]; // Color vectors

                // Vertex conversion
                for (int j = 0; j < verts; ++j)
                {
                    var v1 = new Vector4(positionVertexBatch[j]);
                    var v2 = new Vector4(uvVertexBatch[j]);
                    v1.X = (Int32)v1.GetBinaryX();
                    v1.Y = (Int32)v1.GetBinaryY();
                    v1.Z = (Int32)v1.GetBinaryZ();
                    v1.W = (Int32)v1.GetBinaryW();
                    v2.X = (Int32)v2.GetBinaryX();
                    v2.Y = (Int32)v2.GetBinaryY();
                    v2.Z = (Int32)v2.GetBinaryZ();
                    v2.W = (Int32)v2.GetBinaryW();
                    v1 = v1.Multiply(scaleVec.X);
                    v2 = v2.Multiply(scaleVec.Y);
                    positionVertexBatch[j] = v1;
                    uvVertexBatch[j] = v2;
                }
                var jointInfos = new List<VertexJointInfo>();
                for (Int32 j = 0; j < verts; ++j)
                {
                    var v1 = jointInfosVertexBatch[j];

                    var connValue = v1.GetBinaryW() & 0xFF00;

                    var weightAmount = v1.GetBinaryW() & 0xFF;
                    var weight1 = 0f;
                    var weight2 = 0f;
                    var weight3 = 0f;
                    var jointIndex1 = 0u;
                    var jointIndex2 = 0u;
                    var jointIndex3 = 0u;
                    if (weightAmount > 0)
                    {
                        jointIndex1 = v1.GetBinaryX() & 0x1FF;
                        jointIndex1 /= 4;
                        v1.SetBinaryX(v1.GetBinaryX() & 0xFFFFFE00);
                        weight1 = v1.X;
                    }
                    if (weightAmount > 1)
                    {
                        jointIndex2 = v1.GetBinaryY() & 0x1FF;
                        jointIndex2 /= 4;
                        v1.SetBinaryY(v1.GetBinaryY() & 0xFFFFFE00);
                        weight2 = v1.Y;
                    }
                    if (weightAmount > 2)
                    {
                        jointIndex3 = v1.GetBinaryZ() & 0x1FF;
                        jointIndex3 /= 4;
                        v1.SetBinaryZ(v1.GetBinaryZ() & 0xFFFFFE00);
                        weight3 = v1.Z;
                    }

                    var joint = new VertexJointInfo()
                    {
                        Weight1 = weight1,
                        Weight2 = weight2,
                        Weight3 = weight3,
                        JointIndex1 = (Int32)jointIndex1,
                        JointIndex2 = (Int32)jointIndex2,
                        JointIndex3 = (Int32)jointIndex3,
                        Connection = connValue >> 8 != 128
                    };

                    jointInfos.Add(joint);
                }

                GroupSizes.Add((Int32)verts);
                for (Int32 j = 0; j < verts; j++)
                {
                    var color = new Color(
                        (byte)(colorsVertexBatch[j].GetBinaryX() & 0xFF),
                        (byte)(colorsVertexBatch[j].GetBinaryY() & 0xFF),
                        (byte)(colorsVertexBatch[j].GetBinaryZ() & 0xFF),
                        (byte)(colorsVertexBatch[j].GetBinaryW() & 0xFF));
                    Vertexes.Add(positionVertexBatch[j]);
                    UVW.Add(uvVertexBatch[j]);
                    Colors.Add(Vector4.FromColor(color));
                    SkinJoints.Add(jointInfos[j]);
                }

                i += (Int32)fields + 3;
            }
        }

        public void Write(BinaryWriter writer)
        {
            vertexAmount = Vertexes.Count;
            writer.Write(Material);
            writer.Write(vifCode.Length);
            writer.Write(vertexAmount);
            writer.Write(vifCode);
        }

        public void Compile()
        {
            var data = new List<List<Vector4>>()
            {
                GroupSizes.Select(g => new Vector4(g, 0, 0, 0)).ToList(),
                Vertexes,
                UVW,
                Colors,
                SkinJoints.Select(j => j.GetVector4()).ToList()
            };
            var compiler = new TwinVIFCompiler(ModelFormat.Skin, data, null, 0);
            vifCode = compiler.Compile();
        }

        public UInt32 GetMinSkinCoord()
        {
            var minSkinCoord = UInt32.MaxValue;
            foreach (var vec in Vertexes)
            {
                var binX = vec.GetBinaryX();
                var binY = vec.GetBinaryY();
                var binZ = vec.GetBinaryZ();
                if (binX < minSkinCoord && binX > 0x358637BF)
                {
                    minSkinCoord = binX;
                }
                if (binY < minSkinCoord && binY > 0x358637BF)
                {
                    minSkinCoord = binY;
                }
                if (binZ < minSkinCoord && binZ > 0x358637BF)
                {
                    minSkinCoord = binZ;
                }
            }

            return minSkinCoord;
        }
    }
}
