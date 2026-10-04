using Avalonia.Headless.XUnit;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Extensions;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Common;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// New object instances made from the viewport's menu are the game's wumpa fruit, or the nearest thing the chunk has
[Collection(ProjectCollection.Name)]
public sealed class ViewportCreateMenuTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private GameObject AddObject(string name, uint id, Package? package = null)
    {
        var gameObject = _project.Add(new GameObject(), name, id, package);
        gameObject.SetData(new GameObjectData(gameObject) { Name = name });
        return gameObject;
    }

    [Fact]
    public void TheWumpaOfTheGlobalPackageIsTheDefaultObject()
    {
        var chunk = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/hub/beach" }, "beach", package: _project.Project.Ps2Package);
        AddObject("BASICCRATE", 3);
        Assert.Equal("BASICCRATE", ViewportViewModel.DefaultObjectFor(chunk)!.InvariantName);

        var nut = AddObject("_Beach_g_Actors_Beach_act_WUMPA_NUT", 0x12C, _project.Project.Ps2Package);
        Assert.Equal(nut.URI, ViewportViewModel.DefaultObjectFor(chunk)!.URI);

        var wumpa = AddObject("REDWUMPA", 1);
        Assert.Equal(wumpa.URI, ViewportViewModel.DefaultObjectFor(chunk)!.URI);

        // The Xbox version's objects aren't the PS2 chunk's
        AddObject("WUMPA", 1, _project.Project.GlobalPackageXbox);
        Assert.Equal(wumpa.URI, ViewportViewModel.DefaultObjectFor(chunk)!.URI);
    }

    private LevelChunk AddChunk(string path, Package? package = null)
    {
        return _project.Add(new LevelChunk { AdditionalPath = path }, path.Split('/')[^1], package: package);
    }

    // A sky picked for a chunk that had none has no objects in the scene yet whose link could follow it: the viewport makes the sky's
    // objects for the chunk's Skydome link itself, the scene stayed without a sky until it was opened again
    [AvaloniaFact]
    public void TheViewportFollowsTheChunksSkyLink()
    {
        var chunk = AddChunk("levels/earth/hub/beach");
        var viewport = OpenViewport(chunk, out var document);

        Assert.True(viewport.IsChunkSky(document.PropertyGraph.Find("Root.Skydome")!));
        Assert.False(viewport.IsChunkSky(document.PropertyGraph.Find("Root.ChunkResources")!));
        viewport.Close();
    }

    private T AddInstance<T>(LevelChunk chunk, string name, Func<IAsset, AbstractAssetData> data, int layout = 0) where T : SerializableInstance, new()
    {
        var instance = _project.Add(new T { Chunk = chunk.AdditionalPath!, AdditionalPath = chunk.AdditionalPath, LayoutID = layout }, name);
        instance.SetData(data(instance));
        chunk.ChunkResources.Add(instance.URI);
        return instance;
    }

    private static ViewportViewModel OpenViewport(LevelChunk chunk, out DocumentViewModel document)
    {
        document = new DocumentViewModel(chunk);
        document.Initialize();
        var viewport = new ViewportViewModel();
        viewport.Init(document);
        return viewport;
    }

    // A chunk with its folder, the way the project tree makes one: new instances get files in it
    private LevelChunk CreateChunkWithFolder(string name)
    {
        var package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(package.Name, "levels"));
        var crash = _project.Add(new GameObject(), "Crash", 0x0);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);
        var folder = _project.GetFolder(package, "levels");
        var chunk = (LevelChunk)TT_Lab.Assets.Factory.AssetFactory.CreateAsset(typeof(LevelChunk), folder, name, string.Empty,
            TT_Lab.ServiceProviders.TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(), asset => TT_Lab.Assets.Factory.AssetDataFactory.CreateChunkData(folder, asset))!;
        return chunk;
    }

    // An AI path joins two AI positions, made in the layout of the first
    [AvaloniaFact]
    public void AiPathsJoinTwoAiPositions()
    {
        var chunk = CreateChunkWithFolder("beach");
        var from = AddInstance<AiPosition>(chunk, "Start", asset => new AiPositionData(asset) { Coords = new Vector3(1, 0, 0), Radius = 2 }, 6);
        var to = AddInstance<AiPosition>(chunk, "End", asset => new AiPositionData(asset) { Coords = new Vector3(5, 0, 0) }, 6);
        var viewport = OpenViewport(chunk, out var document);

        var path = viewport.CreateAiPath(from, to);

        Assert.IsType<AiPath>(path);
        Assert.Equal(6, path.LayoutID);
        Assert.Contains(path.URI, chunk.ChunkResources);
        var data = path.GetData<AiPathData>();
        Assert.Equal(from.URI, data.PathBegin);
        Assert.Equal(to.URI, data.PathEnd);
        Assert.True(document.CanUndo);
    }

    // Selected AI paths turn around in one step
    [AvaloniaFact]
    public void AiPathsReverseInOneStep()
    {
        var chunk = CreateChunkWithFolder("beach");
        var from = AddInstance<AiPosition>(chunk, "Start", asset => new AiPositionData(asset) { Coords = new Vector3(1, 0, 0) }, 6);
        var to = AddInstance<AiPosition>(chunk, "End", asset => new AiPositionData(asset) { Coords = new Vector3(5, 0, 0) }, 6);
        var viewport = OpenViewport(chunk, out var document);
        var path = viewport.CreateAiPath(from, to);
        var node = Enumerable.Range(0, chunk.ChunkResources.Count).Select(i => document.PropertyGraph.Find($"Root.ChunkResources[{i}]"))
            .First(resource => resource?.Find("[data]")?.GetValue() == path)!;

        viewport.ReverseAiPaths([new ViewportObject(null!, "AI_PATH", node)]);

        var data = path.GetData<AiPathData>();
        Assert.Equal((to.URI, from.URI), (data.PathBegin, data.PathEnd));
        document.Undo();
        Assert.Equal((from.URI, to.URI), (data.PathBegin, data.PathEnd));
    }

    // Links and emitters are elements of the chunk's resources, put at the end of their lists at the cursor
    [AvaloniaFact]
    public void LinksAndEmittersGoAtTheEndOfTheirLists()
    {
        var chunk = AddChunk("levels/earth/hub/beach");
        var cave = AddChunk("levels/earth/hub/cave");
        var links = AddInstance<ChunkLinks>(chunk, "Links", asset => new ChunkLinksData(asset) { Links = [] });
        var particles = AddInstance<Particles>(chunk, "Particles", asset => new ParticleData(asset) { ParticleSystems = [new ParticleSystem { Name = "Fire" }], ParticleInstances = [] });
        var viewport = OpenViewport(chunk, out var document);

        var link = viewport.CreateChunkLink();
        var emitter = viewport.CreateParticleEmitter();

        Assert.NotNull(link);
        Assert.Equal("Root.ChunkResources[0][data].AssetData.Links[0]", link!.Path);
        var placed = Assert.Single(((IAsset)links).GetData<ChunkLinksData>().Links);
        Assert.Equal(cave.URI, placed.Path);
        // Its load wall stands on the cursor, where the link is
        var at = placed.ChunkMatrix.Column4;
        Assert.Equal(ChunkLink.WallAt(new GlmSharp.vec3(at.X, at.Y, at.Z)).ToGlm(), placed.LoadingWall.ToGlm());
        Assert.NotNull(emitter);
        var madeEmitter = Assert.Single(((IAsset)particles).GetData<ParticleData>().ParticleInstances);
        Assert.Equal("Fire", madeEmitter.Name);
        Assert.True(document.CanUndo);
        document.Undo();
        Assert.Empty(((IAsset)particles).GetData<ParticleData>().ParticleInstances);

        // A chunk without the resource makes none
        var bare = AddChunk("levels/earth/hub/bare");
        Assert.Null(OpenViewport(bare, out _).CreateChunkLink());
    }

    // Lights are elements of the scenery's lists, placing one in a scenery without lighting turns it on in the same step
    [AvaloniaFact]
    public void LightsGoIntoTheScenerysListsAndTurnItsLightingOn()
    {
        var chunk = AddChunk("levels/earth/hub/beach");
        var scenery = AddInstance<Scenery>(chunk, "Scenery", asset => new SceneryData(asset));
        var viewport = OpenViewport(chunk, out var document);

        var light = viewport.CreateLight(Twinsanity.TwinsanityInterchange.Common.Lights.LightType.Spot);

        Assert.Equal("Root.ChunkResources[0][data].AssetData.SpotLights[0]", light!.Path);
        var data = ((IAsset)scenery).GetData<SceneryData>();
        Assert.True(data.HasLighting);
        Assert.Single(data.SpotLights);
        document.Undo();
        Assert.False(data.HasLighting);
        Assert.Empty(data.SpotLights);
    }
}
