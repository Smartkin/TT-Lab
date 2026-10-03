using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
using Twinsanity.TwinsanityInterchange.Common.Lights;

namespace TT_Lab.Rendering.Lighting;

/// <summary>
/// A scenery's lights the way the game lights objects with them (<c>GatherStrongestLights</c> 0x1c7f50, verified in the PAL executable),
/// once per object at its instance's position (<c>FUN_001fe290</c>), in the scenery's order of lights: ambient lights add their color
/// times their intensity, every other light gives a direction and an intensity there and the three of the highest intensity are kept, a
/// light only taking the place of one it's stronger than. The root tree node's bits pick the lights, the build gives it every light's.
/// Without lighting nothing lights the objects and the game draws them black
/// </summary>
public sealed class SceneLights
{
    // Of the attenuation 25 / (d² + 25) of point and spot lights (LightingConstants[3], InitLightingConstants)
    private const float AttenuationDistance = 25.0f;

    private readonly Source[] _sources;

    private SceneLights(Source[] sources)
    {
        _sources = sources;
    }

    public static SceneLights None { get; } = new([]);

    /// <summary>
    /// What new chunks start with, single models are lit by it
    /// </summary>
    public static SceneLights Default { get; } = From(true, [DefaultLights.Ambient()], [DefaultLights.Directional()], [], [], []);

    public int Count => _sources.Length;

    public static SceneLights Of(SceneryData scenery)
    {
        return From(scenery.HasLighting, scenery.AmbientLights, scenery.DirectionalLights, scenery.PointLights, scenery.SpotLights, scenery.LightOrder);
    }

    /// <param name="order">Index and kind of every light (<see cref="SceneryData.LightOrder"/>), kind by kind when it doesn't list every light once</param>
    public static SceneLights From(bool hasLighting, IReadOnlyList<AmbientLight> ambient, IReadOnlyList<DirectionalLight> directional,
        IReadOnlyList<PointLight> point, IReadOnlyList<SpotLight> spot, IReadOnlyList<int> order)
    {
        if (!hasLighting)
        {
            return None;
        }

        var kinds = new[] { ambient.Select(Source.Of).ToArray(), directional.Select(Source.Of).ToArray(), point.Select(Source.Of).ToArray(), spot.Select(Source.Of).ToArray() };
        var listed = new List<Source>();
        var pairs = Enumerable.Range(0, order.Count / 2).Select(i => (Index: order[i * 2], Kind: order[i * 2 + 1])).ToList();
        var everyLightOnce = pairs.Count == kinds.Sum(lights => lights.Length) && pairs.Distinct().Count() == pairs.Count &&
                             pairs.All(pair => pair.Kind is >= 0 and < 4 && pair.Index >= 0 && pair.Index < kinds[pair.Kind].Length);
        if (everyLightOnce)
        {
            listed.AddRange(pairs.Select(pair => kinds[pair.Kind][pair.Index]));
        }
        else
        {
            listed.AddRange(kinds.SelectMany(lights => lights));
        }

        return new SceneLights(listed.Take(SceneryData.MaxLights).ToArray());
    }

    public LightSet At(vec3 position)
    {
        var ambient = vec3.Zero;
        Span<float> intensities = [-1.0f, -1.0f, -1.0f];
        Span<vec3> directions = stackalloc vec3[LightSet.Slots];
        Span<vec3> colors = stackalloc vec3[LightSet.Slots];
        foreach (var source in _sources)
        {
            if (source.Type == LightType.Ambient)
            {
                ambient += source.Color * source.Intensity;
                continue;
            }

            var (direction, intensity) = source.At(position);
            for (var slot = 0; slot < LightSet.Slots; slot++)
            {
                if (intensity <= intensities[slot])
                {
                    continue;
                }

                for (var later = LightSet.Slots - 1; later > slot; later--)
                {
                    intensities[later] = intensities[later - 1];
                    directions[later] = directions[later - 1];
                    colors[later] = colors[later - 1];
                }

                intensities[slot] = intensity;
                directions[slot] = direction;
                colors[slot] = source.Color;
                break;
            }
        }

        // FUN_001c7d50 scales the slots up to the first without a light (an intensity below 0) by their intensity and half
        for (var slot = 0; slot < LightSet.Slots && intensities[slot] >= 0.0f; slot++)
        {
            colors[slot] *= intensities[slot] * 0.5f;
        }

        return new LightSet(ambient * 0.5f, directions[0], colors[0], directions[1], colors[1], directions[2], colors[2]);
    }

    private readonly record struct Source(LightType Type, vec3 Color, float Intensity, vec3 Position, vec3 Direction, int AttenuationPower,
        float InnerConeCosine, float OuterConeCosine, int SpotExponent)
    {
        public static Source Of(Light light)
        {
            var color = new vec3(light.Color.X, light.Color.Y, light.Color.Z);
            var position = new vec3(light.Position.X, light.Position.Y, light.Position.Z);
            return light switch
            {
                DirectionalLight directional => new Source(LightType.Directional, color, light.Intensity, position,
                    new vec3(directional.Direction.X, directional.Direction.Y, directional.Direction.Z), 0, 0.0f, 0.0f, 0),
                PointLight point => new Source(LightType.Point, color, light.Intensity, position, vec3.Zero, point.AttenuationPower, 0.0f, 0.0f, 0),
                // The game reads the power as a signed halfword and the exponent's low byte
                SpotLight spot => new Source(LightType.Spot, color, light.Intensity, position, new vec3(spot.Direction.X, spot.Direction.Y, spot.Direction.Z),
                    (short)spot.AttenuationPower, spot.InnerConeCosine, spot.OuterConeCosine, spot.SpotExponent & 0xFF),
                _ => new Source(LightType.Ambient, color, light.Intensity, position, vec3.Zero, 0, 0.0f, 0.0f, 0),
            };
        }

        // DirectionalLightAt 0x1cb610: the direction and the intensity as they are. PointLightAt 0x1c87b0: the way to the light times the
        // attenuation (no unit vector: the dot products grow and shrink with it) and the intensity times the attenuation power times.
        // SpotLightAt 0x1c8f18: that, and the dot product of that direction with the way it shines, nothing below the outer cosine,
        // fading in up to the inner one, the intensity times it raised to the exponent
        public (vec3 Direction, float Intensity) At(vec3 position)
        {
            if (Type == LightType.Directional)
            {
                return (Direction, Intensity);
            }

            var delta = Position - position;
            var attenuation = AttenuationDistance / (vec3.Dot(delta, delta) + AttenuationDistance);
            var direction = delta * attenuation;
            var intensity = Intensity;
            for (var i = 0; i < AttenuationPower; i++)
            {
                intensity *= attenuation;
            }

            if (Type != LightType.Spot)
            {
                return (direction, intensity);
            }

            var cosine = vec3.Dot(direction, -Direction);
            if (cosine < OuterConeCosine)
            {
                return (direction, 0.0f);
            }

            if (cosine < InnerConeCosine)
            {
                intensity *= Math.Clamp((cosine - OuterConeCosine) / (InnerConeCosine - OuterConeCosine), 0.0f, 1.0f);
            }

            // Squared from the exponent's top bit down, times the cosine for every bit that's set
            var power = 1.0f;
            for (var bit = 0x80; bit != 0; bit >>= 1)
            {
                power = ((SpotExponent & bit) != 0 ? power * cosine : power) * power;
            }

            return (direction, intensity * power);
        }
    }
}
