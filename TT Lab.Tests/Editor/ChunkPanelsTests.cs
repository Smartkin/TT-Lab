using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Composite;
using TT_Lab.Views;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// Scene tabs only have their viewport, the chunk's resources and inspector are panels showing the scene last worked on
[Collection(ProjectCollection.Name)]
public sealed class ChunkPanelsTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly Package _package;

    public ChunkPanelsTests()
    {
        _package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(_package.Name, "levels"), Path.Combine(_package.Name, "Graphics"));
        var crash = _project.Add(new GameObject(), "Crash", 0x0);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
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

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 250 && !condition(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    // The panel's header (a Border) comes before it
    private static Decorator InspectorHost(ChunkInspectorView view) => view.GetVisualDescendants().OfType<Decorator>().Single(decorator => decorator.Name == "InspectorHost");

    private static async Task<TabbedEditorViewModel> OpenScene(ScenesEditorsViewModel scenes, LevelChunk chunk)
    {
        scenes.OpenEditor(chunk);
        var tab = scenes.Tabs.Single(tab => tab.EditableResource == chunk.URI);
        await WaitUntil(() => tab.IsLoaded);
        return tab;
    }

    // Following a link in the inspector shows the way back in the panel's header, with the shared asset a chunk's version is of
    [AvaloniaFact]
    public async Task TheInspectorsHeaderGoesBackAlongFollowedLinks()
    {
        var scenes = new ScenesEditorsViewModel();
        var inspector = new ChunkInspectorViewModel(scenes);
        var inspectorView = new ChunkInspectorView { DataContext = inspector };
        var window = new Window { Content = inspectorView, Width = 500, Height = 600 };
        window.Show();
        var tab = await OpenScene(scenes, CreateChunk("first"));
        var document = tab.Document!;
        var instance = document.PropertyGraph.Find("Root.ChunkResources[3][data]")!;
        document.OpenInspector(instance);
        document.FollowInInspector(instance.Find("AssetData.ObjectId[data]")!);
        await WaitUntil(() => inspector.Inspected == document.Inspector);
        Button? Find(string content) => inspectorView.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Content as string == content && button.IsEffectivelyVisible);
        await WaitUntil(() => Find("Open the shared asset") != null);

        List<Button> Crumbs() => inspectorView.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("crumb")).ToList();
        await WaitUntil(() => Crumbs().Count == 2);
        var crumbs = Crumbs();
        Assert.Equal(["Instance 0", "Crash"], crumbs.Select(crumb => crumb.Content as string));
        Assert.Contains("current", crumbs[1].Classes);

        Find("◀")!.Command!.Execute(null);
        await WaitUntil(() => inspector.Inspected?.Property == instance);
        await WaitUntil(() => Find("Open the shared asset") == null);
        Assert.True(Find("▶")!.IsEffectivelyEnabled);
        window.Close();
    }

    // An instance made in a scene and taken out again by undo never gets saved: it stayed in the project, and once an editor closing
    // released its data it couldn't be opened, the tab stayed loading
    [AvaloniaFact]
    public async Task UnplacedInstancesLeaveWithTheirScene()
    {
        var scenes = new ScenesEditorsViewModel();
        var chunk = CreateChunk("first");
        var tab = await OpenScene(scenes, chunk);
        var made = tab.Viewport!.CreateResource(typeof(Position), 0)!;
        var kept = tab.Viewport!.CreateResource(typeof(Trigger), 0)!;
        tab.Document!.Undo();
        Assert.DoesNotContain(kept.URI, chunk.ChunkResources);

        // Data nobody saved yet stays when editors let go of what they don't use
        made.UnloadData();
        Assert.NotNull(((IAsset)made).GetData<TT_Lab.AssetData.Instance.PositionData>());

        tab.SaveTab();
        scenes.TabsFactory.CloseDockable(tab);
        await WaitUntil(() => !scenes.Tabs.Contains(tab));
        Assert.False(_project.AssetManager.DoesAssetExist(kept.URI));
        Assert.True(_project.AssetManager.DoesAssetExist(made.URI));
    }

    [AvaloniaFact]
    public async Task PanelsShowTheSceneLastWorkedOn()
    {
        var scenes = new ScenesEditorsViewModel();
        var resources = new ChunkResourcesViewModel(scenes);
        var inspector = new ChunkInspectorViewModel(scenes);
        var view = new ChunkResourcesView { DataContext = resources };
        var inspectorView = new ChunkInspectorView { DataContext = inspector };
        var window = new Window { Content = new StackPanel { Children = { view, inspectorView } }, Width = 400, Height = 600 };
        window.Show();
        Assert.Null(resources.Document);

        var first = await OpenScene(scenes, CreateChunk("first"));
        await WaitUntil(() => resources.Document == first.Document);
        // The panel's view gets loaded on a later pass of the dispatcher than its view model gets the scene
        await WaitUntil(() => view.FindDescendantOfType<Decorator>()?.Child != null);
        var firstView = view.FindDescendantOfType<Decorator>()!.Child;
        Assert.NotNull(firstView);

        var second = await OpenScene(scenes, CreateChunk("second"));
        await WaitUntil(() => resources.Document == second.Document);

        var resource = second.Document!.PropertyGraph.Find("Root.ChunkResources[0]")!;
        second.Document.OpenInspector(resource.Find("[data]"));
        await WaitUntil(() => inspector.Inspected == second.Document.Inspector);
        Assert.NotNull(inspector.Inspected);
        var secondInspectorView = InspectorHost(inspectorView).Child;
        Assert.NotNull(secondInspectorView);

        // Going back shows the first scene's view as it was left and what that scene inspects
        scenes.TabsFactory.ActivateEditor(first);
        await WaitUntil(() => resources.Document == first.Document);
        Assert.Same(firstView, view.FindDescendantOfType<Decorator>()!.Child);
        Assert.Null(inspector.Inspected);
        Assert.NotSame(secondInspectorView, InspectorHost(inspectorView).Child);

        // The inspector's views aren't built again either, the default chunk's 255 particle systems took seconds each time
        scenes.TabsFactory.RemoveEditor(first);
        await WaitUntil(() => resources.Document == second.Document);
        Assert.Same(second.Document.Inspector, inspector.Inspected);
        Assert.Same(secondInspectorView, InspectorHost(inspectorView).Child);

        scenes.TabsFactory.RemoveEditor(second);
        await WaitUntil(() => resources.Document == null);
        Assert.Null(inspector.Inspected);
        window.Close();
    }
}
