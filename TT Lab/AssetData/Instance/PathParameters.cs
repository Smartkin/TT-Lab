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
/// path has a step length of its own (0.03 to 4.5 units) which isn't stored anywhere else (<see cref="TwinPathParameters"/>)
/// </remarks>
public static class PathParameters
{
    public const int MinSteps = 5;
    public const float DefaultStepLength = 1.0f;

    public readonly record struct Values(List<Single> ArcLengths, List<Single> InverseSteps);

    public static Values Create(IReadOnlyList<vec3> points, float stepLength)
    {
        var segments = Math.Max(points.Count - 3, 0);
        var controlPoints = points.ToArray();
        var arcLengths = new List<Single>(segments);
        var inverseSteps = new List<Single>(segments);
        var length = 0.0f;
        for (var segment = 0; segment < segments; segment++)
        {
            var segmentLength = SplineMath.GetSegmentLength(controlPoints.AsSpan(segment, 4));
            length += segmentLength;
            arcLengths.Add(length);
            inverseSteps.Add(1.0f / GetSteps(segmentLength, stepLength));
        }

        return new Values(arcLengths, inverseSteps);
    }

    public static int GetSteps(float length, float stepLength) => Math.Max(MinSteps, (int)MathF.Ceiling(length / stepLength));

    /// <summary>
    /// The step length the parameters of a path with that many segments were made with, null when they aren't such parameters
    /// </summary>
    public static float? FindStepLength(IReadOnlyList<Single> arcLengths, IReadOnlyList<Single> inverseSteps, int segments)
    {
        if (segments < 1 || arcLengths.Count != segments || inverseSteps.Count != segments)
        {
            return null;
        }

        var atLeast = 0.0f;
        var below = float.PositiveInfinity;
        var start = 0.0f;
        for (var segment = 0; segment < segments; segment++)
        {
            var length = arcLengths[segment] - start;
            start = arcLengths[segment];
            var steps = MathF.Round(1.0f / inverseSteps[segment]);
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
