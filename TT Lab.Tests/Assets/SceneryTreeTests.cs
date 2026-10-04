using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;
using Vector3 = System.Numerics.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.Tests.Assets;

// A scenery keeps its meshes and LODs as a list, the build makes the tree the game culls them with from where they are (SceneryTree)
public class SceneryTreeTests
{
    private static readonly SceneryTree.Box Root = new(new Vector3(-100), new Vector3(100));

    private static SceneryTree.Box World(float min, float max) => new(new Vector3(min), new Vector3(max));

    private static SceneryPlacement Placed(float min, float max, string? node = null, bool isLod = false, uint id = 0)
    {
        var middle = new Vector3((min + max) / 2);
        var half = new Vector3((max - min) / 2);
        return new SceneryPlacement
        {
            Model = new LabURI($"res://Test/{(isLod ? "Lod" : "Mesh")}/{id}"),
            IsLod = isLod,
            Matrix = System.Numerics.Matrix4x4.CreateTranslation(middle).ToTwin(),
            Box = new BoundingBox { V1 = new Vector4(-half.X, -half.Y, -half.Z, half.Length()), V2 = new Vector4(half.X, half.Y, half.Z, 0) },
            Node = node
        };
    }

    private static uint IdOf(SceneryPlacement placement) => uint.Parse(placement.Model.ToString().Split('/')[^1]);

    private static LabURI UriOf(uint id, bool isLod) => new($"res://Test/{(isLod ? "Lod" : "Mesh")}/{id}");

    // A set bit of a slot is the lower half of its axis (bit 0 X, 1 Y, 2 Z), a middle on the plane goes to the upper half
    [Fact]
    public void NewBoxesGoDownTheOctantsTheirMiddleIsInWhileTheirCellGrownTwiceHoldsThem()
    {
        Assert.Equal("077", SceneryTree.PathOf(Root, 3, World(10, 12), null));
        Assert.Equal("770", SceneryTree.PathOf(Root, 3, World(-80, -70), null));
        // Halfway across the octant's middle, its octant's cell grown twice doesn't hold it
        Assert.Equal("0", SceneryTree.PathOf(Root, 3, World(-30, 30), null));
        Assert.Equal("", SceneryTree.PathOf(Root, 3, World(-150, 150), null));
        Assert.Equal("07", SceneryTree.PathOf(Root, 2, World(10, 12), null));
    }

    [Fact]
    public void BoxesStayInTheirNodeWhileItHoldsThem()
    {
        Assert.Equal("7", SceneryTree.PathOf(Root, 3, World(-30, 30), "7"));
        Assert.Equal("", SceneryTree.PathOf(Root, 3, World(10, 12), ""));
        Assert.Equal("0", SceneryTree.PathOf(Root, 3, World(-30, 30), "77"));
        // Deeper than the tree, or no octant at all
        Assert.Equal("0", SceneryTree.PathOf(Root, 3, World(-30, 30), "7777"));
        Assert.Equal("0", SceneryTree.PathOf(Root, 3, World(-30, 30), "9"));
    }

    [Fact]
    public void TheTreeHasEveryNodeOnTheWayToWhatsPlacedInTheGamesOrder()
    {
        List<SceneryPlacement> placements = [Placed(10, 12, id: 1), Placed(-80, -70, id: 2), Placed(-30, 30, id: 3), Placed(-150, 150, isLod: true, id: 4), Placed(11, 12, isLod: true, id: 5)];

        var tree = SceneryTree.Build(Root, 3, placements, [], Enumerable.Range(0, 128).Select(i => i < 2).ToArray(), IdOf);

        // Every node is followed by its children in slot order, the nodes at the tree's depth are leaves
        Assert.Equal([typeof(TwinSceneryRoot), typeof(TwinSceneryNode), typeof(TwinSceneryNode), typeof(TwinSceneryLeaf), typeof(TwinSceneryNode), typeof(TwinSceneryNode), typeof(TwinSceneryLeaf)],
            tree.Select(node => node.GetType()));
        var root = (TwinSceneryRoot)tree[0];
        Assert.Equal(3u, root.TreeDepth);
        Assert.Equal(ITwinScenery.SceneryType.Node, root.SceneryTypes[0]);
        Assert.Equal(ITwinScenery.SceneryType.Node, root.SceneryTypes[7]);
        Assert.Equal(6, root.SceneryTypes.Count(type => type == ITwinScenery.SceneryType.None));
        Assert.Equal(ITwinScenery.SceneryType.Leaf, ((TwinSceneryNode)tree[2]).SceneryTypes[7]);
        Assert.Equal([4u], root.LodIDs);
        Assert.Equal([3u], tree[1].MeshIDs);
        // A leaf's meshes and their boxes, then its LODs'
        Assert.Equal([1u], tree[3].MeshIDs);
        Assert.Equal([5u], tree[3].LodIDs);
        Assert.Equal([-1f, -0.5f], tree[3].BoundingBoxes.Select(box => box[0].X));
        Assert.Equal([2u], tree[6].MeshIDs);

        // The root is its cell even with something sticking out, the others their cell grown to hold what's under them
        Assert.Equal((-100f, 100f, 1f), (root.BoundsMin.X, root.BoundsMax.X, root.BoundsMin.W));
        Assert.Equal((0f, 100f, new Vector3(100).Length()), (root.BoundsCenter.X, root.BoundsHalfSize.X, root.BoundsCenter.W));
        var octant = tree[1];
        Assert.Equal((-30f, 100f), (octant.BoundsMin.X, octant.BoundsMax.X));
        // The W of the corners and the half size is the radius of the parent's cell, the center's the node's own
        Assert.Equal(new Vector3(100).Length(), octant.BoundsMin.W);
        Assert.Equal(new Vector3(100).Length(), octant.BoundsHalfSize.W);
        Assert.Equal(new Vector3(65).Length(), octant.BoundsCenter.W);
        // Every node lights by the root's lights
        Assert.All(tree, node => Assert.Equal([true, true, false], node.LightsEnabler.Take(3)));
    }

    // A game's tree made into placements and back is the same tree, with the values the tools rounded otherwise and nodes' own light bits
    [Fact]
    public void TheGamesTreeComesBackToTheByte()
    {
        List<SceneryPlacement> placements = [Placed(10, 12, "077", id: 1), Placed(-80, -70, "77", id: 2), Placed(-30, 30, "7", id: 3), Placed(-150, 150, "", isLod: true, id: 4),
            Placed(11, 12, "077", isLod: true, id: 5)];
        var rootLights = Enumerable.Range(0, 128).Select(i => i < 3).ToArray();
        var game = SceneryTree.Build(Root, 3, placements, [], rootLights, IdOf);
        game[2].BoundsCenter = new Vector4(game[2].BoundsCenter.X, game[2].BoundsCenter.Y + 1e-5f, game[2].BoundsCenter.Z, game[2].BoundsCenter.W);
        game[4].LightsEnabler[100] = true;
        var bytes = Bytes(game);

        var flat = SceneryTree.Flatten(game, rootLights, UriOf);
        var built = SceneryTree.Build(flat.Root, flat.Depth, flat.Placements, flat.Kept, rootLights, IdOf);

        Assert.Equal(bytes, Bytes(built));
        // In the tree's order, a node's meshes before its LODs
        Assert.Equal(["", "077", "077", "7", "77"], flat.Placements.Select(placement => placement.Node));
        Assert.Equal(["07", "7"], flat.Kept.Select(node => node.Path));
    }

    private static byte[] Bytes(IEnumerable<TwinSceneryBaseType> tree)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        foreach (var node in tree)
        {
            node.Write(writer);
        }

        writer.Flush();
        return stream.ToArray();
    }
}
