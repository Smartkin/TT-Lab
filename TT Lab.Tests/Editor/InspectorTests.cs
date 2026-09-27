using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Rendering;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.Views.Editors;
using Twinsanity.TwinsanityInterchange.Common;
using Path = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.Tests.Editor;

[Collection(ProjectCollection.Name)]
public sealed class InspectorTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private DocumentViewModel OpenPath(int points)
    {
        var path = _project.Add(new Path(), "Path");
        path.SetData(new PathData(path) { Points = Enumerable.Range(0, points).Select(i => new Vector3(i, 0, 0)).ToList(), Parameters = [] });
        var document = new DocumentViewModel(path);
        document.Initialize();
        return document;
    }

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 800, Height = 700 };
        window.Show();
        Pump();
        return window;
    }

    // Views get activated and laid out a level at a time
    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static T GetNode<T>(DocumentCompositeViewModel parent, string name) where T : DocumentNodeViewModel
    {
        return (T)parent.Nodes.Single(node => node.Property.Name == name);
    }

    private static List<DockPanel> GetHighlighted(Window window)
    {
        return window.GetVisualDescendants().OfType<DockPanel>().Where(panel => panel.Classes.Contains("highlighted")).ToList();
    }

    // Documents are ReactiveUI objects, which have to be made after the headless application set up ReactiveUI's schedulers
    [AvaloniaFact]
    public void RemovingAnElementKeepsTheOnesAfterItAddressable()
    {
        var document = OpenPath(4);
        var points = document.PropertyGraph.Find("Root.AssetData.Points")!;

        points.RemoveElement(points.Children[1]);

        Assert.Equal(2.0f, document.PropertyGraph.Find("Root.AssetData.Points[1].X")!.GetValue<float>());
        Assert.Equal(3.0f, document.PropertyGraph.Find("Root.AssetData.Points[2].X")!.GetValue<float>());
        Assert.Null(document.PropertyGraph.Find("Root.AssetData.Points[3].X"));
    }

    [AvaloniaFact]
    public void LongListsOnlyGetEditorsForWhatsInView()
    {
        var document = OpenPath(3000);
        var window = Show(new DocumentView { DataContext = document });

        GetNode<DocumentCompositeViewModel>(document.Root, "Points").IsExpanded = true;
        Pump();

        var editors = window.GetVisualDescendants().OfType<Vector3FieldView>().Count();
        Assert.InRange(editors, 1, 100);
    }

    // An asset's data is laid out after the asset's own properties, without an editor of its own
    [AvaloniaFact]
    public void AnAssetsDataIsLaidOutWithItsProperties()
    {
        var document = OpenPath(4);
        var window = Show(new DocumentView { DataContext = document });

        var names = document.Root.Nodes.Select(node => node.Property.Name).ToList();
        Assert.Equal("Alias", names[0]);
        Assert.Equal("Points", names[^1]);
        Assert.DoesNotContain("AssetData", names);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Asset Data");
    }

    [AvaloniaFact]
    public void DataWithNothingToEditShowsNothing()
    {
        var chunk = new LevelChunk(_project.Project.GlobalPackagePS2.URI, "hub");
        chunk.SetData(new DummyData(chunk));
        var document = new DocumentViewModel(chunk);
        document.Initialize();
        var window = Show(new DocumentView { DataContext = document });

        Assert.DoesNotContain(document.Root.Nodes, node => node.Property.Path.StartsWith("Root.AssetData"));
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Asset Data");
    }

    // Selecting a point of a path in the viewport shows the path in the inspector with the point brought into view
    [AvaloniaFact]
    public void InspectorRevealsTheFocusedPart()
    {
        var document = OpenPath(200);
        var host = new ContentControl();
        var window = Show(host);
        var target = document.PropertyGraph.Find("Root.AssetData.Points[150]")!;

        document.OpenInspector(document.PropertyGraph.Find("Root.AssetData"), target);
        host.Content = document.Inspector;
        Pump();

        var highlighted = Assert.Single(GetHighlighted(window));
        Assert.Same(target, ((DocumentNodeViewModel)highlighted.DataContext!).Property);
        var position = highlighted.TranslatePoint(new Point(0, 0), window)!.Value;
        Assert.InRange(position.Y, 0, window.Height);
    }

    [AvaloniaFact]
    public void FocusingAnotherPartMovesTheHighlight()
    {
        var document = OpenPath(50);
        var host = new ContentControl();
        var window = Show(host);
        var assetData = document.PropertyGraph.Find("Root.AssetData");
        document.OpenInspector(assetData, document.PropertyGraph.Find("Root.AssetData.Points[3]"));
        host.Content = document.Inspector;
        Pump();
        var inspector = document.Inspector;

        var target = document.PropertyGraph.Find("Root.AssetData.Points[20]")!;
        document.OpenInspector(assetData, target);
        Pump();

        Assert.Same(inspector, document.Inspector);
        var highlighted = Assert.Single(GetHighlighted(window));
        Assert.Same(target, ((DocumentNodeViewModel)highlighted.DataContext!).Property);
    }

    // The list sits in scroll viewers nested in each other, and each of them only scrolls when the ones inside it already show the part
    [AvaloniaFact]
    public void FocusingAnotherPartAfterScrollingAwayBringsItIntoView()
    {
        var document = OpenPath(200);
        var host = new ContentControl();
        // Below the document like in the scene editor, which leaves the inspector too little room to show the whole list
        var grid = new Grid { RowDefinitions = RowDefinitions.Parse("*, 5, *") };
        Grid.SetRow(host, 2);
        grid.Children.Add(host);
        var window = Show(grid);
        var assetData = document.PropertyGraph.Find("Root.AssetData");
        document.OpenInspector(assetData, document.PropertyGraph.Find("Root.AssetData.Points[150]"));
        host.Content = document.Inspector;
        Pump();
        var list = GetScrollable(window).MaxBy(viewer => viewer.Extent.Height)!;
        list.Offset = new Vector(0, list.Extent.Height / 2);
        Pump();

        var target = document.PropertyGraph.Find("Root.AssetData.Points[20]")!;
        document.OpenInspector(assetData, target);
        Pump();

        var highlighted = Assert.Single(GetHighlighted(window));
        Assert.Same(target, ((DocumentNodeViewModel)highlighted.DataContext!).Property);
        var position = highlighted.TranslatePoint(new Point(0, 0), window)!.Value;
        Assert.InRange(position.Y, 0, window.Height - highlighted.Bounds.Height);
    }

    // Structs in a list differ a lot in size once one of them is expanded, a virtualizing panel's estimates of them made the list jump around
    [AvaloniaFact]
    public void ListsOfStructsKeepTheirPlaceWhileScrolling()
    {
        var links = _project.Add(new ChunkLinks { Chunk = "default" }, "Links");
        links.SetData(new ChunkLinksData(links) { Links = Enumerable.Range(0, 40).Select(_ => new ChunkLink()).ToList() });
        var document = new DocumentViewModel(links);
        document.Initialize();
        var host = new ContentControl();
        var grid = new Grid { RowDefinitions = RowDefinitions.Parse("*, 5, *") };
        Grid.SetRow(host, 2);
        grid.Children.Add(host);
        var window = Show(grid);
        var target = document.PropertyGraph.Find("Root.AssetData.Links[30]")!;
        document.OpenInspector(document.PropertyGraph.Find("Root.AssetData"), target);
        host.Content = document.Inspector;
        Pump();
        var highlighted = Assert.Single(GetHighlighted(window));
        Assert.Same(target, ((DocumentNodeViewModel)highlighted.DataContext!).Property);
        var list = GetScrollable(window).MaxBy(viewer => viewer.Extent.Height)!;
        var extent = list.Extent.Height;
        var top = highlighted.TranslatePoint(new Point(0, 0), window)!.Value.Y;
        Assert.True(list.Offset.Y > 0);

        // A bit at a time like the mouse wheel
        while (list.Offset.Y > 0)
        {
            var offset = Math.Max(list.Offset.Y - 50.0, 0.0);
            var moved = list.Offset.Y - offset;
            list.Offset = new Vector(0, offset);
            Pump();

            Assert.Equal(extent, list.Extent.Height);
            var newTop = highlighted.TranslatePoint(new Point(0, 0), window)!.Value.Y;
            Assert.Equal(top + moved, newTop, 0.5);
            top = newTop;
        }
    }

    // Views of what was scrolled out of sight get created again once it's back, they used to scroll to the revealed part every time
    [AvaloniaFact]
    public void ShowingTheInspectorAgainKeepsWhereTheUserScrolled()
    {
        var document = OpenPath(200);
        var host = new ContentControl();
        var window = Show(host);
        document.OpenInspector(document.PropertyGraph.Find("Root.AssetData"), document.PropertyGraph.Find("Root.AssetData.Points[150]"));
        host.Content = document.Inspector;
        Pump();
        Assert.Contains(GetScrollable(window), viewer => viewer.Offset.Y > 0);

        host.Content = null;
        Pump();
        host.Content = document.Inspector;
        Pump();

        Assert.All(GetScrollable(window), viewer => Assert.Equal(0, viewer.Offset.Y));
    }

    [AvaloniaFact]
    public void FocusedGroupGetsExpanded()
    {
        var document = OpenPath(10);
        var host = new ContentControl();
        Show(host);

        document.OpenInspector(document.PropertyGraph.Find("Root.AssetData"), document.PropertyGraph.Find("Root.AssetData.Points"));
        host.Content = document.Inspector;
        Pump();

        var points = GetNode<DocumentCompositeViewModel>((DocumentCompositeViewModel)document.Inspector!, "Points");
        Assert.True(points.IsHighlighted);
        Assert.True(points.IsExpanded);
        Assert.Equal(10, points.Nodes.Count);
    }

    [AvaloniaFact]
    public void InspectingWithoutFocusShowsTheInspectedExpanded()
    {
        var document = OpenPath(10);
        var host = new ContentControl();
        Show(host);

        document.OpenInspector(document.PropertyGraph.Find("Root.AssetData"));
        host.Content = document.Inspector;
        Pump();

        var inspector = Assert.IsAssignableFrom<DocumentCompositeViewModel>(document.Inspector);
        Assert.True(inspector.IsExpanded);
        Assert.False(inspector.IsHighlighted);
        Assert.NotEmpty(inspector.Nodes);
    }

    [AvaloniaFact]
    public void LayoutIsPickedFromTheOnesTheAssetCanBeIn()
    {
        var position = _project.Add(new Position { Chunk = "default", LayoutID = 0 }, "Position");
        position.SetData(new PositionData(position));
        var document = new DocumentViewModel(position);
        document.Initialize();
        var editor = Assert.IsType<LayoutFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.LayoutID")!).Construct());
        Show(new ContentControl { Content = editor });

        Assert.Equal([0, 1, 2, 3, 4, 5, 6], editor.Layouts.Select(layout => layout.Id));
        Assert.True(editor.CanChooseLayout);
        editor.SelectedLayout = editor.Layouts.Single(layout => layout.Name == "Crates");
        Pump();

        Assert.Equal(5, position.LayoutID);
        Assert.True(document.IsDirty);
    }

    // Collision surfaces are only in their own layout, which nothing else goes in
    [AvaloniaFact]
    public void CollisionSurfacesStayInTheirLayout()
    {
        var surface = _project.Add(new CollisionSurface { Chunk = "default", LayoutID = ChunkLayouts.CollisionSurfaces }, "Surface");
        surface.SetData(new CollisionSurfaceData(surface));
        var document = new DocumentViewModel(surface);
        document.Initialize();

        var editor = Assert.IsType<LayoutFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.LayoutID")!).Construct());

        var layout = Assert.Single(editor.Layouts);
        Assert.Equal(ChunkLayouts.CollisionSurfaces, layout.Id);
        Assert.Same(layout, editor.SelectedLayout);
        Assert.False(editor.CanChooseLayout);
    }

    private static List<ScrollViewer> GetScrollable(Window window)
    {
        return window.GetVisualDescendants().OfType<ScrollViewer>().Where(viewer => viewer.Extent.Height > viewer.Viewport.Height + 1).ToList();
    }

    [AvaloniaFact]
    public void ViewportToolbarSwitchesTools()
    {
        var viewport = new ViewportViewModel();
        Assert.True(viewport.IsSelectTool);

        viewport.SelectToolCommand.Execute(TransformMode.ROTATE).Subscribe();
        viewport.ToggleTransformSpaceCommand.Execute().Subscribe();

        Assert.True(viewport.IsRotateTool);
        Assert.False(viewport.IsSelectTool);
        Assert.True(viewport.IsWorldSpace);
        Assert.Equal("World", viewport.TransformSpaceName);
        viewport.Close();
    }

    // Objects are selected without their visuals, which tools can be used only depends on the properties they transform
    [AvaloniaFact]
    public void ToolsTheSelectionCantUseAreDisabledAndStoodInFor()
    {
        var document = OpenPath(1);
        var point = document.PropertyGraph.Find("Root.AssetData.Points[0]")!;
        var viewport = new ViewportViewModel();
        viewport.SelectToolCommand.Execute(TransformMode.SCALE).Subscribe();

        viewport.SelectObject(new ViewportObject(null!, "POINT", point) { Position = point }, false);

        Assert.True(viewport.CanTranslate);
        Assert.False(viewport.CanRotate);
        Assert.False(viewport.CanScale);
        Assert.True(viewport.IsTranslateTool);

        viewport.SelectToolCommand.Execute(TransformMode.ROTATE).Subscribe();
        Assert.True(viewport.IsTranslateTool);

        viewport.SelectObject(new ViewportObject(null!, "INSTANCE", point) { Position = point, Rotation = point }, false);
        Assert.True(viewport.CanRotate);
        Assert.False(viewport.CanScale);
        Assert.True(viewport.IsTranslateTool);

        // The chosen tool comes back once something can be transformed with it
        viewport.SelectObject(new ViewportObject(null!, "SCENERY", point) { Transform = point }, false);
        Assert.True(viewport.CanScale);
        Assert.True(viewport.IsScaleTool);

        // Nor what the inspector doesn't let be edited, like the load wall of a link that doesn't use it
        point.IsReadOnly = true;
        viewport.SelectObject(new ViewportObject(null!, "WALL", point) { Transform = point }, false);
        Assert.False(viewport.CanTranslate || viewport.CanRotate || viewport.CanScale);
        Assert.True(viewport.IsSelectTool);
        viewport.Close();
    }
}
