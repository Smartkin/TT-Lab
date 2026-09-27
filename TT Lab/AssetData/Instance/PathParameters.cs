using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.Rendering.Objects;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance;

/// <summary>
/// What a path stores along with its points: every segment's arc length from the start of the path, then 1 over the steps it's taken in
/// </summary>
/// <remarks>
/// Worked out from every path of the game. A segment takes at least 5 steps and one for every step length of the path it's longer, each
/// path has a step length of its own (0.03 to 4.5 units) which isn't stored anywhere else. The values are floats, one after the other,
/// the Vector2s are only how the game item keeps them
/// </remarks>
public static class PathParameters
{
    public const int MinSteps = 5;
    public const float DefaultStepLength = 1.0f;

    public static List<Vector2> Create(IReadOnlyList<vec3> points, float stepLength)
    {
        var segments = Math.Max(points.Count - 3, 0);
        var controlPoints = points.ToArray();
        var values = new float[segments * 2];
        var length = 0.0f;
        for (var segment = 0; segment < segments; segment++)
        {
            var segmentLength = SplineMath.GetSegmentLength(controlPoints.AsSpan(segment, 4));
            length += segmentLength;
            values[segment] = length;
            values[segments + segment] = 1.0f / GetSteps(segmentLength, stepLength);
        }

        return Enumerable.Range(0, segments).Select(i => new Vector2 { X = values[i * 2], Y = values[i * 2 + 1] }).ToList();
    }

    public static int GetSteps(float length, float stepLength) => Math.Max(MinSteps, (int)MathF.Ceiling(length / stepLength));

    /// <summary>
    /// The step length the parameters of a path with that many segments were made with, null when they aren't such parameters
    /// </summary>
    public static float? FindStepLength(IReadOnlyList<Vector2> parameters, int segments)
    {
        if (segments < 1 || parameters.Count != segments)
        {
            return null;
        }

        var values = parameters.SelectMany(parameter => new[] { parameter.X, parameter.Y }).ToArray();
        var atLeast = 0.0f;
        var below = float.PositiveInfinity;
        var start = 0.0f;
        for (var segment = 0; segment < segments; segment++)
        {
            var length = values[segment] - start;
            start = values[segment];
            var steps = MathF.Round(1.0f / values[segments + segment]);
            if (!float.IsFinite(steps) || steps < MinSteps)
            {
                return null;
            }

            atLeast = Math.Max(atLeast, length / steps);
            if (steps > MinSteps)
            {
                below = Math.Min(below, length / (steps - 1));
            }
        }

        // Segments that took the least steps only tell how long a step is at least, the longest of them still has to take the least
        if (float.IsPositiveInfinity(below))
        {
            return atLeast > 0.0f ? atLeast * 1.01f : null;
        }

        return (atLeast + below) / 2.0f;
    }
}
