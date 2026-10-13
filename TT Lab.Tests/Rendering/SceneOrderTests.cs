using TT_Lab.Rendering.Services;

namespace TT_Lab.Tests.Rendering;

// The game draws its buckets one after the other, every shader type mixed and nothing sorted by depth (the decomp's buckets.cpp and
// materials.cpp). TT Lab drew every opaque pass and then every blended one, a pass per shader type: roof05's bucket-6 landscape billboards
// came after the bucket 8 and 10 mists, the reverse of the game, and the glass materials' two shaders were drawn the other way round
public class SceneOrderTests
{
    private static string[] Ordered(params (string Name, SceneOrder.Draw Draw)[] draws)
    {
        return SceneOrder.Order(draws.Select(draw => draw.Draw).ToList()).Select(index => draws[index].Name).ToArray();
    }

    [Fact]
    public void BucketsComeOneAfterTheOther()
    {
        var mist = new object();
        var billboard = new object();
        var ground = new object();

        var order = Ordered(
            ("mist", new SceneOrder.Draw(8, false, mist, 1, 0)),
            ("billboard", new SceneOrder.Draw(6, false, billboard, 2, 0)),
            ("ground", new SceneOrder.Draw(2, false, ground, 3, 0)));

        Assert.Equal(["ground", "billboard", "mist"], order);
    }

    // In a bucket a material is drawn where it's first used, with all of its draws: its models in the order of their first draws
    [Fact]
    public void AMaterialsDrawsComeTogetherAtItsFirstUse()
    {
        var wall = new object();
        var window = new object();

        var order = Ordered(
            ("wall 1", new SceneOrder.Draw(6, false, wall, 1, 0)),
            ("window 2", new SceneOrder.Draw(6, false, window, 2, 0)),
            ("wall 3", new SceneOrder.Draw(6, false, wall, 3, 0)),
            ("window 4", new SceneOrder.Draw(6, false, window, 4, 0)));

        Assert.Equal(["wall 1", "wall 3", "window 2", "window 4"], order);
    }

    // The skins and blend skins are drawn straight into their bucket as they're submitted, before the materials the frame's end adds
    [Fact]
    public void SkinsComeFirstInTheirBucket()
    {
        var scenery = new object();
        var skin = new object();

        var order = Ordered(
            ("scenery", new SceneOrder.Draw(2, false, scenery, 1, 0)),
            ("skin", new SceneOrder.Draw(2, true, skin, 5, 0)),
            ("other bucket", new SceneOrder.Draw(0, false, scenery, 9, 0)));

        Assert.Equal(["other bucket", "skin", "scenery"], order);
    }

    // A material's shaders are all drawn in its bucket, one after the other for each model
    [Fact]
    public void AMaterialsShadersAreDrawnInTurnForEachModel()
    {
        var glass = new object();

        var order = Ordered(
            ("model 2 shader 1", new SceneOrder.Draw(11, false, glass, 2, 1)),
            ("model 1 shader 1", new SceneOrder.Draw(11, false, glass, 1, 1)),
            ("model 2 shader 0", new SceneOrder.Draw(11, false, glass, 2, 0)),
            ("model 1 shader 0", new SceneOrder.Draw(11, false, glass, 1, 0)));

        Assert.Equal(["model 1 shader 0", "model 1 shader 1", "model 2 shader 0", "model 2 shader 1"], order);
    }

    // Every placement of a scenery's mesh is a batch of its own, but the game's model of them takes its instances in a block that opens at
    // its first draw: roof05's two landscape billboard meshes are drawn one after the other, not in the order their placements are in the tree
    [Fact]
    public void AModelsPlacementsAreDrawnTogetherAtItsFirstDraw()
    {
        var landscape = new object();

        var order = Ordered(
            ("A at 0", new SceneOrder.Draw(6, false, landscape, 1, 0, GameModel: 7)),
            ("B at 3", new SceneOrder.Draw(6, false, landscape, 2, 0, GameModel: 8)),
            ("A at 192", new SceneOrder.Draw(6, false, landscape, 3, 0, GameModel: 7)),
            ("B at 962", new SceneOrder.Draw(6, false, landscape, 4, 0, GameModel: 8)),
            ("A at 1645", new SceneOrder.Draw(6, false, landscape, 5, 0, GameModel: 7)));

        Assert.Equal(["A at 0", "A at 192", "A at 1645", "B at 3", "B at 962"], order);
    }

    // A block draws its instances with one part of the model, then with the next (WriteSubModels): parts of one material come one after the
    // other for every placement
    [Fact]
    public void AModelsPartsAreDrawnOneAfterTheOther()
    {
        var bricks = new object();

        var order = Ordered(
            ("first placement part 0", new SceneOrder.Draw(2, false, bricks, 1, 0, GameModel: 3, Part: 0)),
            ("first placement part 1", new SceneOrder.Draw(2, false, bricks, 2, 0, GameModel: 3, Part: 1)),
            ("second placement part 0", new SceneOrder.Draw(2, false, bricks, 3, 0, GameModel: 3, Part: 0)),
            ("second placement part 1", new SceneOrder.Draw(2, false, bricks, 4, 0, GameModel: 3, Part: 1)));

        Assert.Equal(["first placement part 0", "second placement part 0", "first placement part 1", "second placement part 1"], order);
    }
}
