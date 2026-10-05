using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Passes;
using TT_Lab.Rendering.Services;

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
    private readonly bool _isPreview;
    private readonly List<Mesh> _meshes = [];
    private readonly HashSet<Mesh> _batchedMeshes = new(ReferenceEqualityComparer.Instance);
    private readonly List<(Mesh Mesh, int Instance)> _individualDraws = [];
    private int _baseInstance;
    private int _instanceCount;

    public RenderBatch(RenderContext context, ModelBuffer batchedBuffer, bool isPreview = false) : base(context)
    {
        _batchedBuffer = batchedBuffer;
        _isPreview = isPreview;
        _batchedBuffer.MaterialReplaced += BatchedBufferOnMaterialReplaced;
    }

    private void BatchedBufferOnMaterialReplaced()
    {
        RequestPassSwitch?.Invoke();
    }

    public override (string, int)[] GetPriorityPasses()
    {
        var passes = _batchedBuffer.GetPriorityPass();
        if (!_isPreview)
        {
            return passes;
        }

        var priority = passes.Length == 0 ? 0 : passes[0].Item2;
        return [(PassService.PreviewPassName, priority), (PassService.PreviewSilhouettePassName, priority)];
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
        var lit = _batchedBuffer.IsLit;
        foreach (var mesh in _meshes)
        {
            if (!mesh.IsVisible || mesh.RequiresIndividualDraw)
            {
                continue;
            }

            instances.Add(mesh.RenderTransform, mesh.Diffuse, lit ? instances.LightSetOf(mesh) : InstanceBuffer.NoLightSet);
            _instanceCount++;
        }

        foreach (var mesh in _meshes)
        {
            if (mesh.IsVisible && mesh.RequiresIndividualDraw)
            {
                _individualDraws.Add((mesh, instances.Add(mesh.RenderTransform, mesh.Diffuse, lit ? instances.LightSetOf(mesh) : InstanceBuffer.NoLightSet)));
            }
        }
    }

    protected override void RenderSelf(float delta)
    {
        if (_instanceCount == 0 && _individualDraws.Count == 0)
        {
            return;
        }

        if (_isPreview)
        {
            RenderPreview();
            return;
        }

        if (!_batchedBuffer.Bind())
        {
            return;
        }

        Draw();
        _batchedBuffer.Unbind();
    }

    // A preview draws as its material's passes would, then once more as a silhouette over what hides it
    private void RenderPreview()
    {
        if (Context.CurrentPass is not PreviewPass pass)
        {
            return;
        }

        if (!pass.IsSilhouette)
        {
            foreach (var (passName, _) in _batchedBuffer.GetPriorityPass())
            {
                if (_batchedBuffer.Bind(passName))
                {
                    Draw();
                    _batchedBuffer.Unbind();
                }
            }

            return;
        }

        if (_batchedBuffer.GetPriorityPass() is not [var (firstPass, _), ..] || !_batchedBuffer.Bind(firstPass))
        {
            return;
        }

        pass.BeginSilhouette();
        Draw();
        pass.EndSilhouette();
        _batchedBuffer.Unbind();
    }

    private void Draw()
    {
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
    }
}
