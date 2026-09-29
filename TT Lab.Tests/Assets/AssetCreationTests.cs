using Avalonia.Headless.XUnit;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using Newtonsoft.Json;
using Splat;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Services.Implementations;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.ResourceTree;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;
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

    private static Folder AddFolder(Folder parent, string name)
    {
        return (Folder)AssetFactory.CreateAsset(typeof(Folder), parent, name, string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<Folder>(),
            asset => AssetDataFactory.CreateFolderData(parent, asset))!;
    }

    private static Folder ProjectAssetsFolder =>
        (Folder)Assert.Single(Locator.Current.GetService<ProjectManager>()!.FullProjectTree, element => element.Alias == "assets").Asset;

    private Package CreatePackage(string name)
    {
        return (Package)AssetFactory.CreateAsset(typeof(Package), ProjectAssetsFolder, name, _project.Project.BasePackage.ID.ToString(),
            TwinIdGeneratorServiceProvider.GetGenerator<Package>(), AssetDataFactory.CreatePackageData)!;
    }

    // What the folder's Create Asset dialog offers
    private static List<string> Offered(Folder folder)
    {
        var dialogue = new CreateAssetViewModel(new DataValidatorService(), new ActiveChunkService());
        ((FolderElementViewModel)folder.GetResourceTreeElement()).ListAssetsToCreate(dialogue);
        return dialogue.CreatableAssets.Select(model => model.DisplayName).ToList();
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

    // The build writes the chunks of the levels folder at the root of a package, whichever package it is
    [AvaloniaFact]
    public void ChunksAreOnlyOfferedInAPackagesLevelsFolder()
    {
        var levels = _project.GetFolder(_package, "levels");
        var earth = AddFolder(levels, "earth");
        var misplaced = AddFolder(_project.GetFolder(_package, "Graphics"), "levels");

        Assert.Contains("Chunk", Offered(levels));
        Assert.Contains("Chunk", Offered(earth));
        Assert.DoesNotContain("Chunk", Offered(_project.GetFolder(_package, "Graphics")));
        Assert.DoesNotContain("Chunk", Offered(misplaced));
        Assert.DoesNotContain("Chunk", Offered(_package.GetPackageFolder()));
        AddCrash();
        AddSurface();
        Assert.Equal(Path.Combine("levels", "earth", "cave"), CreateChunk(earth, "cave")!.AdditionalPath);
        Assert.Null(CreateChunk(misplaced, "grotto"));
    }

    // The game has one file for a path, another package's chunk at it would be built over it
    [AvaloniaFact]
    public void AChunksPathIsItsOwnInItsVersionOfTheGame()
    {
        AddCrash();
        AddSurface();
        Assert.NotNull(CreateChunk(_project.GetFolder(_package, "levels"), "cave"));
        var modLevels = AddFolder(CreatePackage("Mod").GetPackageFolder(), "levels");

        Assert.Null(CreateChunk(modLevels, "cave"));
        Assert.NotNull(CreateChunk(modLevels, "grotto"));
        var xbox = _project.Project.GlobalPackageXbox;
        var crash = _project.Add(new GameObject(), "Crash", 0x0, xbox);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0, xbox);
        Assert.NotNull(CreateChunk(AddFolder(_project.Project.XboxPackage.GetPackageFolder(), "levels"), "cave"));
    }

    // Packages are the folders of the project's assets folder, a package's own assets folder is a folder like any other
    [AvaloniaFact]
    public void PackagesAreMadeInTheProjectsAssetsFolder()
    {
        Assert.Equal(["Package"], Offered(ProjectAssetsFolder));

        var mod = CreatePackage("Mod");

        Assert.Equal("Mod", mod.Name);
        Assert.True(File.Exists(Path.Combine(_project.AssetsPath, "Mod", "Mod.json")));
        var folder = mod.GetPackageFolder();
        Assert.Equal(FolderMark.IsPackage | FolderMark.Locked, folder.Mark);
        Assert.Equal(mod.URI, folder.Package);
        Assert.Contains(folder.URI, ProjectAssetsFolder.Children);
        Assert.Empty(folder.Children);
        // It depends on the version's package and the project's own package depends on it, on disk as well
        Assert.Equal([_project.Project.Ps2Package.URI], mod.Dependencies);
        Assert.Equal(GamePlatform.PS2, _project.Project.GetPlatform(mod.URI));
        var basePackagePath = Path.Combine(_project.AssetsPath, _project.Project.BasePackage.Name, $"{_project.Project.BasePackage.Name}.json");
        Assert.Contains(mod.URI, JsonConvert.DeserializeObject<Package>(File.ReadAllText(basePackagePath))!.Dependencies);
        // Its assets belong to it
        var objects = AddFolder(folder, "assets");
        Assert.Equal(mod.URI, objects.Package);
        Assert.DoesNotContain("Package", Offered(objects));
        Assert.Contains("Game Object", Offered(objects));
        Assert.DoesNotContain("Package", Offered(_package.GetPackageFolder()));
        // The tree following the file system keeps it as it is
        Locator.Current.GetService<ProjectManager>()!.SyncProjectTree();
        Assert.Single(ProjectAssetsFolder.Children, uri => uri == folder.URI);
        Assert.Contains(objects.URI, folder.Children);
    }

    [AvaloniaFact]
    public void BuildsWriteTheChunksOfEveryPackageOfTheVersion()
    {
        var mod = CreatePackage("Mod");
        var modLevels = AddFolder(mod.GetPackageFolder(), "levels");
        AddFolder(_project.Project.XboxPackage.GetPackageFolder(), "levels");

        Assert.Equal([_project.GetFolder(_package, "levels"), modLevels], _project.Project.GetLevelsFolders(GamePlatform.PS2));
        Assert.Empty(_project.Project.GetLevelsFolders(GamePlatform.Xbox));
        _project.Project.XboxPackage.Enabled = true;
        Assert.Single(_project.Project.GetLevelsFolders(GamePlatform.Xbox));
        mod.Enabled = false;
        Assert.Equal([_project.GetFolder(_package, "levels")], _project.Project.GetLevelsFolders(GamePlatform.PS2));
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
