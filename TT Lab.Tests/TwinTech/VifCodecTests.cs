using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.Tests.TwinTech;

public class VifCodecTests
{
    [Theory]
    [InlineData(TwinVifPadding.QuadWord, false, false)]
    [InlineData(TwinVifPadding.QuadWord, true, true)]
    [InlineData(TwinVifPadding.NopPerByte, true, false)]
    [InlineData(TwinVifPadding.NopPerByte, false, true)]
    public void SubModelsReadBackWhatTheyWereCompiledFrom(TwinVifPadding padding, bool normals, bool emits)
    {
        var original = CreateSubModel(new Random(7), [38, 17, 5], padding, normals, emits);
        original.Compile();
        var bytes = Serialize(original);

        var read = Deserialize(new PS2SubModel(), bytes);
        read.CalculateData();

        Assert.Equal(original.GroupSizes, read.GroupSizes);
        Assert.Equal(Bits(original.Vertexes, 3), Bits(read.Vertexes, 3));
        Assert.Equal(Bits(original.UVW, 3), Bits(read.UVW, 3));
        Assert.Equal(Bits(original.Colors, 4), Bits(read.Colors, 4));
        Assert.Equal(original.Colors.Select(color => color.StoresColorWithAlphaBlend), read.Colors.Select(color => color.StoresColorWithAlphaBlend));
        Assert.Equal(original.Connection, read.Connection);
        Assert.Equal(normals ? Bits(original.Normals, 3) : [], Bits(read.Normals, 3));
        Assert.Equal(emits ? Bits(original.EmitColor, 4) : [], Bits(read.EmitColor, 4));
        read.Compile();
        Assert.Equal(bytes, Serialize(read));
    }

    [Theory]
    [InlineData(TwinVifPadding.QuadWord)]
    [InlineData(TwinVifPadding.NopPerByte)]
    public void SkinsReadBackWhatTheyWereCompiledFrom(TwinVifPadding padding)
    {
        var random = new Random(11);
        var positions = Enumerable.Range(0, 70).Select(_ => new Vector4(Next(random, -3, 3), Next(random, 0, 5), Next(random, -1, 1), Next(random, -1, 1))).ToList();
        var original = new PS2SubSkin
        {
            Material = 0x1234,
            Vertexes = positions,
            UVW = positions.Select(_ => new Vector4(Next(random, -2, 2), Next(random, -2, 2), Next(random, -1, 1), Next(random, -1, 1))).ToList(),
            Colors = positions.Select(_ => RandomColor(random, false)).ToList(),
            SkinJoints = positions.Select(_ => RandomJoints(random)).ToList(),
            GroupSizes = [38, 32],
            Padding = padding
        };
        original.Compile();
        var bytes = Serialize(original);

        var read = Deserialize(new PS2SubSkin(), bytes);
        read.CalculateData();

        Assert.Equal(0x1234U, read.Material);
        Assert.Equal(original.GroupSizes, read.GroupSizes);
        var scale = original.Compression.PositionScale;
        Assert.All(original.Vertexes.Zip(read.Vertexes), pair => AssertClose(pair.First, pair.Second, scale / 2 + 1e-6f));
        Assert.All(original.UVW.Zip(read.UVW), pair => AssertClose(pair.First, pair.Second, TwinVIFCompiler.SkinUvScale / 2 + 1e-6f));
        Assert.Equal(Bits(original.Colors, 4), Bits(read.Colors, 4));
        Assert.Equal(original.SkinJoints.Select(Joints), read.SkinJoints.Select(Joints));
        Assert.All(original.SkinJoints.Zip(read.SkinJoints), pair =>
        {
            Assert.Equal(pair.First.Weight1, pair.Second.Weight1, 1e-3f);
            Assert.Equal(pair.First.Weight2, pair.Second.Weight2, 1e-3f);
            Assert.Equal(pair.First.Weight3, pair.Second.Weight3, 1e-3f);
        });
        read.Compile();
        Assert.Equal(bytes, Serialize(read));
    }

    [Fact]
    public void BlendSkinModelsKeepTheirShapes()
    {
        var random = new Random(3);
        var blendShape = new Vector3(0.01f, 0.02f, 0.005f);
        var positions = Enumerable.Range(0, 30).Select(_ => new Vector4(Next(random, -1, 1), Next(random, -1, 1), Next(random, -1, 1), Next(random, -1, 1))).ToList();
        var original = new PS2BlendSkinModel(2)
        {
            Vertexes = positions,
            UVW = positions.Select(_ => new Vector4(Next(random, 0, 1), Next(random, 0, 1), Next(random, -1, 1), Next(random, -1, 1))).ToList(),
            Colors = positions.Select(_ => RandomColor(random, false)).ToList(),
            SkinJoints = positions.Select(_ => RandomJoints(random)).ToList(),
            BlendShape = blendShape,
            Faces = Enumerable.Range(0, 2).Select(_ => (ITwinBlendSkinFace)new PS2BlendSkinFace(blendShape)
            {
                Vertices = positions.Select(_ => new VertexBlendShape
                {
                    BlendShape = blendShape,
                    Offset = new Vector4(random.Next(-127, 128) * blendShape.X, random.Next(-127, 128) * blendShape.Y, random.Next(-127, 128) * blendShape.Z, 1.0f)
                }).ToList()
            }).ToList()
        };
        original.Compile();
        var bytes = Serialize(original);

        var read = Deserialize(new PS2BlendSkinModel(2), bytes);
        read.CalculateData();

        Assert.Equal(Bits(original.Faces.SelectMany(face => face.Vertices.Select(vertex => vertex.Offset)), 3), Bits(read.Faces.SelectMany(face => face.Vertices.Select(vertex => vertex.Offset)), 3));
        Assert.Equal(original.SkinJoints.Select(Joints), read.SkinJoints.Select(Joints));
        read.Compile();
        Assert.Equal(bytes, Serialize(read));
    }

    [Theory]
    [InlineData(TwinVifPadding.QuadWord)]
    [InlineData(TwinVifPadding.NopPerByte)]
    public void PaddingIsTellable(TwinVifPadding padding)
    {
        var writer = new TwinVifPacket.Writer();
        writer.Unpack(0, PackFormat.V4_32, 1, [1, 2, 3, 4]);

        var packet = writer.Finish(padding);

        Assert.Equal(0, packet.Length % 4);
        Assert.Equal(padding, TwinVifPacket.DetectPadding(packet));
        Assert.Equal([1U, 2U, 3U, 4U], TwinVifPacket.ReadBatches(packet).Single().Single().Data);
    }

    [Fact]
    public void CompressionFitsWhatItWasMadeFor()
    {
        var random = new Random(5);
        var positions = Enumerable.Range(0, 200).Select(_ => new Vector4(Next(random, -40, 90), Next(random, -5, 5), Next(random, 100, 300), Next(random, -1, 1))).ToList();

        var compression = TwinSkinCompression.FitTo(positions);

        Assert.True(compression.Fits(positions, []));
        Assert.All(positions, position =>
        {
            var packed = compression.PackPosition(position);
            var offsets = packed.Select((value, i) => (UInt32)(value + compression.PositionOffset[i])).ToArray();
            AssertClose(position, compression.UnpackPosition(offsets), compression.PositionScale / 2 + 1e-5f);
        });
    }

    [Fact]
    public void JointsPackIntoTheWordsTheGameReads()
    {
        var joints = new VertexJointInfo { JointIndex1 = 5, JointIndex2 = 17, JointIndex3 = 2, Weight1 = 0.5f, Weight2 = 0.3f, Weight3 = 0.2f, WeightsAmount = 3, Connection = false };

        var read = VertexJointInfo.FromPackedWords(joints.GetPackedWords());

        Assert.Equal(Joints(joints), Joints(read));
        Assert.Equal(0.5f, read.Weight1, 1e-3f);
        Assert.Equal(0.3f, read.Weight2, 1e-3f);
        Assert.Equal(0.2f, read.Weight3, 1e-3f);
    }

    private static PS2SubModel CreateSubModel(Random random, int[] groupSizes, TwinVifPadding padding, bool normals, bool emits)
    {
        var count = groupSizes.Sum();
        return new PS2SubModel
        {
            GroupSizes = [..groupSizes],
            Padding = padding,
            Vertexes = Enumerable.Range(0, count).Select(_ => new Vector4(Next(random, -50, 50), Next(random, -50, 50), Next(random, -50, 50), 0)).ToList(),
            // The lowest byte of every UV component holds a color channel
            UVW = Enumerable.Range(0, count).Select(_ => Masked(new Vector4(Next(random, -4, 4), Next(random, -4, 4), 1.0f, 0))).ToList(),
            Colors = Enumerable.Range(0, count).Select(_ => RandomColor(random, true)).ToList(),
            Connection = Enumerable.Range(0, count).Select(_ => random.Next(4) != 0).ToList(),
            Normals = normals ? Enumerable.Range(0, count).Select(_ => new Vector4(Next(random, -1, 1), Next(random, -1, 1), Next(random, -1, 1), 0)).ToList() : [],
            EmitColor = emits ? Enumerable.Range(0, count).Select(_ => RandomColor(random, true)).ToList() : []
        };
    }

    private static Vector4 Masked(Vector4 uv)
    {
        uv.SetBinaryX(uv.GetBinaryX() & 0xFFFFFF00);
        uv.SetBinaryY(uv.GetBinaryY() & 0xFFFFFF00);
        uv.SetBinaryZ(uv.GetBinaryZ() & 0xFFFFFF00);
        return uv;
    }

    // Rigid models store alpha halved with the blending flag above it, skins store all 8 bits
    private static Vector4 RandomColor(Random random, bool withBlendFlag)
    {
        var bytes = new byte[4];
        random.NextBytes(bytes);
        return Vector4.FromColor(new Color(bytes[0], bytes[1], bytes[2], bytes[3], withBlendFlag));
    }

    private static VertexJointInfo RandomJoints(Random random)
    {
        var amount = random.Next(1, 4);
        var weights = Enumerable.Range(0, amount).Select(_ => Next(random, 0.1f, 1)).ToArray();
        var sum = weights.Sum();
        return new VertexJointInfo
        {
            JointIndex1 = random.Next(40),
            JointIndex2 = amount > 1 ? random.Next(40) : 0,
            JointIndex3 = amount > 2 ? random.Next(40) : 0,
            Weight1 = weights[0] / sum,
            Weight2 = amount > 1 ? weights[1] / sum : 0,
            Weight3 = amount > 2 ? weights[2] / sum : 0,
            WeightsAmount = amount,
            Connection = random.Next(4) != 0
        };
    }

    private static (int, int, int, int, bool) Joints(VertexJointInfo joints)
    {
        var amount = joints.WeightsAmount;
        return (joints.JointIndex1, amount > 1 ? joints.JointIndex2 : 0, amount > 2 ? joints.JointIndex3 : 0, amount, joints.Connection);
    }

    private static float Next(Random random, float min, float max) => min + random.NextSingle() * (max - min);

    private static List<uint> Bits(IEnumerable<Vector4> vectors, int components)
    {
        return vectors.SelectMany(vector => new[] { vector.GetBinaryX(), vector.GetBinaryY(), vector.GetBinaryZ(), vector.GetBinaryW() }.Take(components)).ToList();
    }

    private static void AssertClose(Vector4 expected, Vector4 actual, float tolerance)
    {
        Assert.True(Math.Abs(expected.X - actual.X) <= tolerance && Math.Abs(expected.Y - actual.Y) <= tolerance &&
                    Math.Abs(expected.Z - actual.Z) <= tolerance && Math.Abs(expected.W - actual.W) <= tolerance, $"Expected {expected} but got {actual}");
    }

    private static byte[] Serialize(ITwinSerializable item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static T Deserialize<T>(T item, byte[] bytes) where T : ITwinSerializable
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        item.Read(reader, bytes.Length);
        return item;
    }
}
