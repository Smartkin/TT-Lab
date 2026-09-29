using Newtonsoft.Json;
using TT_Lab.AssetData.Instance.Particle;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Particles;

namespace TT_Lab.Tests.Assets;

// The game reads a system's or emitter's 16 character name up to its NUL. The default chunk's systems have leftovers of the tools' memory
// after it, which used to become part of the name (an emitter naming the system then didn't match it) and are now kept apart
public sealed class ParticleNamesTests
{
    // The curves' keys are objects the data copies
    private static TwinParticleSystem MakeSystem(char[] name)
    {
        var system = new TwinParticleSystem { Name = name, Distortion = new Vector2(), TextureStart = new Vector2(), TextureEnd = new Vector2() };
        for (var i = 0; i < 8; i++)
        {
            system.ColorGradients[i] = new Vector4();
            system.AlphaGradient[i] = new Vector2();
            system.SizeWidth[i] = new Vector2();
            system.SizeHeight[i] = new Vector2();
            system.Rotation[i] = new Vector2();
            system.UnusedGradient1[i] = new Vector2();
            system.UnusedGradient2[i] = new Vector2();
            system.CollisionRadius[i] = new Vector2();
        }

        return system;
    }

    private static char[] Buffer(string name, string leftover)
    {
        var buffer = new char[16];
        name.CopyTo(0, buffer, 0, name.Length);
        leftover.CopyTo(0, buffer, name.Length + 1, leftover.Length);
        return buffer;
    }

    [Fact]
    public void SystemNamesEndAtTheirNulAndKeepWhatFollowed()
    {
        var twin = MakeSystem(Buffer("NT_BREAK", "K"));

        var system = new ParticleSystem(twin);

        Assert.Equal("NT_BREAK", system.Name);
        Assert.Equal("K", system.NameLeftover);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        system.Write(writer);
        writer.Flush();
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        var read = new TwinParticleSystem(system.Version);
        read.Read(reader, (int)stream.Length);
        Assert.Equal(twin.Name, read.Name);
    }

    [Fact]
    public void EmitterNamesEndAtTheirNulAndKeepWhatFollowed()
    {
        var twin = new TwinParticleEmitter { Name = Buffer("RAV_DOTS", "A") };

        var emitter = new ParticleSystemInstance(twin);

        Assert.Equal("RAV_DOTS", emitter.Name);
        Assert.Equal("A", emitter.NameLeftover);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        emitter.Write(writer);
        writer.Flush();
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        var read = new TwinParticleEmitter(emitter.Version);
        read.Read(reader, (int)stream.Length);
        Assert.Equal(twin.Name, read.Name);
    }

    [Fact]
    public void DecalVariantNamesEndAtTheirNulAndKeepWhatFollowed()
    {
        var twinName = new TwinDecalVariantName { Name = ['N', 'U', 'L', 'L', '\0', '\u00E5', 'L', '\0'], Leftover1 = 7, Leftover2 = 9 };

        var variant = new DecalVariant(new TwinDecalVariant(), twinName);

        Assert.Equal("NULL", variant.Name);
        Assert.Equal("\u00E5L", variant.NameLeftover);
        var written = variant.ToTwinName();
        Assert.Equal(twinName.Name, written.Name);
        Assert.Equal((7u, 9u), (written.Leftover1, written.Leftover2));
    }

    [Fact]
    public void CleanNamesHaveNoLeftover()
    {
        var system = new ParticleSystem(MakeSystem(Buffer("DUST", "")));

        Assert.Equal("DUST", system.Name);
        Assert.Null(system.NameLeftover);
        Assert.DoesNotContain("NameLeftover", JsonConvert.SerializeObject(system));
        Assert.Equal(Buffer("DUST", ""), ParticleNames.Join(system.Name, system.NameLeftover));
        // A name filling the buffer has no NUL and nothing after it
        Assert.Equal("SIXTEENCHARSLONG", ParticleNames.Split("SIXTEENCHARSLONG".ToCharArray()).Name);
        Assert.Equal("SIXTEENCHARSLONG".ToCharArray(), ParticleNames.Join("SIXTEENCHARSLONGER", "X"));
    }
}
