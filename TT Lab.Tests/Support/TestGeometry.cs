using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.Tests.Support;

/// <summary>
/// Made up model data in the game's format, strips of quads with a vertex that comes back later on like the game's strips have
/// </summary>
public static class TestGeometry
{
    public static PS2SubModel RigidSubModel(Random random, TwinVifPadding padding, int[] groupSizes, bool full)
    {
        var subModel = new PS2SubModel { Padding = padding, GroupSizes = [..groupSizes], Vertexes = [], UVW = [], Colors = [], Connection = [], Normals = [], EmitColor = [], UnusedBlob = [] };
        foreach (var size in groupSizes)
        {
            for (var i = 0; i < size; i++)
            {
                subModel.Vertexes.Add(new Vector4(i / 2 + random.NextSingle() * 0.1f, i % 2, random.NextSingle(), 0));
                // The lowest byte of every UV component holds a color channel
                var uv = new Vector4(random.NextSingle() * 4, random.NextSingle() * 4, full ? random.NextSingle() + 0.5f : 1.0f, 0);
                uv.SetBinaryX(uv.GetBinaryX() & 0xFFFFFF00);
                uv.SetBinaryY(uv.GetBinaryY() & 0xFFFFFF00);
                uv.SetBinaryZ(uv.GetBinaryZ() & 0xFFFFFF00);
                subModel.UVW.Add(uv);
                subModel.Colors.Add(RandomColor(random, true));
                subModel.Connection.Add(i >= 2);
                if (full)
                {
                    var normal = new Vector4(random.NextSingle() - 0.5f, random.NextSingle(), random.NextSingle() - 0.5f, 0);
                    subModel.Normals.Add(Flagged(normal, (uint)(random.Next(2) * 0x20)));
                    subModel.EmitColor.Add(RandomColor(random, true));
                }
            }
        }

        var last = groupSizes[0] - 1;
        CopyVertex(subModel.Vertexes, 3, last);
        CopyVertex(subModel.UVW, 3, last);
        CopyVertex(subModel.Colors, 3, last);
        if (full)
        {
            CopyVertex(subModel.Normals, 3, last);
            CopyVertex(subModel.EmitColor, 3, last);
        }

        return subModel;
    }

    public static PS2SubSkin SubSkin(Random random, TwinVifPadding padding, uint material, int[] groupSizes)
    {
        var positions = Enumerable.Range(0, groupSizes.Sum()).Select(i => new Vector4(i / 2, i % 2, random.NextSingle(), random.NextSingle() * 2 - 1)).ToList();
        var subSkin = new PS2SubSkin
        {
            Material = material,
            Padding = padding,
            GroupSizes = [..groupSizes],
            Vertexes = positions,
            UVW = positions.Select(_ => new Vector4(random.NextSingle(), random.NextSingle(), random.NextSingle() * 2 - 1, random.NextSingle() * 2 - 1)).ToList(),
            Colors = positions.Select(_ => RandomColor(random, false)).ToList(),
            SkinJoints = []
        };
        foreach (var size in groupSizes)
        {
            for (var i = 0; i < size; i++)
            {
                subSkin.SkinJoints.Add(RandomJoints(random, i >= 2));
            }
        }

        return subSkin;
    }

    /// <summary>
    /// A blend skin's material, all of its models packed with the same settings like the game's
    /// </summary>
    public static PS2SubBlendSkin SubBlend(Random random, int shapes, uint material, int[] modelSizes)
    {
        var models = modelSizes.Select(size => SubSkin(random, TwinVifPadding.QuadWord, material, [size])).ToList();
        var compression = TwinSkinCompression.FitTo(models.SelectMany(model => model.Vertexes));
        var subBlend = new PS2SubBlendSkin(shapes) { Material = material };
        foreach (var skin in models)
        {
            var blendShape = new Vector3(0.01f, 0.02f, 0.005f);
            subBlend.Models.Add(new PS2BlendSkinModel(shapes)
            {
                Vertexes = skin.Vertexes,
                UVW = skin.UVW,
                Colors = skin.Colors,
                SkinJoints = skin.SkinJoints,
                BlendShape = blendShape,
                Compression = compression,
                Padding = TwinVifPadding.QuadWord,
                Faces = Enumerable.Range(0, shapes).Select(_ => (ITwinBlendSkinFace)new PS2BlendSkinFace(blendShape)
                {
                    Vertices = skin.Vertexes.Select(_ => new VertexBlendShape
                    {
                        BlendShape = blendShape,
                        Offset = new Vector4(random.Next(-127, 128) * blendShape.X, random.Next(-127, 128) * blendShape.Y, random.Next(-127, 128) * blendShape.Z, 1.0f)
                    }).ToList()
                }).ToList()
            });
        }

        return subBlend;
    }

    /// <summary>
    /// Rigid models keep flags in the lowest byte of a normal's X
    /// </summary>
    public static Vector4 Flagged(Vector4 normal, uint flags)
    {
        normal.SetBinaryX(normal.GetBinaryX() & 0xFFFFFF00 | flags);
        return normal;
    }

    // Rigid models store alpha halved with the blending flag above it, skins store all 8 bits
    public static Vector4 RandomColor(Random random, bool withBlendFlag)
    {
        var bytes = new byte[4];
        random.NextBytes(bytes);
        return Vector4.FromColor(new Color(bytes[0], bytes[1], bytes[2], bytes[3], withBlendFlag));
    }

    public static VertexJointInfo RandomJoints(Random random, bool draws)
    {
        var amount = random.Next(1, 4);
        var weights = Enumerable.Range(0, amount).Select(_ => 0.1f + random.NextSingle()).ToArray();
        var sum = weights.Sum();
        return new VertexJointInfo
        {
            JointIndex1 = random.Next(3),
            JointIndex2 = amount > 1 ? random.Next(3) : 0,
            JointIndex3 = amount > 2 ? random.Next(3) : 0,
            Weight1 = weights[0] / sum,
            Weight2 = amount > 1 ? weights[1] / sum : 0,
            Weight3 = amount > 2 ? weights[2] / sum : 0,
            WeightsAmount = amount,
            Connection = draws
        };
    }

    public static byte[] Serialize(ITwinSerializable item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    public static T Deserialize<T>(T item, byte[] bytes) where T : ITwinSerializable
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        item.Read(reader, bytes.Length);
        return item;
    }

    private static void CopyVertex(List<Vector4> values, int from, int to)
    {
        values[to] = new Vector4(values[from]) { StoresColorWithAlphaBlend = values[from].StoresColorWithAlphaBlend };
    }
}
