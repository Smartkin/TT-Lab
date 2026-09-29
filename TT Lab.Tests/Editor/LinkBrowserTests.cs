using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;

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

        var link = Show(document, "Root.AssetData.OnSpawnScriptId");
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
}
