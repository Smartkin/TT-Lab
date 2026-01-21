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
    
    private readonly Dictionary<ModelBuffer, RenderBatch> _renderBatches = [];

    public void AddMeshToBatch(Mesh mesh)
    {
        foreach (var modelBuffer in mesh.GetModels())
        {
            if (!_renderBatches.TryGetValue(modelBuffer, out var renderBatch))
            {
                renderBatch = new RenderBatch(context, modelBuffer);
                _renderBatches.Add(modelBuffer, renderBatch);
                NewBatchCreated?.Invoke(renderBatch);
            }

            renderBatch.AddToBatch(mesh);
        }
    }

    public void RemoveMeshFromBatch(Mesh mesh)
    {
        foreach (var modelBuffer in mesh.GetModels())
        {
            if (!_renderBatches.TryGetValue(modelBuffer, out var renderBatch))
            {
                continue;
            }
            
            renderBatch.RemoveFromBatch(mesh);
        }
    }
    
    public IEnumerable<RenderBatch> GetRenderBatches() => _renderBatches.Values;
}