using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.Views.Editors;
using Twinsanity.TwinsanityInterchange.Common;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// The chunk's resources are shown like the project tree: a folder for each kind, the layouts under it, and a right click on a folder
// makes a new one of its kind
[Collection(ProjectCollection.Name)]
public sealed class ChunkResourcesTreeTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private LevelChunk CreateChunk()
    {
        var package = _project.Project.Ps2Package;
        _project.BuildProjectTree(Path.Combine(package.Name, "levels"));
        var crash = _project.Add(new GameObject(), "Crash", 0x0);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);
        var folder = _project.GetFolder(package, "levels");
        var chunk = (LevelChunk)AssetFactory.CreateAsset(typeof(LevelChunk), folder, "beach", string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(), asset => AssetDataFactory.CreateChunkData(folder, asset))!;
        TwinIdGeneratorServiceProvider.RegisterGeneratorServiceForChunk(chunk);
        return chunk;
    }

    private T AddInstance<T>(LevelChunk chunk, string name, Func<IAsset, TT_Lab.AssetData.AbstractAssetData> data, int layout) where T : SerializableInstance, new()
    {
        var instance = _project.Add(new T { Chunk = chunk.AdditionalPath!, AdditionalPath = chunk.AdditionalPath, LayoutID = layout }, name, package: _project.Project.Ps2Package);
        instance.SetData(data(instance));
        chunk.ChunkResources.Add(instance.URI);
        return instance;
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static (DocumentViewModel Document, ChunkResourcesTreeViewModel Tree, Window Window) Open(LevelChunk chunk)
    {
        var viewport = new ViewportViewModel();
        var document = new DocumentViewModel(chunk, viewport);
        document.Initialize();
        viewport.Init(document);
        var tree = Assert.IsType<ChunkResourcesTreeViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.ChunkResources")!).Construct());
        var window = new Window { Content = new ContentControl { Content = tree }, Width = 400, Height = 700 };
        window.Show();
        Pump();
        return (document, tree, window);
    }

    private static ChunkResourceRow Folder(ChunkResourcesTreeViewModel tree, string caption) => tree.Folders.Single(folder => folder.Caption == caption);

    private static ViewportMenuEntry Entry(ChunkResourcesTreeViewModel tree, ChunkResourceRow row, string startsWith) =>
        tree.GetMenu(row).Single(entry => entry.Header.StartsWith(startsWith, StringComparison.Ordinal));

    [AvaloniaFact]
    public void ResourcesAreFoldersOfKindsAndLayouts()
    {
        var chunk = CreateChunk();
        AddInstance<Camera>(chunk, "Camera 0", asset => new CameraData(asset), 4);
        AddInstance<AiPosition>(chunk, "AI 0", asset => new AiPositionData(asset), 6);
        var (_, tree, window) = Open(chunk);

        // Every kind a level has, even without any yet, so a first one can be made
        Assert.Equal(["AI Path", "AI Position", "Camera", "Chunk Links", "Object Instance", "Particles", "Path", "Position", "Scenery", "Trigger"],
            tree.Folders.Select(folder => folder.Caption));
        var instances = Folder(tree, "Object Instance");
        var main = Assert.Single(instances.Children);
        Assert.Equal((ChunkResourceRowKind.LayoutFolder, "Layout 0 (Main)", "1"), (main.Kind, main.Caption, main.Count));
        Assert.Equal("Instance 0", Assert.Single(main.Children).Caption);
        Assert.Equal("Layout 4 (Cameras)", Assert.Single(Folder(tree, "Camera").Children).Caption);
        // Resources a chunk has once aren't in layouts
        Assert.Equal("Scenery 0", Assert.Single(Folder(tree, "Scenery").Children).Caption);
        Assert.Empty(Folder(tree, "Trigger").Children);

        // The tree shows them, with the icons of their kinds
        var view = window.GetVisualDescendants().OfType<ChunkResourcesTreeView>().Single();
        Assert.Equal(10, view.GetVisualDescendants().OfType<TreeViewItem>().Count());
        instances.IsExpanded = true;
        main.IsExpanded = true;
        // The layout's folder gets its item once the kind's is laid out, a pass each
        for (var pass = 0; pass < 10 && !view.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == "Instance 0"); pass++)
        {
            window.UpdateLayout();
            Pump();
        }

        Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Instance 0");
        window.Close();
    }

    [AvaloniaFact]
    public void FoldersMakeNewResourcesOfTheirKind()
    {
        var chunk = CreateChunk();
        AddInstance<Camera>(chunk, "Camera 0", asset => new CameraData(asset), 4);
        var (document, tree, window) = Open(chunk);

        // A folder only makes its own kind
        var triggerMenu = tree.GetMenu(Folder(tree, "Trigger"));
        Assert.Equal("Create Trigger", Assert.Single(triggerMenu).Header);
        triggerMenu[0].Action!();
        Pump();
        var trigger = Assert.Single(Folder(tree, "Trigger").Children);
        Assert.Equal("Layout 0 (Main)", trigger.Caption);
        var made = Assert.Single(trigger.Children);
        Assert.StartsWith("New Trigger", made.Caption);
        // Picked and inspected right away
        Assert.Same(made, tree.SelectedRow);
        Assert.Equal(made.Element!.Find("[data]"), document.Inspector?.Property);
        Assert.True(trigger.IsExpanded);

        // In the layout the chunk keeps the kind in, or the folder's
        Entry(tree, Folder(tree, "Camera"), "Create Camera").Action!();
        Assert.Equal(2, Folder(tree, "Camera").Children.Single().Children.Count);
        Assert.All(chunk.ChunkResources.Select(AssetManager.Get().GetAsset).OfType<Camera>(), camera => Assert.Equal(4, camera.LayoutID));
        var main = Folder(tree, "Object Instance").Children.Single();
        Assert.Equal("Create Object Instance of Crash in layout 0", Entry(tree, main, "Create Object Instance").Header);
        Entry(tree, main, "Create Object Instance").Action!();
        Assert.Equal(2, main.Children.Count);

        // Paths get the points the game's shortest ones have, at the cursor
        Entry(tree, Folder(tree, "Path"), "Create Path").Action!();
        var path = chunk.ChunkResources.Select(AssetManager.Get().GetAsset).OfType<TT_Lab.Assets.Instance.Path>().Single();
        Assert.Equal(4, ((IAsset)path).GetData<PathData>().Points.Count);

        // AI paths join two AI positions picked in the scene, links and emitters go into the chunk's own lists
        Assert.False(Entry(tree, Folder(tree, "AI Path"), "Create AI Path").IsEnabled);
        Entry(tree, Folder(tree, "Chunk Links"), "Add a chunk link").Action!();
        Entry(tree, Folder(tree, "Particles").Children.Single(), "Add a particle emitter").Action!();
        var links = chunk.ChunkResources.Select(AssetManager.Get().GetAsset).OfType<ChunkLinks>().Single();
        Assert.Single(((IAsset)links).GetData<ChunkLinksData>().Links);

        // Each is a step, undoing takes them out of the tree again
        document.Undo();
        document.Undo();
        Assert.Empty(((IAsset)links).GetData<ChunkLinksData>().Links);
        document.Undo();
        Assert.Empty(Folder(tree, "Path").Children);
        document.Undo();
        Assert.Single(main.Children);
        document.Undo();
        Assert.Single(Folder(tree, "Camera").Children.Single().Children);
        document.Undo();
        Assert.Empty(Folder(tree, "Trigger").Children);
        Assert.False(document.CanUndo);
        window.Close();
    }

    // The inspector showed a resource whose placing got undone, and editing it changed nothing the chunk had while its steps pointed at
    // whatever came to its place in the list
    [AvaloniaFact]
    public void TheInspectorLeavesResourcesTakenOut()
    {
        var chunk = CreateChunk();
        var (document, tree, window) = Open(chunk);
        Entry(tree, Folder(tree, "Trigger"), "Create Trigger").Action!();
        Pump();
        var made = Assert.IsAssignableFrom<IAsset>(document.Inspector?.Property.Target);
        Assert.IsType<Trigger>(made);

        document.Undo();
        Pump();
        Assert.Null(document.Inspector);
        Assert.Null(tree.SelectedRow);

        // A resource deleted closes the inspector too, put back by undo it is the same asset to inspect
        var instance = Folder(tree, "Object Instance").Children.Single().Children.Single();
        tree.SelectedRow = instance;
        var inspected = document.Inspector!.Property.Target;
        tree.Delete(instance);
        Pump();
        Assert.Null(document.Inspector);
        document.Undo();
        Pump();
        Assert.Null(document.Inspector);
        tree.SelectedRow = Folder(tree, "Object Instance").Children.Single().Children.Single();
        Assert.Same(inspected, document.Inspector!.Property.Target);
        window.Close();
    }

    [AvaloniaFact]
    public void ResourcesGetRenamedDeletedAndFollowTheInspector()
    {
        var chunk = CreateChunk();
        var position = AddInstance<Position>(chunk, "Spot", asset => new PositionData(asset) { Coords = new Vector3(1, 2, 3) }, 0);
        var (document, tree, window) = Open(chunk);
        var row = Folder(tree, "Position").Children.Single().Children.Single();

        // Something inspected elsewhere, like the scene's selection, is picked here with its folders open
        document.OpenInspector(row.Element!.Find("[data]"));
        Pump();
        Assert.Same(row, tree.SelectedRow);
        Assert.True(Folder(tree, "Position").IsExpanded);
        document.OpenInspector(null);
        Pump();
        Assert.Null(tree.SelectedRow);

        // Picking a row inspects it
        tree.SelectedRow = Folder(tree, "Object Instance").Children.Single().Children.Single();
        Assert.Equal("Instance 0", (document.Inspector?.Property.Target as IAsset)?.Alias);

        // Renaming is a step of the document, and saved with it
        tree.BeginRename(row);
        Assert.True(row.IsRenaming);
        row.NewName = "Checkpoint";
        tree.EndRename(row, true);
        Assert.Equal("Checkpoint", position.Alias);
        Assert.Equal("Checkpoint", row.Caption);
        Assert.True(document.IsDirty);
        document.Undo();
        Assert.Equal("Spot", position.Alias);
        Assert.Equal("Spot", row.Caption);
        // A rename given up on changes nothing
        tree.BeginRename(row);
        row.NewName = "Nothing";
        tree.EndRename(row, false);
        Assert.Equal("Spot", position.Alias);

        // Only instances of layouts get deleted, the chunk's scenery stays
        Assert.DoesNotContain(tree.GetMenu(Folder(tree, "Scenery").Children.Single()), entry => entry.Header.StartsWith("Delete"));
        Entry(tree, row, "Delete").Action!();
        Assert.DoesNotContain(position.URI, chunk.ChunkResources);
        Assert.Empty(Folder(tree, "Position").Children);
        document.Undo();
        Assert.Contains(position.URI, chunk.ChunkResources);
        Assert.Equal("Spot", Folder(tree, "Position").Children.Single().Children.Single().Caption);
        window.Close();
    }
}
