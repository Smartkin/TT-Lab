using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Collision;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Extensions;
using TT_Lab.Project.Prefabs;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using NVector3 = System.Numerics.Vector3;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// The viewport's scenery mode edits the scenery's placed meshes and its collision through the chunk's document: every edit a step of its
// history, placeholders on a checker material, collision made of meshes the way the add-on makes it, meshes saved as prefabs
[Collection(ProjectCollection.Name)]
public sealed class SceneryModeTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly Package _package;

    public SceneryModeTests()
    {
        _package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(_package.Name, "levels"));
        var crash = _project.Add(new GameObject(), "Crash", 0x0);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);
    }

    public void Dispose() => _project.Dispose();

    // A new chunk: its scenery has a 20 unit ground and the ground's collision of two triangles
    private LevelChunk CreateChunk(string name)
    {
        var folder = _project.GetFolder(_package, "levels");
        var chunk = (LevelChunk)AssetFactory.CreateAsset(typeof(LevelChunk), folder, name, string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(), asset => AssetDataFactory.CreateChunkData(folder, asset))!;
        return chunk;
    }

    private static ViewportViewModel OpenViewport(LevelChunk chunk, out DocumentViewModel document)
    {
        document = new DocumentViewModel(chunk);
        document.Initialize();
        var viewport = new ViewportViewModel();
        viewport.Init(document);
        return viewport;
    }

    private static Scenery SceneryOf(LevelChunk chunk) => chunk.ChunkResources.Select(AssetManager.Get().GetAsset).OfType<Scenery>().Single();

    private static SceneryData DataOf(LevelChunk chunk) => ((IAsset)SceneryOf(chunk)).GetData<SceneryData>();

    private static PropertyNode SceneryNode(DocumentViewModel document) => document.PropertyGraph.Find("Root.ChunkResources")!.Children
        .Select(resource => resource.Find("[data]")!).Single(data => data.GetValue() is Scenery);

    private static List<(NVector3 A, NVector3 B, NVector3 C)> TrianglesOf(CollisionData collision) =>
        collision.Triangles.Select(triangle => (Position(collision, triangle.Face.Indexes![0]), Position(collision, triangle.Face.Indexes[1]), Position(collision, triangle.Face.Indexes[2]))).ToList();

    private static NVector3 Position(CollisionData collision, int vertex) => new(collision.Vertexes[vertex].X, collision.Vertexes[vertex].Y, collision.Vertexes[vertex].Z);

    [AvaloniaFact]
    public void PlaceholdersAreMeshesOfTheSceneryOnTheCheckerMaterial()
    {
        var chunk = CreateChunk("beach");
        var viewport = OpenViewport(chunk, out var document);
        var scenery = DataOf(chunk);
        var before = scenery.Placements.Count;

        viewport.AddPlaceholder(PlaceholderShape.Cube);
        viewport.AddPlaceholder(PlaceholderShape.Ramp);

        Assert.Equal(before + 2, scenery.Placements.Count);
        var cube = scenery.Placements[before];
        Assert.True(AssetManager.Get().GetAsset(cube.Model).IsInternal);
        var material = AssetManager.Get().GetAsset<Material>(AssetManager.Get().GetAssetData<MeshData>(cube.Model).Materials.Single());
        Assert.StartsWith(SceneryPlaceholders.CheckerMaterialId, material.Parameters[TlmMaterials.BlenderMaterialParameter]!.ToString());
        Assert.False(material.IsInternal);
        // Every placeholder is drawn with the one checker material of the version
        Assert.Equal([material.URI], AssetManager.Get().GetAssetData<MeshData>(scenery.Placements[before + 1].Model).Materials);
        // Its box is the cube's, standing on the ground
        Assert.Equal((-1.0f, 0.0f, -1.0f, 1.0f, 2.0f, 1.0f), (cube.Box.V1.X, cube.Box.V1.Y, cube.Box.V1.Z, cube.Box.V2.X, cube.Box.V2.Y, cube.Box.V2.Z));

        document.Undo();
        Assert.Equal(before + 1, scenery.Placements.Count);
        document.Redo();
        Assert.Equal(before + 2, scenery.Placements.Count);

        // Saved with the scenery, the cube's triangles come back
        document.Save();
        var read = new TestAssets(_project).Reload<SceneryData>(SceneryOf(chunk));
        Assert.Equal(before + 2, read.Placements.Count);
        var readCube = AssetManager.Get().GetAssetData<ModelData>(AssetManager.Get().GetAssetData<MeshData>(read.Placements[before].Model).Model);
        Assert.Equal(12, readCube.Faces.Sum(part => part.Count));
        viewport.Close();
    }

    // A placed mesh is moved through its matrix, which undo puts back as it was
    [AvaloniaFact]
    public void PlacedMeshesMoveAsStepsOfTheHistory()
    {
        var chunk = CreateChunk("beach");
        var viewport = OpenViewport(chunk, out var document);
        var matrix = SceneryNode(document).Find($"AssetData.{nameof(SceneryData.Placements)}[0].{nameof(SceneryPlacement.Matrix)}")!;
        var before = matrix.GetValue();
        var moved = System.Numerics.Matrix4x4.CreateTranslation(3, 0, 4).ToTwin();

        matrix.SetValue(moved);
        Assert.Same(moved, DataOf(chunk).Placements[0].Matrix);

        document.Undo();
        Assert.Same(before, DataOf(chunk).Placements[0].Matrix);
        viewport.Close();
    }

    // The collision is one value of the history: an edit is one step, undo puts back the very triangles there were, saving writes them
    [AvaloniaFact]
    public void CollisionEditsAreStepsOfTheHistoryAndGetSaved()
    {
        var chunk = CreateChunk("beach");
        var viewport = OpenViewport(chunk, out var document);
        var collision = DataOf(chunk).CollisionDataOrNull()!;
        var ground = collision.Geometry;

        viewport.AddCollisionPrimitive(PlaceholderShape.Cube);

        Assert.Equal(14, collision.Triangles.Count);
        Assert.True(document.CanUndo);
        document.Undo();
        Assert.Same(ground, collision.Geometry);
        Assert.Equal(2, collision.Triangles.Count);
        document.Redo();
        Assert.Equal(14, collision.Triangles.Count);

        viewport.EditCollision("Deleted the ground", geometry => (CollisionEdits.Delete(geometry, [0, 1]), []));
        Assert.Equal(12, collision.Triangles.Count);
        var edited = TrianglesOf(collision);

        document.Save();
        // Reading the scenery again makes its collision again
        var read = new TestAssets(_project).Reload<SceneryData>(SceneryOf(chunk)).CollisionDataOrNull()!;
        Assert.Equal(edited, TrianglesOf(read));
        viewport.Close();
    }

    // Collision made of a mesh is turned to the game's winding, the triangles the collision has already are left out
    [AvaloniaFact]
    public void CollisionOfMeshesLeavesOutWhatTheCollisionHas()
    {
        var chunk = CreateChunk("beach");
        var viewport = OpenViewport(chunk, out _);
        var scenery = DataOf(chunk);
        var collision = scenery.CollisionDataOrNull()!;
        var surface = collision.Triangles[0].Surface;
        viewport.AddPlaceholder(PlaceholderShape.Cube);

        // The ground's mesh is where its collision is
        var (_, groundAdded, groundResult) = CollisionEdits.Add(collision.Geometry, ViewportViewModel.PlacementTriangles(scenery.Placements[0]), surface);
        Assert.Empty(groundAdded);
        Assert.Equal(2, groundResult.Skipped);

        var (withCube, cubeAdded, _) = CollisionEdits.Add(collision.Geometry, ViewportViewModel.PlacementTriangles(scenery.Placements[^1]), surface);
        Assert.Equal(12, cubeAdded.Length);
        // Turned into the solid: each faces the middle of the cube standing at the origin
        var middle = new NVector3(0, 1, 0);
        Assert.All(cubeAdded, index =>
        {
            var (a, b, c) = withCube.Corners(index);
            Assert.True(NVector3.Dot(NVector3.Cross(b - a, c - a), (a + b + c) / 3 - middle) < 0);
        });
        viewport.Close();
    }

    // Collision of the selected meshes is as coarse as the game's, which only collides Crash with 32 triangles at a time: a sphere stands
    // on its hull, wound into the solid
    [AvaloniaFact]
    public void CollisionOfTheSelectedMeshesIsAsCoarseAsTheGames()
    {
        var chunk = CreateChunk("beach");
        var viewport = OpenViewport(chunk, out _);
        var scenery = DataOf(chunk);
        var collision = scenery.CollisionDataOrNull()!;
        var surface = collision.Triangles[0].Surface;
        viewport.AddPlaceholder(PlaceholderShape.Sphere);
        var sphere = ViewportViewModel.PlacementTriangles(scenery.Placements[^1]).ToList();

        var (edited, added, result) = CollisionEdits.AddMeshes(collision.Geometry, [sphere], surface);

        Assert.Equal(1, result.Hulls);
        Assert.InRange(added.Length, 4, sphere.Count / 2);
        var middle = sphere.Aggregate(NVector3.Zero, (sum, triangle) => sum + triangle.A + triangle.B + triangle.C) / (3 * sphere.Count);
        Assert.All(added, index =>
        {
            var (a, b, c) = edited.Corners(index);
            Assert.True(NVector3.Dot(NVector3.Cross(b - a, c - a), (a + b + c) / 3 - middle) < 0);
        });
        Assert.Equal(0, CollisionEdits.Crowding(edited, added).Places);
        viewport.Close();
    }

    // A scenery prefab keeps its meshes, placing it makes them the other chunk's own, around where it goes
    [AvaloniaFact]
    public void SceneryPrefabsTakeTheirMeshesToOtherChunks()
    {
        var beach = CreateChunk("beach");
        var viewport = OpenViewport(beach, out _);
        viewport.AddPlaceholder(PlaceholderShape.Pyramid);
        var source = DataOf(beach).Placements[^1];
        var library = new PrefabLibrary(_project.Project);

        var prefab = library.CaptureScenery(SceneryOf(beach), [source], source.Matrix.ToSystem().Translation, "Pyramid");
        Assert.Equal("1 scenery mesh, PS2, from " + beach.AdditionalPath, PrefabLibrary.Describe(prefab));

        var cove = CreateChunk("cove");
        var placed = Assert.Single(library.PlaceScenery(prefab, DataOf(cove), new NVector3(5, 1, 5)));
        Assert.Equal(new NVector3(5, 1, 5), placed.Matrix.ToSystem().Translation);
        var mesh = AssetManager.Get().GetAsset(placed.Model);
        Assert.Equal(SceneryOf(cove).URI, mesh.InternalOwner?.URI);
        Assert.Equal(AssetManager.Get().GetAssetData<MeshData>(source.Model).Materials, AssetManager.Get().GetAssetData<MeshData>(placed.Model).Materials);
        Assert.Equal(ViewportViewModel.PlacementTriangles(source).Count(), ViewportViewModel.PlacementTriangles(placed).Count());
        viewport.Close();
    }
}
