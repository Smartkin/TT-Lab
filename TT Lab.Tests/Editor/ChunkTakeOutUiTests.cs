using Avalonia.Headless.XUnit;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Tests.Editor;

// Taking resources out of a chunk in its scene fixes the links of the chunk's other resources to them in the same step of its history: they
// stayed, and the chunk didn't build ("isn't one of the chunk's resources") until each was taken out by hand
[Collection(ProjectCollection.Name)]
public sealed class ChunkTakeOutUiTests : IDisposable
{
    private const string Beach = "levels/earth/hub/beach";

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private T AddInstance<T>(LevelChunk chunk, string name, UInt32 id, Func<IAsset, AbstractAssetData> data, int layout = 0) where T : SerializableInstance, new()
    {
        var instance = _project.Add(new T { Chunk = Beach, AdditionalPath = Beach, LayoutID = layout }, name, id, _project.Project.Ps2Package);
        instance.SetData(data(instance));
        chunk.ChunkResources.Add(instance.URI);
        return instance;
    }

    private static (DocumentViewModel Document, ViewportViewModel Viewport) Open(LevelChunk chunk)
    {
        var viewport = new ViewportViewModel();
        var document = new DocumentViewModel(chunk, viewport);
        document.Initialize();
        viewport.Init(document);
        return (document, viewport);
    }

    [AvaloniaFact]
    public void DeletingInTheSceneFixesTheLinksInTheSameStep()
    {
        var chunk = _project.Add(new LevelChunk { AdditionalPath = Beach }, "beach", 0, _project.Project.Ps2Package);
        var crab = AddInstance<ObjectInstance>(chunk, "Crab 0", 0, asset => new ObjectInstanceData(asset));
        var other = AddInstance<ObjectInstance>(chunk, "Crab 1", 1, asset => new ObjectInstanceData(asset) { Instances = [crab.URI] });
        var box = AddInstance<Trigger>(chunk, "Box", 0, asset => new TriggerData(asset) { Instances = [crab.URI, other.URI] });
        var (document, viewport) = Open(chunk);
        var resources = document.PropertyGraph.Root.Find(nameof(LevelChunk.ChunkResources))!;
        var steps = document.History.Root.Children.Count;

        viewport.DeleteResources([resources.Children[0]]);

        Assert.Equal([other.URI, box.URI], chunk.ChunkResources);
        Assert.Empty(((IAsset)other).GetData<ObjectInstanceData>().Instances);
        Assert.Equal([other.URI], ((IAsset)box).GetData<TriggerData>().Instances);
        // One step for all of it, named after taking the resource out
        Assert.Equal(steps + 1, document.History.Root.Children.Count);
        Assert.StartsWith("Removed", document.History.Current.Description);

        document.History.Undo();
        Assert.Equal([crab.URI, other.URI, box.URI], chunk.ChunkResources);
        Assert.Equal([crab.URI], ((IAsset)other).GetData<ObjectInstanceData>().Instances);
        Assert.Equal([crab.URI, other.URI], ((IAsset)box).GetData<TriggerData>().Instances);

        document.History.Redo();
        Assert.Equal([other.URI, box.URI], chunk.ChunkResources);
        Assert.Empty(((IAsset)other).GetData<ObjectInstanceData>().Instances);
        Assert.Equal([other.URI], ((IAsset)box).GetData<TriggerData>().Instances);
    }

    // An AI path can't do without either end, no instance has one to stand in: it keeps the link and the log says to link another
    [AvaloniaFact]
    public void AnAiPathKeepsAnEndTakenOut()
    {
        var chunk = _project.Add(new LevelChunk { AdditionalPath = Beach }, "beach", 0, _project.Project.Ps2Package);
        var from = AddInstance<AiPosition>(chunk, "From", 0, asset => new AiPositionData(asset), 6);
        var to = AddInstance<AiPosition>(chunk, "To", 1, asset => new AiPositionData(asset), 6);
        var path = AddInstance<AiPath>(chunk, "Path", 0, asset => new AiPathData(asset) { PathBegin = from.URI, PathEnd = to.URI }, 6);
        var (document, viewport) = Open(chunk);
        var resources = document.PropertyGraph.Root.Find(nameof(LevelChunk.ChunkResources))!;

        viewport.DeleteResources([resources.Children[0], resources.Children[1]]);

        Assert.Equal([path.URI], chunk.ChunkResources);
        var data = ((IAsset)path).GetData<AiPathData>();
        Assert.Equal((from.URI, to.URI), (data.PathBegin, data.PathEnd));
        document.History.Undo();
        Assert.Equal([from.URI, to.URI, path.URI], chunk.ChunkResources);
    }
}
