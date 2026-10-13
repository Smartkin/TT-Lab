using System;
using System.Collections.Generic;
using System.Threading;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Materials;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Passes;

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
    // How many of the meshes are drawn on their own (skins), none in most batches
    private int _individualMeshes;
    private int _baseInstance;
    private int _instanceCount;
    // Where the batch keeps its instances from frame to frame (InstanceBuffer.Keep), -1 while it writes them every frame
    private int _keptStart = -1;
    private int _keptCapacity;
    // Set when anything its kept instances are made of changed (a mesh's visibility, color or transform, the meshes or the material),
    // while it waits in its context's queue of changed batches
    private int _changed;

    public RenderBatch(RenderContext context, ModelBuffer batchedBuffer, bool isPreview = false) : base(context)
    {
        _batchedBuffer = batchedBuffer;
        _isPreview = isPreview;
        _batchedBuffer.MaterialReplaced += BatchedBufferOnMaterialReplaced;
    }

    private void BatchedBufferOnMaterialReplaced()
    {
        MarkChanged();
        RequestPassSwitch?.Invoke();
    }

    /// <summary>
    /// Has the batch's instances written again, from any thread: told after the change
    /// </summary>
    public void MarkChanged()
    {
        if (Interlocked.Exchange(ref _changed, 1) == 0)
        {
            Context.QueueChangedBatch(this);
        }
    }

    /// <summary>
    /// Whether the renderer has the batch write its instances every frame
    /// </summary>
    internal bool IsFrameBatch { get; set; }

    // Lit batches gather their objects' light sets every frame and skins are drawn on their own, only the others keep their instances
    private bool KeepsInstances => _individualMeshes == 0 && !_batchedBuffer.IsLit;

    public ModelBuffer Buffer => _batchedBuffer;

    public int InstanceCount => _instanceCount;

    public int BaseInstance => _baseInstance;

    /// <summary>
    /// Whether the batch has anything to draw this frame
    /// </summary>
    public bool HasDraws => _instanceCount > 0 || _individualDraws.Count > 0;

    /// <summary>
    /// Whether the batch's draw can be one of an indirect draw's (<see cref="IndirectDraws"/>): its instances drawn at once from the
    /// context's vertex pool, nothing drawn on its own, no preview
    /// </summary>
    public bool DrawsIndirectly => _individualDraws.Count == 0 && _instanceCount > 0 && CanDrawIndirectly;

    /// <summary>
    /// The batch's draws in the passes' indirect draws (<see cref="IndirectDraws"/>)
    /// </summary>
    internal List<int> Commands { get; } = [];

    /// <summary>
    /// Whether some of the batch's meshes are drawn on their own (skins)
    /// </summary>
    public bool DrawsAlone => _individualMeshes > 0;

    private static int _drawnAloneVersion;

    /// <summary>
    /// Changes whenever a batch starts or stops having meshes drawn on their own
    /// </summary>
    public static int DrawnAloneVersion => Volatile.Read(ref _drawnAloneVersion);

    /// <summary>
    /// Whether the batch is ever drawn in an indirect draw: no preview, its model in the context's vertex pool
    /// </summary>
    public bool CanDrawIndirectly => !_isPreview && _batchedBuffer.IsPooled;

    /// <summary>
    /// Whether the batch draws what's about to be placed, in the preview passes
    /// </summary>
    public bool IsPreview => _isPreview;

    private static long _sequenceCounter;

    // A part of the fragments failing its alpha test drawn apart has 101 triangles at most in retail (MobileWumpa's), a draw for each of a
    // bigger one's would cost more than the scene (DrawApart)
    private const int MostTrianglesInOrder = 256;

    /// <summary>
    /// The order the batch was made in, its model's first use: the game draws a material's models in the order of their first draws
    /// (SceneOrder)
    /// </summary>
    public long Sequence { get; } = Interlocked.Increment(ref _sequenceCounter);

    public void AddToBatch(Mesh mesh)
    {
        if (_batchedMeshes.Add(mesh))
        {
            _meshes.Add(mesh);
            if (mesh.RequiresIndividualDraw)
            {
                _individualMeshes++;
            }

            if (mesh.RequiresIndividualDraw && _individualMeshes == 1)
            {
                Interlocked.Increment(ref _drawnAloneVersion);
            }

            mesh.JoinBatch(this);
            MarkChanged();
        }
    }

    public void RemoveFromBatch(Mesh mesh)
    {
        if (_batchedMeshes.Remove(mesh))
        {
            _meshes.Remove(mesh);
            if (mesh.RequiresIndividualDraw)
            {
                _individualMeshes--;
            }

            if (mesh.RequiresIndividualDraw && _individualMeshes == 0)
            {
                Interlocked.Increment(ref _drawnAloneVersion);
            }

            mesh.LeaveBatch(this);
            MarkChanged();
        }
    }

    /// <summary>
    /// Writes the batch's kept instances again, once something they're made of changed. False when the batch writes its instances every
    /// frame instead (<see cref="PrepareInstances"/>), after every batch kept its own
    /// </summary>
    public bool PrepareKeptInstances(InstanceBuffer instances)
    {
        // Out of the queue before reading the meshes: a change made meanwhile puts the batch in again
        Interlocked.Exchange(ref _changed, 0);
        // Batches stay when their meshes go (BatchStorage keeps them, a model made again gets new ones): an emptied one gives its instances
        // back and isn't written every frame, a mesh joining it puts it in the queue again
        if (_meshes.Count == 0)
        {
            ReleaseKeptInstances(instances);
            _individualDraws.Clear();
            _baseInstance = 0;
            _instanceCount = 0;
            return true;
        }

        if (!KeepsInstances)
        {
            ReleaseKeptInstances(instances);
            return false;
        }

        if (_keptStart < 0 || _keptCapacity < _meshes.Count)
        {
            ReleaseKeptInstances(instances);
            _keptCapacity = _meshes.Count;
            _keptStart = instances.Keep(_keptCapacity);
        }

        _individualDraws.Clear();
        _baseInstance = _keptStart;
        var count = 0;
        for (var i = 0; i < _meshes.Count; i++)
        {
            var mesh = _meshes[i];
            if (mesh.IsVisible)
            {
                instances.Write(_keptStart + count, mesh.RenderTransform, mesh.Diffuse, InstanceBuffer.NoLightSet, mesh.Turn);
                count++;
            }
        }

        _instanceCount = count;
        return true;
    }

    private void ReleaseKeptInstances(InstanceBuffer instances)
    {
        if (_keptStart < 0)
        {
            return;
        }

        instances.Release(_keptStart, _keptCapacity);
        _keptStart = -1;
        _keptCapacity = 0;
    }

    /// <summary>
    /// The instances of a batch that doesn't keep them, written every frame
    /// </summary>
    public void PrepareInstances(InstanceBuffer instances)
    {
        _individualDraws.Clear();
        _instanceCount = 0;
        _baseInstance = instances.Count;
        var lit = _batchedBuffer.IsLit;
        var individual = _individualMeshes > 0;
        for (var i = 0; i < _meshes.Count; i++)
        {
            var mesh = _meshes[i];
            if (!mesh.IsVisible || (individual && mesh.RequiresIndividualDraw))
            {
                continue;
            }

            instances.Add(mesh.RenderTransform, mesh.Diffuse, lit ? instances.LightSetOf(mesh) : InstanceBuffer.NoLightSet, mesh.Turn);
            _instanceCount++;
        }

        if (!individual)
        {
            return;
        }

        foreach (var mesh in _meshes)
        {
            if (mesh.IsVisible && mesh.RequiresIndividualDraw)
            {
                _individualDraws.Add((mesh, instances.Add(mesh.RenderTransform, mesh.Diffuse, lit ? instances.LightSetOf(mesh) : InstanceBuffer.NoLightSet, mesh.Turn)));
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

        for (var shader = 0; shader < _batchedBuffer.ShaderCount; shader++)
        {
            DrawShader(shader);
        }
    }

    /// <summary>
    /// The batch's draws with one shader of its material, where the pass has it
    /// </summary>
    public void RenderShader(int shader, float delta)
    {
        if (!IsVisible || (_instanceCount == 0 && _individualDraws.Count == 0))
        {
            return;
        }

        DrawShader(shader);
    }

    private void DrawShader(int shader)
    {
        if (!_batchedBuffer.Bind(shader))
        {
            return;
        }

        Draw(_batchedBuffer.GetMaterial());
        _batchedBuffer.Unbind();
    }

    // A preview draws as its material's shaders would, then once more as a silhouette over what hides it
    private void RenderPreview()
    {
        if (Context.CurrentPass is not PreviewPass pass)
        {
            return;
        }

        if (!pass.IsSilhouette)
        {
            for (var shader = 0; shader < _batchedBuffer.ShaderCount; shader++)
            {
                DrawShader(shader);
            }

            return;
        }

        if (!_batchedBuffer.Bind(0))
        {
            return;
        }

        // Over the material's state, only the fragments passing the alpha test of a material drawing its failing ones apart
        pass.BeginSilhouette();
        Draw(null);
        pass.EndSilhouette();
        _batchedBuffer.Unbind();
    }

    // The draws with the material bound, which draws the fragments failing its alpha test apart when they write something else than the
    // passing ones (DrawApart)
    private void Draw(TwinMaterial? material)
    {
        var gl = Context.Gl;
        var apart = material is { FailsApart: true };
        if (_instanceCount > 0)
        {
            if (apart)
            {
                for (var i = 0; i < _instanceCount; i++)
                {
                    DrawApart(material!, (uint)(_baseInstance + i));
                }
            }
            else
            {
                gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, _batchedBuffer.FirstVertex, _batchedBuffer.IndexCount, (uint)_instanceCount, (uint)_baseInstance);
            }
        }

        foreach (var (mesh, instance) in _individualDraws)
        {
            mesh.BeginIndividualDraw(_batchedBuffer);
            if (apart)
            {
                DrawApart(material!, (uint)instance);
            }
            else
            {
                gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, _batchedBuffer.FirstVertex, _batchedBuffer.IndexCount, 1, (uint)instance);
            }

            mesh.EndIndividualDraw(_batchedBuffer);
        }

        if (apart)
        {
            material!.EndSubpasses();
        }
    }

    // An instance's passing fragments, then its failing ones, a triangle at a time: the GS writes a triangle's pixels before the next one's,
    // so a later triangle drawn behind an earlier one's failing pixels, which leave the depth alone, is seen there (the nitro crate's broken
    // pieces, which nothing culls). A triangle's passing and failing fragments never lie over each other. No triangle of a flat part lies
    // over another, which is drawn whole, and so is a part of more triangles than MostTrianglesInOrder, the way PCSX2's hardware renderer
    // draws them all
    private void DrawApart(TwinMaterial material, uint instance)
    {
        var gl = Context.Gl;
        var corners = (int)_batchedBuffer.IndexCount;
        var step = _batchedBuffer.IsFlat || corners > MostTrianglesInOrder * 3 ? corners : 3;
        for (var first = 0; first < corners; first += step)
        {
            material.BeginPassing();
            gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, _batchedBuffer.FirstVertex + first, (uint)step, 1, instance);
            material.BeginFailing();
            gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, _batchedBuffer.FirstVertex + first, (uint)step, 1, instance);
        }
    }
}
