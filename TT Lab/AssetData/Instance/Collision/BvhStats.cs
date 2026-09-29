using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance.Collision;

/// <summary>
/// How a collision tree does under the game's queries, for comparing trees
/// </summary>
public static class BvhStats
{
    public sealed record Summary(int Nodes, int Leaves, int Triangles, int MaxDepth, double AverageLeafDepth, int MinLeafTriangles, double AverageLeafTriangles,
        int MaxLeafTriangles, double SahCost, double SiblingOverlap, double LeafSlack, double BoxesPerRay, double TrianglesPerRay)
    {
        public override string ToString()
        {
            return $"{Nodes} boxes, {Leaves} leaves of {MinLeafTriangles}-{MaxLeafTriangles} (avg {AverageLeafTriangles:F1}) triangles, depth max {MaxDepth} avg {AverageLeafDepth:F1}, "
                   + $"SAH {SahCost:F1}, sibling overlap {SiblingOverlap:P1}, leaf slack {LeafSlack:P1}, per ray {BoxesPerRay:F1} boxes and {TrianglesPerRay:F1} triangles";
        }
    }

    public static Summary Measure(BvhBuilder.Tree tree, IReadOnlyList<BvhBuilder.Face> faces, IReadOnlyList<Vector4> vectors, int rays = 2000, int seed = 1)
    {
        var nodes = tree.Nodes;
        var depths = new int[nodes.Count];
        var leafDepths = new List<int>();
        var overlaps = new List<double>();
        var slacks = new List<double>();
        var rootArea = Area(nodes[0]);
        var sah = 0.0;
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.IsLeaf)
            {
                var group = tree.Groups[node.Group];
                leafDepths.Add(depths[i]);
                sah += Area(node) / rootArea * group.Size;
                slacks.Add(Slack(node, tree, group, faces, vectors));
                continue;
            }

            sah += Area(node) / rootArea;
            depths[node.Left] = depths[i] + 1;
            depths[node.Right] = depths[i] + 1;
            overlaps.Add(Overlap(nodes[node.Left], nodes[node.Right], node));
        }

        var (boxesPerRay, trianglesPerRay) = RayCost(tree, rays, seed);
        return new Summary(nodes.Count, tree.Groups.Count, tree.TriangleOrder.Length, depths.Max(), leafDepths.Average(), tree.Groups.Min(group => group.Size),
            tree.Groups.Average(group => group.Size), tree.Groups.Max(group => group.Size), sah, overlaps.Count == 0 ? 0.0 : overlaps.Average(),
            slacks.Count == 0 ? 0.0 : slacks.Average(), boxesPerRay, trianglesPerRay);
    }

    private static float Area(BvhBuilder.Node node)
    {
        var e = node.Max - node.Min;
        return e.x * e.y + e.y * e.z + e.z * e.x;
    }

    private static float Volume(vec3 min, vec3 max)
    {
        var e = vec3.Max(max - min, vec3.Zero);
        return e.x * e.y * e.z;
    }

    // How much of the parent's volume both children cover
    private static double Overlap(BvhBuilder.Node left, BvhBuilder.Node right, BvhBuilder.Node parent)
    {
        var parentVolume = Volume(parent.Min, parent.Max);
        return parentVolume <= 0.0f ? 0.0 : Volume(vec3.Max(left.Min, right.Min), vec3.Min(left.Max, right.Max)) / parentVolume;
    }

    // How much of a leaf's box lies outside the tight box of its triangles
    private static double Slack(BvhBuilder.Node node, BvhBuilder.Tree tree, BvhBuilder.Group group, IReadOnlyList<BvhBuilder.Face> faces, IReadOnlyList<Vector4> vectors)
    {
        var min = new vec3(float.MaxValue);
        var max = new vec3(float.MinValue);
        for (var i = 0; i < group.Size; i++)
        {
            var face = faces[tree.TriangleOrder[group.Offset + i]];
            foreach (var index in new[] { face.A, face.B, face.C })
            {
                var vector = vectors[index];
                var point = new vec3(vector.X, vector.Y, vector.Z);
                min = vec3.Min(min, point);
                max = vec3.Max(max, point);
            }
        }

        var boxVolume = Volume(node.Min, node.Max);
        return boxVolume <= 0.0f ? 0.0 : 1.0 - Volume(min, max) / boxVolume;
    }

    /// <summary>
    /// The boxes and triangles the game tests for downward rays from random points of the root's top, walking the tree like
    /// <c>CheckCollisionRayCast</c> does: both children of every box the segment crosses, every triangle of every leaf reached
    /// </summary>
    public static (double Boxes, double Triangles) RayCost(BvhBuilder.Tree tree, int rays, int seed)
    {
        var root = tree.Nodes[0];
        var random = new Random(seed);
        long boxes = 0;
        long triangles = 0;
        for (var i = 0; i < rays; i++)
        {
            var x = root.Min.x + (float)random.NextDouble() * (root.Max.x - root.Min.x);
            var z = root.Min.z + (float)random.NextDouble() * (root.Max.z - root.Min.z);
            var start = new vec3(x, root.Max.y + 1.0f, z);
            var end = new vec3(x, root.Min.y - 1.0f, z);
            Walk(tree, 0, start, end, ref boxes, ref triangles);
        }

        return ((double)boxes / rays, (double)triangles / rays);
    }

    private static void Walk(BvhBuilder.Tree tree, int index, vec3 start, vec3 end, ref long boxes, ref long triangles)
    {
        var node = tree.Nodes[index];
        if (node.IsLeaf)
        {
            triangles += tree.Groups[node.Group].Size;
            return;
        }

        boxes += 2;
        if (SegmentCrosses(tree.Nodes[node.Left], start, end))
        {
            Walk(tree, node.Left, start, end, ref boxes, ref triangles);
        }

        if (SegmentCrosses(tree.Nodes[node.Right], start, end))
        {
            Walk(tree, node.Right, start, end, ref boxes, ref triangles);
        }
    }

    private static bool SegmentCrosses(BvhBuilder.Node node, vec3 start, vec3 end)
    {
        var enter = 0.0f;
        var exit = 1.0f;
        for (var axis = 0; axis < 3; axis++)
        {
            var direction = end[axis] - start[axis];
            if (MathF.Abs(direction) < 1e-8f)
            {
                if (start[axis] < node.Min[axis] || start[axis] > node.Max[axis])
                {
                    return false;
                }

                continue;
            }

            var t1 = (node.Min[axis] - start[axis]) / direction;
            var t2 = (node.Max[axis] - start[axis]) / direction;
            enter = MathF.Max(enter, MathF.Min(t1, t2));
            exit = MathF.Min(exit, MathF.Max(t1, t2));
            if (enter > exit)
            {
                return false;
            }
        }

        return true;
    }
}
