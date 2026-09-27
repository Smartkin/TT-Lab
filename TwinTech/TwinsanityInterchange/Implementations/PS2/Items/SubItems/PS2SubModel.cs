using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems
{
    public class PS2SubModel : ITwinSubModel
    {
        private UInt32 VertexesCount { get; set; }
        private Byte[] VertexData { get; set; }


        public Byte[] UnusedBlob { get; set; }
        public List<Vector4> Vertexes { get; set; }
        public List<Vector4> UVW { get; set; }
        public List<Vector4> Colors { get; set; }
        public List<Vector4> EmitColor { get; set; }
        public List<Vector4> Normals { get; set; }
        public List<bool> Connection { get; set; }
        public List<Int32> GroupSizes { get; set; }
        public TwinVifPadding Padding { get; set; }
        public PS2SubModel()
        {

        }
        public int GetLength()
        {
            return 12 + (VertexData != null ? VertexData.Length : 0) + (UnusedBlob != null ? UnusedBlob.Length : 0);
        }

        public void Read(BinaryReader reader, int length)
        {
            VertexesCount = reader.ReadUInt32();
            int vertexLen = reader.ReadInt32();
            VertexData = reader.ReadBytes(vertexLen);
            int blobLen = reader.ReadInt32();
            UnusedBlob = reader.ReadBytes(blobLen);
        }

        public void CalculateData()
        {
            Vertexes = new List<Vector4>();
            UVW = new List<Vector4>();
            EmitColor = new List<Vector4>();
            Normals = new List<Vector4>();
            Colors = new List<Vector4>();
            Connection = new List<bool>();
            GroupSizes = new List<Int32>();
            Padding = TwinVifPacket.DetectPadding(VertexData);
            foreach (var batch in TwinVifPacket.ReadBatches(VertexData))
            {
                var positions = batch.FirstOrDefault(u => u.Address == 3);
                var uvColors = batch.FirstOrDefault(u => u.Address == 4);
                if (positions == null || uvColors == null)
                {
                    continue;
                }

                var normals = batch.FirstOrDefault(u => u.Address == 5);
                var emits = batch.FirstOrDefault(u => u.Address == 6);
                var count = positions.Amount;
                GroupSizes.Add(count);
                for (var i = 0; i < count; i++)
                {
                    var position = positions.GetVector(i);
                    var vertex = new Vector4();
                    vertex.SetBinaryX(position[0]);
                    vertex.SetBinaryY(position[1]);
                    vertex.SetBinaryZ(position[2]);
                    Vertexes.Add(vertex);

                    var uvColor = uvColors.GetVector(i);
                    var uv = new Vector4();
                    uv.SetBinaryX(uvColor[0] & 0xFFFFFF00);
                    uv.SetBinaryY(uvColor[1] & 0xFFFFFF00);
                    uv.SetBinaryZ(uvColor[2] & 0xFFFFFF00);
                    UVW.Add(uv);
                    Colors.Add(Vector4.FromColor(new Color((Byte)uvColor[0], (Byte)uvColor[1], (Byte)uvColor[2], (Byte)uvColor[3], true)));
                    Connection.Add((uvColor[3] & 0x8000) == 0);

                    if (normals != null)
                    {
                        var normal = normals.GetVector(i);
                        var vector = new Vector4();
                        vector.SetBinaryX(normal[0]);
                        vector.SetBinaryY(normal[1]);
                        vector.SetBinaryZ(normal[2]);
                        Normals.Add(vector);
                    }

                    if (emits != null)
                    {
                        var emit = emits.GetVector(i);
                        EmitColor.Add(Vector4.FromColor(new Color((Byte)emit[0], (Byte)emit[1], (Byte)emit[2], (Byte)emit[3], true)));
                    }
                }
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(VertexesCount);
            writer.Write(VertexData.Length);
            writer.Write(VertexData);
            writer.Write(UnusedBlob.Length);
            writer.Write(UnusedBlob);
        }

        public void Compile()
        {
            VertexesCount = (UInt32)Vertexes.Count;
            var hasNormals = Normals != null && Normals.Count == Vertexes.Count;
            var hasEmitColors = EmitColor != null && EmitColor.Count == Vertexes.Count;
            var batches = new List<TwinVIFCompiler.RigidBatch>();
            var start = 0;
            foreach (var size in GroupSizes)
            {
                var batch = new TwinVIFCompiler.RigidBatch
                {
                    Normals = hasNormals ? new List<Vector4>() : null,
                    EmitColors = hasEmitColors ? new List<Color>() : null
                };
                for (var i = start; i < start + size; i++)
                {
                    batch.Positions.Add(Vertexes[i]);
                    batch.Uvs.Add(i < UVW.Count ? UVW[i] : new Vector4(0, 0, 1, 0));
                    batch.Colors.Add(ToGameColor(i < Colors.Count ? Colors[i] : new Vector4(1, 1, 1, 1)));
                    batch.Adc.Add(!Connection[i]);
                    batch.Normals?.Add(Normals[i]);
                    batch.EmitColors?.Add(ToGameColor(EmitColor[i]));
                }

                batches.Add(batch);
                start += size;
            }

            VertexData = TwinVIFCompiler.CompileRigid(batches, Padding);
            UnusedBlob ??= Array.Empty<Byte>();
        }

        // Inverse of how the colors get read, 7 bits of alpha stored doubled and the blending flag above them
        private static Color ToGameColor(Vector4 color)
        {
            static Byte ToByte(Single value) => (Byte)Math.Clamp(Math.Round(value * 255.0f), 0, 255);
            var alpha = (Byte)(ToByte(color.W) >> 1 | (color.StoresColorWithAlphaBlend ? 0x80 : 0));
            return new Color(ToByte(color.X), ToByte(color.Y), ToByte(color.Z), alpha);
        }
    }
}
