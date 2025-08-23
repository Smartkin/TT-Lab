using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using g3;
using GlmSharp;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance.Collision;

/// <summary>
/// Initially the approach was writing a custom BVH with SAH algorithm but the results turned out unsatisfactory.
/// Thus, a geometry3sharp library is used instead with some modifications
/// </summary>
public class BvhBuilder
{
    private readonly struct Triangle (vec3[] vectors)
    {
        public vec3[] Vectors { get; } = vectors;
        public vec3 Centroid { get; } = (vectors[0] + vectors[1] + vectors[2]) * 0.3333f;
    }

    public class BvhNode
    {
        public vec3 BboxMin { get; set; }
        public vec3 BboxMax { get; set; }
        public bool IsLeaf => TriangleCount > 0;
        public BvhNode? LeftNode { get; set; }
        public BvhNode? RightNode { get; set; }
        public int FirstTriangleIndex { get; set; }
        public int TriangleCount { get; set; }
    }

    private class BvhContext(Triangle[] triangles, int[] indices)
    {
        public Triangle[] Triangles { get; } = triangles;
        public int[] TriangleIndices { get; } = indices;
    }

    private static void UpdateBvhNodeBounds(BvhContext context, BvhNode node)
    {
        for (var i = 0; i < node.TriangleCount; i++)
        {
            var leafTriangle = context.Triangles[context.TriangleIndices[node.FirstTriangleIndex + i]];
            node.BboxMin = glm.Min(node.BboxMin, leafTriangle.Vectors[0]);
            node.BboxMin = glm.Min(node.BboxMin, leafTriangle.Vectors[1]);
            node.BboxMin = glm.Min(node.BboxMin, leafTriangle.Vectors[2]);
            node.BboxMax = glm.Max(node.BboxMax, leafTriangle.Vectors[0]);
            node.BboxMax = glm.Max(node.BboxMax, leafTriangle.Vectors[1]);
            node.BboxMax = glm.Max(node.BboxMax, leafTriangle.Vectors[2]);
        }
    }

    private struct Aabb
    {
        public Aabb()
        {
        }

        public vec3 Min { get; set; } = new(float.MaxValue);
        public vec3 Max { get; set; } = new(float.MinValue);

        public void Grow(vec3 p)
        {
            Min = glm.Min(Min, p);
            Max = glm.Max(Max, p);
        }

        public void Grow(Aabb p)
        {
            Grow(p.Min);
            Grow(p.Max);
        }

        public float Area()
        {
            var e = Max - Min;
            return e.x * e.y + e.y * e.z + e.z * e.x;
        }
    }

    private static float EvaluateSah(BvhContext context, BvhNode node, int axis, float pos)
    {
        var leftBox = new Aabb();
        var rightBox = new Aabb();
        var leftCount = 0;
        var rightCount = 0;
        for (var i = 0; i < node.TriangleCount; i++)
        {
            var triangle = context.Triangles[context.TriangleIndices[node.FirstTriangleIndex + i]];
            if (triangle.Centroid[axis] < pos)
            {
                leftCount++;
                leftBox.Grow(triangle.Vectors[0]);
                leftBox.Grow(triangle.Vectors[1]);
                leftBox.Grow(triangle.Vectors[2]);
            }
            else
            {
                rightCount++;
                rightBox.Grow(triangle.Vectors[0]);
                rightBox.Grow(triangle.Vectors[1]);
                rightBox.Grow(triangle.Vectors[2]);
            }
        }
        
        var cost = leftCount * leftBox.Area() + rightCount * rightBox.Area();
        return cost > 0 ? cost : float.MaxValue;
    }

    private struct Bin
    {
        public Aabb Bounds = new();
        public int TriangleCount = 0;

        public Bin()
        {
        }
    }

    private static float FindBestSplitPlane(BvhContext context, BvhNode node, ref int axis, ref float pos)
    {
        var bestCost = float.MaxValue;
        for (var a = 0; a < 3; ++a)
        {
            var boundsMin = float.MaxValue;
            var boundsMax = float.MinValue;
            for (var i = 0; i < node.TriangleCount; ++i)
            {
                var triangle = context.Triangles[context.TriangleIndices[node.FirstTriangleIndex + i]];
                boundsMin = float.Min(boundsMin, triangle.Centroid[a]);
                boundsMax = float.Max(boundsMax, triangle.Centroid[a]);
            }
            if (Math.Abs(boundsMin - boundsMax) < 0.000001f)
            {
                continue;
            }

            const int binAmount = 2000;
            var bins = new Bin[binAmount];
            var scale = (boundsMax - boundsMin) / binAmount;

            for (var i = 0; i < node.TriangleCount; ++i)
            {
                var triangle = context.Triangles[context.TriangleIndices[node.FirstTriangleIndex + i]];
                var binIdx = Math.Min(binAmount - 1, (int)((triangle.Centroid[a] - boundsMin) * scale));
                bins[binIdx].TriangleCount++;
                bins[binIdx].Bounds.Grow(triangle.Vectors[0]);
                bins[binIdx].Bounds.Grow(triangle.Vectors[1]);
                bins[binIdx].Bounds.Grow(triangle.Vectors[2]);
            }

            var leftArea = new float[binAmount - 1];
            var rightArea = new float[binAmount - 1];
            var leftCount = new int[binAmount - 1];
            var rightCount = new int[binAmount - 1];
            var leftBox = new Aabb();
            var rightBox = new Aabb();
            var leftSum = 0;
            var rightSum = 0;
            for (var i = 0; i < binAmount - 1; ++i)
            {
                leftSum += bins[i].TriangleCount;
                leftCount[i] = leftSum;
                leftBox.Grow(bins[i].Bounds);
                leftArea[i] = leftBox.Area();
                rightSum += bins[binAmount - 1 - i].TriangleCount;
                rightCount[binAmount - 2 - i] = rightSum;
                rightBox.Grow(bins[binAmount - 1 - i].Bounds);
                rightArea[binAmount - 2 - i] = rightBox.Area();
            }
            
            scale = (boundsMax - boundsMin) / binAmount;
            for (var i = 0; i < binAmount - 1; ++i)
            {
                var planeCost = leftCount[i] * leftArea[i] + rightCount[i] * rightArea[i];
                if (!(planeCost < bestCost))
                {
                    continue;
                }

                bestCost = planeCost;
                axis = a;
                pos = boundsMin + scale * (i + 1);
            }
        }

        return bestCost;
    }

    private static float CalculateNodeCost(BvhNode node)
    {
        var e = node.BboxMax - node.BboxMin;
        var surfaceArea = e.x * e.y + e.y * e.z + e.z * e.x;
        return node.TriangleCount * surfaceArea;
    }

    private void Subdivide(BvhContext context, BvhNode node)
    {
        const int maxTriangles = 30;
        if (node.TriangleCount <= maxTriangles)
        {
            return;
        }

        var bestAxis = 0;
        var bestPos = 0.0f;
        FindBestSplitPlane(context, node, ref bestAxis, ref bestPos);

        var i = node.FirstTriangleIndex;
        var j = i + node.TriangleCount - 1;
        while (i <= j)
        {
            if (context.Triangles[context.TriangleIndices[i]].Centroid[bestAxis] < bestPos)
            {
                i++;
            }
            else
            {
                (context.TriangleIndices[i], context.TriangleIndices[j]) = (context.TriangleIndices[j], context.TriangleIndices[i]);
                j--;
            }
        }

        var leftCount = i - node.FirstTriangleIndex;
        if (leftCount <= 0 || leftCount == node.TriangleCount)
        {
            return;
        }

        var leftNode = new BvhNode
        {
            FirstTriangleIndex = node.FirstTriangleIndex,
            TriangleCount = leftCount,
            BboxMin = new vec3(float.MaxValue),
            BboxMax = new vec3(float.MinValue),
        };
        var rightNode = new BvhNode
        {
            FirstTriangleIndex = i,
            TriangleCount = node.TriangleCount - leftCount,
            BboxMin = new vec3(float.MaxValue),
            BboxMax = new vec3(float.MinValue),
        };
        node.LeftNode = leftNode;
        node.RightNode = rightNode;
        node.TriangleCount = 0;
        UpdateBvhNodeBounds(context, leftNode);
        UpdateBvhNodeBounds(context, rightNode);
        
        Subdivide(context, leftNode);
        Subdivide(context, rightNode);
    }

    private List<CollisionTrigger> triggers = [];
    private List<GroupInformation> groups = [];
    private void TraverseBvh(BvhNode node)
    {
        var trigger = new CollisionTrigger();
        trigger.V1 = new Vector3(node.BboxMin.x, node.BboxMin.y, node.BboxMin.z);
        trigger.V2 = new Vector3(node.BboxMax.x, node.BboxMax.y, node.BboxMax.z);
        triggers.Add(trigger);
        trigger.MinTriggerIndex = triggers.Count;
        if (node.IsLeaf)
        {
            var group = new GroupInformation
            {
                Offset = (uint)node.FirstTriangleIndex,
                Size = (uint)node.TriangleCount
            };
            groups.Add(group);
            trigger.MinTriggerIndex = -groups.Count;
            trigger.MaxTriggerIndex = -groups.Count;
        }
        else
        {
            TraverseBvh(node.LeftNode!);
            trigger.MaxTriggerIndex = triggers.Count;
            TraverseBvh(node.RightNode!);
        }
    }

    public static void BuildBvh(CollisionData collision)
    {
        // var builder = new BvhBuilder();
        var indices = collision.Triangles.SelectMany(t => t.Face.Indexes!).ToArray();
        
        // var triList = new List<Triangle>();
        // var triIndices = new List<int>();
        // var triIdx = 0;
        var mesh = new DMesh3Builder();
        mesh.AppendNewMesh(false, false, false, false);
        foreach (var vec in collision.Vectors)
        {
            mesh.AppendVertex(vec.X, vec.Y, vec.Z);
        }
        for (var i = 0; i < indices.Length; i += 3)
        {
            // var v1 = collision.Vectors[indices[i]];
            // var v2 = collision.Vectors[indices[i + 1]];
            // var v3 = collision.Vectors[indices[i + 2]];
            mesh.AppendTriangle(indices[i], indices[i + 1], indices[i + 2]);
            // triList.Add(new Triangle([new vec3(v1.X, v1.Y, v1.Z), new vec3(v2.X, v2.Y, v2.Z), new vec3(v3.X, v3.Y, v3.Z)]));
            // triIndices.Add(triIdx++);
        }
        // var bvhContext = new BvhContext(triList.ToArray(), triIndices.ToArray());
        // var root = new BvhNode
        // {
        //     BboxMin = new vec3(float.MaxValue),
        //     BboxMax = new vec3(float.MinValue),
        //     FirstTriangleIndex = 0,
        //     TriangleCount = bvhContext.Triangles.Length
        // };
        // UpdateBvhNodeBounds(bvhContext, root);
        var watch = new Stopwatch();
        watch.Start();
        Log.WriteLine("Building collision bounding volume hierarchy...");
        // builder.Subdivide(bvhContext, root);

        var meshTree = new DMeshAabbTreeCustom(mesh.Meshes[0], collision.Triangles)
        {
            TopDownLeafMaxTriCount = 30,
            BottomUpClusterLookahead = 30
        };
        meshTree.Build(DMeshAABBTree3.BuildStrategy.TopDownMedian);
        var triggers = new List<CollisionTrigger>();
        var groups = new List<GroupInformation>();
        var triangleList = new List<CollisionTriangle>();
        var groupOffset = 0;
        var treeTraversal = new DMeshAabbTreeCustom.TreeTraversalCustom
        {
            BoxEnter = (boxId, box) =>
            {
                var trigger = new CollisionTrigger
                {
                    V1 = new Vector3(box.Min.x, box.Min.y, box.Min.z),
                    V2 = new Vector3(box.Max.x, box.Max.y, box.Max.z),
                };
                triggers.Add(trigger);
                return trigger;
            },
            BoxExit = (minTriggerId, maxTriggerId, trigger) =>
            {
                trigger.MinTriggerIndex = minTriggerId + 1;
                trigger.MaxTriggerIndex = maxTriggerId + 1;
            },
            GroupEnter = (groupId, triangleAmount, newTriList, trigger) =>
            {
                trigger.MinTriggerIndex = -groupId;
                trigger.MaxTriggerIndex = -groupId;
                var group = new GroupInformation
                {
                    Offset = (uint)groupOffset,
                    Size = (uint)triangleAmount
                };
                triangleList.AddRange(newTriList);
                groupOffset += triangleAmount;
                groups.Add(group);
            }
        };
        meshTree.DoTraversal(treeTraversal);
        collision.Triggers = triggers;
        collision.Groups = groups;
        collision.Triangles = triangleList;
        
        // builder.TraverseBvh(root);
        // collision.Triggers = builder.triggers;
        // collision.Groups = builder.groups;
        
        var elapsed = watch.Elapsed;
        Log.WriteLine($"Building collision BVH completed in {elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:00}");
    }
}