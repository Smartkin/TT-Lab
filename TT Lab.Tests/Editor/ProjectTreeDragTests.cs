using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Caliburn.Micro;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.ResourceTree;
using TT_Lab.Views;

namespace TT_Lab.Tests.Editor;

// Assets get dragged out of the project tree onto what takes them, in whichever of TT Lab's windows is under the pointer like the Prefabs
// panel's prefabs: game objects onto a chunk's scene, what can move onto a folder of the tree
[Collection(ProjectCollection.Name)]
public sealed class ProjectTreeDragTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly Func<IEnumerable<TopLevel>> _windows = PrefabDropTargets.Windows;

    private readonly Func<string, string, IReadOnlyList<string>, Task<int?>> _ask = AssetRelocation.Ask;

    public void Dispose()
    {
        PrefabDropTargets.Windows = _windows;
        AssetRelocation.Ask = _ask;
        _project.Dispose();
    }

    private static ProjectManager Manager => Locator.Current.GetService<ProjectManager>()!;

    // Takes the game objects dropped onto it, like a chunk's scene
    private sealed class Scene : Border, IAssetDropTarget
    {
        public List<IAsset> Dropped { get; } = [];

        public List<string> Heard { get; } = [];

        public bool CanDropAsset(IAsset asset, Visual hit) => asset is GameObject;

        public void DropAsset(IAsset asset, Visual hit, PixelPoint screen) => Dropped.Add(asset);

        public void DragAssetOver(IAsset asset, Visual hit, PixelPoint screen) => Heard.Add($"{asset.Alias} over");

        public void DragAssetLeave() => Heard.Add("left");
    }

    private static Window Show(Control content, double width, double height)
    {
        var window = new Window { Content = content, Width = width, Height = height };
        window.Show();
        return window;
    }

    // Hit tests go by what the compositor got last, which waits for the render timer
    private static void Render()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    // The rows' items expanded down to the row of the alias
    private static Control ShowRow(ProjectTreeView view, params string[] aliases)
    {
        TreeViewItem? item = null;
        foreach (var alias in aliases)
        {
            Render();
            item = view.GetVisualDescendants().OfType<TreeViewItem>().First(candidate => (candidate.DataContext as ResourceTreeElementViewModel)?.Alias == alias);
            item.IsExpanded = true;
        }

        Render();
        return view.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == aliases[^1] && text.IsEffectivelyVisible);
    }

    private static void Drag(Window window, Control from, Point to)
    {
        var start = from.TranslatePoint(new Point(3, 3), window)!.Value;
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(20, 0), RawInputModifiers.LeftMouseButton);
        // Past the tree's window the pointer's events still go to it, the way the window manager hands them over
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        window.MouseUp(to, MouseButton.Left);
    }

    [AvaloniaFact]
    public void AGameObjectDraggedOutOfTheTreeLandsOnTheSceneUnderThePointer()
    {
        var crate = _project.Add(new GameObject(), "Crate", 0x10);
        crate.SetData(new GameObjectData(crate) { Name = "Crate" });
        _project.BuildProjectTree();
        var view = new ProjectTreeView { DataContext = new ProjectTreeViewModel(Manager, new EventAggregator()) };
        var floating = Show(view, 400, 600);
        var scene = new Scene { Background = Brushes.Black };
        var main = Show(scene, 1400, 700);
        PrefabDropTargets.Windows = () => [floating, main];

        var row = ShowRow(view, "assets", _project.Project.GlobalPackagePS2.Name, "GameObject", "Crate");
        Drag(floating, row, new Point(900, 150));

        Assert.Same(crate, Assert.Single(scene.Dropped));
        Assert.Equal(["Crate over", "left"], scene.Heard);

        // A folder's row isn't something a scene takes, a plain click is no drag
        scene.Heard.Clear();
        Drag(floating, ShowRow(view, "GameObject"), new Point(900, 150));
        Assert.Single(scene.Dropped);
        Assert.Empty(scene.Heard);
        floating.Close();
        main.Close();
    }

    [AvaloniaFact]
    public void ARowDroppedOntoAFolderMovesIntoIt()
    {
        var crate = _project.Add(new GameObject(), "Crate", 0x10);
        crate.SetData(new GameObjectData(crate) { Name = "Crate" });
        _project.BuildProjectTree($"{_project.Project.GlobalPackagePS2.Name}/Crates");
        Manager.WorkableProject = true;
        var asked = new List<string>();
        AssetRelocation.Ask = (title, _, answers) =>
        {
            asked.Add(title);
            return Task.FromResult<int?>(answers.Count == 0 ? null : 0);
        };
        var view = new ProjectTreeView { DataContext = new ProjectTreeViewModel(Manager, new EventAggregator()) };
        var window = Show(view, 400, 700);
        PrefabDropTargets.Windows = () => [window];
        var package = _project.Project.GlobalPackagePS2.Name;
        ShowRow(view, "assets", package, "Crates");
        var row = ShowRow(view, "GameObject", "Crate");
        var crates = view.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Crates" && text.IsEffectivelyVisible);
        var cratesRow = (ResourceTreeElementViewModel)crates.DataContext!;
        var start = row.TranslatePoint(new Point(3, 3), window)!.Value;
        var over = crates.TranslatePoint(new Point(5, 5), window)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(0, 20), RawInputModifiers.LeftMouseButton);
        window.MouseMove(over, RawInputModifiers.LeftMouseButton);
        // The folder it'd go into is marked while it's over it
        Assert.True(cratesRow.IsTargetItem);
        window.MouseUp(over, MouseButton.Left);
        Assert.False(cratesRow.IsTargetItem);
        for (var i = 0; i < 50 && crate.FolderInPackage != "Crates"; i++)
        {
            Render();
        }

        Assert.Equal("Crates", crate.FolderInPackage);
        Assert.Equal(["Move"], asked);
        Assert.Contains(cratesRow.GetInternalChildren()!, child => child.Asset == crate);
        window.Close();
    }
}
