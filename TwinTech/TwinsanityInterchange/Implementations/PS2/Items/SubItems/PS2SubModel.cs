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

        [Flags]
        private enum FieldsPresent
        {
            Vertex = 0,
            UV_Color = 1,
            Normals = 2,
            EmitColors = 4
        }

        public void CalculateData()
        {
            var interpreter = VIFInterpreter.InterpretCode(VertexData);
            var data = interpreter.GetMem();
            Vertexes = new List<Vector4>();
            UVW = new List<Vector4>();
            EmitColor = new List<Vector4>();
            Normals = new List<Vector4>();
            Colors = new List<Vector4>();
            Connection = new List<bool>();
            GroupSizes = new List<Int32>();
            var index = 0;
            for (var i = 0; i < data.Count;)
            {
                var verts = data[i][0].GetBinaryX() & 0xFF;
                var fieldsPresent = FieldsPresent.Vertex;
                var outputAddr = interpreter.GetAddressOutput();
                var fields = 0;
                foreach (var addr in outputAddr[index++])
                {
                    switch (addr)
                    {
                        case 0x3:
                            fieldsPresent |= FieldsPresent.Vertex;
                            fields++;
                            break;
                        case 0x4:
                            fieldsPresent |= FieldsPresent.UV_Color;
                            fields++;
                            break;
                        case 0x5:
                            fieldsPresent |= FieldsPresent.Normals;
                            fields++;
                            break;
                        case 0x6:
                            fieldsPresent |= FieldsPresent.EmitColors;
                            fields++;
                            break;
                    }
                    if (i + fields + 2 >= data.Count)
                        break;

                }
                Vertexes.AddRange(data[i + 2].Where((v) => v != null));
                if (fieldsPresent.HasFlag(FieldsPresent.UV_Color))
                {
                    var uv_con = data[i + 3].Where((v) => v != null);
                    foreach (var e in uv_con)
                    {
                        var conn = (e.GetBinaryW() & 0xFF00) >> 8;
                        Connection.Add(conn != 128);
                        var r = e.GetBinaryX() & 0xFF;
                        var g = e.GetBinaryY() & 0xFF;
                        var b = e.GetBinaryZ() & 0xFF;
                        var a = e.GetBinaryW() & 0xFF;

                        Color col = new((byte)r, (byte)g, (byte)b, (byte)a, true);
                        Colors.Add(Vector4.FromColor(col));

                        Vector4 uv = new(e);
                        uv.SetBinaryX(uv.GetBinaryX() & 0xFFFFFF00);
                        uv.SetBinaryY(uv.GetBinaryY() & 0xFFFFFF00);
                        uv.SetBinaryZ(uv.GetBinaryZ() & 0xFFFFFF00);
                        UVW.Add(uv);
                    }
                }
                if (fieldsPresent.HasFlag(FieldsPresent.Normals))
                {
                    foreach (var e in data[i + 4])
                    {
                        if (e == null)
                            break;
                        Normals.Add(new Vector4(e.X, e.Y, e.Z, 1.0f));
                    }
                }
                if (fieldsPresent.HasFlag(FieldsPresent.EmitColors))
                {
                    foreach (var e in data[i + fields + 1])
                    {
                        if (e == null)
                            break;

                        var r = (byte)(e.GetBinaryX() & 0xFF);
                        var g = (byte)(e.GetBinaryY() & 0xFF);
                        var b = (byte)(e.GetBinaryZ() & 0xFF);
                        var a = (byte)(e.GetBinaryW() & 0xFF);
                        Color col = new(r, g, b, a, true);

                        EmitColor.Add(Vector4.FromColor(col));
                    }
                }
                i += fields + 2;
                GroupSizes.Add((Int32)verts);
                TrimList(UVW, Vertexes.Count);
                TrimList(Colors, Vertexes.Count);
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
            TrimList(UVW, (Int32)VertexesCount);
            TrimList(Colors, (Int32)VertexesCount, new Vector4());
            var data = new List<List<Vector4>>
            {
                GroupSizes.Select(i => new Vector4(i, 0, 0, 0)).ToList(),
                Vertexes,
                Colors,
                UVW
            };
            var hasNormals = Normals.Count == Vertexes.Count;
            var hasEmitColors = EmitColor.Count == Vertexes.Count;
            // Normals are optional
            if (hasNormals)
            {
                data.Add(Normals);
            }
            // Emit colors are optional
            if (hasEmitColors)
            {
                data.Add(EmitColor);
            }
            var compiler = new TwinVIFCompiler(data, Connection, hasNormals, hasEmitColors);
            VertexData = compiler.Compile();
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

        private static void TrimList(List<Vector4> list, Int32 desiredLength, Vector4 defaultValue = null)
        {
            if (list != null)
            {
                if (list.Count > desiredLength)
                {
                    list.RemoveRange(desiredLength, list.Count - desiredLength);
                }
                while (list.Count < desiredLength)
                {
                    if (defaultValue != null)
                    {
                        list.Add(new Vector4(defaultValue));
                    }
                    else
                    {
                        list.Add(new Vector4());
                    }
                }
            }
        }
    }
}
