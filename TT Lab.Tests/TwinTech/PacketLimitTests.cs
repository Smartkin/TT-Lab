using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets.Factory;
using TT_Lab.MeshProcessor;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems;
using TwinVector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.Tests.TwinTech;

// A packet goes out with one DMA tag whose size field is 16 bits. A skin made in Blender with several times the vertexes of the
// game's overflowed it, the game sent a fifth of the skin and hung waiting for the rest
public sealed class PacketLimitTests
{
    private static StripLayout Layout(int batches, int vertexesPerBatch)
    {
        var layout = new StripLayout();
        for (var batch = 0; batch < batches; batch++)
        {
            layout.Batches.Add(new StripBatch { Vertexes = Enumerable.Range(0, vertexesPerBatch).Select(i => new StripVertex(i, i >= 2)).ToList() });
        }

        return layout;
    }

    // A sub skin's material, packet size and vertex amount, or a sub model's vertex count and packet size, come before the packet
    private static byte[] Packet(ITwinSerializable item, int headerBytes)
    {
        item.Compile();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray()[headerBytes..];
    }

    private static void AssertFitsADmaTag(byte[] packet)
    {
        var quadWords = packet.Length / 16 - 1;
        Assert.True(quadWords <= 0xFFFF, $"{quadWords} quad words");
        var tag = BitConverter.ToUInt64(packet, 0);
        Assert.Equal((ulong)quadWords, tag & 0xFFFF);
        Assert.Equal(6UL, tag >> 28 & 0x7);
    }

    [Fact]
    public void BatchSizesAreWhatTheCompilerWrites()
    {
        var skinBatch = new TwinVIFCompiler.SkinBatch();
        var rigidBatch = new TwinVIFCompiler.RigidBatch { Normals = [], EmitColors = [] };
        for (var i = 0; i < 38; i++)
        {
            skinBatch.Positions.Add([i, 0, 0, 1]);
            skinBatch.Uvs.Add([0, 0, 0, 0]);
            skinBatch.Colors.Add(new Color(1, 2, 3, 4));
            skinBatch.Joints.Add(new VertexJointInfo { Weight1 = 1.0f, WeightsAmount = 1, Connection = true });
            rigidBatch.Positions.Add(new TwinVector4(i, 0, 0, 1));
            rigidBatch.Uvs.Add(new TwinVector4(0, 0, 1, 0));
            rigidBatch.Colors.Add(new Color(1, 2, 3, 4));
            rigidBatch.Adc.Add(i < 2);
            rigidBatch.Normals.Add(new TwinVector4(0, 1, 0, 0));
            rigidBatch.EmitColors.Add(new Color(5, 6, 7, 8));
        }

        var skin = TwinVIFCompiler.CompileSkin([skinBatch, skinBatch], new TwinSkinCompression(), false, TwinVifPadding.QuadWord);
        var rigid = TwinVIFCompiler.CompileRigid([rigidBatch, rigidBatch], TwinVifPadding.QuadWord);

        Assert.Equal(RoundUp(16 + 2 * TwinVIFCompiler.SkinBatchBytes(38)), skin.Length);
        Assert.Equal(RoundUp(16 + 2 * TwinVIFCompiler.RigidBatchBytes(38, true, true)), rigid.Length);
        rigidBatch.Normals = null;
        rigidBatch.EmitColors = null;
        Assert.Equal(RoundUp(16 + 2 * TwinVIFCompiler.RigidBatchBytes(38, false, false)), TwinVIFCompiler.CompileRigid([rigidBatch, rigidBatch], TwinVifPadding.QuadWord).Length);
    }

    [Fact]
    public void PartsBeyondOnePacketBecomeSeveralSubSkins()
    {
        var vertexes = Enumerable.Range(0, 38).Select(i => new Vertex(new TwinVector4(i, 0, 0, 0), new TwinVector4(0.5f, 0.5f, 0.5f, 0.5f), new TwinVector4(0, 0, 1, 0))
        {
            JointInfo = new VertexJointInfo { Weight1 = 1.0f, WeightsAmount = 1, Connection = true }
        }).ToList();
        // 1000 batches of 38 are close to 1.5 MB of packet
        var part = new SkinPartExport(7, vertexes, Layout(1000, 38), null);

        var skin = new PS2ItemFactory().GenerateSkin([part]);

        Assert.Equal(2, skin.SubSkins.Count);
        Assert.Equal(1000, skin.SubSkins.Sum(sub => sub.GroupSizes.Count));
        Assert.Equal(38000, skin.SubSkins.Sum(sub => sub.Vertexes.Count));
        Assert.All(skin.SubSkins, sub => Assert.Equal(7u, sub.Material));
        Assert.All(skin.SubSkins, sub => AssertFitsADmaTag(Packet(sub, 12)));
        // Small parts stay one sub skin
        Assert.Single(new PS2ItemFactory().GenerateSkin([new SkinPartExport(7, vertexes, Layout(10, 38), null)]).SubSkins);
    }

    [Fact]
    public void PartsBeyondOnePacketBecomeSeveralSubModels()
    {
        var vertexes = Enumerable.Range(0, 38).Select(i => new Vertex(new TwinVector4(i, 0, 0, 0), new TwinVector4(0.5f, 0.5f, 0.5f, 1.0f), new TwinVector4(0, 0, 1, 0))
        {
            Normal = new TwinVector4(0, 1, 0, 0)
        }).ToList();

        var model = new PS2ItemFactory().GenerateModel([new RigidPartExport(vertexes, Layout(1000, 38))]);

        Assert.Equal(2, model.SubModels.Count);
        Assert.Equal(1000, model.SubModels.Sum(sub => sub.GroupSizes.Count));
        Assert.All(model.SubModels, sub => AssertFitsADmaTag(Packet(sub, 8)));
    }

    [Fact]
    public void APacketBeyondADmaTagIsRefused()
    {
        var writer = new TwinVifPacket.Writer();
        for (var i = 0; i < 300; i++)
        {
            writer.Unpack(0, PackFormat.V4_32, 255, Enumerable.Range(0, 255 * 4).Select(value => (uint)value));
        }

        Assert.Throws<InvalidOperationException>(() => writer.Finish(TwinVifPadding.NopPerByte));
    }

    private static int RoundUp(int bytes) => (bytes + 15) / 16 * 16;
}
