using System.Diagnostics;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.Tests.Editor;

// Paths of the property graph's nodes follow the parent's, so an element put into or taken out of a big list moves the ones after it
// without the whole graph being renamed and indexed again: fifty instances deleted and undone took seconds in a chunk before
[Collection(ProjectCollection.Name)]
public sealed class PropertyGraphListTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private (DocumentViewModel Document, PropertyNode Resources) OpenChunkWith(int instances)
    {
        var crash = _project.Add(new GameObject(), "Crash", 0x0);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
        var chunk = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/hub/big" }, "big");
        for (var i = 0; i < instances; i++)
        {
            var instance = _project.Add(new ObjectInstance { Chunk = chunk.AdditionalPath!, AdditionalPath = chunk.AdditionalPath, LayoutID = 0 }, $"Inst{i}", (uint)i);
            instance.SetData(new ObjectInstanceData(instance) { ObjectId = crash.URI });
            chunk.ChunkResources.Add(instance.URI);
        }

        var document = new DocumentViewModel(chunk);
        document.Initialize();
        return (document, document.PropertyGraph.Root.Find(nameof(LevelChunk.ChunkResources))!);
    }

    [Fact]
    public void ElementsMovedByAChangeKeepTheirPathsRightAndFindable()
    {
        var (document, resources) = OpenChunkWith(6);
        var third = resources.Children[3];
        var thirdPosition = third.Find("[data].AssetData.Position")!;
        var last = resources.Children[5];
        var value = resources.Children[1].GetValue()!;

        resources.RemoveElement(resources.Children[1]);

        Assert.Equal("Root.ChunkResources[2]", third.Path);
        Assert.Equal("Root.ChunkResources[2][data].AssetData.Position", thirdPosition.Path);
        Assert.Equal("Root.ChunkResources[4]", last.Path);
        Assert.Same(third, document.PropertyGraph.Find("Root.ChunkResources[2]"));
        Assert.Same(thirdPosition, document.PropertyGraph.Find("Root.ChunkResources[2][data].AssetData.Position"));
        Assert.Null(document.PropertyGraph.Find("Root.ChunkResources[5]"));

        var inserted = resources.InsertElement(0, value)!;

        Assert.Equal("Root.ChunkResources[0]", inserted.Path);
        Assert.Equal("Root.ChunkResources[3]", third.Path);
        Assert.Equal("Root.ChunkResources[3][data].AssetData.Position", thirdPosition.Path);
        Assert.Same(inserted.Find("[data]"), document.PropertyGraph.Find("Root.ChunkResources[0][data]"));
        Assert.NotNull(document.PropertyGraph.Find("Root.ChunkResources[0][data].AssetData.ObjectId"));
        Assert.Same(document.PropertyGraph.Root, document.PropertyGraph.Find("Root"));
        Assert.Null(document.PropertyGraph.Find("Elsewhere.ChunkResources[0]"));
    }

    [Fact]
    public void UndoingManyDeletionsInABigChunkIsQuick()
    {
        var (document, resources) = OpenChunkWith(1500);
        var watch = Stopwatch.StartNew();
        using (document.History.BeginGroup("delete 50"))
        {
            for (var i = 0; i < 50; i++)
            {
                resources.RemoveElement(resources.Children[100]);
            }
        }

        document.Undo();
        document.Redo();
        document.Undo();

        // Took 18 seconds when every change renamed and indexed the whole graph, tens of milliseconds now, well under a second on any machine
        Assert.True(watch.ElapsedMilliseconds < 2000, $"{watch.ElapsedMilliseconds} ms");
        Assert.Equal(1500, resources.Children.Count);
        Assert.Equal("Root.ChunkResources[1499]", resources.Children[1499].Path);
        Assert.Same(resources.Children[100], document.PropertyGraph.Find("Root.ChunkResources[100]"));
    }
}
