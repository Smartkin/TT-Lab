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

    // Every particle's values come from these every frame: the keys are read straight from the arrays, closures over them allocated
    // per particle
    public static float Evaluate(Vector2[] keys, float time)
    {
        for (var i = 0; i < MaxKeys - 1; i++)
        {
            var from = keys[i].X;
            var to = keys[i + 1].X;
            if (from > time || time > to)
            {
                continue;
            }

            return Interpolate(from, to, keys[i].Y, keys[i + 1].Y, time);
        }

        return 0.0f;
    }

    /// <summary>
    /// Color keys hold the red, green and blue after the time
    /// </summary>
    public static vec3 EvaluateColor(Vector4[] keys, float time)
    {
        for (var i = 0; i < MaxKeys - 1; i++)
        {
            var from = keys[i].X;
            var to = keys[i + 1].X;
            if (from > time || time > to)
            {
                continue;
            }

            return new vec3(Interpolate(from, to, keys[i].Y, keys[i + 1].Y, time), Interpolate(from, to, keys[i].Z, keys[i + 1].Z, time),
                Interpolate(from, to, keys[i].W, keys[i + 1].W, time));
        }

        return vec3.Zero;
    }

    // The game's search: the first pair in a row enclosing the time, 0 when there's none. A pair at the same time gives its first key's value
    private static float Interpolate(float from, float to, float fromValue, float toValue, float time)
    {
        if (time - from == 0.0f)
        {
            return fromValue;
        }

        return fromValue + (time - from) / (to - from) * (toValue - fromValue);
    }
}
