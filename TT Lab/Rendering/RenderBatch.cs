using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;

namespace TT_Lab.Rendering;

/// <summary>
/// Renderables that write their instances into the frame's instance buffer before any pass gets rendered
/// </summary>
public interface IInstancedRenderable
{
    void PrepareInstances(InstanceBuffer instances);
}

/// <summary>
/// Every mesh using the same model, drawn with a single instanced draw per pass
/// </summary>
public class RenderBatch : Renderable, IInstancedRenderable
{
    public event Action? RequestPassSwitch;

    private readonly ModelBuffer _batchedBuffer;
    private readonly List<Mesh> _meshes = [];
    private readonly HashSet<Mesh> _batchedMeshes = new(ReferenceEqualityComparer.Instance);
    private readonly List<(Mesh Mesh, int Instance)> _individualDraws = [];
    private int _baseInstance;
    private int _instanceCount;

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
        if (_batchedMeshes.Add(mesh))
        {
            _meshes.Add(mesh);
        }
    }

    public void RemoveFromBatch(Mesh mesh)
    {
        if (_batchedMeshes.Remove(mesh))
        {
            _meshes.Remove(mesh);
        }
    }

    public void PrepareInstances(InstanceBuffer instances)
    {
        _individualDraws.Clear();
        _instanceCount = 0;
        _baseInstance = instances.Count;
        foreach (var mesh in _meshes)
        {
            if (!mesh.IsVisible || mesh.RequiresIndividualDraw)
            {
                continue;
            }

            instances.Add(mesh.RenderTransform, mesh.Diffuse);
            _instanceCount++;
        }

        foreach (var mesh in _meshes)
        {
            if (mesh.IsVisible && mesh.RequiresIndividualDraw)
            {
                _individualDraws.Add((mesh, instances.Add(mesh.RenderTransform, mesh.Diffuse)));
            }
        }
    }

    protected override void RenderSelf(float delta)
    {
        if ((_instanceCount == 0 && _individualDraws.Count == 0) || !_batchedBuffer.Bind())
        {
            return;
        }

        var gl = Context.Gl;
        if (_instanceCount > 0)
        {
            gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, 0, _batchedBuffer.IndexCount, (uint)_instanceCount, (uint)_baseInstance);
        }

        foreach (var (mesh, instance) in _individualDraws)
        {
            mesh.BeginIndividualDraw(_batchedBuffer);
            gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, 0, _batchedBuffer.IndexCount, 1, (uint)instance);
            mesh.EndIndividualDraw(_batchedBuffer);
        }

        _batchedBuffer.Unbind();
    }
}
