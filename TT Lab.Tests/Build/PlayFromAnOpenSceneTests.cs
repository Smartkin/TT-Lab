using Avalonia.Headless.XUnit;
using Splat;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Collision;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
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
// the start chunk again every time (12 s for the beach). What open editors show counts as its files while it has no edit they don't
[Collection(ProjectCollection.Name)]
public sealed class PlayFromAnOpenSceneTests : IDisposable
{
    private const string BeachPath = "levels/earth/beach";
    // A build that writes the chunk's files again gives them the time it wrote them
    private static readonly DateTime Marked = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private readonly TestProject _project = new();
    private readonly LevelChunk _beach;
    private readonly EditorsViewModel _editors;

    public PlayFromAnOpenSceneTests()
    {
        var global = _project.Project.GlobalPackagePS2;
        var package = _project.Project.Ps2Package;
        _project.BuildProjectTree(Path.Combine(global.Name, "Startup"), Path.Combine(package.Name, "levels", "earth", "beach"));
        var defaultChunk = _project.Add(new LevelChunk { AdditionalPath = "startup/default" }, "default");
        _project.GetFolder(global, "Startup").AddChild(defaultChunk);
        defaultChunk.ChunkResources.Add(AddParticles(global, "startup/default").URI);
        var floor = _project.Add(new CollisionSurface { Chunk = "default" }, "Floor");
        floor.SetData(new CollisionSurfaceData(floor));

        // A scenery of one triangle of collision, links and particles without any
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
        _beach.ChunkResources.AddRange([scenery.URI, links.URI, AddParticles(package, BeachPath).URI]);
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

    private string[] BeachFiles
    {
        get
        {
            var levels = Path.Combine(_project.Project.ProjectPath, "build", "archives", "Levels", "earth");
            return [Path.Combine(levels, "beach.rm2"), Path.Combine(levels, "beach.sm2")];
        }
    }

    // Whether Play's build of the beach, its links and the default chunk wrote the beach's files again
    private bool Played()
    {
        foreach (var file in BeachFiles.Where(File.Exists))
        {
            File.SetLastWriteTimeUtc(file, Marked);
        }

        Pcsx2Service.BuildChunks(_project.Project, Pcsx2Service.ChunkWithLinks(_beach));
        return BeachFiles.Any(file => File.GetLastWriteTimeUtc(file) != Marked);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var waited = 0; waited < 10000 && !condition(); waited += 20)
        {
            await Task.Delay(20);
        }
    }

    // Kept with its scene open and unchanged, built again while the scene has an edit, undone or not, until it's saved, once more then (what
    // was built before counts as changed) and kept again
    [AvaloniaFact]
    public async Task ThePlayedChunkIsKeptWithItsSceneOpenUntilTheSceneEditsIt()
    {
        Assert.True(Played());
        Assert.False(Played());

        var scenes = _editors.ScenesEditorsViewModel;
        _editors.OpenEditor(_beach);
        var tab = scenes.Tabs.Single(tab => tab.EditableResource == _beach.URI);
        await WaitUntil(() => tab.IsLoaded);
        Assert.True(tab.IsLoaded);
        Assert.All(_beach.ChunkResources.Select(_project.AssetManager.GetAsset), resource => Assert.True(resource.IsLoaded, resource.Name));
        Assert.False(Played());

        var document = tab.Document!;
        var particles = _beach.ChunkResources.Select(_project.AssetManager.GetAsset).OfType<Particles>().Single();
        var systems = document.PropertyGraph.Find($"Root.ChunkResources[{_beach.ChunkResources.IndexOf(particles.URI)}][data].AssetData.ParticleSystems")!;
        systems.InsertElement(0, new ParticleSystem { Name = "SPARK" });
        Assert.True(document.IsDirty);
        Assert.True(Played());

        // Undone, the scene is where it was saved, but another editor sharing the data can have saved the edit before
        document.Undo();
        Assert.False(document.IsDirty);
        Assert.True(Played());
        tab.SaveTab();
        Assert.True(Played());
        Assert.False(Played());

        scenes.TabsFactory.CloseDockable(tab);
        await WaitUntil(() => !scenes.Tabs.Contains(tab));
    }
}
