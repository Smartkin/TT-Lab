using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;

namespace TT_Lab.Tests.TwinTech;

// An object's template block (its header's bits 28 and 29) holds the state flags its instances get next to its property values: an object of
// flags and no values has it too. The flags set on such an object (the beach's seagull's) reached no file
public sealed class ObjectTemplateBlockTests
{
    private const UInt32 TemplateBits = 0x30000000;

    public static TheoryData<string> Versions => new() { "PS2", "Xbox" };

    private static PS2AnyObject Make(string version)
    {
        return version == "PS2"
            ? new PS2AnyObject { Name = "SEAGULL", BehaviourPack = new PS2BehaviourCommandPack() }
            : new XboxAnyObject { Name = "SEAGULL", BehaviourPack = new XboxBehaviourCommandPack() };
    }

    private static byte[] Write(PS2AnyObject gameObject)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        gameObject.Write(writer);
        writer.Flush();
        Assert.Equal(gameObject.GetLength(), (int)stream.Length);
        return stream.ToArray();
    }

    private static PS2AnyObject Read(string version, byte[] bytes)
    {
        var read = version == "PS2" ? new PS2AnyObject() : new XboxAnyObject();
        using var reader = new BinaryReader(new MemoryStream(bytes));
        read.Read(reader, bytes.Length);
        Assert.Equal(bytes.Length, reader.BaseStream.Position);
        return read;
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void AnObjectOfStateFlagsAndNoValuesKeepsItsFlags(string version)
    {
        var gameObject = Make(version);
        gameObject.InstanceStateFlags = Enums.InstanceState.Visible | Enums.InstanceState.CollisionActive;

        var bytes = Write(gameObject);

        Assert.Equal(TemplateBits, BitConverter.ToUInt32(bytes, 0) & TemplateBits);
        var read = Read(version, bytes);
        Assert.Equal(gameObject.InstanceStateFlags, read.InstanceStateFlags);
        Assert.Empty(read.TaggedProperties);
        Assert.Empty(read.FloatProperties);
        Assert.Empty(read.IntProperties);
        Assert.Equal(bytes, Write(read));
    }

    // Read without the block an object's flags are 0, it's written back without one
    [Theory]
    [MemberData(nameof(Versions))]
    public void AnObjectOfNoFlagsAndNoValuesHasNoBlock(string version)
    {
        var bytes = Write(Make(version));

        Assert.Equal(0u, BitConverter.ToUInt32(bytes, 0) & TemplateBits);
        var read = Read(version, bytes);
        Assert.Equal((Enums.InstanceState)0, read.InstanceStateFlags);
        Assert.Equal(bytes, Write(read));
    }
}
