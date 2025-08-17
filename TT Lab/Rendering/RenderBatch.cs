using System;
using System.Collections.Generic;
using System.Linq;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;

namespace TT_Lab.Rendering;

public class RenderBatch : Renderable
{
    public event Action? RequestPassSwitch;
    
    private readonly ModelBuffer _batchedBuffer;
    private readonly List<Mesh> _meshes = [];

    public RenderBatch(RenderContext context, ModelBuffer batchedBuffer) : base(context)
    {
        _batchedBuffer = batchedBuffer;
        _batchedBuffer.MaterialReplaced += BatchedBufferOnMaterialReplaced;
    }

    private void BatchedBufferOnMaterialReplaced()
    {
        RequestPassSwitch?.Invoke();
    }

    public override (string, int)[] GetPriorityPasses()
    {
        return _batchedBuffer.GetPriorityPass();
    }

    public void AddToBatch(Mesh mesh)
    {
        _meshes.Add(mesh);
    }

    protected override void RenderSelf(float delta)
    {
        if (!_batchedBuffer.Bind())
        {
            return;
        }

        foreach (var mesh in _meshes.Where(mesh => mesh.IsVisible))
        {
            mesh.Render(delta);
            Context.Gl.DrawArrays(PrimitiveType.Triangles, 0, _batchedBuffer.IndexCount);
            mesh.EndRender();
        }
        
        _batchedBuffer.Unbind();
    }
    
    
}