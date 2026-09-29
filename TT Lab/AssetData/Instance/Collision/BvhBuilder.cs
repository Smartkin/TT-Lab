using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GlmSharp;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Collision;

namespace TT_Lab.AssetData.Instance.Collision;

/// <summary>
/// Builds a chunk's collision tree the way the game walks it (<c>CheckCollisionRayCast</c> 0x27fb58): a binary tree of boxes whose
/// inner nodes name their two children by index and whose leaves name a group, a run of triangles. A query tests the segment against
/// both children of every node it enters and every triangle of every leaf it reaches, and a box query gathers at most 256 leaves
/// (<c>FUN_0027f310</c>), so leaves stay as big as the game's. The game's files are balanced trees halved by triangle count until a
/// node has 30 or fewer, which <see cref="Strategy.MedianBestAxis"/> makes again within a few boxes and the same cost per ray
/// (<c>bvhstats</c> of the Command Interface measures a chunk's tree against the ones built here)
/// </summary>
public sealed class BvhBuilder
{
    /// <summary>
    /// Leaves get at most this many triangles, like the game's, a leaf's triangles are all tested by the game
    /// </summary>
    public const int DefaultMaxLeafTriangles = 30;

    public const Strategy DefaultStrategy = Strategy.MedianBestAxis;

    // Bins the centroids get sorted into along every axis when picking a split
    private const int BinCount = 16;

    public readonly record struct Face(int A, int B, int C);

    /// <summary>
    /// How a node's triangles get split in two
    /// </summary>
    public enum Strategy
    {
        /// <summary>
        /// Binned surface area heuristic: the plane along any axis that makes the cheapest pair of boxes
        /// </summary>
        Sah,

        /// <summary>
        /// Halves by centroid order along the widest axis, a balanced tree like the game's files have
        /// </summary>
        MedianWidestAxis,

        /// <summary>
        /// Halves by centroid order along the axis whose halves make the cheapest pair of boxes
        /// </summary>
        MedianBestAxis,
    }

    /// <summary>
    /// A box of the tree. An inner node's children are indexes into the nodes, a leaf's are both the negated group index minus one, the
    /// way the game's file has them
    /// </summary>
    public sealed record Node(vec3 Min, vec3 Max, int Left, int Right)
    {
        public bool IsLeaf => Left < 0;
        public int Group => ~Left;
    }

    public sealed record Group(int Offset, int Size);

    /// <summary>
    /// The tree with the triangles in the order the groups run over them: the triangle at <c>TriangleOrder[i]</c> of the input is the
    /// output's triangle i
    /// </summary>
    public sealed record Tree(List<Node> Nodes, List<Group> Groups, int[] TriangleOrder);

    private readonly struct Triangle(vec3 a, vec3 b, vec3 c)
    {
        public readonly vec3 Min = vec3.Min(vec3.Min(a, b), c);
        public readonly vec3 Max = vec3.Max(vec3.Max(a, b), c);
        public readonly vec3 Centroid = (a + b + c) / 3.0f;
    }

    private struct Aabb()
    {
        public vec3 Min = new(float.MaxValue);
        public vec3 Max = new(float.MinValue);

        public void Grow(in Triangle triangle)
        {
            Min = vec3.Min(Min, triangle.Min);
            Max = vec3.Max(Max, triangle.Max);
        }

        public void Grow(in Aabb other)
        {
            Min = vec3.Min(Min, other.Min);
            Max = vec3.Max(Max, other.Max);
        }

        public readonly float Area()
        {
            if (Min.x > Max.x)
            {
                return 0.0f;
            }

            var e = Max - Min;
            return e.x * e.y + e.y * e.z + e.z * e.x;
        }
    }

    private sealed class BuildNode
    {
        public Aabb Bounds;
        public int First;
        public int Count;
        public BuildNode? Left;
        public BuildNode? Right;
    }

    private readonly Triangle[] _triangles;
    private readonly int[] _order;
    private readonly int _maxLeafTriangles;
    private readonly Strategy _strategy;

    private BvhBuilder(Triangle[] triangles, int maxLeafTriangles, Strategy strategy)
    {
        _triangles = triangles;
        _order = Enumerable.Range(0, triangles.Length).ToArray();
        _maxLeafTriangles = Math.Max(1, maxLeafTriangles);
        _strategy = strategy;
    }

    public static Tree Build(IReadOnlyList<Face> faces, IReadOnlyList<Vector4> vectors, int maxLeafTriangles = DefaultMaxLeafTriangles, Strategy strategy = DefaultStrategy)
    {
        var triangles = new Triangle[faces.Count];
        for (var i = 0; i < faces.Count; i++)
        {
            var face = faces[i];
            triangles[i] = new Triangle(ToGlm(vectors[face.A]), ToGlm(vectors[face.B]), ToGlm(vectors[face.C]));
        }

        var builder = new BvhBuilder(triangles, maxLeafTriangles, strategy);
        var root = new BuildNode { First = 0, Count = triangles.Length };
        builder.UpdateBounds(root);
        if (triangles.Length > 0)
        {
            builder.Subdivide(root);
        }

        var nodes = new List<Node>();
        var groups = new List<Group>();
        builder.Flatten(root, nodes, groups);
        return new Tree(nodes, groups, builder._order);
    }

    /// <summary>
    /// The tree a game file holds, for looking at it
    /// </summary>
    public static Tree FromTwin(IReadOnlyList<TwinCollisionTrigger> triggers, IReadOnlyList<TwinGroupInformation> groups, int triangleCount)
    {
        return new Tree(triggers.Select(trigger => new Node(ToGlm(trigger.V1), ToGlm(trigger.V2), trigger.MinTriggerIndex, trigger.MaxTriggerIndex)).ToList(),
            groups.Select(group => new Group((int)group.Offset, (int)group.Size)).ToList(), Enumerable.Range(0, triangleCount).ToArray());
    }

    public static void BuildBvh(CollisionData collision)
    {
        var watch = Stopwatch.StartNew();
        Log.WriteLine("Building collision bounding volume hierarchy...");
        var faces = collision.Triangles.Select(triangle => new Face(triangle.Face.Indexes![0], triangle.Face.Indexes[1], triangle.Face.Indexes[2])).ToList();
        var tree = Build(faces, collision.Vectors);
        collision.Triggers = tree.Nodes.Select(node => new CollisionTrigger
        {
            V1 = new Vector3(node.Min.x, node.Min.y, node.Min.z),
            V2 = new Vector3(node.Max.x, node.Max.y, node.Max.z),
            MinTriggerIndex = node.Left,
            MaxTriggerIndex = node.Right,
        }).ToList();
        collision.Groups = tree.Groups.Select(group => new GroupInformation { Offset = (uint)group.Offset, Size = (uint)group.Size }).ToList();
        collision.Triangles = tree.TriangleOrder.Select(index => collision.Triangles[index]).ToList();
        Log.WriteLine($"Building collision BVH completed in {watch.Elapsed.TotalSeconds:F2} s: {tree.Nodes.Count} boxes, {tree.Groups.Count} leaves");
    }

    private static vec3 ToGlm(Vector4 vector) => new(vector.X, vector.Y, vector.Z);

    private static vec3 ToGlm(Vector3 vector) => new(vector.X, vector.Y, vector.Z);

    private void UpdateBounds(BuildNode node)
    {
        var bounds = new Aabb();
        for (var i = 0; i < node.Count; i++)
        {
            bounds.Grow(_triangles[_order[node.First + i]]);
        }

        node.Bounds = bounds;
    }

    private void Subdivide(BuildNode node)
    {
        if (node.Count <= _maxLeafTriangles)
        {
            return;
        }

        if (_strategy == Strategy.MedianWidestAxis)
        {
            SplitInHalf(node);
            return;
        }

        if (_strategy == Strategy.MedianBestAxis)
        {
            SplitInHalf(node, BestMedianAxis(node));
            return;
        }

        if (!FindBestSplit(node, out var axis, out var position, out var splitCost))
        {
            // Nothing separates the centroids, halve the run so leaves stay small
            SplitInHalf(node);
            return;
        }

        // Splitting has to beat testing every triangle of the node
        if (splitCost >= node.Count * node.Bounds.Area() && node.Count <= _maxLeafTriangles * 4)
        {
            return;
        }

        var i = node.First;
        var j = node.First + node.Count - 1;
        while (i <= j)
        {
            if (_triangles[_order[i]].Centroid[axis] < position)
            {
                i++;
            }
            else
            {
                (_order[i], _order[j]) = (_order[j], _order[i]);
                j--;
            }
        }

        var leftCount = i - node.First;
        if (leftCount == 0 || leftCount == node.Count)
        {
            SplitInHalf(node);
            return;
        }

        MakeChildren(node, leftCount);
    }

    // Halves the run along its widest axis by centroid order
    private void SplitInHalf(BuildNode node)
    {
        var extent = node.Bounds.Max - node.Bounds.Min;
        SplitInHalf(node, extent.x >= extent.y && extent.x >= extent.z ? 0 : extent.y >= extent.z ? 1 : 2);
    }

    private void SplitInHalf(BuildNode node, int axis)
    {
        SortByCentroid(node, axis);
        MakeChildren(node, node.Count / 2);
    }

    private void SortByCentroid(BuildNode node, int axis)
    {
        Array.Sort(_order, node.First, node.Count, Comparer<int>.Create((a, b) => _triangles[a].Centroid[axis].CompareTo(_triangles[b].Centroid[axis])));
    }

    // The axis whose halves by centroid order make the cheapest pair of boxes
    private int BestMedianAxis(BuildNode node)
    {
        var bestAxis = 0;
        var bestCost = float.MaxValue;
        var half = node.Count / 2;
        for (var axis = 0; axis < 3; axis++)
        {
            SortByCentroid(node, axis);
            var left = new Aabb();
            var right = new Aabb();
            for (var i = 0; i < node.Count; i++)
            {
                if (i < half)
                {
                    left.Grow(_triangles[_order[node.First + i]]);
                }
                else
                {
                    right.Grow(_triangles[_order[node.First + i]]);
                }
            }

            var cost = half * left.Area() + (node.Count - half) * right.Area();
            if (cost < bestCost)
            {
                bestCost = cost;
                bestAxis = axis;
            }
        }

        return bestAxis;
    }

    private void MakeChildren(BuildNode node, int leftCount)
    {
        node.Left = new BuildNode { First = node.First, Count = leftCount };
        node.Right = new BuildNode { First = node.First + leftCount, Count = node.Count - leftCount };
        UpdateBounds(node.Left);
        UpdateBounds(node.Right);
        Subdivide(node.Left);
        Subdivide(node.Right);
    }

    private bool FindBestSplit(BuildNode node, out int bestAxis, out float bestPosition, out float bestCost)
    {
        bestAxis = 0;
        bestPosition = 0.0f;
        bestCost = float.MaxValue;
        var found = false;
        for (var axis = 0; axis < 3; axis++)
        {
            var centroidMin = float.MaxValue;
            var centroidMax = float.MinValue;
            for (var i = 0; i < node.Count; i++)
            {
                var centroid = _triangles[_order[node.First + i]].Centroid[axis];
                centroidMin = MathF.Min(centroidMin, centroid);
                centroidMax = MathF.Max(centroidMax, centroid);
            }

            if (centroidMax - centroidMin < 1e-5f)
            {
                continue;
            }

            var bins = new Aabb[BinCount];
            var counts = new int[BinCount];
            for (var i = 0; i < BinCount; i++)
            {
                bins[i] = new Aabb();
            }

            var scale = BinCount / (centroidMax - centroidMin);
            for (var i = 0; i < node.Count; i++)
            {
                ref readonly var triangle = ref _triangles[_order[node.First + i]];
                var bin = Math.Min(BinCount - 1, (int)((triangle.Centroid[axis] - centroidMin) * scale));
                counts[bin]++;
                bins[bin].Grow(triangle);
            }

            var leftArea = new float[BinCount - 1];
            var leftCount = new int[BinCount - 1];
            var rightArea = new float[BinCount - 1];
            var rightCount = new int[BinCount - 1];
            var left = new Aabb();
            var right = new Aabb();
            var leftSum = 0;
            var rightSum = 0;
            for (var i = 0; i < BinCount - 1; i++)
            {
                leftSum += counts[i];
                leftCount[i] = leftSum;
                left.Grow(bins[i]);
                leftArea[i] = left.Area();
                rightSum += counts[BinCount - 1 - i];
                rightCount[BinCount - 2 - i] = rightSum;
                right.Grow(bins[BinCount - 1 - i]);
                rightArea[BinCount - 2 - i] = right.Area();
            }

            var binWidth = (centroidMax - centroidMin) / BinCount;
            for (var i = 0; i < BinCount - 1; i++)
            {
                if (leftCount[i] == 0 || rightCount[i] == 0)
                {
                    continue;
                }

                var cost = leftCount[i] * leftArea[i] + rightCount[i] * rightArea[i];
                if (cost >= bestCost)
                {
                    continue;
                }

                found = true;
                bestCost = cost;
                bestAxis = axis;
                bestPosition = centroidMin + binWidth * (i + 1);
            }
        }

        return found;
    }

    // Nodes go depth first, each inner node right before its left subtree, the way the game's files have them
    private void Flatten(BuildNode node, List<Node> nodes, List<Group> groups)
    {
        var index = nodes.Count;
        if (node.Left == null)
        {
            var group = groups.Count;
            groups.Add(new Group(node.First, node.Count));
            nodes.Add(new Node(node.Bounds.Min, node.Bounds.Max, ~group, ~group));
            return;
        }

        nodes.Add(null!);
        var left = nodes.Count;
        Flatten(node.Left, nodes, groups);
        var right = nodes.Count;
        Flatten(node.Right!, nodes, groups);
        nodes[index] = new Node(node.Bounds.Min, node.Bounds.Max, left, right);
    }
}
