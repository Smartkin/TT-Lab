using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace TT_Lab.Tests.TwinTech;

// Layout items keep the values the game reads where the game reads them (the decompilation's ObjectInstance, AiPath and LayoutPath)
public sealed class LayoutItemTests
{
    private static byte[] Write(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static T Read<T>(byte[] bytes) where T : Twinsanity.TwinsanityInterchange.Interfaces.ITwinSerializable, new()
    {
        var item = new T();
        using var reader = new BinaryReader(new MemoryStream(bytes));
        item.Read(reader, bytes.Length);
        return item;
    }

    [Fact]
    public void InstanceTurnsAreSignedWordsAndListsCountFirst()
    {
        var instance = new PS2AnyInstance { RotationX = -16384, RotationY = 49152, RotationZ = 0, ObjectId = 7, RefListIndex = -1, SpawnScriptId = 0xFFFF };
        instance.Instances.AddRange([3, 4]);
        instance.InstancesGrowth = 10;
        instance.TaggedProperties.Add(0xFFFF);
        instance.IntProperties.Add(-1);

        var bytes = Write(instance.Write);

        Assert.Equal(instance.GetLength(), bytes.Length);
        // The three turns after the position, a quarter turn back sign extended
        Assert.Equal(0xFFFFC000u, BitConverter.ToUInt32(bytes, 16));
        Assert.Equal(49152, BitConverter.ToInt32(bytes, 20));
        // The instance list: its count, its room and its growth, then the IDs
        Assert.Equal((2, 2, 10), (BitConverter.ToInt32(bytes, 28), BitConverter.ToInt32(bytes, 32), BitConverter.ToInt32(bytes, 36)));
        var read = Read<PS2AnyInstance>(bytes);
        Assert.Equal((-16384, 49152), (read.RotationX, read.RotationY));
        Assert.Equal([0xFFFFu], read.TaggedProperties);
        Assert.Equal([-1], read.IntProperties);
        Assert.Equal(bytes, Write(read.Write));
    }

    [Fact]
    public void AiPathsHaveTheirPositionsFlagsAndTheToolsChunks()
    {
        var path = new PS2AnyAIPath { PositionA = 1, PositionB = 2, Flags = Enums.AiPathFlags.NeedsJump | Enums.AiPathFlags.Flag8, ChunkA = 3, ChunkB = 3 };

        var bytes = Write(path.Write);

        Assert.Equal(new ushort[] { 1, 2, 0x104, 3, 3 }, Enumerable.Range(0, 5).Select(i => BitConverter.ToUInt16(bytes, i * 2)));
        Assert.Equal(bytes, Write(Read<PS2AnyAIPath>(bytes).Write));
    }

    [Fact]
    public void PathParametersAreTheLengthsThenTheSteps()
    {
        var path = new PS2AnyPath { PointList = [new Vector4(0, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(2, 0, 0, 1), new Vector4(3, 0, 0, 1), new Vector4(4, 0, 0, 1)] };
        path.ArcLengths.AddRange([10.0f, 20.0f]);
        path.InverseSteps.AddRange([0.2f, 0.25f]);

        var bytes = Write(path.Write);

        Assert.Equal(path.GetLength(), bytes.Length);
        var parameters = 4 + 5 * 16;
        Assert.Equal(2, BitConverter.ToInt32(bytes, parameters));
        Assert.Equal([10.0f, 20.0f, 0.2f, 0.25f], Enumerable.Range(0, 4).Select(i => BitConverter.ToSingle(bytes, parameters + 4 + i * 4)));
        var read = Read<PS2AnyPath>(bytes);
        Assert.Equal([10.0f, 20.0f], read.ArcLengths);
        Assert.Equal([0.2f, 0.25f], read.InverseSteps);
    }
}
