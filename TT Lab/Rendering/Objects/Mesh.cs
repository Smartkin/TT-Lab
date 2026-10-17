using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Rendering.Buffers;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Objects;

public class Mesh(RenderContext context, List<ModelBuffer> models) : Renderable(context)
{
    private readonly IReadOnlyList<ModelBuffer> _models = models;
    // The batches drawing the mesh, told when what its instances are made of changes. Replaced whole: the UI thread reads it
    private RenderBatch[] _batches = [];

    public IReadOnlyList<ModelBuffer> GetModels() => _models;

    /// <summary>
    /// Whether the mesh is placed in a scenery (a placed mesh, a LOD's level, a dynamic scenery's model): the game turns those to face the
    /// camera when a shader of any of their parts' materials is a billboard's, never an object's parts (SetPlacedModelRenderDMA)
    /// </summary>
    public bool IsPlaced { get; set; }

    /// <summary>
    /// How the mesh's instances turn to the camera
    /// </summary>
    public int Turn => IsPlaced && _models.Any(model => model.Material.Shaders.Any(shader => shader.ShaderType == TwinShader.Type.UnlitBillboard))
        ? InstanceBuffer.BillboardTurn
        : InstanceBuffer.NoTurn;

    internal void JoinBatch(RenderBatch batch)
    {
        _batches = [.. _batches, batch];
    }

    internal void LeaveBatch(RenderBatch batch)
    {
        _batches = Array.FindAll(_batches, other => other != batch);
    }

    protected override void OnDrawnChanged()
    {
        foreach (var batch in _batches)
        {
            batch.MarkChanged();
        }
    }

    public virtual Mesh Clone()
    {
        var mesh = new Mesh(Context, models);
        return mesh;
    }

    /// <summary>
    /// Frees what the mesh's models are drawn from, which its copies share: only once nothing draws them any more. On the render thread
    /// </summary>
    public virtual void ReleaseBuffers()
    {
        foreach (var model in _models)
        {
            model.Release();
        }
    }

    /// <summary>
    /// Meshes that need their own uniforms can't be drawn in the same draw as the other meshes using their model
    /// </summary>
    public virtual bool RequiresIndividualDraw => false;

    public virtual void BeginIndividualDraw(ModelBuffer model)
    {
    }

    public virtual void EndIndividualDraw(ModelBuffer model)
    {
    }
}
