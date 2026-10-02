using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Tests.TwinTech;

// The scenery lights as the PAL executable reads them: the header's type byte and enabled bit, the intensity every kind multiplies
// its color by, the point and spot lights' attenuation and the bounds the game works out again at load
public class LightTests
{
    [Fact]
    public void HeaderIsTheTypeAndTheEnabledBit()
    {
        var lights = new Light[] { new AmbientLight(), new DirectionalLight(), new PointLight(), new SpotLight { Enabled = false } };

        var headers = lights.Select(light => BitConverter.ToUInt32(Write(light), 0)).ToList();

        Assert.Equal(new uint[] { 0x100, 0x101, 0x102, 0x3 }, headers);
        Assert.False(Read<SpotLight>(Write(lights[3])).Enabled);
        Assert.True(Read<PointLight>(Write(lights[2])).Enabled);
    }

    [Fact]
    public void LightsComeBackFromTheirBytes()
    {
        var spot = new SpotLight
        {
            Intensity = 2.5f, Color = new Vector4(1, 0.5f, 0.25f, 1), Position = new Vector4(1, 2, 3, 1), Direction = new Vector4(0, -1, 0, 0),
            InnerConeCosine = 0.6f, OuterConeCosine = 0.5f, ConeAngle = 18956, FalloffAngle = 917, AttenuationPower = 1, SpotExponent = 4
        };
        var point = new PointLight { Intensity = 60, AttenuationPower = 2, Position = new Vector4(5, 6, 7, 1) };
        var directional = new DirectionalLight { Intensity = 1.5f, Direction = new Vector4(0.6f, 0.8f, 0, 0), Leftover = 6922 };

        var bytes = (Spot: Write(spot), Point: Write(point), Directional: Write(directional));

        Assert.Equal(spot.GetLength(), bytes.Spot.Length);
        Assert.Equal(point.GetLength(), bytes.Point.Length);
        Assert.Equal(directional.GetLength(), bytes.Directional.Length);
        Assert.Equal(bytes.Spot, Write(Read<SpotLight>(bytes.Spot)));
        Assert.Equal(bytes.Point, Write(Read<PointLight>(bytes.Point)));
        Assert.Equal(bytes.Directional, Write(Read<DirectionalLight>(bytes.Directional)));
        var read = Read<SpotLight>(bytes.Spot);
        Assert.Equal((18956u, 917u, (ushort)1, (ushort)4), (read.ConeAngle, read.FalloffAngle, read.AttenuationPower, read.SpotExponent));
    }

    // The game boxes every light in at load: a point light 100 times its intensity around it, an ambient one 100000 times
    [Fact]
    public void BoundsAreTheGamesBoxes()
    {
        var point = new PointLight { Intensity = 60, Position = new Vector4(10, 20, 30, 1) };
        var ambient = new AmbientLight { Intensity = 1.5f };

        point.ComputeBounds();
        ambient.ComputeBounds();

        Assert.Equal((-5990f, -5980f, -5970f, 1f), (point.BoundsMin.X, point.BoundsMin.Y, point.BoundsMin.Z, point.BoundsMin.W));
        Assert.Equal((6010f, 6020f, 6030f, 1f), (point.BoundsMax.X, point.BoundsMax.Y, point.BoundsMax.Z, point.BoundsMax.W));
        Assert.Equal((-150000f, 150000f), (ambient.BoundsMin.X, ambient.BoundsMax.Y));
    }

    // A point light's attenuation at distance d is 25 / (d² + 25), applied as many times as its power says
    [Fact]
    public void PointLightsFadeByTheirAttenuationPower()
    {
        var light = new PointLight { Intensity = 8, AttenuationPower = 2 };

        Assert.Equal(8f, light.IntensityAt(0), 1e-5f);
        Assert.Equal(8f * 0.5f * 0.5f, light.IntensityAt(5), 1e-5f);
        light.AttenuationPower = 0;
        Assert.Equal(8f, light.IntensityAt(100), 1e-5f);
    }

    // The retail data's cone of 18956 units (104 degrees) with a falloff of 917 (5 degrees) gives the cosines the game lights with
    [Fact]
    public void SpotConesGiveTheGamesCosines()
    {
        var light = new SpotLight();

        light.SetCone(104.128f, 5.037f);

        Assert.Equal(18956u, light.ConeAngle);
        Assert.Equal(917u, light.FalloffAngle);
        Assert.Equal(0.6149f, light.InnerConeCosine, 1e-3f);
        Assert.Equal(0.5432f, light.OuterConeCosine, 1e-3f);
        Assert.Equal(104.128f, light.ConeAngleDegrees, 1e-2f);
        Assert.Equal((light.InnerConeCosine, light.OuterConeCosine), SpotLight.ConeCosines(18956, 917));
    }

    private static byte[] Write(ITwinSerializable item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static T Read<T>(byte[] bytes) where T : ITwinSerializable, new()
    {
        var item = new T();
        using var reader = new BinaryReader(new MemoryStream(bytes));
        item.Read(reader, bytes.Length);
        Assert.Equal(bytes.Length, reader.BaseStream.Position);
        return item;
    }
}
