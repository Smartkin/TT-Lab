using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using GlmSharp;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using Splat;
using TT_Lab.Project;
using TT_Lab.Project.Prefabs;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using InstancePath = TT_Lab.Assets.Instance.Path;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// Instances and parts of a chunk's resources get saved as prefabs of the project to place in any chunk, without their links to the
// chunk's instances
[Collection(ProjectCollection.Name)]
public sealed class PrefabLibraryTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly Package _package;
    private readonly GameObject _crash;
    private readonly BehaviourGraph _script;
    private readonly PrefabLibrary _library;

    public PrefabLibraryTests()
    {
        _package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(_package.Name, "levels"), Path.Combine(_package.Name, "Graphics"));
        _crash = _project.Add(new GameObject(), "Crash", 0x0);
        _crash.SetData(new GameObjectData(_crash) { Name = "Crash" });
        _script = _project.Add(new BehaviourGraph(), "Script", 0x10);
        _script.SetData(new BehaviourGraphData(_script));
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);
        _library = new PrefabLibrary(_project.Project);
    }

    public void Dispose() => _project.Dispose();

    private LevelChunk CreateChunk(string name)
    {
        var folder = _project.GetFolder(_package, "levels");
        var chunk = (LevelChunk)AssetFactory.CreateAsset(typeof(LevelChunk), folder, name, string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(), asset => AssetDataFactory.CreateChunkData(folder, asset))!;
        TwinIdGeneratorServiceProvider.RegisterGeneratorServiceForChunk(chunk);
        return chunk;
    }

    private static T AddInstance<T>(LevelChunk chunk, string name, Func<IAsset, AbstractAssetData> data) where T : SerializableInstance
    {
        var instance = (T)AssetFactory.CreateAsset(typeof(T), chunk.GetChunkFolder(), name, string.Empty,
            TwinIdGeneratorServiceProvider.GetGeneratorForChunk(typeof(T), chunk.AdditionalPath!, Enums.Layouts.LAYER_1), asset =>
            {
                var instanceAsset = (SerializableInstance)asset;
                instanceAsset.Chunk = chunk.AdditionalPath!;
                instanceAsset.AdditionalPath = chunk.AdditionalPath;
                instanceAsset.RegenerateLinks();
                asset.SetData(data(asset));
                return AssetCreationStatus.Success;
            }, Enums.Layouts.LAYER_1)!;
        chunk.ChunkResources.Add(instance.URI);
        return instance;
    }

    // A crate linked to another instance, a position and a path of its chunk
    private ObjectInstance AddCrate(LevelChunk chunk)
    {
        var position = AddInstance<Position>(chunk, "Spot", asset => new PositionData(asset));
        var path = AddInstance<InstancePath>(chunk, "Route", asset => new PathData(asset));
        var other = AddInstance<ObjectInstance>(chunk, "Other", asset => new ObjectInstanceData(asset) { ObjectId = _crash.URI });
        return AddInstance<ObjectInstance>(chunk, "Crate", asset => new ObjectInstanceData(asset)
        {
            Position = new Vector3(1, 2, 3),
            Rotation = new Vector3(0, 90, 0),
            ObjectId = _crash.URI,
            SpawnScript = _script.URI,
            RefListIndex = 7,
            StateFlags = Enums.InstanceState.Visible | Enums.InstanceState.CollisionActive,
            TaggedProperties = [new(1), new(2)],
            FloatProperties = [0.5f],
            IntProperties = [9],
            Instances = [other.URI],
            Positions = [position.URI],
            Paths = [path.URI],
        });
    }

    private static ChunkLinksData LinksOf(LevelChunk chunk) => ((IAsset)AssetManager.Get().GetAsset(chunk.ChunkResources[1])).GetData<ChunkLinksData>();

    private static DocumentViewModel Open(LevelChunk chunk)
    {
        var document = new DocumentViewModel(chunk);
        document.Initialize();
        return document;
    }

    [Fact]
    public void AnInstanceKeepsEverythingButItsLinksToTheChunksInstances()
    {
        var crate = AddCrate(CreateChunk("first"));

        var prefab = _library.Capture(new PrefabSource(crate, null, null, crate.Alias), "Crate");

        Assert.Equal(PrefabKind.Instance, prefab.Kind);
        Assert.Equal("PS2", prefab.Platform);
        Assert.Equal(0, prefab.LayoutID);
        Assert.Equal(typeof(ObjectInstance).FullName, prefab.AssetType);
        Assert.Equal(typeof(ObjectInstanceData).FullName, prefab.DataType);
        Assert.Equal("Object instance of the main layout, PS2, from Crate", PrefabLibrary.Describe(prefab));
        var data = prefab.Data;
        Assert.Empty(data["Instances"]!);
        Assert.Empty(data["Positions"]!);
        Assert.Empty(data["Paths"]!);
        Assert.Equal(_crash.URI.ToString(), data["ObjectId"]!["_uri"]!.ToString());
        Assert.Equal(_script.URI.ToString(), data["SpawnScript"]!["_uri"]!.ToString());
        Assert.Equal(7, data["RefListIndex"]!.Value<int>());
        Assert.Equal(90f, data["Rotation"]!["Y"]!.Value<float>());
        Assert.Equal([1u, 2u], data["TaggedProperties"]!.Select(value => value.Value<uint>()));
        Assert.Equal(0.5f, data["FloatProperties"]![0]!.Value<float>());
        // The instance itself keeps its links
        var own = ((IAsset)crate).GetData<ObjectInstanceData>();
        Assert.Single(own.Instances);
        Assert.Single(own.Positions);
        Assert.Single(own.Paths);
    }

    [Fact]
    public void PrefabsAreFilesInTheProjectsPrefabsFolder()
    {
        var crate = AddCrate(CreateChunk("first"));
        var prefab = _library.Capture(new PrefabSource(crate, null, null, crate.Alias), "Crate / TNT");

        _library.Save(prefab);

        var file = Path.Combine(_project.Project.ProjectPath, "prefabs", "Crate _ TNT.json");
        Assert.True(File.Exists(file));
        var loaded = Assert.Single(_library.Load());
        Assert.Equal("Crate / TNT", loaded.Name);
        Assert.Equal(PrefabKind.Instance, loaded.Kind);
        Assert.Equal(file, loaded.FilePath);
        Assert.Equal(prefab.Data.ToString(), loaded.Data.ToString());

        _library.Delete(loaded);

        Assert.False(File.Exists(file));
        Assert.Empty(_library.Load());
    }

    [Fact]
    public void AnInstancePrefabBecomesAnInstanceOfTheChunkItsPlacedIn()
    {
        var first = CreateChunk("first");
        var second = CreateChunk("second");
        var prefab = _library.Capture(new PrefabSource(AddCrate(first), null, null, "Crate"), "Crate");
        Assert.True(_library.CanPlace(prefab, second, null, out _));

        var placed = _library.PlaceInstance(prefab, second);

        Assert.IsType<ObjectInstance>(placed);
        Assert.Equal(second.AdditionalPath, placed.Chunk);
        Assert.Equal(0, placed.LayoutID);
        Assert.StartsWith("Crate ", placed.Alias);
        Assert.True(_project.AssetManager.DoesAssetExist(placed.URI));
        var others = _project.AssetManager.GetAllAssetsOf<ObjectInstance>().Where(instance => instance != placed && instance.Chunk == second.AdditionalPath);
        Assert.DoesNotContain(placed.ID, others.Select(instance => instance.ID));
        var data = ((IAsset)placed).GetData<ObjectInstanceData>();
        Assert.Equal(_crash.URI, data.ObjectId);
        Assert.Equal(_script.URI, data.SpawnScript);
        Assert.Equal(7, data.RefListIndex);
        Assert.Equal(Enums.InstanceState.Visible | Enums.InstanceState.CollisionActive, data.StateFlags);
        Assert.Equal([0.5f], data.FloatProperties);
        Assert.Empty(data.Instances);
        Assert.Empty(data.Positions);
        Assert.Empty(data.Paths);
    }

    [Fact]
    public void PrefabsOfTheOtherVersionOfTheGameStayOut()
    {
        var chunk = CreateChunk("first");
        var prefab = _library.Capture(new PrefabSource(AddCrate(chunk), null, null, "Crate"), "Crate");
        prefab.Platform = "Xbox";

        Assert.False(_library.CanPlace(prefab, chunk, null, out var reason));
        Assert.Contains("Xbox", reason);
    }

    // Scenery, dynamic scenery, collision, chunk links and particles exist once in a chunk, so a prefab of them makes no sense, their
    // parts can be one. A part of an instance or of another part is saved with what it belongs to
    [AvaloniaFact]
    public void TheResourcesAChunkHasOnceCantBePrefabsButTheirPartsCan()
    {
        var chunk = CreateChunk("first");
        var link = new ChunkLink();
        link.Hulls.Add(new ChunkLinkHull());
        LinksOf(chunk).Links.Add(link);
        AddCrate(chunk);
        var document = Open(chunk);
        var scenery = document.PropertyGraph.Find("Root.ChunkResources[0]")!;
        var links = document.PropertyGraph.Find("Root.ChunkResources[1]")!;
        var crate = document.PropertyGraph.Find("Root.ChunkResources[7]")!;

        Assert.False(PrefabLibrary.TryGetSource(scenery, null, out _, out var reason));
        Assert.Equal("Scenery exists once in a chunk, its parts can be prefabs", reason);
        Assert.False(PrefabLibrary.TryGetSource(links, null, out _, out reason));
        Assert.Equal("Chunk links exists once in a chunk, its parts can be prefabs", reason);

        Assert.True(PrefabLibrary.TryGetSource(links, links.Find("[data].AssetData.Links[0]"), out var source, out _));
        Assert.Equal(PrefabKind.Element, source.Kind);
        Assert.Equal("AssetData.Links", source.ListPath);
        Assert.Equal(link.DocumentName, source.DefaultName);

        Assert.False(PrefabLibrary.TryGetSource(links, links.Find("[data].AssetData.Links[0].Hulls[0]"), out _, out reason));
        Assert.Equal("It's a part of another part, save that one instead", reason);

        Assert.True(PrefabLibrary.TryGetSource(crate, null, out source, out _));
        Assert.Equal(PrefabKind.Instance, source.Kind);
        Assert.Equal("Crate", source.DefaultName);
        Assert.False(PrefabLibrary.TryGetSource(crate, crate.Find("[data].AssetData.TaggedProperties[0]"), out _, out reason));
        Assert.Equal("It's a part of object instance Crate, save that instead", reason);
    }

    [AvaloniaFact]
    public void AChunkLinkPrefabGoesIntoAnotherChunksLinks()
    {
        var first = CreateChunk("first");
        var second = CreateChunk("second");
        var third = CreateChunk("third");
        var link = new ChunkLink { Path = third.URI, Visibility = ChunkLinkVisibility.Always, KeepLoaded = true };
        link.Hulls.Add(new ChunkLinkHull());
        LinksOf(first).Links.Add(link);
        var document = Open(first);
        var links = document.PropertyGraph.Find("Root.ChunkResources[1]")!;
        Assert.True(PrefabLibrary.TryGetSource(links, links.Find("[data].AssetData.Links[0]"), out var source, out _));

        var prefab = _library.Capture(source, "Link to third");

        Assert.Equal(PrefabKind.Element, prefab.Kind);
        Assert.Equal(typeof(ChunkLinks).FullName, prefab.AssetType);
        Assert.Equal(typeof(ChunkLink).FullName, prefab.ElementType);
        Assert.Equal("AssetData.Links", prefab.ListPath);
        Assert.Equal("Chunk link of the chunk links, PS2, from Chunk Link", PrefabLibrary.Describe(prefab));
        // The chunk it links to isn't an instance of the chunk, it stays
        Assert.Equal(third.URI.ToString(), prefab.Data["Path"]!["_uri"]!.ToString());

        var target = Open(second);
        // Whether the chunk has the resource is read off its document
        Assert.False(_library.CanPlace(prefab, second, null, out _));
        Assert.True(_library.CanPlace(prefab, second, target, out _));

        var element = _library.PlaceElement(prefab, target);

        Assert.Equal("Root.ChunkResources[1][data].AssetData.Links[0]", element.Path);
        var placed = Assert.Single(LinksOf(second).Links);
        Assert.Equal(third.URI, placed.Path);
        Assert.Equal(ChunkLinkVisibility.Always, placed.Visibility);
        Assert.True(placed.KeepLoaded);
        Assert.Single(placed.Hulls);
        Assert.True(target.CanUndo);
        Assert.True(target.IsDirty);
    }

    [AvaloniaFact]
    public void AnElementPrefabNeedsTheResourceItsAPartOf()
    {
        var chunk = CreateChunk("first");
        var document = Open(chunk);
        var prefab = new Prefab
        {
            Name = "System", Kind = PrefabKind.Element, Platform = "PS2", Package = _package.URI, AssetType = typeof(DefaultParticles).FullName!,
            ListPath = "AssetData.ParticleSystems", ElementType = typeof(ChunkLink).FullName!, Data = new JObject()
        };

        Assert.False(_library.CanPlace(prefab, chunk, document, out var reason));
        Assert.Equal("The chunk has no default particles", reason);
        Assert.Throws<InvalidOperationException>(() => _library.PlaceElement(prefab, document));
    }

    // Several instances saved together keep where they stand to the first, and come back around the cursor the same way
    [AvaloniaFact]
    public void GroupsOfInstancesKeepTheirPlacesToEachOtherAndTheirPicture()
    {
        var chunk = CreateChunk("first");
        var crate = AddCrate(chunk);
        var spot = AddInstance<Position>(chunk, "Spot 2", asset => new PositionData(asset) { Coords = new Vector3(4, 2, 3) });
        using var picture = new WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Unpremul);

        var prefab = _library.CaptureGroup([(crate, new vec3(1, 2, 3)), (spot, new vec3(4, 2, 3))], "Crate and spot");
        _library.Save(prefab, picture);

        Assert.Equal(PrefabKind.Group, prefab.Kind);
        Assert.Equal("Group of 2 instances, PS2", PrefabLibrary.Describe(prefab));
        Assert.NotNull(prefab.Items);
        Assert.Equal([0.0f, 0.0f, 0.0f], prefab.Items![0].Offset);
        Assert.Equal([3.0f, 0.0f, 0.0f], prefab.Items[1].Offset);
        Assert.Equal(typeof(Position).FullName, prefab.Items[1].AssetType);
        Assert.Empty(prefab.Items[0].Data["Instances"]!);
        Assert.True(File.Exists(prefab.PreviewPath));

        var loaded = Assert.Single(_library.Load(), item => item.Name == "Crate and spot");
        Assert.Equal(prefab.PreviewPath, loaded.PreviewPath);
        var other = CreateChunk("second");
        Assert.True(_library.CanPlace(loaded, other, null, out _));

        var placed = _library.PlaceGroup(loaded, other);

        Assert.Equal(2, placed.Count);
        Assert.IsType<ObjectInstance>(placed[0].Instance);
        Assert.Equal(new vec3(3, 0, 0), placed[1].Offset);
        Assert.Equal(other.AdditionalPath, placed[1].Instance.Chunk);
        Assert.Equal(_crash.URI, ((IAsset)placed[0].Instance).GetData<ObjectInstanceData>().ObjectId);

        _library.Delete(loaded);
        Assert.False(File.Exists(loaded.FilePath ?? string.Empty));
        Assert.Null(loaded.PreviewPath);
        Assert.DoesNotContain(_library.Load(), item => item.Name == "Crate and spot");
    }

    private static ProjectManager Manager => Locator.Current.GetService<ProjectManager>()!;

    private static Folder? FolderHolding(Folder folder, LabURI uri)
    {
        if (folder.Children.Contains(uri))
        {
            return folder;
        }

        return folder.Children.Select(AssetManager.Get().GetAsset).OfType<Folder>().Select(child => FolderHolding(child, uri)).FirstOrDefault(found => found != null);
    }

    // Placed instances went under the chunk's folder in the project tree while their files go in their layout's folder, so the tree
    // following the file system took them out of the project once they were saved (or before, when other files changed first), and
    // closing the chunk's tab crashed looking them up
    [AvaloniaFact]
    public void PlacedInstancesStayInTheProjectThroughSavingAndClosing()
    {
        var chunk = CreateChunk("first");
        var crate = AddCrate(chunk);
        var spot = AddInstance<Position>(chunk, "Spot 2", asset => new PositionData(asset) { Coords = new Vector3(4, 2, 3) });
        var prefab = _library.CaptureGroup([(crate, new vec3(1, 2, 3)), (spot, new vec3(4, 2, 3))], "Crate and spot");
        var other = CreateChunk("second");
        var document = new DocumentViewModel(other);
        document.Initialize();
        var viewport = new ViewportViewModel();
        viewport.Init(document);

        var placed = _library.PlaceGroup(prefab, other);
        viewport.PlaceInstances(placed.Select(item => ((IAsset)item.Instance, (vec3?)null)).ToList(), "Placed Crate and spot");

        // In the folders their files go in, on disk as well
        foreach (var (instance, _) in placed)
        {
            var folder = FolderHolding(other.GetChunkFolder(), instance.URI);
            Assert.NotNull(folder);
            Assert.EndsWith($"/second/{instance.Type.Name}/Layout_{instance.LayoutID}", folder!.GetPath());
            Assert.True(Directory.Exists(instance.FullPath));
        }

        // Other files changing sync the tree before the chunk gets saved
        Manager.SyncProjectTree();
        Assert.All(placed, item => Assert.Same(item.Instance, _project.AssetManager.GetAsset(item.Instance.URI)));

        document.Save();
        Manager.SyncProjectTree();

        foreach (var (instance, _) in placed)
        {
            Assert.Same(instance, _project.AssetManager.GetAsset(instance.URI));
            Assert.True(File.Exists(Path.Combine(instance.FullPath, $"{instance.Name}.json")));
            Assert.Single(FolderHolding(other.GetChunkFolder(), instance.URI)!.Children, uri => uri == instance.URI);
        }

        // Closing the tab of a saved chunk reads it back and releases its resources
        other.Deserialize(File.ReadAllText(Path.Combine(other.FullPath, $"{other.Name}.json")));
        other.Dispose();
    }
}
