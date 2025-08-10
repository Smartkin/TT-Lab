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
    private readonly Dictionary<ModelBuffer, RenderBatch> _renderBatches = [];

    public void AddMeshToBatch(Mesh mesh)
    {
        foreach (var modelBuffer in mesh.GetModels())
        {
            if (!_renderBatches.TryGetValue(modelBuffer, out var value))
            {
                value = new RenderBatch(context, modelBuffer);
                _renderBatches.Add(modelBuffer, value);
            }

            value.AddToBatch(mesh);
        }
    }
    
    public IEnumerable<RenderBatch> GetRenderBatches() => _renderBatches.Values;
}