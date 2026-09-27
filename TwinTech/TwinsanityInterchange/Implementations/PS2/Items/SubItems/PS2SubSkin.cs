using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

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
        public TwinSkinCompression Compression { get; set; }
        public TwinVifPadding Padding { get; set; }

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
            var decoded = TwinSkinPacketDecoder.Decode(vifCode);
            Vertexes = decoded.Positions;
            UVW = decoded.Uvs;
            Colors = decoded.Colors;
            SkinJoints = decoded.Joints;
            GroupSizes = decoded.GroupSizes;
            Compression = decoded.Compression;
            Padding = TwinVifPacket.DetectPadding(vifCode);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Material);
            writer.Write(vifCode.Length);
            writer.Write(vertexAmount);
            writer.Write(vifCode);
        }

        public void Compile()
        {
            Compression ??= TwinSkinCompression.FitTo(Vertexes);
            vertexAmount = Vertexes.Count;
            var batches = TwinSkinPacketDecoder.ToBatches(GroupSizes, Vertexes, UVW, Colors, SkinJoints, Compression);
            vifCode = TwinVIFCompiler.CompileSkin(batches, Compression, false, Padding);
        }
    }
}
