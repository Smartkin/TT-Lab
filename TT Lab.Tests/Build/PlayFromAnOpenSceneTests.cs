using Avalonia.Headless.XUnit;
using Splat;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Collision;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Pcsx2;
using TT_Lab.ViewModels;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Build;

// Play starts from the chunk's open scene, whose document loads every resource of the chunk: loaded data counted as changed, and Play built
// the start chunk again every time (12 s for the beach). What open editors show counts as its files while it has no edit they don't, what a
// scene reads besides its document's links too: the default chunk's particles its emitters play, the font its loading overlay draws with
[Collection(ProjectCollection.Name)]
public sealed class PlayFromAnOpenSceneTests : IDisposable
{
    private const string BeachPath = "levels/earth/beach";
    // A build that writes a file again gives it the time it wrote it
    private static readonly DateTime Marked = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private readonly TestProject _project = new();
    private readonly LevelChunk _beach;
    private readonly DefaultParticles _defaults;
    private readonly Material _page;
    private readonly Font _font;
    private readonly EditorsViewModel _editors;

    public PlayFromAnOpenSceneTests()
    {
        var global = _project.Project.GlobalPackagePS2;
        var package = _project.Project.Ps2Package;
        _project.BuildProjectTree(Path.Combine(global.Name, "Startup"), Path.Combine(package.Name, "levels", "earth", "beach"));
        var startup = _project.GetFolder(global, "Startup");
        var defaultChunk = _project.Add(new LevelChunk { AdditionalPath = "startup/default" }, "default");
        startup.AddChild(defaultChunk);
        defaultChunk.ChunkResources.Add(AddParticles(global, "startup/default").URI);
        var floor = _project.Add(new CollisionSurface { Chunk = "default" }, "Floor");
        floor.SetData(new CollisionSurfaceData(floor));
        // The systems every chunk's emitters can play, drawn with the pages' pictures
        var assets = new TestAssets(_project);
        var pageTexture = assets.AddTexture("Page", 0xFF305070);
        _page = assets.AddMaterial("Page", pageTexture.URI);
        _defaults = _project.Add(new DefaultParticles { Chunk = "startup/default" }, "Global Particles");
        _defaults.SetData(new DefaultParticleData(_defaults) { ParticleSystems = [new ParticleSystem { Name = "SPARK" }], TextureIDs = [pageTexture.URI], MaterialIDs = [_page.URI] });
        // The startup's font, the loading overlay's
        var fontTexture = assets.AddTexture("Font Page", 0xFFFFFFFF);
        var fontPage = _project.Add(new PTC { GlobalPath = "Startup" }, "Font_page_0");
        fontPage.SetData(new PTCData(fontPage) { TextureID = fontTexture.URI, MaterialID = assets.AddMaterial("Font Page", fontTexture.URI).URI });
        _font = _project.Add(new Font { GlobalPath = "Startup" }, "Font");
        _font.SetData(new FontData(_font) { FontPages = [fontPage.URI] });
        startup.AddChild(_font);

        // A scenery of one triangle of collision, links and particles of an emitter playing a default system
        _beach = _project.Add(new LevelChunk { AdditionalPath = BeachPath }, "beach", package: package);
        _project.GetFolder(package, "levels", "earth", "beach").AddChild(_beach);
        var scenery = _project.Add(new Scenery { Chunk = BeachPath }, "Scenery", package: package);
        var collision = new Collision { Package = package.URI, Chunk = BeachPath, InvariantName = "Collision", Alias = "Collision", IsInternal = true, InternalOwner = scenery };
        var collisionData = new CollisionData(collision);
        collisionData.Vertexes.AddRange([new Vector4(0, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(0, 0, 1, 1)]);
        collisionData.Triangles.Add(new CollisionTriangle { Face = new IndexedFace(0, 1, 2), Surface = floor.URI });
        collision.SetData(collisionData);
        _project.AssetManager.AddAsset(collision);
        scenery.SetData(new SceneryData(scenery) { Collision = collision.URI });
        var links = _project.Add(new ChunkLinks { Chunk = BeachPath }, "Chunk Links", Constants.SCENERY_LINK_ITEM, package);
        links.SetData(new ChunkLinksData(links));
        var particles = AddParticles(package, BeachPath);
        ((IAsset)particles).GetData<ParticleData>().ParticleInstances.Add(new ParticleSystemInstance { Name = "SPARK" });
        _beach.ChunkResources.AddRange([scenery.URI, links.URI, particles.URI]);
        _project.Project.Serialize();

        // Opened the way TT Lab opens projects, closing editors lets go of their data
        var projectManager = Locator.Current.GetService<ProjectManager>()!;
        projectManager.WorkableProject = true;
        _editors = new EditorsViewModel(new ScenesEditorsViewModel(), new ResourcesEditorsViewModel(), projectManager);
        _editors.ShowToBuilds();
    }

    public void Dispose()
    {
        BuildCache.EditorsShowing = null;
        _project.Dispose();
    }

    private Particles AddParticles(Package package, string chunk)
    {
        var particles = _project.Add(new Particles { Chunk = chunk }, "Particles", Constants.LEVEL_PARTICLES_ITEM, package);
        particles.SetData(new ParticleData(particles));
        return particles;
    }

    private string FilesPath => Path.Combine(_project.Project.ProjectPath, "build", "archives");

    // Which of the beach's and the startup font's files Play's build of the beach, its links, the default chunk and the startup's files wrote
    // again
    private List<string> Played()
    {
        var watched = new Dictionary<string, string[]>
        {
            ["beach"] = [Path.Combine(FilesPath, "Levels", "earth", "beach.rm2"), Path.Combine(FilesPath, "Levels", "earth", "beach.sm2")],
            ["font"] = [Path.Combine(FilesPath, "Startup", ((IAsset)_font).ExportFileName)],
        };
        foreach (var file in watched.Values.SelectMany(files => files).Where(File.Exists))
        {
            File.SetLastWriteTimeUtc(file, Marked);
        }

        Pcsx2Service.BuildChunks(_project.Project, Pcsx2Service.ChunkWithLinks(_beach));
        return watched.Where(files => files.Value.Any(file => File.GetLastWriteTimeUtc(file) != Marked)).Select(files => files.Key).ToList();
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var waited = 0; waited < 10000 && !condition(); waited += 20)
        {
            await Task.Delay(20);
        }
    }

    // Kept with its scene open and unchanged, what it reads of the default chunk's particles and the font included, built again while the
    // scene has an edit, undone or not, until it's saved, once more then (what was built before counts as changed) and kept again
    [AvaloniaFact]
    public async Task ThePlayedChunkIsKeptWithItsSceneOpenUntilTheSceneEditsIt()
    {
        Assert.Equal(["beach", "font"], Played());
        Assert.Empty(Played());

        var scenes = _editors.ScenesEditorsViewModel;
        _editors.OpenEditor(_beach);
        var tab = scenes.Tabs.Single(tab => tab.EditableResource == _beach.URI);
        await WaitUntil(() => tab.IsLoaded && _font.IsLoaded);
        Assert.True(tab.IsLoaded);
        Assert.All(_beach.ChunkResources.Select(_project.AssetManager.GetAsset), resource => Assert.True(resource.IsLoaded, resource.Name));
        // Read besides the scene's links: its emitter's default system (ParticleSystemInstance.FollowPlayedSystem), the loading overlay's font
        Assert.True(_defaults.IsLoaded);
        Assert.True(_font.IsLoaded);
        Assert.DoesNotContain(_defaults.URI, tab.GetReferencedAssets());
        Assert.Empty(Played());
        // The pages' materials the scene's particles read the blending of (ParticleData.GetPageBlends)
        ((IAsset)_page).GetData<MaterialData>();
        Assert.Contains(_page.URI, _editors.Showing(new HashSet<LabURI> { _page.URI }));

        var document = tab.Document!;
        var particles = _beach.ChunkResources.Select(_project.AssetManager.GetAsset).OfType<Particles>().Single();
        var systems = document.PropertyGraph.Find($"Root.ChunkResources[{_beach.ChunkResources.IndexOf(particles.URI)}][data].AssetData.ParticleSystems")!;
        systems.InsertElement(0, new ParticleSystem { Name = "SMOKE" });
        Assert.True(document.IsDirty);
        Assert.Equal(["beach"], Played());

        // Undone, the scene is where it was saved, but another editor sharing the data can have saved the edit before
        document.Undo();
        Assert.False(document.IsDirty);
        Assert.Equal(["beach"], Played());
        tab.SaveTab();
        Assert.Equal(["beach"], Played());
        Assert.Empty(Played());

        scenes.TabsFactory.CloseDockable(tab);
        await WaitUntil(() => !scenes.Tabs.Contains(tab));
    }
}
