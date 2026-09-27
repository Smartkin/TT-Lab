using System;
using GlmSharp;

namespace TT_Lab.Rendering.Objects;

public static class SplineMath
{
    /// <summary>
    /// Samples the uniform cubic B-spline the game moves paths along. Every segment is shaped by four consecutive points, so the curve
    /// only approaches the first and last points. The game stores the arc length of these segments along with the path
    /// </summary>
    public static vec3[] SampleUniformCubicBSpline(ReadOnlySpan<vec3> controlPoints, int samplesPerSegment)
    {
        var segments = controlPoints.Length - 3;
        if (segments < 1 || samplesPerSegment < 1)
        {
            return [];
        }

        var samples = new vec3[segments * samplesPerSegment + 1];
        for (var segment = 0; segment < segments; segment++)
        {
            for (var sample = 0; sample < samplesPerSegment; sample++)
            {
                samples[segment * samplesPerSegment + sample] = EvaluateSegment(controlPoints.Slice(segment, 4), (float)sample / samplesPerSegment);
            }
        }

        samples[^1] = EvaluateSegment(controlPoints.Slice(segments - 1, 4), 1.0f);
        return samples;
    }

    public static vec3 EvaluateSegment(ReadOnlySpan<vec3> points, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        var inverse = 1.0f - t;
        var b0 = inverse * inverse * inverse / 6.0f;
        var b1 = (3.0f * t3 - 6.0f * t2 + 4.0f) / 6.0f;
        var b2 = (-3.0f * t3 + 3.0f * t2 + 3.0f * t + 1.0f) / 6.0f;
        var b3 = t3 / 6.0f;
        return points[0] * b0 + points[1] * b1 + points[2] * b2 + points[3] * b3;
    }

    /// <summary>
    /// Arc length of a segment, within about a millionth of the lengths the game stores with its paths
    /// </summary>
    public static float GetSegmentLength(ReadOnlySpan<vec3> points)
    {
        const int samples = 100;
        var length = 0.0;
        var previous = EvaluateSegment(points, 0.0f);
        for (var sample = 1; sample <= samples; sample++)
        {
            var next = EvaluateSegment(points, (float)sample / samples);
            length += (next - previous).Length;
            previous = next;
        }

        return (float)length;
    }

    public static float GetLength(ReadOnlySpan<vec3> points)
    {
        var length = 0.0f;
        for (var i = 1; i < points.Length; i++)
        {
            length += (points[i] - points[i - 1]).Length;
        }

        return length;
    }
}
