using System.Collections.Generic;
using System.Diagnostics;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;

namespace TT_Lab.Rendering.Services;

public class BatchService(RenderContext context)
{
    public BatchStorage GenerateBatchStorage()
    {
        return new BatchStorage(context);
    }
}

public class BatchStorage(RenderContext context)
{
    public delegate void NewBatchCreatedHandler(RenderBatch renderBatch);
    
    public event NewBatchCreatedHandler? NewBatchCreated;
    
    // A preview's meshes are drawn in passes of their own, never with the scene's of the same model
    private readonly Dictionary<(ModelBuffer, bool), RenderBatch> _renderBatches = [];

    public void AddMeshToBatch(Mesh mesh)
    {
        var isPreview = mesh.IsInPreview;
        foreach (var modelBuffer in mesh.GetModels())
        {
            Debug.Assert(!modelBuffer.IsReleased, $"Drawing a released model of {mesh.Name}");
            if (!_renderBatches.TryGetValue((modelBuffer, isPreview), out var renderBatch))
            {
                renderBatch = new RenderBatch(context, modelBuffer, isPreview);
                _renderBatches.Add((modelBuffer, isPreview), renderBatch);
                NewBatchCreated?.Invoke(renderBatch);
            }

            renderBatch.AddToBatch(mesh);
        }
    }

    public void RemoveMeshFromBatch(Mesh mesh)
    {
        Removals++;
        var isPreview = mesh.IsInPreview;
        foreach (var modelBuffer in mesh.GetModels())
        {
            if (!_renderBatches.TryGetValue((modelBuffer, isPreview), out var renderBatch))
            {
                continue;
            }
            
            renderBatch.RemoveFromBatch(mesh);
        }
    }
    
    public IEnumerable<RenderBatch> GetRenderBatches() => _renderBatches.Values;

    /// <summary>
    /// Goes up whenever a mesh leaves the scene, which is when a model can stop being drawn
    /// </summary>
    public int Removals { get; private set; }

    /// <summary>
    /// Whether a mesh in the scene draws the model, shown or not
    /// </summary>
    public bool Draws(ModelBuffer model) => HasMeshes(model, false) || HasMeshes(model, true);

    private bool HasMeshes(ModelBuffer model, bool isPreview) => _renderBatches.TryGetValue((model, isPreview), out var batch) && batch.MeshCount > 0;

    /// <summary>
    /// Takes out the batches of a model that got released, which nothing draws any more, adding them to the ones dropped: batches stayed
    /// when their meshes went, and the passes kept drawing every one of them
    /// </summary>
    public void Drop(ModelBuffer model, List<RenderBatch> dropped)
    {
        Drop(model, false, dropped);
        Drop(model, true, dropped);
    }

    private void Drop(ModelBuffer model, bool isPreview, List<RenderBatch> dropped)
    {
        if (!_renderBatches.Remove((model, isPreview), out var batch))
        {
            return;
        }

        Debug.Assert(batch.MeshCount == 0, "Dropping a batch that still draws meshes");
        batch.Detach();
        dropped.Add(batch);
    }
}