using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Materials;

namespace TT_Lab.Rendering.Objects;

public class Mesh(RenderContext context, List<ModelBuffer> models) : Renderable(context)
{
    private readonly IReadOnlyList<ModelBuffer> _models = models;
    private PolygonMode _renderMode = PolygonMode.Fill;
    private readonly List<MaterialPropertyOverrider> _materialOverrides = [];

    public void SetRenderMode(PolygonMode mode)
    {
        _renderMode = mode;
    }

    public void AddMaterialOverride(MaterialPropertyOverrider overrider)
    {
        _materialOverrides.Add(overrider);
    }

    public void RemoveMaterialOverride(MaterialPropertyOverrider overrider)
    {
        _materialOverrides.Remove(overrider);
    }

    public IReadOnlyList<ModelBuffer> GetModels() => _models;

    public virtual Mesh Clone()
    {
        var mesh = new Mesh(Context, models);
        return mesh;
    }

    public override (String, Int32)[] GetPriorityPasses()
    {
        var result = new List<(String, int)>();
        foreach (var model in _models)
        {
            result.AddRange(model.GetPriorityPass());
        }

        return result.ToArray();
    }

    /// <summary>
    /// Meshes that need their own uniforms or state can't be drawn in the same draw as the other meshes using their model
    /// </summary>
    public virtual bool RequiresIndividualDraw => _renderMode != PolygonMode.Fill || _materialOverrides.Count > 0;

    public virtual void BeginIndividualDraw(ModelBuffer model)
    {
        Context.State.SetPolygonMode(_renderMode);
        var material = model.GetMaterial();
        if (material == null)
        {
            return;
        }

        foreach (var materialPropertyOverrider in _materialOverrides)
        {
            materialPropertyOverrider.Override(material);
        }
    }

    public virtual void EndIndividualDraw(ModelBuffer model)
    {
        Context.State.SetPolygonMode(PolygonMode.Fill);
        var material = model.GetMaterial();
        if (material == null)
        {
            return;
        }

        foreach (var materialPropertyOverrider in _materialOverrides)
        {
            materialPropertyOverrider.UnOverride(material);
        }
    }
}
