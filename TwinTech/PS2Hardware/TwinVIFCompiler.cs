using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.PS2Hardware
{
    /// <summary>
    /// Compiles vertex data into the VIF packets of Twinsanity's PS2 model formats
    /// </summary>
    /// <remarks>
    /// Every packet is a list of batches. A batch is a GIF tag, a descriptor and the vertexes of one or more triangle strips
    /// interleaved in VU memory with a stride of 4 quad words, the VU program draws the batch when MSCAL runs it.
    /// A vertex with its ADC bit set doesn't draw the triangle it ends, which is how strips restart and swap inside a batch
    /// </remarks>
    public static class TwinVIFCompiler
    {
        /// <summary>
        /// Most vertexes a batch can hold, the game's own models never go over it
        /// </summary>
        public const Int32 MaxBatchVertexes = 38;

        /// <summary>
        /// Scale the game's skins pack their UVs with
        /// </summary>
        public const Single SkinUvScale = 1.0f / 2048.0f;

        private const UInt16 GifTagAddress = 0;
        private const UInt16 DescriptorAddress = 1;
        private const UInt16 ScaleAddress = 2;
        private const UInt16 PositionAddress = 3;
        private const UInt16 UvAddress = 4;
        private const UInt16 NormalAddress = 5;
        private const UInt16 EmitColorAddress = 6;
        private const UInt16 SkinJointAddress = 5;
        private const UInt16 SkinColorAddress = 6;
        private const UInt32 AdcBit = 0x8000;

        /// <summary>
        /// Vertex data of one batch of a rigid model. Colors hold the bytes as they get written, alpha included
        /// </summary>
        public class RigidBatch
        {
            /// <summary>
            /// Vertex positions
            /// </summary>
            public List<Vector4> Positions { get; set; } = new();
            /// <summary>
            /// Texture coordinates and Q, their lowest 8 bits hold the color so they get cut off
            /// </summary>
            public List<Vector4> Uvs { get; set; } = new();
            /// <summary>
            /// Vertex colors
            /// </summary>
            public List<Color> Colors { get; set; } = new();
            /// <summary>
            /// Whether each vertex doesn't draw the triangle it ends
            /// </summary>
            public List<Boolean> Adc { get; set; } = new();
            /// <summary>
            /// Vertex normals, written as they are, or null when the model has none
            /// </summary>
            public List<Vector4> Normals { get; set; }
            /// <summary>
            /// Emission colors or null when the model has none
            /// </summary>
            public List<Color> EmitColors { get; set; }
        }

        /// <summary>
        /// Compiles the batches of a rigid model's submodel
        /// </summary>
        public static Byte[] CompileRigid(IReadOnlyList<RigidBatch> batches, TwinVifPadding padding)
        {
            var writer = new TwinVifPacket.Writer();
            foreach (var batch in batches)
            {
                var count = batch.Positions.Count;
                WriteBatchHeader(writer, count);
                writer.Code(VIFCodeEnum.STCYCL, 0x0104);
                writer.Unpack(PositionAddress, PackFormat.V3_32, count, batch.Positions.SelectMany(p => new[] { p.GetBinaryX(), p.GetBinaryY(), p.GetBinaryZ() }));
                var uvColors = new List<UInt32>(count * 4);
                for (var i = 0; i < count; i++)
                {
                    var uv = batch.Uvs[i];
                    var color = batch.Colors[i];
                    uvColors.Add(uv.GetBinaryX() & 0xFFFFFF00 | color.R);
                    uvColors.Add(uv.GetBinaryY() & 0xFFFFFF00 | color.G);
                    uvColors.Add(uv.GetBinaryZ() & 0xFFFFFF00 | color.B);
                    uvColors.Add(color.A | (batch.Adc[i] ? AdcBit : 0));
                }

                writer.Unpack(UvAddress, PackFormat.V4_32, count, uvColors);
                if (batch.Normals != null)
                {
                    writer.Unpack(NormalAddress, PackFormat.V3_32, count, batch.Normals.SelectMany(n => new[] { n.GetBinaryX(), n.GetBinaryY(), n.GetBinaryZ() }));
                }

                if (batch.EmitColors != null)
                {
                    writer.Unpack(EmitColorAddress, PackFormat.V4_8, count, TwinVifPacket.Pack8(batch.EmitColors.Select(c => new Int32[] { c.R, c.G, c.B, c.A })), true);
                }

                writer.Code(VIFCodeEnum.MSCAL);
                writer.Code(VIFCodeEnum.STCYCL, 0x0101);
            }

            return writer.Finish(padding);
        }

        /// <summary>
        /// Vertex data of one batch of a skin or a blend skin, positions and UVs already packed into integers
        /// </summary>
        public class SkinBatch
        {
            /// <summary>
            /// Packed positions, the fourth component is the X of the vertex's normal
            /// </summary>
            public List<Int32[]> Positions { get; set; } = new();
            /// <summary>
            /// Packed UVs, the third and fourth components are the Y and Z of the vertex's normal
            /// </summary>
            public List<Int32[]> Uvs { get; set; } = new();
            /// <summary>
            /// Vertex colors
            /// </summary>
            public List<Color> Colors { get; set; } = new();
            /// <summary>
            /// Joints and weights of the vertexes, their connection flags are the ADC bits
            /// </summary>
            public List<VertexJointInfo> Joints { get; set; } = new();
        }

        /// <summary>
        /// Compiles the batches of a skin's submodel or the single batch of a blend skin's model
        /// </summary>
        public static Byte[] CompileSkin(IReadOnlyList<SkinBatch> batches, TwinSkinCompression compression, Boolean isBlendSkin, TwinVifPadding padding)
        {
            var writer = new TwinVifPacket.Writer();
            foreach (var batch in batches)
            {
                var count = batch.Positions.Count;
                WriteBatchHeader(writer, count);
                writer.Unpack(ScaleAddress, PackFormat.V2_32, 1, new[] { BitConverter.SingleToUInt32Bits(compression.PositionScale), BitConverter.SingleToUInt32Bits(compression.UvScale) });
                writer.Code(VIFCodeEnum.STMOD, 1);
                writer.Registers(VIFCodeEnum.STROW, compression.PositionOffset.Select(o => (UInt32)o).ToArray());
                writer.Code(VIFCodeEnum.STCYCL, 0x0104);
                writer.Unpack(PositionAddress, PackFormat.V4_16, count, TwinVifPacket.Pack16(batch.Positions));
                writer.Registers(VIFCodeEnum.STROW, compression.UvOffset.Select(o => (UInt32)o).ToArray());
                writer.Unpack(UvAddress, PackFormat.V4_16, count, TwinVifPacket.Pack16(batch.Uvs));
                writer.Code(VIFCodeEnum.STMOD, 0);
                writer.Unpack(SkinColorAddress, PackFormat.V4_8, count, TwinVifPacket.Pack8(batch.Colors.Select(c => new Int32[] { c.R, c.G, c.B, c.A })));
                writer.Unpack(SkinJointAddress, PackFormat.V4_32, count, batch.Joints.SelectMany(j => j.GetPackedWords()));
                if (isBlendSkin)
                {
                    continue;
                }

                writer.Code(VIFCodeEnum.MSCAL);
                writer.Code(VIFCodeEnum.STCYCL, 0x0101);
            }

            return writer.Finish(padding);
        }

        /// <summary>
        /// Packs the offsets of a blend skin's shape, one signed byte per component, padded to whole quad words
        /// </summary>
        public static Byte[] CompileBlendFace(IReadOnlyList<Int32[]> offsets)
        {
            var words = TwinVifPacket.Pack8(offsets.Select(o => new[] { o[0], o[1], o[2], 0 })).ToList();
            while (words.Count % 4 != 0)
            {
                words.Add(0);
            }

            return words.SelectMany(BitConverter.GetBytes).ToArray();
        }

        private static void WriteBatchHeader(TwinVifPacket.Writer writer, Int32 count)
        {
            // GIF tag of a triangle strip in PACKED mode with ST, RGBAQ and XYZ2 registers, the VU program fills in the rest
            writer.Unpack(GifTagAddress, PackFormat.V4_32, 1, new[] { 0x8000 | (UInt32)count, 0x30024000U, 0x512U, 0U });
            writer.Code(VIFCodeEnum.STCYCL, 0x0101);
            writer.Unpack(DescriptorAddress, PackFormat.V2_32, 1, new[] { (UInt32)count * 4, 0x8000 | (UInt32)count });
        }
    }
}
