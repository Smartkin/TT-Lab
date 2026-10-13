using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.Extensions;

namespace TT_Lab.AssetData.Graphics.TlModel;

/// <summary>
/// How a joint's rotations become the game's Euler angles so that the game's blends between frames follow them
/// </summary>
/// <remarks>
/// The game blends a joint from a frame to the next angle by angle (AnimateJoint: X, Y and Z each the shorter way round, the rotation of
/// the angles turned by the joint's additional rotation when the animation says so), not along the shortest turn between the two. A
/// rotation has two sets of angles, (x, y, z) and (x + half a turn, half a turn - y, z + half a turn), and blending from one set to the
/// other swings the joint around on the way: a looping animation whose last frame has the other set than its first twitches every time it
/// starts over. Close to a middle angle of a quarter turn (gimbal lock) the angles of nearby rotations are far apart and neither set
/// blends along the turn: the Blender add-on gives joints the additional rotation that turns the angles' axes, and gimbal lock with them,
/// away from where their animations go (animation_frames.py)
/// </remarks>
internal static class TlmAnimationFrames
{
    private const double Turn = Math.PI * 2;
    // The other sets of angles at gimbal lock (the first angle kept from the frame before) that still make the rotation
    private const double SameRotationDot = 0.999999;
    private const int MostCandidates = 8;
    // The cosine of the middle angle under which other first angles than a rotation's own two can make it, within SameRotationDot
    private const double NearGimbalLockCosine = 0.01;
    // Steps of an angle this close to half a turn, which rounding can turn the other way round
    private const double AmbiguousStep = Math.PI - Math.PI / 180;
    // Blends straying this far (45 degrees) already count as they are, whichever way their half turns go
    private static readonly double LargeDeviation = FromDegrees(45);

    /// <summary>
    /// The Euler angles of every frame of a joint's track: of each frame's sets of angles the ones whose blends follow the turns between
    /// the frames best, from the last frame into the first as well when the track comes back to where it started
    /// </summary>
    /// <param name="rotations">Every frame's rotation the angles are of, without the joint's additional rotation</param>
    /// <param name="kept">The game's angles of the frames that keep them, null for the others</param>
    public static vec3[] ChooseAngles(IReadOnlyList<quat> rotations, IReadOnlyList<vec3?> kept)
    {
        var frames = rotations.Count;
        var candidates = new List<dvec3>[frames];
        // Frames whose candidates are their two sets of angles, the blends between two such frames stray as far between their other sets
        var paired = new bool[frames];
        for (var frame = 0; frame < frames; frame++)
        {
            if (kept[frame] is { } angles)
            {
                candidates[frame] = [new dvec3(angles.x, angles.y, angles.z)];
                continue;
            }

            candidates[frame] = Candidates(ToDouble(rotations[frame]).Normalized, frame > 0 ? candidates[frame - 1] : null, out paired[frame]);
        }

        var quaternions = candidates.Select(list => list.Select(FromAngles).ToArray()).ToArray();
        var loops = IsLoop(rotations.Select(rotation => ToDouble(rotation).Normalized).ToArray());
        var deviations = new double[loops ? frames : Math.Max(frames - 1, 0)][,];
        for (var frame = 0; frame < deviations.Length; frame++)
        {
            var next = (frame + 1) % frames;
            if (paired[frame] && paired[next])
            {
                var same = Deviation(candidates[frame][0], candidates[next][0], quaternions[frame][0], quaternions[next][0]);
                var other = Deviation(candidates[frame][0], candidates[next][1], quaternions[frame][0], quaternions[next][1]);
                deviations[frame] = new[,] { { same, other }, { other, same } };
                continue;
            }

            deviations[frame] = new double[candidates[frame].Count, candidates[next].Count];
            for (var from = 0; from < candidates[frame].Count; from++)
            {
                for (var to = 0; to < candidates[next].Count; to++)
                {
                    deviations[frame][from, to] = Deviation(candidates[frame][from], candidates[next][to], quaternions[frame][from], quaternions[next][to]);
                }
            }
        }

        int[]? chosen = null;
        var chosenCost = double.PositiveInfinity;
        // Starting from each of the first frame's sets in turn, its first ones first: blends between the other sets of every frame stray
        // as far, and a loop has to come back to the set it started with
        for (var start = 0; start < candidates[0].Count; start++)
        {
            var costs = candidates[0].Select((_, index) => index == start ? 0.0 : double.PositiveInfinity).ToArray();
            var previous = new int[frames][];
            for (var frame = 1; frame < frames; frame++)
            {
                var next = new double[candidates[frame].Count];
                previous[frame] = new int[next.Length];
                for (var to = 0; to < next.Length; to++)
                {
                    next[to] = double.PositiveInfinity;
                    for (var from = 0; from < costs.Length; from++)
                    {
                        var cost = costs[from] + deviations[frame - 1][from, to];
                        if (IsLess(cost, next[to]))
                        {
                            next[to] = cost;
                            previous[frame][to] = from;
                        }
                    }
                }

                costs = next;
            }

            for (var last = 0; last < costs.Length; last++)
            {
                var cost = costs[last] + (loops ? deviations[frames - 1][last, start] : 0.0);
                if (!IsLess(cost, chosenCost))
                {
                    continue;
                }

                chosenCost = cost;
                chosen = new int[frames];
                chosen[frames - 1] = last;
                for (var frame = frames - 1; frame > 0; frame--)
                {
                    chosen[frame - 1] = previous[frame][chosen[frame]];
                }
            }
        }

        // The stored angles stay within half a turn of the frame before's, the game blends each the shorter way round anyway
        var result = new vec3[frames];
        var before = dvec3.Zero;
        for (var frame = 0; frame < frames; frame++)
        {
            var angles = candidates[frame][chosen?[frame] ?? 0];
            if (kept[frame] == null)
            {
                angles = frame == 0
                    ? new dvec3(ShorterWay(angles.x), ShorterWay(angles.y), ShorterWay(angles.z))
                    : new dvec3(before.x + ShorterWay(angles.x - before.x), before.y + ShorterWay(angles.y - before.y), before.z + ShorterWay(angles.z - before.z));
            }

            result[frame] = new vec3((float)angles.x, (float)angles.y, (float)angles.z);
            before = angles;
        }

        return result;
    }

    /// <summary>
    /// How far the game's blends from a frame's angles to the next's stray from the turns between their rotations, the largest of every
    /// frame of the track (the last frame's into the first included for a loop), in degrees
    /// </summary>
    public static double LargestDeviation(IReadOnlyList<vec3> angles)
    {
        var count = angles.Count;
        var quaternions = angles.Select(frame => FromAngles(new dvec3(frame.x, frame.y, frame.z))).ToArray();
        var segments = IsLoop(quaternions) ? count : count - 1;
        var largest = 0.0;
        for (var frame = 0; frame < segments; frame++)
        {
            var next = (frame + 1) % count;
            var deviation = Deviation(new dvec3(angles[frame].x, angles[frame].y, angles[frame].z), new dvec3(angles[next].x, angles[next].y, angles[next].z),
                quaternions[frame], quaternions[next]);
            largest = Math.Max(largest, deviation);
        }

        return ToDegrees(largest);
    }

    // How far the game's blend from one set of angles to another strays from the shortest turn between their rotations, as the squared
    // distance of their quaternions halfway
    private static double Deviation(dvec3 from, dvec3 to, dquat fromRotation, dquat toRotation)
    {
        var step = new dvec3(ShorterWay(to.x - from.x), ShorterWay(to.y - from.y), ShorterWay(to.z - from.z));
        var target = dquat.Dot(fromRotation, toRotation) < 0 ? Negated(toRotation) : toRotation;
        var halfway = (fromRotation + target).Normalized;
        var deviation = Distance(FromAngles(from + step * 0.5), halfway);
        var ambiguous = (Math.Abs(step.x) >= AmbiguousStep ? 1 : 0) | (Math.Abs(step.y) >= AmbiguousStep ? 2 : 0) | (Math.Abs(step.z) >= AmbiguousStep ? 4 : 0);
        if (ambiguous == 0 || deviation >= LargeDeviation)
        {
            return deviation;
        }

        // A step of about half a turn goes either way in the game once the angles are rounded to its units (exactly half a turn goes the
        // way the angles were stored), the worse way counts: at gimbal lock the first and last angles stepping half a turn are the same
        // rotation only while they turn the same way
        for (var flips = ambiguous; flips > 0; flips = (flips - 1) & ambiguous)
        {
            var other = new dvec3(Flipped(step.x, flips & 1), Flipped(step.y, flips & 2), Flipped(step.z, flips & 4));
            deviation = Math.Max(deviation, Distance(FromAngles(from + other * 0.5), halfway));
        }

        return deviation;

        static double Flipped(double angle, int flip) => flip != 0 ? angle - Math.Sign(angle) * Turn : angle;
    }

    // The angle between two rotations of quaternions that far apart: their squared distance is 2 - 2 cos of half the angle, as unit quaternions
    private static double ToDegrees(double distance) => 4 * Math.Asin(Math.Min(1, Math.Sqrt(distance) / 2)) * 180 / Math.PI;

    private static double FromDegrees(double degrees) => Math.Pow(2 * Math.Sin(degrees * Math.PI / 720), 2);

    private static List<dvec3> Candidates(dquat rotation, List<dvec3>? before, out bool paired)
    {
        var angles = rotation.ToEulerAngles();
        var candidates = new List<dvec3>(2);
        Add(angles.x, angles.y);
        Add(angles.x + Math.PI, Math.PI - angles.y);
        paired = candidates.Count == 2;
        // At gimbal lock only the first and last angles' difference matters, the first one kept from the frame before doesn't jump. Further
        // from it no other first angle makes the rotation
        if (Math.Cos(angles.y) < NearGimbalLockCosine)
        {
            foreach (var previous in before ?? [])
            {
                Add(previous.x, angles.y);
                Add(previous.x, Math.PI - angles.y);
            }

            paired &= candidates.Count == 2;
        }

        if (candidates.Count == 0)
        {
            candidates.Add(angles);
        }

        return candidates;

        void Add(double x, double y)
        {
            var candidate = WithSolvedLastAngle(rotation, x, y);
            // Written so NaN, which fails every comparison, doesn't pass either
            if (candidates.Count >= MostCandidates || !(Math.Abs(dquat.Dot(FromAngles(candidate), rotation)) >= SameRotationDot))
            {
                return;
            }

            foreach (var other in candidates)
            {
                if (SameAngles(other, candidate))
                {
                    return;
                }
            }

            candidates.Add(candidate);
        }
    }

    // Rotations are qz * qy * qx: taking qy * qx back out leaves the turn about Z, exact where reading all three angles off the
    // quaternion loses a lot close to gimbal lock
    private static dvec3 WithSolvedLastAngle(dquat rotation, double x, double y)
    {
        var turnAboutZ = rotation * FromAngles(new dvec3(x, y, 0)).Conjugate;
        return new dvec3(x, y, 2 * Math.Atan2(turnAboutZ.z, turnAboutZ.w));
    }

    // The game's rotation of the angles, like quat(vec3): qz * qy * qx
    private static dquat FromAngles(dvec3 angles)
    {
        var (sx, cx) = Math.SinCos(angles.x / 2);
        var (sy, cy) = Math.SinCos(angles.y / 2);
        var (sz, cz) = Math.SinCos(angles.z / 2);
        return new dquat(sx * cy * cz - cx * sy * sz, cx * sy * cz + sx * cy * sz, cx * cy * sz - sx * sy * cz, cx * cy * cz + sx * sy * sz);
    }

    // The track comes back to where it started: its last frame is no farther from its first than the frames are from each other
    private static bool IsLoop(IReadOnlyList<dquat> rotations)
    {
        var count = rotations.Count;
        if (count < 2)
        {
            return false;
        }

        // The cosines of half the steps, the smallest is the largest step
        var largestStep = Enumerable.Range(0, count - 1).Min(index => Math.Abs(dquat.Dot(rotations[index], rotations[index + 1])));
        return Math.Abs(dquat.Dot(rotations[count - 1], rotations[0])) >= largestStep - 1e-12;
    }

    // Lower costs win by more than rounding, so of equally good choices the first stays
    private static bool IsLess(double cost, double than) => double.IsPositiveInfinity(than) ? cost < than : cost < than - than * 1e-12 - 1e-15;

    private static double Distance(dquat first, dquat second) => Math.Min((first - second).LengthSqr, (first + second).LengthSqr);

    private static double ShorterWay(double difference) => difference - Math.Round(difference / Turn) * Turn;

    private static bool SameAngles(dvec3 first, dvec3 second) =>
        Math.Abs(ShorterWay(first.x - second.x)) < 1e-9 && Math.Abs(ShorterWay(first.y - second.y)) < 1e-9 && Math.Abs(ShorterWay(first.z - second.z)) < 1e-9;

    private static dquat Negated(dquat rotation) => new(-rotation.x, -rotation.y, -rotation.z, -rotation.w);

    private static dquat ToDouble(quat rotation) => new(rotation.x, rotation.y, rotation.z, rotation.w);
}
