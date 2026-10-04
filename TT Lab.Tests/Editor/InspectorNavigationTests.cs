using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Caliburn.Micro;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.Controls;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Project.Messages;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// Following a link in a chunk's inspector is a step along a trail the inspector goes back and forth on, like a browser's history
[Collection(ProjectCollection.Name)]
public sealed class InspectorNavigationTests : IDisposable
{
    // A new chunk's resources are its scenery, links, particles and the instance of Crash
    private const string CrashInstance = "Root.ChunkResources[3][data]";

    private readonly TestProject _project = new();
    private readonly Package _package;
    private readonly GameObject _crash;
    private readonly Shell _shell = new();

    private sealed class Shell : ILabManager
    {
        public List<IAsset> Opened { get; } = [];
        public Task HandleAsync(ProjectManagerMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
        public void SaveProject() { }
        public void OpenEditor(IAsset asset) => Opened.Add(asset);
        public void BuildPs2() { }
        public void BuildPs2Iso() { }
        public void BuildXbox() { }
        public void BuildXboxImage() { }
    }

    public InspectorNavigationTests()
    {
        _package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(_package.Name, "levels"));
        _crash = _project.Add(new GameObject(), "Crash", 0x0);
        _crash.SetData(new GameObjectData(_crash) { Name = "Crash" });
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);
        Locator.CurrentMutable.RegisterConstant<ILabManager>(_shell);
    }

    public void Dispose()
    {
        Locator.CurrentMutable.UnregisterAll<ILabManager>();
        _project.Dispose();
    }

    private LevelChunk CreateChunk(string name)
    {
        var folder = _project.GetFolder(_package, "levels");
        var chunk = (LevelChunk)AssetFactory.CreateAsset(typeof(LevelChunk), folder, name, string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(), asset => AssetDataFactory.CreateChunkData(folder, asset))!;
        return chunk;
    }

    private T AddInstance<T>(LevelChunk chunk, string name, Func<IAsset, AbstractAssetData> data, int layout = 0) where T : SerializableInstance, new()
    {
        var instance = _project.Add(new T { Chunk = chunk.AdditionalPath!, AdditionalPath = chunk.AdditionalPath, LayoutID = layout }, name);
        instance.SetData(data(instance));
        chunk.ChunkResources.Add(instance.URI);
        return instance;
    }

    private static DocumentViewModel Open(LevelChunk chunk)
    {
        var document = new DocumentViewModel(chunk);
        document.Initialize();
        return document;
    }

    private static void Follow(DocumentViewModel document, PropertyNode link)
    {
        var editor = Assert.IsType<UriLinkViewModel>(EditorDescRegistry.GetDesc(document, link).Construct());
        editor.OpenDocumentCommand.Execute().Subscribe();
    }

    private static PropertyNode ResourceOf(DocumentViewModel document, IAsset asset) =>
        document.PropertyGraph.Root.FindChild(".ChunkResources")!.Children.Single(element => Equals(element.GetValue(), asset.URI)).FindChild("[data]")!;

    [Fact]
    public void TheTrailGoesBackAndForthLikeABrowsersHistory()
    {
        var (a, b, c, d) = (new PropertyNode("A", "A", new object()), new PropertyNode("B", "B", new object()), new PropertyNode("C", "C", new object()), new PropertyNode("D", "D", new object()));
        var trail = new InspectorTrail();
        trail.Start(a);
        trail.Follow(b);
        trail.Follow(c);

        Assert.Same(b, trail.Back());
        Assert.True(trail.CanGoForward);
        // Following from the middle drops what was ahead
        trail.Follow(d);
        Assert.Equal([a, b, d], trail.Nodes);
        Assert.False(trail.CanGoForward);

        // What left the graph is passed over
        trail.Prune(node => node != b);
        Assert.Equal([a, d], trail.Nodes);
        Assert.Same(d, trail.Current);
        trail.Prune(node => node != d);
        Assert.Same(a, trail.Current);
        trail.Start(c);
        Assert.Equal([c], trail.Nodes);
        Assert.False(trail.CanGoBack);
    }

    [AvaloniaFact]
    public void FollowingASharedObjectGoesBackToTheInstance()
    {
        var document = Open(CreateChunk("hub"));
        var instance = document.PropertyGraph.Find(CrashInstance)!;
        document.OpenInspector(instance);
        Assert.False(document.CanInspectBack);
        Assert.Null(document.InspectedSharedAsset);

        Follow(document, instance.Find("AssetData.ObjectId")!);

        var view = document.PropertyGraph.Find($"{CrashInstance}.AssetData.ObjectId[data]")!;
        Assert.Same(view, document.Inspector!.Property);
        Assert.True(document.CanInspectBack);
        Assert.Equal(["Instance 0", "Crash"], document.InspectorCrumbs.Select(crumb => crumb.Caption));
        Assert.Equal([false, true], document.InspectorCrumbs.Select(crumb => crumb.IsCurrent));
        // The chunk's version of the shared object, whose own editor edits it for every chunk
        Assert.Same(_crash, document.InspectedSharedAsset);
        document.OpenSharedAssetCommand.Execute().Subscribe();
        Assert.Same(_crash, Assert.Single(_shell.Opened));

        document.InspectBack();
        Assert.Same(instance, document.Inspector!.Property);
        Assert.True(document.CanInspectForward);
        Assert.Null(document.InspectedSharedAsset);
        document.InspectForward();
        Assert.Same(view, document.Inspector!.Property);
        document.InspectCrumb(document.InspectorCrumbs[0]);
        Assert.Same(instance, document.Inspector!.Property);

        // Something picked elsewhere starts over
        document.OpenInspector(document.PropertyGraph.Find("Root.ChunkResources[0][data]"));
        Assert.Single(document.InspectorCrumbs);
        Assert.False(document.CanInspectBack);
        Assert.False(document.CanInspectForward);
    }

    [AvaloniaFact]
    public void LinksToTheChunksOwnResourcesInspectThemWhereTheChunkHasThem()
    {
        var chunk = CreateChunk("beach");
        var from = AddInstance<AiPosition>(chunk, "Start", asset => new AiPositionData(asset) { Coords = new Vector3(1, 0, 0) }, 6);
        var to = AddInstance<AiPosition>(chunk, "End", asset => new AiPositionData(asset) { Coords = new Vector3(5, 0, 0) }, 6);
        var path = AddInstance<AiPath>(chunk, "Path", asset => new AiPathData(asset) { PathBegin = from.URI, PathEnd = to.URI }, 6);
        var document = Open(chunk);
        var pathNode = ResourceOf(document, path);
        document.OpenInspector(pathNode);

        Follow(document, pathNode.Find("AssetData.PathEnd")!);

        // Not the link's own copy of the position's nodes: the scene and the resources panel follow the chunk's
        Assert.Same(ResourceOf(document, to), document.Inspector!.Property);
        Assert.Null(document.InspectedSharedAsset);
        document.InspectBack();
        Assert.Same(pathNode, document.Inspector!.Property);
        Assert.Empty(_shell.Opened);
    }

    private const string Code = "behaviour TEST {\n   state S {\n   }\n}\n";

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 250 && !condition(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private static void Pump()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    // A chunk's view of a behaviour shares the behaviour's data: closing the behaviour's own tab disposed it, and the script showed empty
    // in the chunk's inspector from then on
    [AvaloniaFact]
    public async Task ClosingTheSharedAssetsTabKeepsWhatTheChunkShows()
    {
        var graph = _project.Add(new BehaviourGraph(), "COM_TEST", 0x10);
        graph.SetData(new BehaviourGraphData(graph) { Graph = Code });
        graph.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        ((IAsset)_crash).GetData<GameObjectData>().BehaviourSlots.Add(graph.URI);
        _crash.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        var document = Open(CreateChunk("hub"));
        var window = new Window
        {
            Content = new DocumentScrollViewer { Content = new ContentControl { [!ContentControl.ContentProperty] = new Binding(nameof(DocumentViewModel.Inspector)) { Source = document } } },
            Width = 800, Height = 900
        };
        window.Show();
        var instance = document.PropertyGraph.Find(CrashInstance)!;
        var behaviour = instance.Find("AssetData.ObjectId[data].AssetData.BehaviourSlots[0][data]")!;
        document.OpenInspector(instance);
        Follow(document, instance.Find("AssetData.ObjectId")!);
        Follow(document, behaviour.Parent!);
        Pump();

        var resources = new ResourcesEditorsViewModel();
        resources.OpenEditor(document.InspectedSharedAsset!);
        var tab = resources.Tabs.Single();
        await WaitUntil(() => tab.IsLoaded);
        resources.TabsFactory.CloseDockable(tab);
        await WaitUntil(() => !resources.Tabs.Contains(tab));
        document.InspectBack();
        Pump();
        document.InspectForward();
        Pump();

        Assert.Equal(Code, behaviour.Find("AssetData.Graph")!.GetValue());
        Assert.Equal(Code, window.GetVisualDescendants().OfType<TextEditor>().Single().Text);
        Assert.Equal(Code, ((IAsset)graph).GetData<BehaviourGraphData>().Graph);
        window.Close();
    }
}
