using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance.Particle;

/// <summary>
/// A key of a particle system's curve: the time in the particle's life and up to three values, a color's red, green and blue
/// </summary>
public readonly record struct CurveKey(Single Time, Single Value0, Single Value1 = 0, Single Value2 = 0)
{
    public CurveKey Lerp(CurveKey to, Single amount)
    {
        return new CurveKey(Time + (to.Time - Time) * amount, Value0 + (to.Value0 - Value0) * amount, Value1 + (to.Value1 - Value1) * amount,
            Value2 + (to.Value2 - Value2) * amount);
    }
}

/// <summary>
/// Edits the 8 keys of a particle system's curve the way the game reads them: from time 0 on and up to the first key at 1, which ends the
/// curve. Keys after it are leftovers the game doesn't read
/// </summary>
public static class ParticleCurveKeys
{
    public const int MaxKeys = 8;

    public static CurveKey[] FromCurve(Vector2[] curve) => curve.Select(key => new CurveKey(key.X, key.Y)).ToArray();

    public static CurveKey[] FromGradient(Vector4[] gradient) => gradient.Select(key => new CurveKey(key.X, key.Y, key.Z, key.W)).ToArray();

    public static Vector2 ToCurveKey(CurveKey key) => new() { X = key.Time, Y = key.Value0 };

    public static Vector4 ToGradientKey(CurveKey key) => new(key.Time, key.Value0, key.Value1, key.Value2);

    public static int Count(IReadOnlyList<CurveKey> keys)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            if (keys[i].Time >= 1.0f)
            {
                return i + 1;
            }
        }

        return keys.Count;
    }

    // The last key keeps ending the curve, keys go no further than their neighbours
    public static CurveKey[] Move(IReadOnlyList<CurveKey> keys, int index, Single time)
    {
        var result = keys.ToArray();
        var count = Count(keys);
        var isEnd = index == count - 1 && keys[index].Time >= 1.0f;
        if (index == 0)
        {
            time = 0.0f;
        }
        else if (isEnd)
        {
            time = 1.0f;
        }
        else
        {
            var next = index + 1 < count ? keys[index + 1].Time : 1.0f;
            time = Math.Clamp(time, keys[index - 1].Time, Math.Min(next, 1.0f));
            // Only the key ending the curve can be at 1
            if (time >= 1.0f)
            {
                time = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(1.0f) - 1);
            }
        }

        result[index] = result[index] with { Time = time };
        return result;
    }

    public static CurveKey[] SetValues(IReadOnlyList<CurveKey> keys, int index, CurveKey values)
    {
        var result = keys.ToArray();
        result[index] = values with { Time = keys[index].Time };
        return result;
    }

    public static bool CanInsert(IReadOnlyList<CurveKey> keys) => Count(keys) < MaxKeys;

    /// <summary>
    /// Adds a key where the curve is at the time, returns where it went or -1 when the curve has all its keys
    /// </summary>
    public static int Insert(IReadOnlyList<CurveKey> keys, Single time, out CurveKey[] result)
    {
        result = keys.ToArray();
        var count = Count(keys);
        if (count >= MaxKeys || count < 2)
        {
            return -1;
        }

        time = Math.Clamp(time, 0.0f, 1.0f);
        var index = 1;
        while (index < count - 1 && keys[index].Time <= time)
        {
            index++;
        }

        var previous = keys[index - 1];
        var next = keys[index];
        var span = next.Time - previous.Time;
        var key = span <= 0.0f ? previous : previous.Lerp(next, (Math.Clamp(time, previous.Time, next.Time) - previous.Time) / span);
        Array.Copy(keys.ToArray(), index, result, index + 1, result.Length - index - 1);
        result[index] = key;
        result = Move(result, index, time);
        return index;
    }

    public static bool CanRemove(IReadOnlyList<CurveKey> keys, int index)
    {
        var count = Count(keys);
        return count > 2 && index > 0 && index < count - 1;
    }

    public static CurveKey[] Remove(IReadOnlyList<CurveKey> keys, int index)
    {
        if (!CanRemove(keys, index))
        {
            return keys.ToArray();
        }

        var result = new CurveKey[keys.Count];
        for (var i = 0; i < keys.Count - 1; i++)
        {
            result[i] = keys[i < index ? i : i + 1];
        }

        // Past the end of the curve, the game doesn't read it
        result[^1] = result[^2];
        return result;
    }
}
