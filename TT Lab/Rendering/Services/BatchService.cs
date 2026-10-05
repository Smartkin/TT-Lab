using System.Collections.Generic;
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
}