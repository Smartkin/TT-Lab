using System.Text.Json.Nodes;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Assets;

[Collection(ProjectCollection.Name)]
public sealed class AssetCreationTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly Package _package;

    public AssetCreationTests()
    {
        _package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(_package.Name, "levels"), Path.Combine(_package.Name, "Graphics"));
    }

    public void Dispose() => _project.Dispose();

    private GameObject AddCrash()
    {
        var crash = _project.Add(new GameObject(), "Crash", 0x0);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
        return crash;
    }

    private CollisionSurface AddSurface()
    {
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        return _project.Add(surface, "Surface", 0x0);
    }

    private LevelChunk? CreateChunk(Folder folder, string name)
    {
        return (LevelChunk?)AssetFactory.CreateAsset(typeof(LevelChunk), folder, name, string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(), asset => AssetDataFactory.CreateChunkData(folder, asset));
    }

    [Fact]
    public void NewChunkHasCrashAndAFloor()
    {
        var crash = AddCrash();
        var surface = AddSurface();

        var chunk = CreateChunk(_project.GetFolder(_package, "levels"), "testlevel");

        Assert.NotNull(chunk);
        Assert.Equal(Path.Combine("levels", "testlevel"), chunk.AdditionalPath);
        Assert.False(chunk.IsGlobalDefaultChunk);
        Assert.True(chunk.SupportsViewport);
        var resources = chunk.ChunkResources.Select(_project.AssetManager.GetAsset).ToList();
        Assert.Equal([typeof(Scenery), typeof(ChunkLinks), typeof(Particles), typeof(ObjectInstance)], resources.Select(resource => resource.GetType()));
        Assert.All(resources, resource => Assert.True(File.Exists(Path.Combine(resource.FullPath, $"{resource.Name}.json")), $"{resource.Name} wasn't saved"));

        var instance = resources.OfType<ObjectInstance>().Single();
        var instanceData = ((IAsset)instance).GetData<ObjectInstanceData>();
        Assert.Equal(crash.URI, instanceData.ObjectId);
        Assert.Equal((0f, 0f, 0f), (instanceData.Position.X, instanceData.Position.Y, instanceData.Position.Z));

        var scenery = ((IAsset)resources.OfType<Scenery>().Single()).GetData<SceneryData>();
        var collision = _project.AssetManager.GetAssetData<CollisionData>(scenery.Collision);
        Assert.Equal(4, collision.Vectors.Count);
        Assert.All(collision.Vectors, vector => Assert.Equal(0f, vector.Y));
        Assert.Equal(10f, collision.Vectors.Max(vector => vector.X) - collision.Vectors.Min(vector => vector.X));
        Assert.Equal(10f, collision.Vectors.Max(vector => vector.Z) - collision.Vectors.Min(vector => vector.Z));
        Assert.Equal(2, collision.Triangles.Count);
        Assert.All(collision.Triangles, triangle => Assert.Equal(surface.URI, triangle.Surface));
    }

    [Fact]
    public void ChunksCanOnlyBeCreatedInTheLevelsFolder()
    {
        AddCrash();
        AddSurface();

        Assert.Null(CreateChunk(_project.GetFolder(_package, "Graphics"), "testlevel"));
    }

    [Fact]
    public void ChunkNeedsCrash()
    {
        AddSurface();

        Assert.Null(CreateChunk(_project.GetFolder(_package, "levels"), "testlevel"));
    }

    [Fact]
    public void NewSkydomeHasADefaultModel()
    {
        var skydome = AssetFactory.CreateAsset(typeof(Skydome), _project.GetFolder(_package, "Graphics"), "Sky", string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<Skydome>(), AssetDataFactory.CreateSkydomeData);

        Assert.NotNull(skydome);
        Assert.True(File.Exists(skydome.FullDataPath));
        var file = TlmFile.Load(skydome.FullDataPath);
        Assert.Equal(SkydomeData.TlmKind, file.Root!.GetKind());
        var meshes = file.Root.GetChildren(SkydomeData.SkydomeMeshKind).Select(node => node[TlmNodes.MeshKey]).OfType<JsonObject>().ToList();
        Assert.NotEmpty(meshes);
        Assert.True(meshes.SelectMany(mesh => mesh["parts"]!.AsArray().OfType<JsonObject>()).Sum(part => part.GetInt("vertices")) > 100);
    }
}
