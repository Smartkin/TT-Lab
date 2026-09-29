using System;
using GlmSharp;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Particles;

/// <summary>
/// The 8 key curves of particle systems: each key is the time in the particle's life (0 to 1) and its value. The game samples them at 64
/// steps of the life when it first draws a system: a step takes the first pair of keys in a row whose times enclose it and interpolates
/// between them, steps no pair encloses are 0. The curve ends with the first key at the end of the life, the keys after it are
/// leftovers of the tools
/// </summary>
public static class ParticleCurves
{
    public const int MaxKeys = 8;

    /// <summary>
    /// How many of the keys belong to the curve
    /// </summary>
    public static int CountKeys(Func<int, float> timeOf)
    {
        for (var i = 0; i < MaxKeys; i++)
        {
            if (timeOf(i) >= 1.0f)
            {
                return i + 1;
            }
        }

        return MaxKeys;
    }

    public static int CountKeys(Vector2[] keys) => CountKeys(i => keys[i].X);

    public static int CountKeys(Vector4[] keys) => CountKeys(i => keys[i].X);

    public static float Evaluate(Vector2[] keys, float time)
    {
        return Evaluate(i => keys[i].X, i => keys[i].Y, time);
    }

    /// <summary>
    /// Color keys hold the red, green and blue after the time
    /// </summary>
    public static vec3 EvaluateColor(Vector4[] keys, float time)
    {
        return new vec3(Evaluate(i => keys[i].X, i => keys[i].Y, time),
            Evaluate(i => keys[i].X, i => keys[i].Z, time),
            Evaluate(i => keys[i].X, i => keys[i].W, time));
    }

    // The game's search: the first pair in a row enclosing the time, 0 when there's none. A pair at the same time gives its first key's value
    private static float Evaluate(Func<int, float> timeOf, Func<int, float> valueOf, float time)
    {
        for (var i = 0; i < MaxKeys - 1; i++)
        {
            var from = timeOf(i);
            var to = timeOf(i + 1);
            if (from > time || time > to)
            {
                continue;
            }

            if (time - from == 0.0f)
            {
                return valueOf(i);
            }

            return valueOf(i) + (time - from) / (to - from) * (valueOf(i + 1) - valueOf(i));
        }

        return 0.0f;
    }
}
