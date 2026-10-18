using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Serializer;
using Newtonsoft.Json;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.Project.Prefabs;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.ResourceTree;
using TT_Lab.Views;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// The Project Tree's crosshair (and Alt+Shift+L) picks the row of the asset of the editor last worked in, opening the folders down to it,
// or the row of what holds it when the tree doesn't list it. The folders stay open through file syncs and searches
[Collection(ProjectCollection.Name)]
public sealed class ProjectTreeLocateTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly ScenesEditorsViewModel _scenes = new();
    private readonly ResourcesEditorsViewModel _resources = new();

    public void Dispose() => _project.Dispose();

    private static ProjectManager Manager => Locator.Current.GetService<ProjectManager>()!;

    private Package Package => _project.Project.GlobalPackagePS2;

    private GameObject AddObject(string name, uint id, string? folder = null)
    {
        var gameObject = _project.Add(new GameObject { FolderInPackage = folder }, name, id);
        gameObject.SetData(new GameObjectData(gameObject) { Name = name });
        return gameObject;
    }

    private ProjectTreeViewModel CreateTree() => new(Manager, new TestProject.NullEventAggregator(), _scenes, _resources);

    // The row at the path of aliases from the top of the tree
    private static ResourceTreeElementViewModel Row(params string[] path)
    {
        IEnumerable<ResourceTreeElementViewModel> rows = Manager.FullProjectTree;
        ResourceTreeElementViewModel? row = null;
        foreach (var alias in path)
        {
            row = rows.Single(candidate => candidate.Alias == alias);
            rows = row.GetInternalChildren() ?? [];
        }

        return row!;
    }

    private string[] InPackage(params string[] path) => ["assets", Package.Name, .. path];

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 400, Height = 700 };
        window.Show();
        return window;
    }

    // Hit tests and the containers the tree makes go by layout passes and the render timer
    private static void Render()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 250 && !condition(); i++)
        {
            Render();
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private static TreeViewItem? ItemOf(Control view, ResourceTreeElementViewModel row) =>
        view.GetVisualDescendants().OfType<TreeViewItem>().FirstOrDefault(item => item.DataContext == row && item.IsEffectivelyVisible);

    private static List<string> ShownNames(Control view) =>
        view.GetVisualDescendants().OfType<TreeViewItem>().Where(item => item.IsEffectivelyVisible)
            .Select(item => item.DataContext).OfType<ResourceTreeElementViewModel>().Select(row => row.Alias).ToList();

    private static void Click(Window window, Control control)
    {
        Render();
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    public async Task TheCrosshairOpensTheFoldersDownToTheEditorsAssetPicksItAndFocusesIt()
    {
        var crate = AddObject("Crate", 0x10);
        AddObject("Barrel", 0x11);
        _project.BuildProjectTree();
        var tree = CreateTree();
        var view = new ProjectTreeView { DataContext = tree };
        var window = Show(view);
        var button = view.FindControl<Button>("LocateButton")!;
        Render();
        // Nothing to locate without an editor, the tooltip says why
        Assert.False(tree.CanLocate);
        Assert.False(button.IsEffectivelyEnabled);
        Assert.StartsWith("No editor is open", tree.LocateTip);

        _resources.OpenEditor(crate);
        var tab = _resources.Tabs.Single();
        await WaitUntil(() => tree.CanLocate && button.IsEffectivelyEnabled);
        Assert.Contains("Alt+Shift+L", tree.LocateTip);
        Assert.DoesNotContain("Crate", ShownNames(view));

        Click(window, button);

        var crateRow = Row(InPackage("GameObject", "Crate"));
        await WaitUntil(() => ItemOf(view, crateRow) is { IsSelected: true, IsKeyboardFocusWithin: true });
        Assert.Same(crateRow, tree.SelectedRow);
        Assert.True(Row("assets").IsExpanded);
        Assert.True(Row(InPackage()).IsExpanded);
        Assert.True(Row(InPackage("GameObject")).IsExpanded);
        // Only what's on the way to it
        Assert.False(Row("disc").IsExpanded);

        // With the editor closed there's nothing to locate again
        await WaitUntil(() => tab.IsLoaded);
        _resources.TabsFactory.CloseDockable(tab);
        await WaitUntil(() => !tree.CanLocate);
        window.Close();
    }

    // The tree lists neither a chunk's contents (its folder's row has no children) nor the parts of other assets: the row of what holds the
    // asset is picked, all the way up
    [AvaloniaFact]
    public void WhatTheTreeDoesntListIsLocatedAsWhatHoldsIt()
    {
        var crate = AddObject("Crate", 0x10);
        var defaultChunk = _project.Add(new LevelChunk { AdditionalPath = "startup/default" }, "default");
        var beach = _project.Add(new LevelChunk { AdditionalPath = "levels/beach" }, "beach", 0x1);
        _project.BuildProjectTree();
        // Saving the project would read their data, which they don't have
        var surface = _project.Add(new CollisionSurface { Chunk = "startup/default", LayoutID = ChunkLayouts.CollisionSurfaces }, "Surface");
        var scenery = _project.Add(new Scenery { Chunk = "levels/beach" }, "Scenery");
        var ground = _project.Add(new Material { AdditionalPath = "levels/beach" }, "Ground");
        var collision = _project.Add(new Collision { IsInternal = true, InternalOwner = scenery }, "Collision");
        var part = _project.Add(new Collision { IsInternal = true, InternalOwner = crate }, "Part");
        var tree = CreateTree();
        var defaultRow = Row(InPackage("startup", "default"));
        var beachRow = Row(InPackage("levels", "beach"));
        Assert.Null(defaultRow.Children);

        foreach (var (asset, row) in new (IAsset Asset, ResourceTreeElementViewModel Row)[]
                 {
                     // The default chunk's collision surface opens in an editor of its own, a scenery's material is in its chunk's folder
                     (surface, defaultRow), (ground, beachRow), (scenery, beachRow),
                     // The scenery's collision is its part, a chunk's scene and a package's settings are their folders
                     (collision, beachRow), (part, Row(InPackage("GameObject", "Crate"))), (beach, beachRow), (defaultChunk, defaultRow),
                     (Package, Row(InPackage()))
                 })
        {
            tree.Locate(asset);
            Assert.Same(row, tree.SelectedRow);
            Assert.True(row.Parent!.IsExpanded);
        }

        // Nothing holding it has a row: the pick stays
        var stray = _project.Add(new Collision { IsInternal = true }, "Stray");
        tree.Locate(stray);
        Assert.Same(Row(InPackage()), tree.SelectedRow);
    }

    // Alt+Shift+L reopens a closed Project Tree to locate, its view comes after the row got located
    [AvaloniaFact]
    public async Task ARowLocatedBeforeTheTreeIsShownIsRevealedOnceItIs()
    {
        var crate = AddObject("Crate", 0x10);
        _project.BuildProjectTree();
        var tree = CreateTree();
        tree.Locate(crate);
        var crateRow = Row(InPackage("GameObject", "Crate"));
        Assert.Same(crateRow, tree.SelectedRow);

        var view = new ProjectTreeView { DataContext = tree };
        var window = Show(view);

        await WaitUntil(() => ItemOf(view, crateRow) is { IsSelected: true, IsKeyboardFocusWithin: true });
        Assert.Null(tree.PendingReveal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ASearchIsClearedOnlyWhenItHidesTheRow()
    {
        var crate = AddObject("Crate", 0x10);
        AddObject("Barrel", 0x11);
        _project.BuildProjectTree();
        var tree = CreateTree();
        var view = new ProjectTreeView { DataContext = tree };
        var window = Show(view);
        Render();
        var crateRow = Row(InPackage("GameObject", "Crate"));
        var search = view.FindControl<TextBox>("SearchBar")!;

        search.Text = "Barrel";
        Assert.Equal("Barrel", tree.SearchAsset);
        tree.Locate(crate);
        Assert.Equal(string.Empty, tree.SearchAsset);
        Assert.Equal(string.Empty, search.Text);
        await WaitUntil(() => ItemOf(view, crateRow) is { IsSelected: true, IsKeyboardFocusWithin: true });

        search.Text = "Cra";
        tree.Locate(crate);
        Assert.Equal("Cra", tree.SearchAsset);
        Assert.Same(crateRow, tree.SelectedRow);
        await WaitUntil(() => ItemOf(view, crateRow) is { IsSelected: true } && !ShownNames(view).Contains("Barrel"));
        window.Close();
    }

    // Every sync of the file system gave the tree new items and closed every folder
    [AvaloniaFact]
    public async Task SyncsKeepTheFoldersOpen()
    {
        AddObject("Crate", 0x10);
        _project.BuildProjectTree();
        var tree = CreateTree();
        var view = new ProjectTreeView { DataContext = tree };
        var window = Show(view);
        Row(InPackage("GameObject")).IsExpanded = true;
        await WaitUntil(() => ShownNames(view).Contains("Crate"));

        // Another program writes an object
        var added = new GameObject { Package = Package.URI, InvariantName = "Added", Alias = "Added", Variation = string.Empty, ID = 0x12 };
        added.RegenerateUri();
        File.WriteAllText(Path.Combine(_project.AssetsPath, Package.Name, "GameObject", "Added.json"), JsonConvert.SerializeObject(added, Formatting.Indented));
        var shown = tree.ProjectTree;
        Manager.SyncProjectTree();
        Assert.NotSame(shown, tree.ProjectTree);
        // What the project manager's message does in TT Lab
        await tree.HandleAsync(new ProjectManagerMessage(nameof(ProjectManager.ProjectTree)), CancellationToken.None);

        await WaitUntil(() => ShownNames(view).Contains("Added"));
        Assert.Contains("Crate", ShownNames(view));
        Assert.True(Row("assets").IsExpanded);
        window.Close();
    }

    // A search shows what it finds in closed folders: opening every folder holding a match made the tree's view make a row for each of them
    // on every keystroke. Clearing it puts back the folders open before it and the ones down to what got picked
    [AvaloniaFact]
    public async Task ASearchShowsItsMatchesInClosedFoldersAndClearingItPutsBackTheOpenOnes()
    {
        AddObject("Crate", 0x10);
        AddObject("Crater", 0x11, "Extra");
        AddObject("Barrel", 0x12, "Props");
        _project.BuildProjectTree();
        var tree = CreateTree();
        var view = new ProjectTreeView { DataContext = tree };
        var window = Show(view);
        var objects = Row(InPackage("GameObject"));
        var extra = Row(InPackage("Extra"));
        var props = Row(InPackage("Props"));
        props.IsExpanded = true;
        await WaitUntil(() => ShownNames(view).Contains("Barrel"));
        Assert.False(objects.IsExpanded);
        Assert.False(extra.IsExpanded);

        tree.SearchAsset = "Cra";
        Render();

        Assert.False(Row("assets").IsExpanded);
        Assert.False(props.IsExpanded);
        Assert.False(objects.IsExpanded);
        Assert.False(extra.IsExpanded);
        Assert.Equal(tree.ProjectTree.Count, view.GetVisualDescendants().OfType<TreeViewItem>().Count());
        // A folder opened by hand shows what the search found in it
        objects.IsExpanded = true;
        await WaitUntil(() => ShownNames(view).Contains("Crate"));
        Assert.DoesNotContain("Crater", ShownNames(view));
        var crateRow = Row(InPackage("GameObject", "Crate"));
        Click(window, view.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Crate" && text.IsEffectivelyVisible));
        Assert.Same(crateRow, tree.SelectedRow);

        tree.SearchAsset = string.Empty;

        Assert.True(Row(InPackage()).IsExpanded);
        Assert.True(props.IsExpanded);
        Assert.True(objects.IsExpanded);
        Assert.False(extra.IsExpanded);
        Assert.Same(crateRow, tree.SelectedRow);
        await WaitUntil(() => ShownNames(view).Contains("Barrel") && ItemOf(view, crateRow) is { IsSelected: true });
        Assert.DoesNotContain("Crater", ShownNames(view));
        window.Close();
    }

    // Alt+Shift+L is the Window menu's Locate in Project Tree, which brings the tree to the front and locates
    [AvaloniaFact]
    public async Task AltShiftLLocatesFromTheShell()
    {
        var crate = AddObject("Crate", 0x10);
        _project.BuildProjectTree();
        var aggregator = new TestProject.NullEventAggregator();
        var log = new LogViewModel(aggregator, Manager);
        var tree = CreateTree();
        var chunkResources = new ChunkResourcesViewModel(_scenes);
        var inspector = new ChunkInspectorViewModel(_scenes);
        var history = new HistoryViewModel(_scenes, _resources);
        var prefabs = new PrefabsViewModel(_scenes, aggregator, new PrefabPictures());
        var panels = new Panels([_scenes, _resources, log, tree, chunkResources, inspector, history, prefabs]);
        var factory = new DockFactory(new DockSerializer(panels), _scenes, _resources, log, tree, chunkResources, inspector, history, prefabs);
        try
        {
            var shell = new ShellViewModel(null!, log, new EditorsViewModel(_scenes, _resources, Manager), tree, chunkResources, inspector, history, prefabs, factory,
                aggregator, Manager);
            // Not shown: closing the shell's window saves the preferences and the layout next to the executable
            var window = new ShellView(shell);
            var item = window.FindControl<MenuItem>("LocateInProjectTreeItem")!;
            Assert.Equal(new KeyGesture(Key.L, KeyModifiers.Alt | KeyModifiers.Shift), HotKeyManager.GetHotKey(item));
            Assert.Equal(new KeyGesture(Key.L, KeyModifiers.Alt | KeyModifiers.Shift), item.InputGesture);
            factory.SetActiveDockable(chunkResources);
            _resources.OpenEditor(crate);
            await WaitUntil(() => tree.CanLocate);

            window.KeyPress(Key.L, RawInputModifiers.Alt | RawInputModifiers.Shift, PhysicalKey.L, "L");

            Assert.Same(Row(InPackage("GameObject", "Crate")), tree.SelectedRow);
            Assert.Same(tree, tree.Owner is Dock.Model.Core.IDock dock ? dock.ActiveDockable : null);
            var tab = _resources.Tabs.Single();
            await WaitUntil(() => tab.IsLoaded);
            _resources.TabsFactory.CloseDockable(tab);
            await WaitUntil(() => !_resources.Tabs.Any());
        }
        finally
        {
            // The shell gives the log its panel
            Log.SetViewModel(null);
        }
    }

    // The scene viewport's one-key tools leave keys pressed with Alt to the window: Alt+Shift+L flipped the transform space as well
    [AvaloniaFact]
    public void TheViewportsToolKeysIgnoreAlt()
    {
        var viewport = new ViewportViewModel();
        Assert.False(viewport.IsWorldSpace);

        Assert.False(viewport.UseToolKey(Silk.NET.Input.Key.L, altPressed: true));
        Assert.False(viewport.UseToolKey(Silk.NET.Input.Key.R, altPressed: true));
        Assert.False(viewport.IsWorldSpace);
        Assert.True(viewport.IsSelectTool);

        Assert.True(viewport.UseToolKey(Silk.NET.Input.Key.L, altPressed: false));
        Assert.True(viewport.UseToolKey(Silk.NET.Input.Key.R, altPressed: false));
        Assert.True(viewport.IsWorldSpace);
        Assert.True(viewport.IsRotateTool);
        viewport.Close();
    }

    private sealed class Panels(object[] panels) : IServiceProvider
    {
        public object? GetService(Type serviceType) => panels.FirstOrDefault(panel => panel.GetType() == serviceType);
    }
}
