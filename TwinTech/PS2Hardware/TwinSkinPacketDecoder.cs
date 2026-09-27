using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.PS2Hardware;

/// <summary>
/// Reads the VIF packets of PS2 skins and blend skins back into vertex data
/// </summary>
public static class TwinSkinPacketDecoder
{
    /// <summary>
    /// Vertex data of a whole skin packet
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Vertex positions, W is the X of the vertex's normal
        /// </summary>
        public List<Vector4> Positions { get; } = new();
        /// <summary>
        /// UVs, Z and W are the Y and Z of the vertex's normal
        /// </summary>
        public List<Vector4> Uvs { get; } = new();
        /// <summary>
        /// Vertex colors
        /// </summary>
        public List<Vector4> Colors { get; } = new();
        /// <summary>
        /// Joints and weights of the vertexes
        /// </summary>
        public List<VertexJointInfo> Joints { get; } = new();
        /// <summary>
        /// Amount of vertexes in every batch
        /// </summary>
        public List<Int32> GroupSizes { get; } = new();
        /// <summary>
        /// How the first batch packed its positions and UVs, the game's tools use the same settings for a whole skin
        /// </summary>
        public TwinSkinCompression Compression { get; set; }
    }

    /// <summary>
    /// Unpacks every batch of a skin packet
    /// </summary>
    public static Result Decode(Byte[] packet)
    {
        var result = new Result();
        foreach (var batch in TwinVifPacket.ReadBatches(packet))
        {
            var scale = batch.FirstOrDefault(u => u.Address == 2);
            var positions = batch.FirstOrDefault(u => u.Address == 3);
            var uvs = batch.FirstOrDefault(u => u.Address == 4);
            var joints = batch.FirstOrDefault(u => u.Address == 5);
            var colors = batch.FirstOrDefault(u => u.Address == 6);
            if (scale == null || positions == null || uvs == null || joints == null || colors == null)
            {
                continue;
            }

            var compression = new TwinSkinCompression
            {
                PositionScale = BitConverter.UInt32BitsToSingle(scale.Data[0]),
                UvScale = BitConverter.UInt32BitsToSingle(scale.Data[1]),
                PositionOffset = positions.IsOffsetMode ? positions.Row.Select(r => (Int32)r).ToArray() : new Int32[4],
                UvOffset = uvs.IsOffsetMode ? uvs.Row.Select(r => (Int32)r).ToArray() : new Int32[4]
            };
            result.Compression ??= compression;
            var count = positions.Amount;
            result.GroupSizes.Add(count);
            for (var i = 0; i < count; i++)
            {
                result.Positions.Add(compression.UnpackPosition(positions.GetVector(i)));
                result.Uvs.Add(compression.UnpackUv(uvs.GetVector(i)));
                var color = colors.GetVector(i);
                result.Colors.Add(Vector4.FromColor(new Color((Byte)color[0], (Byte)color[1], (Byte)color[2], (Byte)color[3])));
                result.Joints.Add(VertexJointInfo.FromPackedWords(joints.GetVector(i)));
            }
        }

        return result;
    }

    /// <summary>
    /// Splits vertex data into the batches the compiler packs
    /// </summary>
    public static List<TwinVIFCompiler.SkinBatch> ToBatches(List<Int32> groupSizes, List<Vector4> positions, List<Vector4> uvs, List<Vector4> colors,
        List<VertexJointInfo> joints, TwinSkinCompression compression)
    {
        var batches = new List<TwinVIFCompiler.SkinBatch>();
        var start = 0;
        foreach (var size in groupSizes)
        {
            var batch = new TwinVIFCompiler.SkinBatch();
            for (var i = start; i < start + size; i++)
            {
                batch.Positions.Add(compression.PackPosition(positions[i]));
                batch.Uvs.Add(compression.PackUv(uvs[i]));
                batch.Colors.Add(ToColor(colors[i]));
                batch.Joints.Add(joints[i]);
            }

            batches.Add(batch);
            start += size;
        }

        return batches;
    }

    private static Color ToColor(Vector4 color)
    {
        static Byte ToByte(Single value) => (Byte)Math.Clamp(Math.Round(value * 255.0f), 0, 255);
        return new Color(ToByte(color.X), ToByte(color.Y), ToByte(color.Z), ToByte(color.W));
    }
}
