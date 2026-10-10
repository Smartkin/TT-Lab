using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.Views;

namespace TT_Lab.Tests.Editor;

// A link's browser offers what the link can point at: an instance's spawn script the behaviour graphs, its instances, positions and
// paths only its own chunk's
[Collection(ProjectCollection.Name)]
public sealed class LinkBrowserTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private ObjectInstance AddInstance(string chunk, string name)
    {
        var instance = _project.Add(new ObjectInstance { Chunk = chunk, LayoutID = 0 }, name);
        instance.SetData(new ObjectInstanceData(instance));
        return instance;
    }

    private static UriLinkViewModel Show(DocumentViewModel document, string path)
    {
        var editor = EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct();
        new Window { Content = new ContentControl { Content = editor }, Width = 600, Height = 300 }.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        return Assert.IsType<UriLinkViewModel>(editor);
    }

    [AvaloniaFact]
    public void SpawnScriptsBrowseTheBehaviourGraphs()
    {
        var graph = _project.Add(new BehaviourGraph(), "COM_TEST", 5, _project.Project.Ps2Package);
        graph.SetData(new BehaviourGraphData(graph) { Graph = "graph COM_TEST { state Start { } }" });
        var instance = AddInstance("levels/test", "Instance");
        var document = new DocumentViewModel(instance);
        document.Initialize();

        var link = Show(document, "Root.AssetData.SpawnScript");
        var candidates = link.GetBrowseCandidates();

        Assert.Contains(graph.URI, candidates);
        Assert.DoesNotContain(instance.URI, candidates);
    }

    [AvaloniaFact]
    public void InstanceLinksBrowseTheirOwnChunk()
    {
        var own = AddInstance("levels/test", "Own");
        var neighbour = AddInstance("levels/test", "Neighbour");
        var elsewhere = AddInstance("levels/other", "Elsewhere");
        var position = _project.Add(new Position { Chunk = "levels/test", LayoutID = 0 }, "Position");
        position.SetData(new PositionData(position));
        var document = new DocumentViewModel(own);
        document.Initialize();
        document.PropertyGraph.Find("Root.AssetData.Instances")!.AddElement();
        document.PropertyGraph.Find("Root.AssetData.Positions")!.AddElement();

        var instances = Show(document, "Root.AssetData.Instances[0]").GetBrowseCandidates();
        var positions = Show(document, "Root.AssetData.Positions[0]").GetBrowseCandidates();

        Assert.Contains(neighbour.URI, instances);
        Assert.Contains(own.URI, instances);
        Assert.DoesNotContain(elsewhere.URI, instances);
        Assert.DoesNotContain(position.URI, instances);
        Assert.Equal([position.URI], positions);
    }

    // Links the game takes none in offer Empty, so a slot set once can be emptied again: an object's slots (65535 in its file), a surface's
    // sounds (0xFFFF) and a shader's texture (0, NoTexture). What the build needs an asset for doesn't, like an instance's object
    [AvaloniaFact]
    public void LinksTheGameTakesNoneInOfferEmpty()
    {
        var ogi = _project.Add(new OGI(), "Skeleton");
        ogi.SetData(new OGIData(ogi));
        var graph = _project.Add(new BehaviourGraph(), "COM_TEST", 5, _project.Project.Ps2Package);
        graph.SetData(new BehaviourGraphData(graph) { Graph = "graph COM_TEST { state Start { } }" });
        var crate = _project.Add(new GameObject(), "CRATE", 0x10, _project.Project.Ps2Package);
        var crateData = new GameObjectData(crate) { Name = "CRATE", BehaviourSlots = [graph.URI], ObjectSlots = [crate.URI], SoundSlots = [LabURI.Empty] };
        crateData.ModelSlots.Add(new ModelSlot { Ogi = ogi.URI });
        crate.SetData(crateData);
        var surface = _project.Add(new CollisionSurface { Chunk = "startup/default", LayoutID = 7 }, "SURF_DEFAULT");
        surface.SetData(new CollisionSurfaceData(surface));
        var material = _project.Add(new Material(), "Ground");
        var materialData = new MaterialData(material);
        materialData.Shaders.Add(new LabShader());
        material.SetData(materialData);
        DocumentViewModel Open(IAsset asset)
        {
            var document = new DocumentViewModel(asset);
            document.Initialize();
            return document;
        }

        var crateDocument = Open(crate);
        var surfaceDocument = Open(surface);
        var materialDocument = Open(material);
        foreach (var (document, path) in new[]
                 {
                     (crateDocument, "Root.AssetData.ModelSlots[0].Ogi"), (crateDocument, "Root.AssetData.BehaviourSlots[0]"),
                     (crateDocument, "Root.AssetData.ObjectSlots[0]"), (crateDocument, "Root.AssetData.SoundSlots[0]"),
                     (surfaceDocument, "Root.AssetData.ImpactSoundId"), (surfaceDocument, "Root.AssetData.StepSoundId1"),
                     (surfaceDocument, "Root.AssetData.StepSoundId2"), (surfaceDocument, "Root.AssetData.LandSoundId"),
                     (surfaceDocument, "Root.AssetData.HardImpactSoundId"), (surfaceDocument, "Root.AssetData.ScrapeSoundId"),
                     (materialDocument, "Root.AssetData.Shaders[0].TextureId")
                 })
        {
            Assert.True(Show(document, path).GetBrowseCandidates().Contains(LabURI.Empty), $"{path} doesn't offer Empty");
        }

        // The model slot set to a model gets Empty in its browser, the object slot keeps its kind of asset
        var slot = Show(crateDocument, "Root.AssetData.ModelSlots[0].Ogi");
        Assert.Contains(LabURI.Empty, new ResourceBrowserViewModel(typeof(OGI), slot.GetBrowseCandidates(), ogi.URI).ResourcesToBrowseView);
        Assert.DoesNotContain(graph.URI, Show(crateDocument, "Root.AssetData.ObjectSlots[0]").GetBrowseCandidates());

        var instance = AddInstance("levels/test", "Instance");
        var instanceDocument = Open(instance);
        Assert.DoesNotContain(LabURI.Empty, Show(instanceDocument, "Root.AssetData.ObjectId").GetBrowseCandidates());
    }

    // A double click on a row links it like picking it and pressing Link, one on the list's empty space doesn't
    [AvaloniaFact]
    public void ADoubleClickOnARowLinksIt()
    {
        var walk = _project.Add(new BehaviourGraph(), "COM_WALK", 5, _project.Project.Ps2Package);
        walk.SetData(new BehaviourGraphData(walk) { Graph = "graph COM_WALK { state Start { } }" });
        var run = _project.Add(new BehaviourGraph(), "COM_RUN", 7, _project.Project.Ps2Package);
        run.SetData(new BehaviourGraphData(run) { Graph = "graph COM_RUN { state Start { } }" });
        var browser = new ResourceBrowserViewModel(typeof(BehaviourGraph), [walk.URI, run.URI], walk.URI);
        var view = new ResourceBrowserView { DataContext = browser, Width = 400, Height = 300 };
        view.Show();
        // Clicks are hit tested by what the compositor got last (see PrefabDragAndSearchTests)
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var list = view.GetVisualDescendants().OfType<ListBox>().Single();
        var emptySpace = list.TranslatePoint(new Point(list.Bounds.Width / 2, list.Bounds.Height - 5), view)!.Value;
        DoubleClick(view, emptySpace);
        Assert.True(view.IsVisible);
        Assert.Equal(walk.URI, browser.SelectedLink);

        var row = list.GetVisualDescendants().OfType<ListBoxItem>().Single(item => Equals(item.DataContext, run.URI));
        DoubleClick(view, row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), view)!.Value);

        Assert.Equal(run.URI, browser.SelectedLink);
        Assert.False(view.IsVisible);
    }

    // The double tap comes with the second press, the browser may be closed by its release
    private static void DoubleClick(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        if (window.IsVisible)
        {
            window.MouseUp(point, MouseButton.Left);
        }

        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }
}

