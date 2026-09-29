using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using Twinsanity.TwinsanityInterchange.Common.Lights;

namespace TT_Lab.Rendering;

/// <summary>
/// The lights the game's environment map looks up by: a scene's three strongest directional lights (the chunk's light controller
/// keeps them at slots 4 to 6, <c>FUN_001c7d50</c>), unit vectors towards where the light comes from
/// </summary>
public static class EnvLights
{
    public static readonly vec3[] Defaults = [new(0.0f, 1.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(0.0f, 0.0f, 1.0f)];

    public static vec3[] Of(IEnumerable<DirectionalLight> lights)
    {
        var strongest = lights.OrderByDescending(light => light.Intensity)
            .Select(light => new vec3(light.Direction.X, light.Direction.Y, light.Direction.Z).NormalizedSafe)
            .Take(3)
            .ToList();
        return Enumerable.Range(0, 3).Select(i => i < strongest.Count ? strongest[i] : Defaults[i]).ToArray();
    }
}
