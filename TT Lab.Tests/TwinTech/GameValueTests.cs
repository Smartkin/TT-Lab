using TT_Lab.AssetData.Instance;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Tests.TwinTech;

// The values the game reads of sounds, chunk links, templates, triggers and cameras keep their bytes through their names
public sealed class GameValueTests
{
    private static byte[] Write(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    [Theory]
    [InlineData(8000, 0x2AA)]
    [InlineData(11025, 0x3AC)]
    [InlineData(16000, 0x555)]
    [InlineData(22050, 0x759)]
    [InlineData(32000, 0xAAA)]
    [InlineData(44100, 0xEB3)]
    [InlineData(48000, 0x1000)]
    public void SoundPitchIsTheSpuPitchOfTheSampleRate(int rate, int pitch)
    {
        Assert.Equal(pitch, ITwinSound.PitchOf((uint)rate));
        Assert.Equal(rate, ITwinSound.SampleRateOf((ushort)pitch));
    }

    [Fact]
    public void SoundKeepsAnOddPitchAndTheXboxItsRate()
    {
        var sound = new PS2AnySound { Header = 3, Pitch = 0xAAB, Param1 = 32, Param2 = 16, Param3 = 8192, Param4 = 8192, Sound = new byte[16] };
        var bytes = Write(sound.Write);
        Assert.Equal(0xAB, bytes[4]);
        Assert.Equal(0x0A, bytes[5]);
        Assert.Equal(32004, sound.GetFreq());
        sound.SetFreq(sound.GetFreq());
        Assert.Equal(0xAAB, sound.Pitch);

        var xbox = new XboxAnySound { Sound = new byte[16] };
        xbox.SetFreq(22050);
        Assert.Equal(0x759, xbox.Pitch);
        // The pitch its rate gives changes nothing, another one sets the rate it plays at
        xbox.Pitch = 0x759;
        Assert.Equal(22050, xbox.GetFreq());
        xbox.Pitch = 0xAAA;
        Assert.Equal(32000, xbox.GetFreq());
    }

    [Fact]
    public void ChunkLinkVisibilityIsTheFlagsLowBits()
    {
        var link = new TwinChunkLink { Path = "levels\\x", Visibility = ChunkLinkVisibility.ThroughLoadWall, KeepLoaded = true, LoadsWithoutPlayer = true };
        var bytes = Write(link.Write);
        var read = new TwinChunkLink();
        read.Read(new BinaryReader(new MemoryStream(bytes)), bytes.Length);
        Assert.Equal(ChunkLinkVisibility.ThroughLoadWall, read.Visibility);
        Assert.True(read.KeepLoaded);
        Assert.True(read.LoadsWithoutPlayer);
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(0x82u, BitConverter.ToUInt32(bytes, 4 + 4 + link.Path.Length));
    }

    [Fact]
    public void TemplateWritesItsPropertiesHeaderFromTheLists()
    {
        var template = new PS2AnyTemplate { Name = "HEALTH", ObjectId = 2, ObjectSubType = 1, ObjectType = 1, InstanceStateFlags = (Enums.InstanceState)0x10E };
        template.Floats.Add(1.0f);
        template.Ints.Add(0);
        template.Ints.Add(255);
        var bytes = Write(template.Write);
        var read = new PS2AnyTemplate();
        read.Read(new BinaryReader(new MemoryStream(bytes)), bytes.Length);

        Assert.Equal(0x20100u, BitConverter.ToUInt32(bytes, 4 + 6 + 2 + 2 + 12 + 2));
        Assert.Equal((Enums.InstanceState)0x10E, read.InstanceStateFlags);
        Assert.Equal(10u, read.BehaviourListGrowth);
        Assert.Equal(template.GetLength(), bytes.Length);
    }

    [Fact]
    public void TriggerKindAndPollingLiveInTheHeader()
    {
        var data = new TriggerData(null!) { Header = 0x1832 };
        Assert.Equal(0x32, (byte)data.Header);
        Assert.True(((Enums.TriggerFlags)data.Header).HasFlag(Enums.TriggerFlags.OnEnterOnce));
        Assert.True(((Enums.TriggerFlags)data.Header).HasFlag(Enums.TriggerFlags.NotPolled));
    }

    [Fact]
    public void CameraFlagsRoundTrip()
    {
        var camera = new PS2AnyCamera
        {
            Flags = ITwinCamera.CameraFlags.SetsPitch | ITwinCamera.CameraFlags.ValuesAlongGeometry | ITwinCamera.CameraFlags.Controller31,
            Switches = ITwinCamera.CameraSwitches.ResetsController, PitchStart = 3640, PitchEnd = 3640, DistanceStart = 5, Group = 2,
            TypeIndex1 = ITwinCamera.CameraType.Null, TypeIndex2 = ITwinCamera.CameraType.Null
        };
        var bytes = Write(camera.Write);
        var read = new PS2AnyCamera();
        read.Read(new BinaryReader(new MemoryStream(bytes)), bytes.Length);
        Assert.Equal(camera.Flags, read.Flags);
        Assert.Equal(ITwinCamera.CameraSwitches.ResetsController, read.Switches);
        Assert.Equal(3640u, read.PitchEnd);
        Assert.Equal(5f, read.DistanceStart);
        Assert.Equal(2, read.Group);
    }
}
