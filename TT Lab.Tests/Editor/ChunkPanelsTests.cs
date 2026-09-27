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

    private static async Task<TabbedEditorViewModel> OpenScene(ScenesEditorsViewModel scenes, LevelChunk chunk)
    {
        scenes.OpenEditor(chunk);
        var tab = scenes.Tabs.Single(tab => tab.EditableResource == chunk.URI);
        await WaitUntil(() => tab.IsLoaded);
        return tab;
    }

    [AvaloniaFact]
    public async Task PanelsShowTheSceneLastWorkedOn()
    {
        var scenes = new ScenesEditorsViewModel();
        var resources = new ChunkResourcesViewModel(scenes);
        var inspector = new ChunkInspectorViewModel(scenes);
        var view = new ChunkResourcesView { DataContext = resources };
        var window = new Window { Content = view, Width = 400, Height = 600 };
        window.Show();
        Assert.Null(resources.Document);

        var first = await OpenScene(scenes, CreateChunk("first"));
        await WaitUntil(() => resources.Document == first.Document);
        var firstView = view.FindDescendantOfType<Decorator>()!.Child;
        Assert.NotNull(firstView);

        var second = await OpenScene(scenes, CreateChunk("second"));
        await WaitUntil(() => resources.Document == second.Document);

        var resource = second.Document!.PropertyGraph.Find("Root.ChunkResources[0]")!;
        second.Document.OpenInspector(resource.Find("[data]"));
        await WaitUntil(() => inspector.Inspected == second.Document.Inspector);
        Assert.NotNull(inspector.Inspected);

        // Going back shows the first scene's view as it was left and what that scene inspects
        scenes.TabsFactory.ActivateEditor(first);
        await WaitUntil(() => resources.Document == first.Document);
        Assert.Same(firstView, view.FindDescendantOfType<Decorator>()!.Child);
        Assert.Null(inspector.Inspected);

        scenes.TabsFactory.RemoveEditor(first);
        await WaitUntil(() => resources.Document == second.Document);
        Assert.Same(second.Document.Inspector, inspector.Inspected);

        scenes.TabsFactory.RemoveEditor(second);
        await WaitUntil(() => resources.Document == null);
        Assert.Null(inspector.Inspected);
        window.Close();
    }
}
