using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Caliburn.Micro;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets.Instance;
using TT_Lab.Project.Prefabs;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.Views;

namespace TT_Lab.Tests.Editor;

// A prefab dragged out of the Prefabs panel lands in whichever of TT Lab's windows is under the pointer: Avalonia only told the window the
// drag started in, a floating panel's prefabs never reached the scene. The panel's search looks in the folder shown and the ones in it
[Collection(ProjectCollection.Name)]
public sealed class PrefabDragAndSearchTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly PrefabLibrary _library;
    private readonly Func<IEnumerable<TopLevel>> _windows = PrefabDropTargets.Windows;

    public PrefabDragAndSearchTests()
    {
        _library = new PrefabLibrary(_project.Project);
    }

    public void Dispose()
    {
        PrefabDropTargets.Windows = _windows;
        _project.Dispose();
    }

    // Takes what's dropped onto it, like a scene's viewport, and hears where a prefab is dragged over it
    private sealed class Scene : Border, IPrefabDropTarget
    {
        public List<(Prefab Prefab, PixelPoint Screen)> Dropped { get; } = [];

        public List<string> Heard { get; } = [];

        public bool CanDrop(Prefab prefab, Visual hit) => true;

        public void Drop(Prefab prefab, Visual hit, PixelPoint screen)
        {
            Dropped.Add((prefab, screen));
            Heard.Add($"dropped at {screen}");
        }

        public void DragOver(Prefab prefab, Visual hit, PixelPoint screen) => Heard.Add($"{prefab.Name} over {screen}");

        public void DragLeave() => Heard.Add("left");
    }

    // The window's cursor: a drag's override, else the one of what's under the pointer
    private static Cursor? CursorField(TopLevel window, string name) =>
        (Cursor?)typeof(TopLevel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);

    private static string? ShownCursor(TopLevel window) => (CursorField(window, "_cursorOverride") ?? CursorField(window, "_cursor"))?.ToString();

    private Prefab CratePrefab(string name, string folder = "") => new()
    {
        Name = name, Kind = PrefabKind.Instance, Platform = "PS2", Package = _project.Project.GlobalPackagePS2.URI, LayoutID = 0,
        AssetType = typeof(ObjectInstance).FullName!, DataType = typeof(ObjectInstanceData).FullName, Data = new JObject(), Folder = folder
    };

    private static PrefabsViewModel Panel() => new(new ScenesEditorsViewModel(), new EventAggregator(), new PrefabPictures());

    // Headless windows all start at the screen's corner whatever their position: the main window here is bigger than the panel's, a point
    // past the panel's window is over the main one only
    private static Window Show(Control content, double width, double height)
    {
        var window = new Window { Content = content, Width = width, Height = height };
        window.Show();
        return window;
    }

    // Hit tests go by what the compositor got last, and a commit waits for the render timer to have taken the one before it: right after
    // another test the windows just shown had nothing to hit yet
    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheWindowUnderThePointerTakesTheDrop()
    {
        var panel = Show(new Border { Background = Brushes.Gray }, 300, 300);
        var scene = new Scene { Background = Brushes.Black };
        var main = Show(scene, 800, 400);
        Render();

        Assert.Same(scene, PrefabDropTargets.Find(new PixelPoint(600, 100), panel, [panel, main])?.Target);
        // Where the panel's window is, it's the one dropped onto, and it has nothing to drop onto there; nothing's beyond both
        Assert.Null(PrefabDropTargets.Find(new PixelPoint(100, 100), panel, [panel, main]));
        Assert.Null(PrefabDropTargets.Find(new PixelPoint(2000, 100), panel, [panel, main]));
        panel.Close();
        main.Close();
    }

    [AvaloniaFact]
    public void APrefabDraggedOutOfAFloatingPanelLandsInTheSceneUnderThePointer()
    {
        _library.Save(CratePrefab("Crate"));
        var panel = Panel();
        var view = new PrefabsView { DataContext = panel };
        var floating = Show(view, 500, 500);
        var scene = new Scene { Background = Brushes.Black };
        var main = Show(scene, 1400, 700);
        PrefabDropTargets.Windows = () => [floating, main];
        panel.Refresh();
        Render();

        var tile = view.GetVisualDescendants().OfType<ListBoxItem>().Single();
        var start = tile.TranslatePoint(new Point(60, 40), floating)!.Value;
        floating.MouseDown(start, MouseButton.Left);
        floating.MouseMove(start + new Point(20, 0), RawInputModifiers.LeftMouseButton);
        // Past the floating window's edge the pointer's events still go to it, the way the window manager hands them over
        var over = new Point(900, 150);
        floating.MouseMove(over, RawInputModifiers.LeftMouseButton);
        floating.MouseUp(over, MouseButton.Left);

        var dropped = Assert.Single(scene.Dropped);
        Assert.Equal("Crate", dropped.Prefab.Name);
        Assert.Equal(new PixelPoint(900, 150), dropped.Screen);
        floating.Close();
        main.Close();
    }

    // While the list keeps the pointer the window only took the cursor of what's under it when that was the list itself: the "can't drop"
    // cursor set as the drag started over a tile (X11's X) stayed all the way into the scene. The scene hears where the prefab is dragged
    // over it to show where it goes, and that it left before it's dropped onto
    [AvaloniaFact]
    public void TheCursorAndTheSceneFollowADraggedPrefab()
    {
        _library.Save(CratePrefab("Crate"));
        var panel = Panel();
        var view = new PrefabsView { DataContext = panel };
        var floating = Show(view, 500, 500);
        var scene = new Scene { Background = Brushes.Black };
        var main = Show(scene, 1400, 700);
        PrefabDropTargets.Windows = () => [floating, main];
        panel.Refresh();
        Render();

        var tile = view.GetVisualDescendants().OfType<ListBoxItem>().Single();
        var start = tile.TranslatePoint(new Point(60, 40), floating)!.Value;
        floating.MouseDown(start, MouseButton.Left);
        floating.MouseMove(start + new Point(20, 0), RawInputModifiers.LeftMouseButton);
        Assert.Equal(nameof(StandardCursorType.No), ShownCursor(floating));

        floating.MouseMove(new Point(900, 150), RawInputModifiers.LeftMouseButton);
        Assert.Equal(nameof(StandardCursorType.DragCopy), ShownCursor(floating));
        floating.MouseMove(new Point(950, 200), RawInputModifiers.LeftMouseButton);
        floating.MouseMove(start, RawInputModifiers.LeftMouseButton);
        Assert.Equal(nameof(StandardCursorType.No), ShownCursor(floating));
        floating.MouseMove(new Point(1000, 300), RawInputModifiers.LeftMouseButton);
        floating.MouseUp(new Point(1000, 300), MouseButton.Left);

        Assert.Equal(["Crate over 900, 150", "Crate over 950, 200", "left", "Crate over 1000, 300", "left", "dropped at 1000, 300"], scene.Heard);
        Assert.Null(CursorField(floating, "_cursorOverride"));

        // Given up with Escape the scene hears it left, nothing's dropped
        scene.Heard.Clear();
        floating.MouseDown(start, MouseButton.Left);
        floating.MouseMove(start + new Point(20, 0), RawInputModifiers.LeftMouseButton);
        floating.MouseMove(new Point(900, 150), RawInputModifiers.LeftMouseButton);
        floating.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.LeftMouseButton);
        floating.MouseUp(new Point(900, 150), MouseButton.Left);

        Assert.Equal(["Crate over 900, 150", "left"], scene.Heard);
        Assert.Null(CursorField(floating, "_cursorOverride"));
        floating.Close();
        main.Close();
    }

    // Dropped onto a folder's tile the prefab goes into the folder, let go of anywhere else nothing happens
    [AvaloniaFact]
    public void APrefabDroppedOntoAFolderGoesThere()
    {
        _library.Save(CratePrefab("Crate"));
        _library.CreateFolder(string.Empty, "Crates");
        var panel = Panel();
        var view = new PrefabsView { DataContext = panel };
        var window = Show(view, 600, 500);
        PrefabDropTargets.Windows = () => [window];
        panel.Refresh();
        Render();

        var tiles = view.GetVisualDescendants().OfType<ListBoxItem>().ToList();
        var crate = tiles.Single(tile => tile.DataContext is PrefabEntry).TranslatePoint(new Point(60, 40), window)!.Value;
        var folder = tiles.Single(tile => tile.DataContext is PrefabFolderEntry).TranslatePoint(new Point(60, 40), window)!.Value;
        window.MouseDown(crate, MouseButton.Left);
        window.MouseMove(crate + new Point(0, 20), RawInputModifiers.LeftMouseButton);
        window.MouseMove(folder, RawInputModifiers.LeftMouseButton);
        window.MouseUp(folder, MouseButton.Left);

        Assert.Empty(_library.Load(string.Empty));
        Assert.Equal(["Crate"], _library.Load("Crates").Select(prefab => prefab.Name));
        window.Close();
    }

    [AvaloniaFact]
    public void TheSearchLooksInTheFolderShownAndTheFoldersInIt()
    {
        _library.Save(CratePrefab("CRAB", "levels/beach"));
        _library.Save(CratePrefab("CRAB", "levels/cove"));
        _library.Save(CratePrefab("Rock", "levels/beach/Meshes"));
        _library.Save(CratePrefab("Crate"));
        var panel = Panel();

        panel.SearchText = "crab";
        panel.Refresh();
        Assert.Equal(["CRAB", "CRAB"], panel.Prefabs.Select(entry => entry.Name));
        Assert.Equal(["levels/beach", "levels/cove"], panel.Prefabs.Select(entry => entry.Location));
        Assert.Equal("2 prefabs", panel.ListStatus);

        // Every word typed has to be in a prefab's folders and name
        panel.SearchText = "beach CRAB";
        panel.Refresh();
        Assert.Equal(["levels/beach"], panel.Prefabs.Select(entry => entry.Location));

        // Folders are found by their names
        panel.SearchText = "cove";
        panel.Refresh();
        Assert.Equal(["cove"], panel.Folders.Select(entry => entry.Name));
        Assert.Equal(["levels"], panel.Folders.Select(entry => entry.Location));
        Assert.Equal("1 prefab and 1 folder", panel.ListStatus);

        // Opening a folder shows it, the search then looks only in it
        panel.OpenFolder("levels/beach");
        Assert.Equal(string.Empty, panel.SearchText);
        panel.SearchText = "r";
        panel.Refresh();
        Assert.Equal(["CRAB", "Rock"], panel.Prefabs.Select(entry => entry.Name));
        Assert.Equal(["Here", "Meshes"], panel.Prefabs.Select(entry => entry.Location));

        panel.SearchText = "boulder";
        panel.Refresh();
        Assert.Empty(panel.Items);
        Assert.Equal("Nothing here has that", panel.ListStatus);

        panel.ClearSearchCommand.Execute().Subscribe();
        Assert.Equal(["Meshes"], panel.Folders.Select(entry => entry.Name));
        Assert.Null(panel.Folders.Single().Location);
    }

    // Only the first prefabs found are read, how many there are is still told
    [Fact]
    public void TheSearchReadsTheFirstOnes()
    {
        foreach (var number in Enumerable.Range(1, 12))
        {
            _library.Save(CratePrefab($"Mesh {number}", "levels/beach/Meshes"));
        }

        var (folders, prefabs, matched) = _library.Search(string.Empty, ["mesh"], 5);

        Assert.Equal(["levels/beach/Meshes"], folders);
        Assert.Equal(12, matched);
        Assert.Equal(["Mesh 1", "Mesh 2", "Mesh 3", "Mesh 4", "Mesh 5"], prefabs.Select(prefab => prefab.Name));
    }
}
