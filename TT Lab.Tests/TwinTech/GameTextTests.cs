using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Particles;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace TT_Lab.Tests.TwinTech;

// The game's names are a byte per character and the build's writers encode text as UTF-8: a name with a character past ASCII (typed
// into a particle system's or an object's name) came out longer than its item said, and the rest of the item was read from the wrong place
public sealed class GameTextTests
{
    // A writer like the build's, of the default encoding
    private static byte[] Write(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            write(writer);
        }

        return stream.ToArray();
    }

    private static BinaryReader Reader(byte[] bytes) => new(new MemoryStream(bytes));

    [Fact]
    public void NamesAreAByteACharacter()
    {
        var template = new PS2AnyTemplate { Name = "Ящик ü" };
        var bytes = Write(template.Write);
        Assert.Equal(template.GetLength(), bytes.Length);
        var readTemplate = new PS2AnyTemplate();
        readTemplate.Read(Reader(bytes), bytes.Length);
        // Latin-1's characters stay, the others become '?'
        Assert.Equal("???? ü", readTemplate.Name);

        var system = new TwinParticleSystem { Distortion = new Vector2(), TextureStart = new Vector2(), TextureEnd = new Vector2() };
        for (var i = 0; i < 8; i++)
        {
            system.ColorGradients[i] = new Vector4();
            system.AlphaGradient[i] = system.SizeWidth[i] = system.SizeHeight[i] = system.Rotation[i] = new Vector2();
            system.UnusedGradient1[i] = system.UnusedGradient2[i] = system.CollisionRadius[i] = new Vector2();
        }

        "FEU_ÉTÉ€".CopyTo(0, system.Name, 0, 8);
        bytes = Write(system.Write);
        var readSystem = new TwinParticleSystem(0x1E);
        readSystem.Read(Reader(bytes), bytes.Length);
        Assert.Equal(readSystem.GetLength(), bytes.Length);
        Assert.Equal("FEU_ÉTÉ?", new string(readSystem.Name).TrimEnd('\0'));

        var emitter = new TwinParticleEmitter();
        "Взрыв".CopyTo(0, emitter.Name, 0, 5);
        bytes = Write(emitter.Write);
        Assert.Equal(68, bytes.Length);

        var link = new TwinChunkLink { Path = "levels\\earth\\Пляж" };
        bytes = Write(link.Write);
        Assert.Equal(link.GetLength(), bytes.Length);
    }

    // Every byte the tools left in a name comes back the way it was
    [Fact]
    public void EveryByteOfANameReadsBackAsItWas()
    {
        var name = new string(Enumerable.Range(1, 255).Select(i => (char)i).ToArray());
        var material = new PS2AnyMaterial { Name = name };
        var bytes = Write(material.Write);
        Assert.Equal(material.GetLength(), bytes.Length);

        var read = new PS2AnyMaterial();
        read.Read(Reader(bytes), bytes.Length);

        Assert.Equal(name, read.Name);
        Assert.Equal(bytes, Write(read.Write));
    }
}
