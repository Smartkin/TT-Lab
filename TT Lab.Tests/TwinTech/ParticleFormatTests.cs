using System.Text;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Particles;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;

namespace TT_Lab.Tests.TwinTech;

public sealed class ParticleFormatTests
{
    private static TwinParticleSystem MakeSystem(string name)
    {
        var system = new TwinParticleSystem();
        name.CopyTo(0, system.Name, 0, name.Length);
        system.GenRate = -3;
        system.MaxParticleCount = 7;
        system.TimingOffset = 4;
        system.EmitterOverTime = 6;
        system.EmitterOffTime = 54;
        system.GenSort = (byte)TwinParticleSystem.GenSortType.Radial;
        system.BlendMode = 2;
        system.CutOffRadius = 100;
        system.DrawCutOff = 9999.9f;
        system.Velocity = 5.5f;
        system.RandomEmit = new Vector3(0, 32768, 0);
        system.RandomStart = new Vector3(0.5f, 32768, -16384);
        system.StartBase = new Vector3(1, 2, 3);
        system.Gravity = -2.5f;
        system.ParticleLifeTime = 0.75f;
        system.TextureFrameCount = 16;
        system.TextureFrameStart = 1;
        system.TextureFrameHold = 3;
        system.TextureFrameRate = 320;
        system.JibberXFreq = 0.5f;
        for (var i = 0; i < 8; i++)
        {
            system.ColorGradients[i] = new Vector4(i / 7.0f, 255, 128, 64);
            system.AlphaGradient[i] = new Vector2 { X = i / 7.0f, Y = 255 - i };
            system.SizeWidth[i] = new Vector2 { X = i / 7.0f, Y = 4368 };
            system.SizeHeight[i] = new Vector2 { X = i / 7.0f, Y = 4368 };
            system.Rotation[i] = new Vector2 { X = i / 7.0f, Y = 300 };
            system.UnusedGradient1[i] = new Vector2 { X = i == 0 ? 0 : 1 };
            system.UnusedGradient2[i] = new Vector2 { X = i == 0 ? 0 : 1 };
            system.CollisionRadius[i] = new Vector2 { X = i / 7.0f, Y = 0.1f * i };
        }

        system.Distortion = new Vector2 { X = 0.125f, Y = 0.125f };
        system.MaxSize = 500;
        system.MinRotation = -360;
        system.MaxRotation = 360;
        system.TextureStart = new Vector2 { X = 524288 + 66, Y = 524288 + 2 };
        system.TextureEnd = new Vector2 { X = 524288 + 128, Y = 524288 + 62 };
        system.CollisionNumSpheres = 2;
        system.ParticleGhostsNum = 1;
        system.GhostSeparation = 0.072f;
        system.StarRadialPoints = 5;
        system.StarRadiusRatio = 0.5f;
        system.TexturePage = 2;
        system.ScaleFactor = 1.525f;
        system.BoundingExtents = new Vector4(10, 10, 10, 0);
        return system;
    }

    private static byte[] Write(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Latin1, true))
        {
            write(writer);
        }

        return stream.ToArray();
    }

    // Every field of a system comes back the way it was written
    [Fact]
    public void SystemsRoundTrip()
    {
        var bytes = Write(MakeSystem("RING").Write);
        var read = new TwinParticleSystem(0x1E);
        using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.Latin1);
        read.Read(reader, bytes.Length);

        Assert.Equal(bytes.Length, read.GetLength());
        Assert.Equal(-3, read.GenRate);
        Assert.Equal(4, read.TimingOffset);
        Assert.Equal((byte)TwinParticleSystem.GenSortType.Radial, read.GenSort);
        Assert.Equal(2, read.BlendMode);
        Assert.Equal(-16384, read.RandomStart.Z);
        Assert.Equal(new Vector3(1, 2, 3).Y, read.StartBase.Y);
        Assert.Equal(16, read.TextureFrameCount);
        Assert.Equal(3, read.TextureFrameHold);
        Assert.Equal(0.1f * 3, read.CollisionRadius[3].Y, 5);
        Assert.Equal(1.525f, read.ScaleFactor);
        Assert.Equal(bytes, Write(read.Write));
    }

    // The default chunk's particle data holds the decal system: its packet, the type markers and a type with named variants
    [Fact]
    public void DefaultDataRoundTripsItsDecals()
    {
        var data = new PS2DefaultParticleData();
        data.TextureIDs = [1, 2, 3];
        data.MaterialIDs = [4, 5, 6];
        data.ParticleSystems.Add(MakeSystem("RING"));
        data.DecalTextureID = 7;
        data.DecalMaterialID = 8;
        data.UnusedDecalInt = 1;
        data.DecalUvPacket.Uvs[0] = new Vector4(0.5f, 0.0078125f, 1, 0.0078125f);
        data.DecalUvPacket.UvCount = 2;
        data.DecalTypeMarkers[0] = 0x03658630;
        var type = new TwinDecalType { VariantCount = 2, Leftover2 = 10 };
        type.Variants[0].Colors[1] = new Vector4(199.8f, 245.8f, 255, 132.7f);
        type.Variants[0].Sizes[0] = new Vector4(0.434f, 1, 0.434f, 2.21f);
        "Ripple".CopyTo(0, type.Names[0].Name, 0, 6);
        "FootFall".CopyTo(0, type.Names[1].Name, 0, 8);
        type.Names[1].Leftover1 = 0xF90100;
        data.DecalTypes.Add(type);

        var bytes = Write(data.Write);
        var read = new PS2DefaultParticleData();
        using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.Latin1);
        read.Read(reader, bytes.Length);

        Assert.Equal(bytes.Length, read.GetLength());
        Assert.Equal(TwinDecalType.Length, type.GetLength());
        Assert.Equal(2, read.DecalUvPacket.UvCount);
        Assert.Equal(0.5f, read.DecalUvPacket.Uvs[0].X);
        var readType = Assert.Single(read.DecalTypes);
        Assert.Equal(2, readType.VariantCount);
        Assert.Equal("Ripple", new string(readType.Names[0].Name).TrimEnd('\0'));
        Assert.Equal("FootFall", new string(readType.Names[1].Name));
        Assert.Equal(2.21f, readType.Variants[0].Sizes[0].W);
        Assert.Equal(bytes, Write(read.Write));
    }

    // Emitters keep their rotations and plane
    [Fact]
    public void EmittersRoundTrip()
    {
        var emitter = new TwinParticleEmitter { Position = new Vector3(1, 2, 3), EmitTilt = -16384, EmitYaw = 8192, EmitRoll = 100, TimingOffset = 5, BouncePlaneAngle = 4096, PlaneOffset = -1, BounceFactor = 0.5f, GroupId = 2, SwitchId = -1 };
        "DUST".CopyTo(0, emitter.Name, 0, 4);
        var bytes = Write(emitter.Write);
        var read = new TwinParticleEmitter(0x1E);
        using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.Latin1);
        read.Read(reader, bytes.Length);

        Assert.Equal(68, bytes.Length);
        Assert.Equal(-16384, read.EmitTilt);
        Assert.Equal(100, read.EmitRoll);
        Assert.Equal(4096, read.BouncePlaneAngle);
        Assert.Equal(bytes, Write(read.Write));
    }
}
