using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Newtonsoft.Json.Linq;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.Project.Build;
using TT_Lab.Project.Prefabs;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Assets;

// Assets, folders, chunks and packages move and get duplicated from the project tree. They go where Create Asset would make them, what
// links to what moves gets its new URI wherever it keeps it (assets' JSON, model files, scripts, prefabs, build profiles), what moves to
// another package takes along what that package can't link to and the packages linking to it get it as a dependency, copies made together
// link to each other. The game's own files of the startup folder, the default chunk, instances and root packages stay as they are
[Collection(ProjectCollection.Name)]
public sealed class AssetRelocationTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly Func<string, string, IReadOnlyList<string>, Task<int?>> _ask = AssetRelocation.Ask;
    private readonly Func<AssetRelocation.NameRequest, Task<AssetRelocation.NameAnswer?>> _askName = AssetRelocation.AskName;
    private readonly Func<IAsset, Task<Folder?>> _askFolder = AssetRelocation.AskFolder;
    // What the dialogues asked, and their answers by title: the first answer unless set, null cancels
    private readonly List<string> _asked = [];
    private readonly Dictionary<string, int?> _answers = new();
    private Func<AssetRelocation.NameRequest, AssetRelocation.NameAnswer?> _naming = request => new AssetRelocation.NameAnswer(request.Suggested, false);

    public AssetRelocationTests()
    {
        AssetRelocation.Ask = (title, message, answers) =>
        {
            _asked.Add($"{title}: {message}");
            return Task.FromResult(answers.Count == 0 ? null : _answers.GetValueOrDefault(title, 0));
        };
        AssetRelocation.AskName = request =>
        {
            _asked.Add($"{request.Title}: {request.Message} [{request.Suggested}]");
            return Task.FromResult(_naming(request));
        };
        Manager.WorkableProject = true;
    }

    public void Dispose()
    {
        AssetRelocation.Ask = _ask;
        AssetRelocation.AskName = _askName;
        AssetRelocation.AskFolder = _askFolder;
        _project.Dispose();
    }

    private string Said => string.Join("\n", _asked);

    private static ProjectManager Manager => Locator.Current.GetService<ProjectManager>()!;

    private Package Global => _project.Project.GlobalPackagePS2;

    private Package Ps2 => _project.Project.Ps2Package;

    private static Folder ProjectAssetsFolder => (Folder)Assert.Single(Manager.FullProjectTree, element => element.Alias == "assets").Asset;

    private static string JsonPath(IAsset asset) => Path.Combine(asset.FullPath, $"{asset.Name}.json");

    private static Folder AddFolder(Folder parent, string name)
    {
        return (Folder)AssetFactory.CreateAsset(typeof(Folder), parent, name, string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<Folder>(),
            asset => AssetDataFactory.CreateFolderData(parent, asset))!;
    }

    private static Package AddPackage(string name)
    {
        var project = (TT_Lab.Project.Project)Manager.OpenedProject!;
        return (Package)AssetFactory.CreateAsset(typeof(Package), ProjectAssetsFolder, name, project.BasePackage.ID.ToString(),
            TwinIdGeneratorServiceProvider.GetGenerator<Package>(), AssetDataFactory.CreatePackageData)!;
    }

    // Made the way Create Asset makes it, its links in what it references
    private static T Create<T>(Folder folder, string name, Func<IAsset, AbstractAssetData> data) where T : IAsset
    {
        var asset = (T)AssetFactory.CreateAsset(typeof(T), folder, name, string.Empty, TwinIdGeneratorServiceProvider.GetGenerator(typeof(T)), created =>
        {
            created.SetData(data(created));
            return AssetCreationStatus.Success;
        })!;
        asset.Serialize(SerializationFlags.FixReferences);
        return asset;
    }

    private static GameObject AddObject(Folder folder, string name, params LabURI[] behaviours)
    {
        return Create<GameObject>(folder, name, asset => new GameObjectData(asset) { Name = name, BehaviourSlots = [..behaviours] });
    }

    private static BehaviourGraph AddGraph(Folder folder, string name, string? script = null)
    {
        return Create<BehaviourGraph>(folder, name, asset =>
        {
            var data = new BehaviourGraphData(asset);
            if (script != null)
            {
                data.Graph = script;
            }

            return data;
        });
    }

    private static T Data<T>(IAsset asset) where T : AbstractAssetData => asset.GetData<T>();

    private static List<string> Rows(Folder folder) => folder.GetResourceTreeElement().GetInternalChildren()!.Select(row => row.Alias).ToList();

    private void AddCrashAndASurface()
    {
        var crash = _project.Add(new GameObject(), "Crash", 0x0);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);
    }

    private TextFile AddLevelSelect(string text)
    {
        var file = _project.Add(new TextFile(Global.URI, true, "startup_levelselect.txt", LevelSelect.FileName, text) { GlobalPath = "Startup" }, LevelSelect.FileName);
        file.Serialize(SerializationFlags.SaveData);
        return file;
    }

    private static LevelChunk CreateChunk(Folder folder, string name)
    {
        return (LevelChunk)AssetFactory.CreateAsset(typeof(LevelChunk), folder, name, string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(),
            asset => AssetDataFactory.CreateChunkData(folder, asset))!;
    }

    private static ChunkLinksData LinksOf(LevelChunk chunk) => Data<ChunkLinksData>(AssetManager.Get().GetAsset(chunk.ChunkResources[1]));

    [Fact]
    public async Task AMovedAssetTakesItsFilesAlongAndWhatLinksToItFollows()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "GameObject"), Path.Combine(Global.Name, "Crates"));
        var objects = _project.GetFolder(Global, "GameObject");
        var crates = _project.GetFolder(Global, "Crates");
        var crate = AddObject(objects, "Crate");
        var spawner = AddObject(objects, "Spawner");
        Data<GameObjectData>(spawner).ObjectSlots = [crate.URI];
        spawner.Serialize(SerializationFlags.SaveData | SerializationFlags.FixReferences);
        var oldFiles = new[] { JsonPath(crate), crate.FullDataPath };
        var oldUri = crate.URI;

        Assert.True(await AssetRelocation.MoveAsync(crate, crates), Said);

        Assert.Equal(new LabURI($"res://{Global.Name}/Crates/Crate"), crate.URI);
        Assert.Equal("Crates", crate.FolderInPackage);
        Assert.All(oldFiles, file => Assert.False(File.Exists(file)));
        Assert.True(File.Exists(JsonPath(crate)));
        Assert.True(File.Exists(crate.FullDataPath));
        Assert.False(_project.AssetManager.DoesAssetExist(oldUri));
        Assert.Same(crate, _project.AssetManager.GetAsset(crate.URI));
        // What links to it has its new URI, in its data file and what it references
        Assert.Equal([crate.URI], Data<GameObjectData>(spawner).ObjectSlots);
        Assert.Contains(crate.URI, spawner.References);
        Assert.Contains(crate.URI.ToString(), File.ReadAllText(JsonPath(spawner)));
        // The tree shows it where it went
        Assert.Equal(["Crate"], Rows(crates));
        Assert.Equal(["Spawner"], Rows(objects));
        Assert.Contains(crate.URI, crates.Children);
        Assert.DoesNotContain(oldUri, objects.Children);
        // Moved back it's in the folder of its type again
        Assert.True(await AssetRelocation.MoveAsync(crate, objects), Said);
        Assert.Null(crate.FolderInPackage);
        Assert.Equal(oldUri, crate.URI);
        Assert.Equal([crate.URI], Data<GameObjectData>(spawner).ObjectSlots);
    }

    [Fact]
    public async Task AMovedFolderTakesEverythingInItAlong()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "Characters"), Path.Combine(Global.Name, "Mods"));
        var characters = _project.GetFolder(Global, "Characters");
        var mods = _project.GetFolder(Global, "Mods");
        var scripts = AddFolder(characters, "Scripts");
        var graph = AddGraph(scripts, "ReimuBehaviour");
        var reimu = AddObject(characters, "Reimu", graph.URI);

        Assert.True(await AssetRelocation.MoveAsync(characters, mods), Said);

        Assert.Equal(new LabURI($"res://{Global.Name}/Mods/Characters/Reimu"), reimu.URI);
        Assert.Equal(new LabURI($"res://{Global.Name}/Mods/Characters/Scripts/ReimuBehaviour"), graph.URI);
        Assert.Equal("Mods/Characters/Scripts", graph.FolderInPackage);
        Assert.Equal([graph.URI], Data<GameObjectData>(reimu).BehaviourSlots);
        Assert.True(File.Exists(graph.FullDataPath));
        Assert.False(Directory.Exists(Path.Combine(_project.AssetsPath, Global.Name, "Characters")));
        // The folders are where their directories are, with their rows
        Assert.Equal($"/assets/{Global.Name}/Mods/Characters/Scripts", scripts.GetPath());
        Assert.Equal(new LabURI($"res://__GLOBAL_FOLDER__/assets/{Global.Name}/Mods/Characters"), characters.URI);
        Assert.Same(characters, _project.AssetManager.GetAsset(characters.URI));
        Assert.Equal(mods.URI, characters.Parent);
        Assert.Equal(["Characters"], Rows(mods));
        Assert.Equal(["Scripts", "Reimu"], Rows(characters));
        Assert.Contains(graph.URI, scripts.Children);
        // A sync of the tree with the disk finds nothing to change
        Manager.SyncProjectTree();
        Assert.Equal(["Characters"], Rows(mods));
        Assert.Equal(["Scripts", "Reimu"], Rows(characters));
    }

    // A folder is its directory: renaming it in the tree renamed only the folder in memory, the next look at the disk took it out and
    // read the directory of the old name back, its assets as other objects
    [Fact]
    public async Task ARenamedFolderTakesItsDirectoryAndWhatsInItAlong()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "Characters"), Path.Combine(Global.Name, "GameObject"));
        var characters = _project.GetFolder(Global, "Characters");
        var crate = AddObject(characters, "Crate");
        var user = AddObject(_project.GetFolder(Global, "GameObject"), "User");
        Data<GameObjectData>(user).ObjectSlots = [crate.URI];
        user.Serialize(SerializationFlags.SaveData | SerializationFlags.FixReferences);
        var row = characters.GetResourceTreeElement();

        row.NewAlias = "Heroes";
        row.SaveRenaming(new Avalonia.Input.KeyEventArgs { Key = Avalonia.Input.Key.Enter });
        for (var waited = 0; waited < 100 && characters.Alias != "Heroes"; waited++)
        {
            await Task.Delay(20);
        }

        Assert.Equal("Heroes", characters.Alias);
        Assert.True(Directory.Exists(Path.Combine(_project.AssetsPath, Global.Name, "Heroes")));
        Assert.False(Directory.Exists(Path.Combine(_project.AssetsPath, Global.Name, "Characters")));
        Assert.Equal(new LabURI($"res://{Global.Name}/Heroes/Crate"), crate.URI);
        Assert.Equal([crate.URI], Data<GameObjectData>(user).ObjectSlots);
        Assert.Contains(_asked, asked => asked.StartsWith("Rename: Renaming Characters to Heroes"));
        Manager.SyncProjectTree();
        Assert.Same(characters, _project.AssetManager.GetAsset(characters.URI));
        Assert.Same(crate, _project.AssetManager.GetAsset(crate.URI));
        Assert.Contains("Heroes", Rows(Global.GetPackageFolder()));

        // Declined, the name in the tree goes back
        _answers["Rename"] = null;
        row.NewAlias = "Villains";
        row.SaveRenaming(new Avalonia.Input.KeyEventArgs { Key = Avalonia.Input.Key.Enter });
        for (var waited = 0; waited < 100 && row.NewAlias != "Heroes"; waited++)
        {
            await Task.Delay(20);
        }

        Assert.Equal("Heroes", row.NewAlias);
        Assert.Equal("Heroes", characters.Alias);
    }

    [Fact]
    public async Task ScriptsAndModelFilesFollowWhatMoves()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "BehaviourGraph"), Path.Combine(Global.Name, "Graphs"), Path.Combine(Global.Name, "Material"),
            Path.Combine(Global.Name, "Paints"), Path.Combine(Global.Name, "OGI"));
        var graphs = _project.GetFolder(Global, "BehaviourGraph");
        var other = AddGraph(graphs, "COM_OTHER");
        var oldUri = other.URI;
        var script = $$"""
                     behaviour COM_MAIN {
                         // state Commented("{{oldUri}}") stays
                         state One(COM_OTHER) {
                         }
                         state Two("{{oldUri}}") {
                         }
                     }
                     """;
        var main = AddGraph(graphs, "COM_MAIN", script);
        var paint = Create<Material>(_project.GetFolder(Global, "Material"), "Paint", asset => new MaterialData(asset));
        var model = Create<OGI>(_project.GetFolder(Global, "OGI"), "Model", asset => new OGIData(asset));
        var file = new TlmFile("ogi", "Model");
        file.Materials.Add(new JsonObject { ["uri"] = paint.URI.ToString(), ["name"] = "Paint" });
        file.Save(model.FullDataPath);

        Assert.True(await AssetRelocation.MoveAsync(other, _project.GetFolder(Global, "Graphs")), Said);
        Assert.True(await AssetRelocation.MoveAsync(paint, _project.GetFolder(Global, "Paints")), Said);

        // The name finds it where it is, the URI string is the new one, comments stay
        var text = File.ReadAllText(main.FullDataPath);
        Assert.Contains("state One(COM_OTHER)", text);
        Assert.Contains($"state Two(\"{other.URI}\")", text);
        Assert.Contains($"// state Commented(\"{oldUri}\") stays", text);
        Assert.Same(other, BehaviourReferences.Find(main, "COM_OTHER"));
        Assert.Equal([paint.URI.ToString()], AssetLinks.UrisInModelFile(model.FullDataPath));
        Assert.Equal("Model", TlmFile.Load(model.FullDataPath).Json["asset"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task PrefabsAndBuildProfilesFollowWhatMoves()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "GameObject"), Path.Combine(Global.Name, "Crates"));
        var crate = AddObject(_project.GetFolder(Global, "GameObject"), "Crate");
        var library = new PrefabLibrary(_project.Project);
        var prefab = new Prefab
        {
            Name = "Crate", Kind = PrefabKind.Instance, Platform = "PS2", Package = Global.URI.ToString(), AssetType = typeof(ObjectInstance).FullName!,
            DataType = typeof(ObjectInstanceData).FullName!, LayoutID = 0, Data = new JObject { ["ObjectId"] = new JObject { ["_uri"] = crate.URI.ToString() } }
        };
        library.Save(prefab);
        var profiles = new BuildProfileLibrary(_project.Project);
        profiles.Save(new BuildProfile { Name = "Mine", ExcludedChunks = [crate.URI.ToString()] });

        Assert.True(await AssetRelocation.MoveAsync(crate, _project.GetFolder(Global, "Crates")), Said);

        var saved = Assert.Single(library.Load());
        Assert.Equal(crate.URI.ToString(), (string)saved.Data["ObjectId"]!["_uri"]!);
        Assert.Equal([crate.URI.ToString()], profiles.Load().Single(profile => profile.Name == "Mine").ExcludedChunks);
    }

    [AvaloniaFact]
    public async Task AMovedChunkTakesItsPathAndWhatsInItAlong()
    {
        _project.BuildProjectTree(Path.Combine(Ps2.Name, "levels", "earth"), Path.Combine(Ps2.Name, "levels", "ice"));
        var levelSelect = AddLevelSelect(string.Empty);
        AddCrashAndASurface();
        var beach = CreateChunk(_project.GetFolder(Ps2, "levels", "earth"), "beach");
        var lab = CreateChunk(_project.GetFolder(Ps2, "levels", "ice"), "lab");
        var links = LinksOf(lab);
        links.Links.Add(new ChunkLink { Path = beach.URI });
        _project.AssetManager.GetAsset(lab.ChunkResources[1]).Serialize(SerializationFlags.SaveData | SerializationFlags.FixReferences);
        var profiles = new BuildProfileLibrary(_project.Project);
        profiles.Save(new BuildProfile { Name = "Mine", ExcludedChunks = [beach.URI.ToString()] });
        var inChunk = AssetRelocation.AssetsUnder(Ps2.URI, "levels/earth/beach");
        Assert.Equal(5, inChunk.Count);

        Assert.True(await AssetRelocation.MoveAsync(beach.GetChunkFolder(), _project.GetFolder(Ps2, "levels", "ice")), Said);

        Assert.Equal("levels/ice/beach", beach.AdditionalPath);
        Assert.Equal(new LabURI($"res://{Ps2.Name}/levels/ice/beach/beach"), beach.URI);
        foreach (var asset in inChunk)
        {
            Assert.StartsWith($"res://{Ps2.Name}/levels/ice/beach", asset.URI.ToString());
            Assert.True(File.Exists(JsonPath(asset)), $"{asset.Name} has no file");
            if (asset is SerializableInstance instance)
            {
                Assert.Equal("levels/ice/beach", instance.Chunk);
            }
        }

        Assert.All(beach.ChunkResources, resource => Assert.True(_project.AssetManager.DoesAssetExist(resource)));
        Assert.False(Directory.Exists(Path.Combine(_project.AssetsPath, Ps2.Name, "levels", "earth", "beach")));
        // The other chunk links to it where it is, its scenery finds its materials
        Assert.Equal(beach.URI, Assert.Single(LinksOf(lab).Links).Path);
        var scenery = _project.AssetManager.GetAsset(beach.ChunkResources[0]);
        Assert.All(AssetLinks.UrisInModelFile(scenery.FullDataPath), uri => Assert.True(_project.AssetManager.DoesAssetExist(new LabURI(uri)), $"{uri} isn't in the project"));
        // The level select has it at its path, the build profile leaves it out where it is
        Assert.Equal([("beach", "levels\\ice\\beach"), ("lab", "levels\\ice\\lab")],
            LevelSelect.Entries(Data<TextFileData>(levelSelect).Text).OrderBy(entry => entry.Name).ToList());
        Assert.Equal([beach.URI.ToString()], profiles.Load().Single(profile => profile.Name == "Mine").ExcludedChunks);
        // Its folder is in the tree where it went
        var ice = _project.GetFolder(Ps2, "levels", "ice");
        Assert.Equal(["beach", "lab"], Rows(ice));
        Assert.Same(beach.GetChunkFolder(), _project.AssetManager.GetAsset(beach.GetChunkFolder().URI));
    }

    [Fact]
    public async Task WhatAnotherPackageCantLinkToComesAlong()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "GameObject"));
        var basePackage = _project.Project.BasePackage;
        var objects = AddFolder(basePackage.GetPackageFolder(), "GameObject");
        var graphs = AddFolder(basePackage.GetPackageFolder(), "BehaviourGraph");
        var graph = AddGraph(graphs, "ReimuBehaviour");
        var reimu = AddObject(objects, "Reimu", graph.URI);
        var oldGraph = graph.URI;

        // Declined, nothing moves
        _answers["Move along"] = null;
        Assert.False(await AssetRelocation.MoveAsync(reimu, _project.GetFolder(Global, "GameObject")));
        Assert.Equal(basePackage.URI, reimu.Package);
        Assert.Equal(oldGraph, graph.URI);
        Assert.Contains(_asked, asked => asked.StartsWith("Move along") && asked.Contains("ReimuBehaviour"));

        _answers.Remove("Move along");
        Assert.True(await AssetRelocation.MoveAsync(reimu, _project.GetFolder(Global, "GameObject")), Said);

        Assert.Equal(Global.URI, reimu.Package);
        Assert.Equal(Global.URI, graph.Package);
        Assert.Equal(new LabURI($"res://{Global.Name}/BehaviourGraph/ReimuBehaviour"), graph.URI);
        Assert.Equal([graph.URI], Data<GameObjectData>(reimu).BehaviourSlots);
        Assert.True(File.Exists(graph.FullDataPath));
        // It's in the package's folder of the same place, made for it
        Assert.Contains(graph.URI, _project.GetFolder(Global, "BehaviourGraph").Children);
    }

    [Fact]
    public async Task PackagesLinkingToWhatMovesGetItsPackageAsADependency()
    {
        _project.BuildProjectTree();
        var mod = AddPackage("Mod");
        var other = AddPackage("Other");
        var modGraphs = AddFolder(mod.GetPackageFolder(), "BehaviourGraph");
        var graph = AddGraph(AddFolder(other.GetPackageFolder(), "BehaviourGraph"), "Shared");
        var user = AddObject(AddFolder(other.GetPackageFolder(), "GameObject"), "User", graph.URI);

        _answers["Package dependencies"] = null;
        Assert.False(await AssetRelocation.MoveAsync(graph, modGraphs));
        Assert.DoesNotContain(mod.URI, other.Dependencies);
        Assert.Equal(other.URI, graph.Package);

        _answers.Remove("Package dependencies");
        Assert.True(await AssetRelocation.MoveAsync(graph, modGraphs), Said);

        Assert.Contains(mod.URI, other.Dependencies);
        Assert.Contains(mod.URI.ToString(), File.ReadAllText(JsonPath(other)));
        Assert.Equal(mod.URI, graph.Package);
        Assert.Equal([graph.URI], Data<GameObjectData>(user).BehaviourSlots);
    }

    // A package can use the other version's assets but for game objects, their instances and behaviours: a PS2 object moved into an Xbox
    // package using the PS2 version's assets takes its behaviour along, a behaviour other PS2 objects use doesn't go
    [Fact]
    public async Task GameObjectsTakeTheirBehavioursToTheOtherVersion()
    {
        _project.BuildProjectTree();
        var xbox = _project.Project.XboxPackage;
        xbox.AddDependency(Ps2.URI);
        var ps2Graphs = AddFolder(Ps2.GetPackageFolder(), "BehaviourGraph");
        var ps2Objects = AddFolder(Ps2.GetPackageFolder(), "GameObject");
        var xboxObjects = AddFolder(xbox.GetPackageFolder(), "GameObject");
        var walk = AddGraph(ps2Graphs, "COM_WALK");
        var crate = AddObject(ps2Objects, "CRATE", walk.URI);

        Assert.True(await AssetRelocation.MoveAsync(crate, xboxObjects), Said);

        Assert.Equal(xbox.URI, crate.Package);
        Assert.Equal(xbox.URI, walk.Package);
        Assert.Equal([walk.URI], Data<GameObjectData>(crate).BehaviourSlots);

        var spin = AddGraph(ps2Graphs, "COM_SPIN");
        var barrel = AddObject(ps2Objects, "BARREL", spin.URI);
        AddObject(ps2Objects, "BOX", spin.URI);
        Assert.False(await AssetRelocation.MoveAsync(barrel, xboxObjects));
        Assert.Equal(Ps2.URI, barrel.Package);
        Assert.Contains(_asked, asked => asked.StartsWith("Can't move") && asked.Contains("BOX") && asked.Contains("only used by their own version"));
    }

    [Fact]
    public async Task WhatARootPackageLinksToStaysInIt()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "BehaviourGraph"), Path.Combine(Global.Name, "GameObject"));
        var mod = AddPackage("Mod");
        var graph = AddGraph(_project.GetFolder(Global, "BehaviourGraph"), "Shared");
        AddObject(_project.GetFolder(Global, "GameObject"), "User", graph.URI);
        var uri = graph.URI;

        Assert.False(await AssetRelocation.MoveAsync(graph, AddFolder(mod.GetPackageFolder(), "BehaviourGraph")));

        Assert.Equal(uri, graph.URI);
        Assert.True(File.Exists(graph.FullDataPath));
        Assert.Contains(_asked, asked => asked.StartsWith("Can't move") && asked.Contains("root package"));
    }

    [Fact]
    public void WhatTheGameHasOneOfInPlaceNeitherMovesNorGetsCopied()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "Startup"), Path.Combine(Global.Name, "levels"));
        var startup = _project.GetFolder(Global, "Startup");
        IAsset[] fixedOnes =
        [
            _project.Add(new SaveIcon { GlobalPath = "Startup" }, "Crash"),
            _project.Add(new UiSoundLibrary { GlobalPath = "Startup" }, "Frontend"),
            _project.Add(new PTC { GlobalPath = "Startup" }, "Decal"),
            _project.Add(new PSM { GlobalPath = "Startup" }, "Icons"),
            _project.Add(new TextFile { GlobalPath = "Startup" }, LevelSelect.FileName),
            _project.Add(new LevelChunk { AdditionalPath = "startup/default" }, "default")
        ];
        foreach (var asset in fixedOnes)
        {
            Assert.NotNull(AssetRelocation.WhyNotMovable(asset));
            Assert.NotNull(AssetRelocation.WhyNotDuplicable(asset));
        }

        // Other fonts are only copied, like the game's pictures and texts
        var font = _project.Add(new Font { GlobalPath = "Startup" }, "Arial");
        Assert.Contains("where they are", AssetRelocation.WhyNotMovable(font));
        Assert.Null(AssetRelocation.WhyNotDuplicable(font));
        // Instances are copied in their chunk's scene and between chunks as prefabs
        var instance = _project.Add(new ObjectInstance { Chunk = "levels/test", LayoutID = 0 }, "Instance 0");
        Assert.NotNull(AssetRelocation.WhyNotMovable(instance));
        Assert.NotNull(AssetRelocation.WhyNotDuplicable(instance));
        // Packages stay in the project's assets folder, the root ones aren't copied
        Assert.NotNull(AssetRelocation.WhyNotMovable(Ps2.GetPackageFolder()));
        Assert.Null(AssetRelocation.WhyNotDuplicable(Ps2.GetPackageFolder()));
        Assert.NotNull(AssetRelocation.WhyNotDuplicable(Global.GetPackageFolder()));
        Assert.NotNull(AssetRelocation.WhyNotDuplicable(_project.Project.GlobalPackageXbox));
        // A folder holding one of them stays too
        foreach (var asset in fixedOnes.Take(5))
        {
            startup.AddChild(asset);
        }

        Assert.Contains("is in it", AssetRelocation.WhyNotMovable(startup));
        Assert.NotNull(AssetRelocation.WhyNotDuplicable(startup));
    }

    // The build writes the game's pictures, fonts and texts from where they are (the Startup, Extras and Language folders), moved elsewhere
    // they dropped out of it: they're only duplicated, and a folder holding one stays too
    [Fact]
    public void PicturesFontsAndTextsStayButGetCopied()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "Extras"), Path.Combine(Global.Name, "Language"));
        var extras = _project.GetFolder(Global, "Extras");
        var picture = _project.Add(new PSM { GlobalPath = "Extras" }, "Boss01");
        var font = _project.Add(new TT_Lab.Assets.Global.Font { GlobalPath = "Startup" }, "Font");
        var text = _project.Add(new TextFile { GlobalPath = "Language" }, "English");
        foreach (var asset in new IAsset[] { picture, font, text })
        {
            Assert.Contains("where they are", AssetRelocation.WhyNotMovable(asset));
            Assert.Null(AssetRelocation.WhyNotDuplicable(asset));
        }

        extras.AddChild(picture);
        Assert.Contains("Boss01 is in it", AssetRelocation.WhyNotMovable(extras));
        Assert.Null(AssetRelocation.WhyNotDuplicable(extras));
    }

    [Fact]
    public void ThingsGoWhereCreateAssetMakesThem()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "GameObject"), Path.Combine(Global.Name, "Other"), Path.Combine(Global.Name, "levels", "earth"),
            Path.Combine(Global.Name, "Worlds", "Inner"));
        var objects = _project.GetFolder(Global, "GameObject");
        var crate = AddObject(objects, "Crate");
        var levels = _project.GetFolder(Global, "levels");
        var earth = _project.GetFolder(Global, "levels", "earth");
        var worlds = _project.GetFolder(Global, "Worlds");

        Assert.Null(AssetRelocation.WhyNotInto(crate, _project.GetFolder(Global, "Other")));
        Assert.Contains("already", AssetRelocation.WhyNotInto(crate, objects));
        Assert.Contains("levels", AssetRelocation.WhyNotInto(crate, earth));
        Assert.Contains("package's own folder", AssetRelocation.WhyNotInto(crate, Global.GetPackageFolder()));
        Assert.Contains("packages", AssetRelocation.WhyNotInto(crate, ProjectAssetsFolder));
        // Folders go in a package's folders, never into themselves
        Assert.Null(AssetRelocation.WhyNotInto(worlds, objects));
        Assert.Null(AssetRelocation.WhyNotInto(objects, worlds));
        Assert.Contains("itself", AssetRelocation.WhyNotInto(worlds, _project.GetFolder(Global, "Worlds", "Inner")));
        Assert.Contains("itself", AssetRelocation.WhyNotInto(worlds, worlds));
        // Something of the name is in the way
        var other = AddObject(_project.GetFolder(Global, "Other"), "Crate");
        Assert.Contains("already has", AssetRelocation.WhyNotInto(crate, _project.GetFolder(Global, "Other")));
        Assert.NotNull(other);
        // The levels folder only takes chunks and folders of chunks
        Assert.Null(AssetRelocation.WhyNotInto(earth, worlds));
        Assert.Null(AssetRelocation.WhyNotInto(worlds, levels));
        AddObject(worlds, "Inside");
        Assert.Contains("levels", AssetRelocation.WhyNotInto(worlds, levels));
    }

    [AvaloniaFact]
    public void ChunksOnlyGoInTheLevelsFolders()
    {
        _project.BuildProjectTree(Path.Combine(Ps2.Name, "levels", "earth"), Path.Combine(Ps2.Name, "levels", "ice"), Path.Combine(Ps2.Name, "GameObject"));
        AddCrashAndASurface();
        var beach = CreateChunk(_project.GetFolder(Ps2, "levels", "earth"), "beach");
        var chunkFolder = beach.GetChunkFolder();

        Assert.Null(AssetRelocation.WhyNotInto(chunkFolder, _project.GetFolder(Ps2, "levels", "ice")));
        Assert.Null(AssetRelocation.WhyNotInto(beach, _project.GetFolder(Ps2, "levels")));
        Assert.Contains("levels", AssetRelocation.WhyNotInto(chunkFolder, _project.GetFolder(Ps2, "GameObject")));
        Assert.Contains("levels", AssetRelocation.WhyNotInto(_project.GetFolder(Ps2, "levels", "earth"), _project.GetFolder(Ps2, "GameObject")));
        Assert.Contains("chunk", AssetRelocation.WhyNotInto(_project.GetFolder(Ps2, "levels", "ice"), chunkFolder));
    }

    [Fact]
    public async Task ACopyGetsTheNextFreeNameAndAnIdOfItsOwn()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "GameObject"), Path.Combine(Global.Name, "BehaviourGraph"));
        var graph = AddGraph(_project.GetFolder(Global, "BehaviourGraph"), "Script");
        var objects = _project.GetFolder(Global, "GameObject");
        var crate = AddObject(objects, "Crate", graph.URI);

        var copy = Assert.IsType<GameObject>(await AssetRelocation.DuplicateAsync(crate));
        var third = Assert.IsType<GameObject>(await AssetRelocation.DuplicateAsync(crate));
        var fourth = Assert.IsType<GameObject>(await AssetRelocation.DuplicateAsync(copy));

        Assert.Equal(["Crate (2)", "Crate (3)", "Crate (4)"], new[] { copy, third, fourth }.Select(asset => asset.Alias));
        Assert.Equal(4, new[] { crate, copy, third, fourth }.Select(asset => asset.ID).Distinct().Count());
        Assert.True(File.Exists(JsonPath(copy)));
        Assert.True(File.Exists(copy.FullDataPath));
        Assert.Same(copy, _project.AssetManager.GetAsset(copy.URI));
        // It links to what the original links to, the original stays as it was
        Assert.Equal([graph.URI], Data<GameObjectData>(copy).BehaviourSlots);
        Assert.Equal([graph.URI], Data<GameObjectData>(crate).BehaviourSlots);
        Assert.Contains(graph.URI, copy.References);
        Assert.Equal(["Crate", "Crate (2)", "Crate (3)", "Crate (4)"], Rows(objects));
        Assert.Contains(_asked, asked => asked.EndsWith("[Crate (2)]"));
    }

    [Fact]
    public async Task CopiesOfAFolderLinkToEachOther()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "Characters"));
        var characters = _project.GetFolder(Global, "Characters");
        var graph = AddGraph(characters, "COM_REIMU");
        var helper = AddGraph(characters, "COM_HELPER", """
                                                         behaviour COM_HELPER {
                                                             state Start(COM_REIMU) {
                                                             }
                                                         }
                                                         """);
        var reimu = AddObject(characters, "Reimu", graph.URI, helper.URI);

        _answers["Duplicate"] = 1;
        var copy = Assert.IsType<Folder>(await AssetRelocation.DuplicateAsync(characters));

        Assert.Equal("Characters (2)", copy.Alias);
        var copies = copy.Children.Select(_project.AssetManager.GetAsset).ToDictionary(asset => asset.Alias);
        Assert.Equal(["COM_HELPER (2)", "COM_REIMU (2)", "Reimu (2)"], copies.Keys.Order());
        var reimuCopy = copies["Reimu (2)"];
        Assert.Equal([copies["COM_REIMU (2)"].URI, copies["COM_HELPER (2)"].URI], Data<GameObjectData>(reimuCopy).BehaviourSlots);
        Assert.Equal([graph.URI, helper.URI], Data<GameObjectData>(reimu).BehaviourSlots);
        // The copied script names the copy, by its URI since its name isn't one a script can have
        Assert.Contains($"state Start(\"{copies["COM_REIMU (2)"].URI}\")", File.ReadAllText(copies["COM_HELPER (2)"].FullDataPath));
        Assert.Contains("state Start(COM_REIMU)", File.ReadAllText(helper.FullDataPath));
        Assert.Equal(["Characters", "Characters (2)"], Rows(_project.GetFolder(Global)).Where(name => name.StartsWith("Characters")));
        Assert.NotEqual(reimu.ID, reimuCopy.ID);
        Assert.NotEqual(graph.ID, copies["COM_REIMU (2)"].ID);
        Assert.NotEqual(helper.ID, copies["COM_HELPER (2)"].ID);
    }

    [Fact]
    public async Task CopiesAreNamedOneByOneUntilTheRestGetTheirNamesAllAtOnce()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "Characters"));
        var characters = _project.GetFolder(Global, "Characters");
        AddObject(characters, "Aku");
        AddObject(characters, "Crash");
        AddObject(characters, "Uka");
        var named = new List<string>();
        _naming = request =>
        {
            named.Add(request.Suggested);
            return request.Suggested switch
            {
                "Characters (2)" => new AssetRelocation.NameAnswer("Copied", false),
                "Aku (2)" => new AssetRelocation.NameAnswer("Aku Aku", false),
                _ => new AssetRelocation.NameAnswer("Bandicoot", true)
            };
        };
        _answers["Duplicate"] = 0;

        var copy = Assert.IsType<Folder>(await AssetRelocation.DuplicateAsync(characters));

        Assert.Equal(["Characters (2)", "Aku (2)", "Crash (2)"], named);
        Assert.Equal("Copied", copy.Alias);
        Assert.Equal(["Aku Aku", "Bandicoot", "Uka (2)"], copy.Children.Select(uri => _project.AssetManager.GetAsset(uri).Alias).Order());
        Assert.Contains(_asked, asked => asked.StartsWith("Duplicate") && asked.Contains("one by one"));
    }

    [AvaloniaFact]
    public async Task ACopiedChunkIsAChunkOfItsOwn()
    {
        _project.BuildProjectTree(Path.Combine(Ps2.Name, "levels", "earth"));
        var levelSelect = AddLevelSelect(string.Empty);
        AddCrashAndASurface();
        var earth = _project.GetFolder(Ps2, "levels", "earth");
        var beach = CreateChunk(earth, "beach");
        var instance = AssetManager.Get().GetAsset<ObjectInstance>(beach.ChunkResources[3]);

        var copyFolder = Assert.IsType<Folder>(await AssetRelocation.DuplicateAsync(beach.GetChunkFolder()));

        var copy = Assert.IsType<LevelChunk>(_project.AssetManager.GetAsset(Assert.Single(copyFolder.Children)));
        Assert.Equal("beach (2)", copy.Alias);
        Assert.Equal("levels/earth/beach (2)", copy.AdditionalPath);
        Assert.True(copyFolder.Mark.HasFlag(FolderMark.IsChunk));
        Assert.NotEqual(beach.ID, copy.ID);
        Assert.Equal(beach.ChunkResources.Count, copy.ChunkResources.Count);
        Assert.All(copy.ChunkResources, resource => Assert.StartsWith($"res://{Ps2.Name}/levels/earth/beach (2)/", resource.ToString()));
        var instanceCopy = AssetManager.Get().GetAsset<ObjectInstance>(copy.ChunkResources[3]);
        Assert.Equal(instance.ID, instanceCopy.ID);
        Assert.Equal("levels/earth/beach (2)", instanceCopy.Chunk);
        Assert.Equal(Data<ObjectInstanceData>(instance).ObjectId, Data<ObjectInstanceData>(instanceCopy).ObjectId);
        Assert.All(AssetLinks.UrisInModelFile(AssetManager.Get().GetAsset(copy.ChunkResources[0]).FullDataPath),
            uri => Assert.True(_project.AssetManager.DoesAssetExist(new LabURI(uri))));
        // The original is as it was, the level select lists both
        Assert.All(beach.ChunkResources, resource => Assert.StartsWith($"res://{Ps2.Name}/levels/earth/beach/", resource.ToString()));
        Assert.Equal(["beach", "beach (2)"], LevelSelect.Entries(Data<TextFileData>(levelSelect).Text).Select(entry => entry.Name).Order());
        Assert.Equal(["beach", "beach (2)"], Rows(earth));
    }

    [Fact]
    public async Task ACopiedPackageIsANewPackageOfTheSameDependencies()
    {
        _project.BuildProjectTree();
        var mod = AddPackage("Mod");
        var graph = AddGraph(AddFolder(mod.GetPackageFolder(), "BehaviourGraph"), "COM_MOD");
        AddObject(AddFolder(mod.GetPackageFolder(), "GameObject"), "Modded", graph.URI);

        _answers["Duplicate"] = 1;
        var copyFolder = Assert.IsType<Folder>(await AssetRelocation.DuplicateAsync(mod.GetPackageFolder()));

        var copy = _project.AssetManager.GetAsset<Package>(copyFolder.Package);
        Assert.Equal("Mod (2)", copy.Name);
        Assert.True(copyFolder.Mark.HasFlag(FolderMark.IsPackage));
        Assert.Equal(mod.Dependencies, copy.Dependencies);
        Assert.Contains(copy.URI, _project.Project.BasePackage.Dependencies);
        var objects = _project.AssetManager.GetAllAssetsOf<GameObject>().Where(asset => asset.Package == copy.URI).ToList();
        var graphCopy = Assert.Single(_project.AssetManager.GetAllAssetsOf<BehaviourGraph>(), asset => asset.Package == copy.URI);
        Assert.Equal([graphCopy.URI], Data<GameObjectData>(Assert.Single(objects)).BehaviourSlots);
        Assert.Equal(["BehaviourGraph", "GameObject"], Rows(copyFolder));
    }

    [AvaloniaFact]
    public async Task MoveToAsksForTheFolderShowingWhereItCantGo()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "GameObject"), Path.Combine(Global.Name, "Crates"), Path.Combine(Global.Name, "levels", "earth"));
        var crate = AddObject(_project.GetFolder(Global, "GameObject"), "Crate");
        var choices = TT_Lab.Controls.FolderPickerDialogue.Choices(crate);
        var all = choices.SelectMany(Flatten).ToDictionary(choice => choice.Folder);

        Assert.True(all[_project.GetFolder(Global, "Crates")].CanTake);
        Assert.False(all[_project.GetFolder(Global, "levels", "earth")].CanTake);
        Assert.False(all[_project.GetFolder(Global, "GameObject")].CanTake);
        Assert.True(all[_project.GetFolder(Global, "GameObject")].IsExpanded);

        AssetRelocation.AskFolder = _ => Task.FromResult<Folder?>(_project.GetFolder(Global, "Crates"));
        Assert.True(await AssetRelocation.MoveToAsync(crate), Said);
        Assert.Equal("Crates", crate.FolderInPackage);

        static IEnumerable<TT_Lab.Controls.FolderChoice> Flatten(TT_Lab.Controls.FolderChoice choice) => choice.Children.SelectMany(Flatten).Prepend(choice);
    }

    [AvaloniaFact]
    public async Task TheTreesMenusOfferDuplicateAndMoveToWhereTheyCan()
    {
        _project.BuildProjectTree(Path.Combine(Global.Name, "GameObject"), Path.Combine(Global.Name, "Startup"));
        var crate = AddObject(_project.GetFolder(Global, "GameObject"), "Crate");
        var icon = _project.Add(new SaveIcon { GlobalPath = "Startup" }, "Crash");
        icon.Serialize();
        var startup = _project.GetFolder(Global, "Startup");
        startup.AddChild(icon);

        Assert.Contains("Duplicate", await MenuOf(crate.GetResourceTreeElement()));
        Assert.Contains("Move To...", await MenuOf(crate.GetResourceTreeElement()));
        Assert.DoesNotContain("Duplicate", await MenuOf(icon.GetResourceTreeElement()));
        Assert.DoesNotContain("Move To...", await MenuOf(icon.GetResourceTreeElement()));
        var packageMenu = await MenuOf(Ps2.GetPackageFolder().GetResourceTreeElement());
        Assert.Contains("Duplicate", packageMenu);
        Assert.DoesNotContain("Move To...", packageMenu);
        Assert.DoesNotContain("Duplicate", await MenuOf(Global.GetPackageFolder().GetResourceTreeElement()));
    }

    private static async Task<List<string>> MenuOf(ResourceTreeElementViewModel row)
    {
        await row.CreateContextMenuAction();
        return row.MenuOptions.Select(item => item.Header as string ?? string.Empty).ToList();
    }

    [Fact]
    public void LinksAreWholeJsonStringsAndScriptReferencesAreTokens()
    {
        var map = new Dictionary<string, string> { ["res://P/a/B"] = "res://P/c/B" };
        Assert.Equal("{\"x\":{\"_uri\":\"res://P/c/B\"},\"y\":\"see res://P/a/B\",\"z\":\"res://P/a/Bee\"}",
            AssetLinks.Remap("{\"x\":{\"_uri\":\"res://P/a/B\"},\"y\":\"see res://P/a/B\",\"z\":\"res://P/a/Bee\"}", map, AssetLinks.QuoteNewtonsoft));
        // Escaped strings are read for what they say
        Assert.Equal(["res://P/a/\"q\""], AssetLinks.UrisIn("[\"res://P/a/\\\"q\\\"\"]"));
        Assert.Null(AssetLinks.Remap("{\"x\":1}", map, AssetLinks.QuoteNewtonsoft));

        var script = "behaviour A {\r\n  // state X(Commented)\r\n  [Interrupting] state One(COM_B) {}\r\n  state Two('res://P/a/B') {}\r\n  state Three() {}\r\n}";
        var references = BehaviourScripts(script);
        Assert.Equal([("COM_B", true, true), ("res://P/a/B", false, true)], references.Select(reference => (reference.Text, reference.IsName, reference.IsChild)));
        Assert.Equal("COM_B", script[references[0].Start..references[0].End]);
        Assert.Equal("'res://P/a/B'", script[references[1].Start..references[1].End]);
        Assert.Contains("state One(COM_C)", BehaviourScriptLinks.Replace(script, [(references[0], "COM_C")]));
    }

    private static List<BehaviourScriptLinks.Reference> BehaviourScripts(string script) => BehaviourScriptLinks.Find(script);

    [Fact]
    public void CopiesAreNamedLikeWindowsNamesThem()
    {
        HashSet<string> taken = ["Crate (2)", "Crate (3)"];
        Assert.Equal("Crate (4)", AssetRelocation.SuggestName("Crate", name => !taken.Contains(name)));
        Assert.Equal("Crate (4)", AssetRelocation.SuggestName("Crate (2)", name => !taken.Contains(name)));
        Assert.Equal("Box (2)", AssetRelocation.SuggestName("Box", _ => true));
        Assert.Equal("Box (1) (3)", AssetRelocation.SuggestName("Box (1) (2)", name => name != "Box (1) (2)"));
    }
}
