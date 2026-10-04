using System.Numerics;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Extensions;
using TT_Lab.Project.Prefabs;
using TT_Lab.Tests.Support;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// A new project gets a prefab of every different mesh and LOD placed in its sceneries, like the instances': where one is placed doesn't
// count, the prefab stands at the origin as it is, in its chunk's Meshes and LODs folders
[Collection(ProjectCollection.Name)]
public sealed class SceneryPrefabsTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;
    private readonly PrefabLibrary _library;

    public SceneryPrefabsTests()
    {
        _assets = new TestAssets(_project);
        _library = new PrefabLibrary(_project.Project);
    }

    public void Dispose() => _project.Dispose();

    // The test scenery: a rock placed three times, a tree and a LOD of the two, saved
    private Scenery AddChunkWithScenery(string path)
    {
        var scenery = _assets.AddScenery();
        scenery.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        var chunk = _project.Add(new LevelChunk { AdditionalPath = path }, Path.GetFileName(path));
        chunk.ChunkResources.Add(scenery.URI);
        return scenery;
    }

    [Fact]
    public void EveryDifferentMeshAndLodBecomesAPrefab()
    {
        var scenery = AddChunkWithScenery("levels/earth/hub/beach");

        Assert.Equal((3, 0), SceneryPrefabs.Make(_project.Project, _library));

        Assert.Equal(["LODs", "Meshes"], _library.Folders("levels/earth/hub/beach"));
        Assert.Equal(["Mesh 1", "Mesh 2"], _library.Load("levels/earth/hub/beach/Meshes").Select(prefab => prefab.Name));
        var lod = Assert.Single(_library.Load("levels/earth/hub/beach/LODs"));
        Assert.Equal(("LOD 1", PrefabKind.Scenery, "levels/earth/hub/beach"), (lod.Name, lod.Kind, lod.MadeFrom));
        // The model file is next to the prefab's
        Assert.True(File.Exists(Path.ChangeExtension(lod.FilePath!, ".tlm")));
        Assert.Equal(lod.ModelPath, Path.ChangeExtension(lod.FilePath!, ".tlm"));

        // The rock placed at the origin, at -75 and turned up at 60 is the first mesh, standing at the origin as it is
        var rock = _library.Load("levels/earth/hub/beach/Meshes").First();
        using var stream = new MemoryStream(PrefabLibrary.ModelOf(rock)!);
        var file = TlmFile.Read(stream);
        var node = file.Root!.FindChild(SceneryData.MeshesKind)!.GetChildren().Single();
        Assert.Null(node["translation"]);
        Assert.False(node.GetData().ContainsKey("Matrix"));
        Assert.True(node.GetData().ContainsKey("BoundingBox"));
        var placed = Assert.Single(_library.PlaceScenery(rock, ((IAsset)scenery).GetData<SceneryData>(), new Vector3(5, 1, 5)));
        Assert.Equal(Matrix4x4.CreateTranslation(5, 1, 5), placed.Matrix.ToSystem());
        Assert.False(placed.IsLod);

        // Made again, the library has them all already
        Assert.Equal((0, 3), SceneryPrefabs.Make(_project.Project, _library));
    }

    // A prefab moved elsewhere isn't made again, a new mesh goes after the names its folder has
    [Fact]
    public void PrefabsTheLibraryHasAreNotMadeAgain()
    {
        AddChunkWithScenery("levels/earth/hub/beach");
        SceneryPrefabs.Make(_project.Project, _library);
        var tree = _library.Load("levels/earth/hub/beach/Meshes").Last();
        _library.Move(tree, "Mine");
        Assert.True(File.Exists(Path.ChangeExtension(tree.FilePath!, ".tlm")));

        Assert.Equal((0, 3), SceneryPrefabs.Make(_project.Project, _library));
        Assert.Equal(["Mesh 1"], _library.Load("levels/earth/hub/beach/Meshes").Select(prefab => prefab.Name));

        _library.Delete(tree);
        Assert.False(File.Exists(Path.ChangeExtension(Path.Combine(_library.Folder, "Mine", "Mesh 2.json"), ".tlm")));
        Assert.Equal((1, 2), SceneryPrefabs.Make(_project.Project, _library));
        Assert.Equal(["Mesh 1", "Mesh 2"], _library.Load("levels/earth/hub/beach/Meshes").Select(prefab => prefab.Name));
    }

    // A node copied into another file brings the data its views point at and the materials its parts use, each material once
    [Fact]
    public void NodesCopyTheirDataAndMaterials()
    {
        var source = new TlmFile(SceneryData.TlmAssetType, "Source");
        source.Materials.Add(new JsonObject { ["name"] = "Unused" });
        source.Materials.Add(new JsonObject { ["name"] = "Stone" });
        var node = new JsonObject
        {
            ["kind"] = "scenery_mesh",
            ["mesh"] = new JsonObject
            {
                ["parts"] = new JsonArray(
                    new JsonObject { ["material"] = 1, ["position"] = source.Write<float>([1, 2, 3]) },
                    new JsonObject { ["material"] = 1, ["faces"] = source.Write<uint>([0, 1, 2]) })
            }
        };
        var copy = new TlmFile(SceneryData.TlmAssetType, "Copy");

        var copied = copy.CopyNode(source, node, new Dictionary<int, int>());

        var parts = copied["mesh"]!["parts"]!.AsArray();
        Assert.Equal([1.0f, 2.0f, 3.0f], copy.Read<float>(parts[0]!["position"]));
        Assert.Equal([0u, 1u, 2u], copy.Read<uint>(parts[1]!["faces"]));
        Assert.Equal([0, 0], parts.Select(part => part!["material"]!.GetValue<int>()));
        Assert.Equal("Stone", Assert.Single(copy.Materials)!["name"]!.GetValue<string>());
    }
}
