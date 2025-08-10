using System.Collections.Generic;
using System.Linq;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;

namespace TT_Lab.Rendering;

public class RenderBatch(RenderContext context, ModelBuffer batchedBuffer) : Renderable(context)
{
    private readonly List<Mesh> _meshes = [];

    public override (string, int)[] GetPriorityPasses()
    {
        return batchedBuffer.GetPriorityPass();
    }

    public void AddToBatch(Mesh mesh)
    {
        _meshes.Add(mesh);
    }

    protected override void RenderSelf(float delta)
    {
        if (!batchedBuffer.Bind())
        {
            return;
        }

        foreach (var mesh in _meshes.Where(mesh => mesh.IsVisible))
        {
            mesh.Render(delta);
            Context.Gl.DrawArrays(PrimitiveType.Triangles, 0, batchedBuffer.IndexCount);
            mesh.EndRender();
        }
        
        batchedBuffer.Unbind();
    }
    
    
}