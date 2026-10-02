using TT_Lab.AssetData.Instance.Collision;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Assets;

// The collision tree is built like the game's files have it: a balanced binary tree of tight boxes halved by triangle count down to
// leaves of at most 30 triangles, laid out depth first with every leaf naming a run of the reordered triangles
public sealed class BvhBuilderTests
{
    // A bumpy grid of quads, two triangles each
    private static (List<BvhBuilder.Face> Faces, List<Vector4> Vectors) Grid(int cells, float size = 1.0f)
    {
        var vectors = new List<Vector4>();
        for (var z = 0; z <= cells; z++)
        {
            for (var x = 0; x <= cells; x++)
            {
                vectors.Add(new Vector4(x * size, MathF.Sin(x * 0.7f) * MathF.Cos(z * 0.4f), z * size, 1.0f));
            }
        }

        var faces = new List<BvhBuilder.Face>();
        for (var z = 0; z < cells; z++)
        {
            for (var x = 0; x < cells; x++)
            {
                var a = z * (cells + 1) + x;
                faces.Add(new BvhBuilder.Face(a, a + 1, a + cells + 1));
                faces.Add(new BvhBuilder.Face(a + 1, a + cells + 2, a + cells + 1));
            }
        }

        return (faces, vectors);
    }

    private static void AssertContains(BvhBuilder.Node node, Vector4 vector)
    {
        Assert.True(node.Min.x <= vector.X + 1e-5f && vector.X <= node.Max.x + 1e-5f, $"{vector.X} outside {node.Min.x}..{node.Max.x}");
        Assert.True(node.Min.y <= vector.Y + 1e-5f && vector.Y <= node.Max.y + 1e-5f, $"{vector.Y} outside {node.Min.y}..{node.Max.y}");
        Assert.True(node.Min.z <= vector.Z + 1e-5f && vector.Z <= node.Max.z + 1e-5f, $"{vector.Z} outside {node.Min.z}..{node.Max.z}");
    }

    [Fact]
    public void EveryTriangleIsInOneLeafWhoseBoxHoldsIt()
    {
        var (faces, vectors) = Grid(40);
        var tree = BvhBuilder.Build(faces, vectors);

        Assert.Equal(faces.Count, tree.TriangleOrder.Length);
        Assert.Equal(Enumerable.Range(0, faces.Count), tree.TriangleOrder.Order());
        Assert.Equal(faces.Count, tree.Groups.Sum(group => group.Size));
        var offset = 0;
        foreach (var group in tree.Groups)
        {
            Assert.Equal(offset, group.Offset);
            Assert.InRange(group.Size, 1, BvhBuilder.DefaultMaxLeafTriangles);
            offset += group.Size;
        }

        foreach (var node in tree.Nodes.Where(node => node.IsLeaf))
        {
            var group = tree.Groups[node.Group];
            for (var i = 0; i < group.Size; i++)
            {
                var face = faces[tree.TriangleOrder[group.Offset + i]];
                AssertContains(node, vectors[face.A]);
                AssertContains(node, vectors[face.B]);
                AssertContains(node, vectors[face.C]);
            }
        }
    }

    [Fact]
    public void InnerNodesHoldTheirChildrenAndComeRightBeforeThem()
    {
        var (faces, vectors) = Grid(40);
        var tree = BvhBuilder.Build(faces, vectors);

        var leaves = 0;
        for (var i = 0; i < tree.Nodes.Count; i++)
        {
            var node = tree.Nodes[i];
            if (node.IsLeaf)
            {
                Assert.Equal(node.Left, node.Right);
                leaves++;
                continue;
            }

            // The left child follows its parent, the right one comes after the whole left subtree
            Assert.Equal(i + 1, node.Left);
            Assert.InRange(node.Right, node.Left + 1, tree.Nodes.Count - 1);
            foreach (var child in new[] { tree.Nodes[node.Left], tree.Nodes[node.Right] })
            {
                AssertContains(node, new Vector4(child.Min.x, child.Min.y, child.Min.z, 1.0f));
                AssertContains(node, new Vector4(child.Max.x, child.Max.y, child.Max.z, 1.0f));
            }
        }

        Assert.Equal(tree.Groups.Count, leaves);
        Assert.Equal(2 * leaves - 1, tree.Nodes.Count);
    }

    [Fact]
    public void TheTreeIsBalancedLikeTheGamesAndCheapToWalk()
    {
        var (faces, vectors) = Grid(40);
        var tree = BvhBuilder.Build(faces, vectors);
        var stats = BvhStats.Measure(tree, faces, vectors, rays: 300);

        // 3200 triangles halved down to 30 or fewer is 7 levels, every leaf on the last
        Assert.Equal(7, stats.MaxDepth);
        Assert.Equal(7.0, stats.AverageLeafDepth);
        Assert.InRange(stats.AverageLeafTriangles, 20.0, 30.0);
        Assert.Equal(0.0, stats.LeafSlack, 3);
        // A ray down onto the grid tests a few leaves' triangles, nowhere near all of them
        Assert.InRange(stats.TrianglesPerRay, 1.0, 120.0);
        Assert.InRange(stats.BoxesPerRay, 2.0, 40.0);
    }

    [Fact]
    public void FewTrianglesMakeOneLeaf()
    {
        var (faces, vectors) = Grid(3);
        var tree = BvhBuilder.Build(faces, vectors);

        var node = Assert.Single(tree.Nodes);
        Assert.True(node.IsLeaf);
        Assert.Equal(0, node.Group);
        Assert.Equal(new BvhBuilder.Group(0, 18), Assert.Single(tree.Groups));
    }

    [Fact]
    public void TheGamesFilesReadBackAsTheSameTree()
    {
        var (faces, vectors) = Grid(10);
        var tree = BvhBuilder.Build(faces, vectors);
        var nodes = tree.Nodes.Select(node => new Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionNode
        {
            Min = new Vector3(node.Min.x, node.Min.y, node.Min.z), Max = new Vector3(node.Max.x, node.Max.y, node.Max.z), FirstChild = node.Left, SecondChild = node.Right
        }).ToList();
        var groups = tree.Groups.Select(group => new Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionGroup { FirstTriangle = (uint)group.Offset, Count = (uint)group.Size }).ToList();

        var read = BvhBuilder.FromTwin(nodes, groups, faces.Count);

        Assert.Equal(tree.Nodes, read.Nodes);
        Assert.Equal(tree.Groups, read.Groups);
    }
}
