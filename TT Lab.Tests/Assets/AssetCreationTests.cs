using Avalonia.Headless.XUnit;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
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

    // A new chunk is a level to walk around right away: Crash on a checkered ground 20 units across with its collision, lit like the
    // game's levels (an ambient light of a third of white at 4.5 and a light from above), the lights on in the root node the game
    // gathers them by, which left Crash black before
    [AvaloniaFact]
    public void NewChunkHasCrashOnALitGround()
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

        var sceneryAsset = resources.OfType<Scenery>().Single();
        var scenery = ((IAsset)sceneryAsset).GetData<SceneryData>();
        Assert.True(scenery.HasLighting);
        var ambient = Assert.Single(scenery.AmbientLights);
        Assert.Equal((1.0f / 3.0f, 4.5f), (ambient.Color.X, ambient.Intensity));
        var sun = Assert.Single(scenery.DirectionalLights);
        Assert.InRange(sun.Direction.Y, 0.9f, 1.0f);
        var tree = scenery.BuildTree(_ => 0);
        Assert.Equal([true, true, false], tree[0].LightsEnabler.Take(3));
        // Its tree's root is the box the game keeps the chunk's objects in, the ground's own flat cell left Crash nothing under him
        Assert.Equal((-200f, -100f, -200f, 200f, 100f, 200f), (scenery.BoundsMin.X, scenery.BoundsMin.Y, scenery.BoundsMin.Z, scenery.BoundsMax.X, scenery.BoundsMax.Y, scenery.BoundsMax.Z));
        // The ground goes down the octants its middle is in while their cells grown twice hold it
        var placed = Assert.Single(scenery.Placements);
        Assert.Equal("0777", placed.Node);
        Assert.Equal(5, tree.Count);

        var ground = _project.AssetManager.GetAssetData<MeshData>(placed.Model);
        var material = _project.AssetManager.GetAsset<Material>(Assert.Single(ground.Materials));
        Assert.False(material.IsInternal);
        var shader = Assert.Single(((IAsset)material).GetData<MaterialData>().Shaders);
        Assert.Equal(Twinsanity.TwinsanityInterchange.Common.TwinShader.Type.StandardUnlit, shader.ShaderType);
        Assert.Equal(64, _project.AssetManager.GetAssetData<TextureData>(shader.TextureId).Bitmap!.PixelSize.Width);
        // Crash's shadow falls on it like on the game's scenery: with the GS's FBA on it had none
        Assert.True(shader.AlphaCorrectionValue);

        var collision = _project.AssetManager.GetAssetData<CollisionData>(scenery.Collision);
        Assert.Equal(4, collision.Vertexes.Count);
        Assert.All(collision.Vertexes, vector => Assert.Equal(0f, vector.Y));
        Assert.Equal(20f, collision.Vertexes.Max(vector => vector.X) - collision.Vertexes.Min(vector => vector.X));
        Assert.Equal(20f, collision.Vertexes.Max(vector => vector.Z) - collision.Vertexes.Min(vector => vector.Z));
        Assert.Equal(2, collision.Triangles.Count);
        Assert.All(collision.Triangles, triangle => Assert.Equal(surface.URI, triangle.Surface));

        // The next chunk's ground has the same material
        var next = CreateChunk(_project.GetFolder(_package, "levels"), "nextlevel")!;
        var nextScenery = ((IAsset)next.ChunkResources.Select(_project.AssetManager.GetAsset).OfType<Scenery>().Single()).GetData<SceneryData>();
        Assert.Equal(material.URI, _project.AssetManager.GetAssetData<MeshData>(nextScenery.Placements[0].Model).Materials[0]);
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

    // The levels folder is the game's Levels folder, which only has chunks: it offered every other asset as well
    [AvaloniaFact]
    public void TheLevelsFolderOnlyMakesFoldersAndChunks()
    {
        var levels = _project.GetFolder(_package, "levels");
        var earth = AddFolder(levels, "earth");

        Assert.Equal(["Folder", "Chunk"], Offered(levels));
        Assert.Equal(["Folder", "Chunk"], Offered(earth));
        Assert.Contains("Game Object", Offered(_project.GetFolder(_package, "Graphics")));
    }

    // An asset made in a folder of its package has its files there: every asset went into its type's folder, where the tree then found
    // it. At the package's root or in its type's folder it goes where it went before
    [AvaloniaFact]
    public void AnAssetMadeInAFolderIsInIt()
    {
        var mine = AddFolder(AddFolder(_package.GetPackageFolder(), "My Folder"), "Props");
        GameObject Create(Folder folder, string name) => (GameObject)AssetFactory.CreateAsset(typeof(GameObject), folder, name, _project.Project.BasePackage.ID.ToString(),
            TwinIdGeneratorServiceProvider.GetGenerator<GameObject>(), AssetDataFactory.CreateGameObjectData)!;

        var made = Create(mine, "Thing");

        Assert.Equal("My Folder/Props", made.FolderInPackage);
        Assert.Equal(Path.Combine(_project.AssetsPath, _package.Name, "My Folder", "Props"), Path.GetFullPath(made.FullPath));
        Assert.True(File.Exists(Path.Combine(made.FullPath, $"{made.Name}.json")));
        Assert.Contains(made.URI, mine.Children);
        Assert.Contains(made.GetResourceTreeElement(), mine.GetResourceTreeElement().GetInternalChildren()!);
        // The tree following the file system keeps it there, and the file read back is the same asset
        Locator.Current.GetService<ProjectManager>()!.SyncProjectTree();
        Assert.Contains(made.URI, mine.Children);
        var read = new GameObject();
        read.Deserialize(File.ReadAllText(Path.Combine(made.FullPath, $"{made.Name}.json")));
        read.RegenerateUri();
        Assert.Equal(made.URI, read.URI);

        Assert.Null(Create(_package.GetPackageFolder(), "Rooted").FolderInPackage);
        var typed = Create(_project.GetFolder(_package, "GameObject"), "Typed");
        Assert.Null(typed.FolderInPackage);
        Assert.Equal(Path.Combine(_project.AssetsPath, _package.Name, "GameObject"), Path.GetFullPath(typed.FullPath));
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

    // A chunk's path has the separators of the system the project was made on, and it's the same path on the other
    [AvaloniaFact]
    public void AChunksPathIsItsOwnWhicheverSystemMadeIt()
    {
        AddCrash();
        AddSurface();
        var cave = CreateChunk(_project.GetFolder(_package, "levels"), "cave")!;
        var otherSeparator = Path.DirectorySeparatorChar == '/' ? '\\' : '/';
        cave.AdditionalPath = cave.AdditionalPath!.Replace(Path.DirectorySeparatorChar, otherSeparator);
        var modLevels = AddFolder(CreatePackage("Mod").GetPackageFolder(), "levels");

        Assert.Null(CreateChunk(modLevels, "cave"));
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

    // A new model starts as something to see and build on: a cube a unit across standing on the ground, the rigid body of its one joint, on
    // the checker material of its version of the game, which every new model and scenery placeholder shares. It was an empty model
    [AvaloniaFact]
    public void NewOgiIsACheckeredCube()
    {
        OGI Create(string name) => (OGI)AssetFactory.CreateAsset(typeof(OGI), _project.GetFolder(_package), name, string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<OGI>(), AssetDataFactory.CreateOgiData)!;

        var data = new TestAssets(_project).Reload<OGIData>(Create("Box"));

        var root = Assert.Single(data.Joints);
        Assert.Equal((0xFF, 0xFF), (root.Id, root.ParentIndex));
        Assert.Equal([(Byte)0], data.RigidModelJointIndices);
        Assert.Equal((-0.5f, 0f, -0.5f, 0.5f, 1f, 0.5f),
            (data.BoundingBox[0].X, data.BoundingBox[0].Y, data.BoundingBox[0].Z, data.BoundingBox[1].X, data.BoundingBox[1].Y, data.BoundingBox[1].Z));
        var body = _project.AssetManager.GetAssetData<RigidModelData>(Assert.Single(data.RigidModelIds));
        var model = _project.AssetManager.GetAssetData<ModelData>(body.Model);
        Assert.Equal(12, model.Faces.Sum(part => part.Count));
        var vertexes = model.Vertexes.SelectMany(part => part).ToList();
        Assert.Equal((-0.5f, 0f, -0.5f), (vertexes.Min(vertex => vertex.Position.X), vertexes.Min(vertex => vertex.Position.Y), vertexes.Min(vertex => vertex.Position.Z)));
        Assert.Equal((0.5f, 1f, 0.5f), (vertexes.Max(vertex => vertex.Position.X), vertexes.Max(vertex => vertex.Position.Y), vertexes.Max(vertex => vertex.Position.Z)));
        // Every face goes over the whole picture, the checker's two squares across: a square a unit, like the scenery's, made each face one grey
        Assert.Equal((0f, 1f), (vertexes.Min(vertex => vertex.UV.X), vertexes.Max(vertex => vertex.UV.X)));

        var material = _project.AssetManager.GetAsset<Material>(Assert.Single(body.Materials));
        Assert.False(material.IsInternal);
        Assert.Equal($"{SceneryPlaceholders.CheckerMaterialId}_PS2", material.Parameters[TlmMaterials.BlenderMaterialParameter]?.ToString());
        var shader = Assert.Single(((IAsset)material).GetData<MaterialData>().Shaders);
        Assert.Equal(Twinsanity.TwinsanityInterchange.Common.TwinShader.Type.StandardUnlit, shader.ShaderType);

        var next = new TestAssets(_project).Reload<OGIData>(Create("Box 2"));
        Assert.Equal(body.Materials, _project.AssetManager.GetAssetData<RigidModelData>(next.RigidModelIds[0]).Materials);
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
