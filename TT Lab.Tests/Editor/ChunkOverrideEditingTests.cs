using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.Attributes;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// Editing an asset a chunk shares with other chunks from the chunk's document gives the chunk a value of its own
[Collection(ProjectCollection.Name)]
public sealed class ChunkOverrideEditingTests : IDisposable
{
    // The new chunk's resources are its scenery, links, particles and the instance of Crash
    private const string CrashFloat = "Root.ChunkResources[3][data].AssetData.ObjectId[data].AssetData.InstFloats[0]";

    private readonly TestProject _project = new();
    private readonly Package _package;
    private readonly GameObject _crash;

    public ChunkOverrideEditingTests()
    {
        _package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(_package.Name, "levels"), Path.Combine(_package.Name, "Graphics"));
        _crash = _project.Add(new GameObject(), "Crash", 0x0);
        var crashData = new GameObjectData(_crash) { Name = "Crash" };
        crashData.InstFloats.Add(1.5f);
        _crash.SetData(crashData);
        _crash.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);
    }

    public void Dispose() => _project.Dispose();

    private LevelChunk CreateChunk(string name)
    {
        var folder = _project.GetFolder(_package, "levels");
        return (LevelChunk)AssetFactory.CreateAsset(typeof(LevelChunk), folder, name, string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(), asset => AssetDataFactory.CreateChunkData(folder, asset))!;
    }

    private static DocumentViewModel Open(LevelChunk chunk)
    {
        var document = new DocumentViewModel(chunk);
        document.Initialize();
        return document;
    }

    private static DocumentNodeViewModel Show(DocumentViewModel document, string path)
    {
        var editor = EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct();
        new Window { Content = new ContentControl { Content = editor }, Width = 600, Height = 300 }.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        return editor;
    }

    [AvaloniaFact]
    public void EditsOfSharedAssetsBecomeTheChunksOwn()
    {
        var chunk = CreateChunk("hub");
        var document = Open(chunk);
        var value = document.PropertyGraph.Find(CrashFloat)!;
        Assert.Equal(1.5f, value.GetValue());

        value.SetValue(2.5f);
        document.Save();

        var @override = Assert.Single(chunk.Overrides);
        Assert.Equal(_crash.URI, @override.Asset);
        Assert.Equal(2.5f, (float)@override.Values["AssetData.InstFloats[0]"]);
        Assert.Equal([1.5f], ((IAsset)_crash).GetData<GameObjectData>().InstFloats);
        // The chunk shows its own value when it's opened again, other chunks the shared one
        Assert.Equal(2.5f, Open(chunk).PropertyGraph.Find(CrashFloat)!.GetValue());
        Assert.Equal(1.5f, Open(CreateChunk("beach")).PropertyGraph.Find(CrashFloat)!.GetValue());
    }

    [AvaloniaFact]
    public void OwnValuesCanBeRevertedOrShared()
    {
        var chunk = CreateChunk("hub");
        var document = Open(chunk);
        document.PropertyGraph.Find(CrashFloat)!.SetValue(2.5f);
        var editor = Show(document, CrashFloat);
        Assert.True(editor.CanOverride);
        Assert.True(editor.IsOverridden);

        editor.RevertOverride();
        Assert.False(editor.IsOverridden);
        Assert.Equal(1.5f, document.PropertyGraph.Find(CrashFloat)!.GetValue());
        document.Save();
        Assert.Empty(chunk.Overrides);

        document.PropertyGraph.Find(CrashFloat)!.SetValue(4.0f);
        editor.ApplyOverrideToAsset();
        Assert.False(editor.IsOverridden);
        document.Save();
        Assert.Empty(chunk.Overrides);
        Assert.Equal([4.0f], ((IAsset)_crash).GetData<GameObjectData>().InstFloats);
    }

    [AvaloniaFact]
    public void EmittersShowTheirSystemInTheChunksInspector()
    {
        var chunk = CreateChunk("hub");
        var particles = ((IAsset)_project.AssetManager.GetAsset(chunk.ChunkResources[2])).GetData<TT_Lab.AssetData.Instance.ParticleData>();
        particles.ParticleSystems.AddRange([new TT_Lab.AssetData.Instance.Particle.ParticleSystem { Name = "Smoke" }, new TT_Lab.AssetData.Instance.Particle.ParticleSystem { Name = "Fire" }]);
        particles.ParticleInstances.Add(new TT_Lab.AssetData.Instance.Particle.ParticleSystemInstance { Name = "Fire" });
        var document = Open(chunk);
        var editor = Assert.IsType<TT_Lab.ViewModels.Editors.Instance.ParticleSystemFieldViewModel>(Show(document, "Root.ChunkResources[2][data].AssetData.ParticleInstances[0].Name"));

        editor.GoToSystem();

        Assert.Equal("Root.ChunkResources[2][data]", document.Inspector!.Property.Path);
    }

    // Objects aren't among a chunk's resources, it gets to them through the links of its instances
    [AvaloniaFact]
    public void LinksToSharedAssetsOpenTheChunksVersionInTheInspector()
    {
        var chunk = CreateChunk("hub");
        var document = Open(chunk);
        var link = Assert.IsType<UriLinkViewModel>(Show(document, "Root.ChunkResources[3][data].AssetData.ObjectId"));

        link.OpenDocumentCommand.Execute().Subscribe();

        Assert.Equal("Root.ChunkResources[3][data].AssetData.ObjectId[data]", document.Inspector!.Property.Path);
        Assert.Same(_crash, Assert.IsAssignableFrom<SerializableAsset>(document.Inspector.Property.Target).OverriddenAsset);
        Assert.NotNull(document.PropertyGraph.Find(CrashFloat));
    }

    // The game only goes by objects' IDs, their names are never a chunk's own values
    [AvaloniaFact]
    public void ObjectsNamesArentTheChunksOwn()
    {
        var chunk = CreateChunk("hub");
        chunk.Overrides.Add(new AssetOverride { Asset = _crash.URI, Values = { ["AssetData.InstFloats[0]"] = 2.5f } });
        var document = Open(chunk);

        var view = Assert.IsAssignableFrom<SerializableAsset>(document.PropertyGraph.Find("Root.ChunkResources[3][data].AssetData.ObjectId[data]")!.Target);

        Assert.Equal("Crash", ((IAsset)view).GetData<GameObjectData>().Name);
        Assert.Equal(2.5f, document.PropertyGraph.Find(CrashFloat)!.GetValue());
        Assert.Equal(["AssetData.InstFloats[0]"], document.PropertyGraph.Overrides!.GetOwnValues(view).Keys);
    }

    [AvaloniaFact]
    public void LinksShowTheChunkHasValuesOfItsOwnInWhatTheyLinkTo()
    {
        var chunk = CreateChunk("hub");
        chunk.Overrides.Add(new AssetOverride { Asset = _crash.URI, Values = { ["AssetData.Name"] = "Hub's crash" } });
        var document = Open(chunk);
        var link = Show(document, "Root.ChunkResources[3][data].AssetData.ObjectId");
        Assert.False(link.IsOverridden);

        document.PropertyGraph.Find(CrashFloat)!.SetValue(2.5f);

        Assert.True(link.IsOverridden);
    }

    // The asset can change in its own editor while the chunk's document is open, the chunk's view of it keeps what it had then
    [AvaloniaFact]
    public void ValuesChangedElsewhereDontBecomeTheChunksOwn()
    {
        var chunk = CreateChunk("hub");
        var document = Open(chunk);
        var commands = Show(document, "Root.ChunkResources[3][data].AssetData.ObjectId[data].AssetData.BehaviourPack");
        var crash = ((IAsset)_crash).GetData<GameObjectData>();
        crash.BehaviourPack = "SetSurface(0x01FF0008);";
        _crash.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);

        document.PropertyGraph.Find(CrashFloat)!.SetValue(2.5f);
        document.Save();

        Assert.Equal(["AssetData.InstFloats[0]"], Assert.Single(chunk.Overrides).Values.Keys);
        Assert.False(commands.IsOverridden);
        Assert.Equal("SetSurface(0x01FF0008);", crash.BehaviourPack);
    }

    private sealed class TwoLinks : IDocumentModel
    {
        [Editable(MaxLinkGraphDepth = 1)]
        public LabURI Shallow { get; set; } = LabURI.Empty;

        [Editable]
        public LabURI Deep { get; set; } = LabURI.Empty;

        public string DocumentName => "Links";
    }

    // A link cut short by its depth used to stay entered, a later link to the same asset got none of its properties
    [AvaloniaFact]
    public void LinksCutShortDontHideTheAssetFromLaterLinks()
    {
        var graph = PropertyGraphBuilder.Build(new TwoLinks { Shallow = _crash.URI, Deep = _crash.URI });

        Assert.Null(graph.Find("Root.Shallow[data].AssetData"));
        Assert.NotNull(graph.Find("Root.Deep[data].AssetData.InstFloats[0]"));
    }

    [AvaloniaFact]
    public void TheChunksOwnAssetsAreEditedAsTheyAre()
    {
        var chunk = CreateChunk("hub");
        var document = Open(chunk);
        var instanceNode = document.PropertyGraph.Find("Root.ChunkResources[3][data]")!;

        Assert.IsType<ObjectInstance>(instanceNode.Target);
        Assert.Null(((ObjectInstance)instanceNode.Target).OverriddenAsset);
        Assert.False(Show(document, "Root.ChunkResources[3][data].AssetData.Position").CanOverride);
    }
}
