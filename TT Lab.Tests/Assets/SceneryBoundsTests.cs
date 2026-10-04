using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using GlmSharp;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using SceneryBoundsTop = TT_Lab.Rendering.Objects.SceneryBoundsTop;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Path = System.IO.Path;
using Vector3 = Twinsanity.TwinsanityInterchange.Common.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.Tests.Assets;

// The game keeps a chunk's objects in the scenery tree's node whose cell holds them (FUN_001eb250), and one in none has nothing under it:
// the root's cell has to hold every place objects go. A new chunk's root was its flat ground's, Crash crept along and couldn't jump
[Collection(ProjectCollection.Name)]
public sealed class SceneryBoundsTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public SceneryBoundsTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    private static (float, float, float) Min(SceneryData data) => (data.BoundsMin.X, data.BoundsMin.Y, data.BoundsMin.Z);

    private static (float, float, float) Max(SceneryData data) => (data.BoundsMax.X, data.BoundsMax.Y, data.BoundsMax.Z);

    // The built tree's root is the cell the way the game reads it: the corners, the middle with the radius in W and the half size
    [Fact]
    public void TheTreesRootIsTheCellTheWayTheGameReadsIt()
    {
        var data = new SceneryData(_project.Add(new Scenery { Chunk = "levels/test" }, "Scenery 0"));

        SceneryBounds.SetCell(data, new vec3(1, 2, 3), new vec3(10, 20, 30));
        var root = Assert.Single(data.BuildTree(_ => 0));

        Assert.Equal((-9f, -18f, -27f, 1f), (root.BoundsMin.X, root.BoundsMin.Y, root.BoundsMin.Z, root.BoundsMin.W));
        Assert.Equal((11f, 22f, 33f, 1f), (root.BoundsMax.X, root.BoundsMax.Y, root.BoundsMax.Z, root.BoundsMax.W));
        Assert.Equal((1f, 2f, 3f), (root.BoundsCenter.X, root.BoundsCenter.Y, root.BoundsCenter.Z));
        Assert.Equal(MathF.Sqrt(1400), root.BoundsCenter.W, 1e-4f);
        Assert.Equal((10f, 20f, 30f, 1f), (root.BoundsHalfSize.X, root.BoundsHalfSize.Y, root.BoundsHalfSize.Z, root.BoundsHalfSize.W));
    }

    private (SceneryData Root, DocumentViewModel Document) Open()
    {
        var scenery = _assets.AddScenery();
        var document = new DocumentViewModel(scenery);
        document.Initialize();
        return (((IAsset)scenery).GetData<SceneryData>(), document);
    }

    [AvaloniaFact]
    public void BoundsAreEditedAsTheirMiddleAndHalfSizeAndTheHandleStandsOnTheirTop()
    {
        var (root, document) = Open();
        var center = document.PropertyGraph.Find("Root.AssetData.Bounds.Center")!;
        var halfSize = document.PropertyGraph.Find("Root.AssetData.Bounds.HalfSize")!;

        center.SetValue(new Vector3(5, 10, 0));
        halfSize.SetValue(new Vector3(300, 150, 300));

        Assert.Equal((-295f, -140f, -300f), Min(root));
        Assert.Equal((305f, 160f, 300f), Max(root));
        var top = new SceneryBoundsTop(halfSize);
        Assert.Equal(new vec3(5, 160, 0), top.ToPosition(center.GetValue()));
        var moved = (Vector3)top.ToData(new vec3(-20, 160, 40));
        Assert.Equal((-20f, 10f, 40f), (moved.X, moved.Y, moved.Z));

        document.Undo();
        document.Undo();
        Assert.Equal((-100f, -20f, -100f), Min(root));
        Assert.Equal((100f, 20f, 100f), Max(root));
    }

    private static Vector3FieldViewModel ShowField(DocumentViewModel document, PropertyNode node)
    {
        var field = Assert.IsType<Vector3FieldViewModel>(EditorDescRegistry.GetDesc(document, node).Construct());
        field.Activator.Activate();
        field.X.Activator.Activate();
        return field;
    }

    // The gizmo sets the half size while the inspector shows it. The field showed a change by setting the value it read again, and a
    // value its getter makes anew never equals the last one: it set itself until the stack ran out and took TT Lab with it
    [AvaloniaFact]
    public void TheInspectorShowsWhatTheViewportSets()
    {
        var (root, document) = Open();
        var halfSize = document.PropertyGraph.Find("Root.AssetData.Bounds.HalfSize")!;
        var field = ShowField(document, halfSize);

        halfSize.SetValue(new Vector3(300, 150, 300));

        Assert.Equal((-300f, -150f, -300f), Min(root));
        Assert.Equal("300", field.X.Text);
        document.Undo();
        Assert.Equal((-100f, -20f, -100f), Min(root));
        Assert.Equal("100", field.X.Text);
    }

    // A part typed into the inspector sets the whole box, set on its own it changed a copy and the box stayed as it was
    [AvaloniaFact]
    public void APartTypedIntoTheInspectorSetsTheBox()
    {
        var (root, document) = Open();
        var halfSize = document.PropertyGraph.Find("Root.AssetData.Bounds.HalfSize")!;
        var field = ShowField(document, halfSize);

        field.X.Text = "250";

        Assert.Equal((-250f, -20f, -100f), Min(root));
        Assert.Equal((250f, 20f, 100f), Max(root));
        Assert.Equal(250f, halfSize.GetValue<Vector3>()!.X);
        Assert.EndsWith("Bounds › HalfSize = (250, 20, 100)", document.History.Current.Description);
        document.Undo();
        Assert.Equal((-100f, -20f, -100f), Min(root));
        Assert.Equal("100", field.X.Text);
        Assert.False(document.History.CanUndo);
    }

    private SceneryData LoadWithRoot(JsonObject? cell, Vector4 farVertex)
    {
        var scenery = _project.Add(new Scenery { Chunk = "levels/test" }, "Scenery 0");
        var (_, collision) = _assets.AddCollision(_project.Add(new Collision { Chunk = "levels/test" }, "Collision"));
        collision.Vertexes[0] = farVertex;
        var file = new TlmFile(SceneryData.TlmAssetType, "Scenery 0");
        var root = TlmNodes.Create(SceneryData.TlmKind, "Scenery 0", cell ?? new JsonObject());
        root.AddChild(collision.WriteTlmNode(file));
        file.Root = root;
        Directory.CreateDirectory(Path.GetDirectoryName(scenery.FullDataPath)!);
        file.Save(scenery.FullDataPath);
        return ((IAsset)scenery).GetData<SceneryData>();
    }

    private static JsonObject Cell(float x, float y, float z) => new()
    {
        ["BoundsMin"] = new JsonArray(-x, -y, -z),
        ["BoundsMax"] = new JsonArray(x, y, z)
    };

    // A root as flat as its ground holds nothing: it gets a box around the origin like the game's, holding the collision with room to spare
    [Fact]
    public void AFlatRootGetsABoxLikeTheGamesAroundTheCollision()
    {
        var data = LoadWithRoot(Cell(10, 0, 10), new Vector4(-300, 0, 0, 1));

        Assert.Equal((-450f, -100f, -200f), Min(data));
        Assert.Equal((450f, 100f, 200f), Max(data));
    }

    // Made in Blender, the root has no cell of its own and would only be as big as what's in it
    [Fact]
    public void ARootMadeInBlenderGetsTheDefaultBox()
    {
        var data = LoadWithRoot(null, new Vector4(-30, 0, 0, 1));

        Assert.Equal((-200f, -100f, -200f), Min(data));
        Assert.Equal((200f, 100f, 200f), Max(data));
    }

    [Fact]
    public void ARootThatHoldsSomethingStaysAsItIs()
    {
        var data = LoadWithRoot(Cell(50, 20, 50), new Vector4(-30, 0, 0, 1));

        Assert.Equal((-50f, -20f, -50f), Min(data));
        Assert.Equal((50f, 20f, 50f), Max(data));
    }
}
