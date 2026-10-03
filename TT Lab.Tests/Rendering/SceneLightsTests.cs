using GlmSharp;
using TT_Lab.Rendering.Lighting;
using TT_Lab.Rendering.Objects;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Lights;

namespace TT_Lab.Tests.Rendering;

// The game lights an object by the scenery's lights at its instance's position (GatherStrongestLights 0x1c7f50, DirectionalLightAt,
// PointLightAt, SpotLightAt, FUN_001c7d50, verified in the PAL executable), and the lit VU1 programs light its vertexes with them
public sealed class SceneLightsTests
{
    private static SceneLights Lights(IReadOnlyList<int>? order, params Light[] lights)
    {
        return SceneLights.From(true, lights.OfType<AmbientLight>().ToList(), lights.OfType<DirectionalLight>().ToList(), lights.OfType<PointLight>().ToList(),
            lights.OfType<SpotLight>().ToList(), order ?? []);
    }

    private static DirectionalLight Directional(float intensity, vec3 direction, float red = 0.5f)
    {
        return new DirectionalLight { Intensity = intensity, Color = new Vector4(red, 0.25f, 0.25f, 0), Direction = new Vector4(direction.x, direction.y, direction.z, 0) };
    }

    private static void Near(vec3 expected, vec3 actual)
    {
        Assert.Equal(expected.x, actual.x, 1e-5f);
        Assert.Equal(expected.y, actual.y, 1e-5f);
        Assert.Equal(expected.z, actual.z, 1e-5f);
    }

    [Fact]
    public void AmbientLightsAddUpAndColorsAreHalved()
    {
        var lights = Lights(null,
            new AmbientLight { Intensity = 3, Color = new Vector4(1 / 3f, 1 / 3f, 1 / 3f, 0) },
            new AmbientLight { Intensity = 2, Color = new Vector4(0.5f, 0, 0, 0) },
            Directional(2, vec3.UnitY));

        var set = lights.At(new vec3(100, -40, 7));

        Near(new vec3(1.0f, 0.5f, 0.5f), set.Ambient);
        Near(vec3.UnitY, set.Direction0);
        Near(new vec3(0.5f, 0.25f, 0.25f), set.Color0);
        // The slots without a light stay black
        Near(vec3.Zero, set.Color1);
        Near(vec3.Zero, set.Color2);
    }

    [Fact]
    public void WithoutLightingNothingLights()
    {
        var lights = SceneLights.From(false, [new AmbientLight { Intensity = 5, Color = new Vector4(1, 1, 1, 0) }], [], [], [], []);

        Assert.Equal(LightSet.Dark, lights.At(vec3.Zero));
    }

    // 25 / (d² + 25) is a half 5 units away, the way to the light is times it rather than a unit vector
    [Fact]
    public void PointLightsFallOffWithDistance()
    {
        var point = new PointLight { Intensity = 2, Color = new Vector4(0.5f, 0.25f, 0.25f, 0), Position = new Vector4(0, 5, 0, 1), AttenuationPower = 1 };

        var once = Lights(null, point).At(vec3.Zero);
        point.AttenuationPower = 2;
        var twice = Lights(null, point).At(vec3.Zero);
        point.AttenuationPower = 0;
        var never = Lights(null, point).At(vec3.Zero);

        Near(new vec3(0, 2.5f, 0), once.Direction0);
        Near(new vec3(0.5f, 0.25f, 0.25f) * 1.0f * 0.5f, once.Color0);
        Near(new vec3(0.5f, 0.25f, 0.25f) * 0.5f * 0.5f, twice.Color0);
        Near(new vec3(0.5f, 0.25f, 0.25f) * 2.0f * 0.5f, never.Color0);
    }

    // The cosines are compared with the dot product of the cone's axis and the way to the light times the falloff
    [Fact]
    public void SpotLightsLightWithinTheirConeAndFadeOutPastIt()
    {
        var spot = new SpotLight
        {
            Intensity = 1, Color = new Vector4(1, 1, 1, 0), Position = new Vector4(1, 0.7f, 0, 1), Direction = new Vector4(0, -1, 0, 0),
            InnerConeCosine = 0.9f, OuterConeCosine = 0.5f
        };
        var delta = new vec3(1, 0.7f, 0);
        var falloff = 25 / (delta.LengthSqr + 25);
        var dot = delta.y * falloff;

        var fading = Lights(null, spot).At(vec3.Zero);
        Near(delta * falloff, fading.Direction0);
        Near(new vec3((dot - 0.5f) / 0.4f * 0.5f), fading.Color0);

        spot.SpotExponent = 0x102;
        var squared = Lights(null, spot).At(vec3.Zero);
        Near(new vec3((dot - 0.5f) / 0.4f * dot * dot * 0.5f), squared.Color0);

        // Right under it the dot product is past the inner cosine: full strength
        spot.SpotExponent = 0;
        var under = Lights(null, spot).At(new vec3(1, -2, 0));
        Near(new vec3(0.5f), under.Color0);

        // Off to the side it gives nothing, yet takes a slot
        var aside = Lights(null, spot, Directional(0.5f, vec3.UnitX)).At(new vec3(-30, 0.7f, 0));
        Near(vec3.UnitX, aside.Direction0);
        Near(vec3.Zero, aside.Color1);
    }

    [Fact]
    public void TheThreeStrongestLightAnObjectAndTheFirstOfEqualOnesWins()
    {
        var lights = Lights(null,
            Directional(1, vec3.UnitX),
            Directional(3, vec3.UnitY),
            Directional(2, vec3.UnitZ),
            Directional(3, -vec3.UnitX));

        var set = lights.At(vec3.Zero);

        Near(vec3.UnitY, set.Direction0);
        Near(-vec3.UnitX, set.Direction1);
        Near(vec3.UnitZ, set.Direction2);
        Near(new vec3(0.5f, 0.25f, 0.25f) * 2 * 0.5f, set.Color2);
    }

    [Fact]
    public void TheScenerysOrderOfLightsDecidesBetweenEqualOnes()
    {
        var directional = Directional(2, vec3.UnitY);
        var point = new PointLight { Intensity = 2, Color = new Vector4(1, 0, 0, 0), Position = new Vector4(0, 0, 5, 1) };

        Near(vec3.UnitY, Lights(null, directional, point).At(vec3.Zero).Direction0);
        // Index and kind of each light, the point light first
        Near(new vec3(0, 0, 2.5f), Lights([0, 2, 0, 1], directional, point).At(vec3.Zero).Direction0);
        // An order that doesn't list every light goes kind by kind
        Near(vec3.UnitY, Lights([0, 2], directional, point).At(vec3.Zero).Direction0);
    }

    [Fact]
    public void LitVertexesAreTheirBytesTimesTheLightClampedTo255()
    {
        var set = new LightSet(new vec3(0.5f), vec3.UnitY * 2, new vec3(0.25f), vec3.Zero, vec3.Zero, vec3.Zero, vec3.Zero);

        Near(new vec3(1.0f), set.At(vec3.UnitY));
        Near(new vec3(0.5f), set.At(-vec3.UnitY));
        // 128 is the texture as it is, the GS takes up to twice it
        Near(new vec3(1.0f), LightSet.Shade(new vec3(128 / 255f), set.At(vec3.UnitY)));
        Near(new vec3(255 / 128f), LightSet.Shade(new vec3(1.0f), new vec3(3.0f)));
    }

    // A third grey ambient light at 4.5 and a third grey directional light at 3 from above
    [Fact]
    public void NewChunksAndSingleModelsGetTheDefaultLights()
    {
        var set = SceneLights.Default.At(new vec3(3, 4, 5));

        Near(new vec3(0.75f), set.Ambient);
        Near(new vec3(0.5f), set.Color0);
        Assert.InRange(set.Direction0.y, 0.9f, 1.0f);
        Assert.Equal(1.0f, set.Direction0.Length, 1e-5f);
    }

    [Fact]
    public void ALightsTurnPointsPlusZAlongItsDirection()
    {
        foreach (var direction in new[] { vec3.UnitZ, -vec3.UnitZ, vec3.UnitY, new vec3(0.3f, -1, 0.3f), new vec3(-2, 0.5f, -0.1f) })
        {
            Near(direction.Normalized, LightDirectionRotation.FromZTo(direction) * vec3.UnitZ);
        }

        Assert.Equal(quat.Identity, LightDirectionRotation.FromZTo(vec3.Zero));
    }

    // Where the falloff has halved the intensity
    [Fact]
    public void ALightsReachIsWhereItsIntensityHalves()
    {
        Assert.Equal(0.0f, SceneryLightVisual.ReachOf(0));
        foreach (var power in new[] { 1, 2, 3 })
        {
            var reach = SceneryLightVisual.ReachOf(power);
            Assert.Equal(0.5f, MathF.Pow(25 / (reach * reach + 25), power), 1e-5f);
        }
    }
}
