using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using PrimitiveType = Silk.NET.OpenGL.PrimitiveType;

namespace TT_Lab.Rendering.Objects;

public class Mesh(RenderContext context, List<ModelBuffer> models) : Renderable(context)
{
    private readonly IReadOnlyList<ModelBuffer> _models = models;
    private PolygonMode _renderMode = PolygonMode.Fill;

    public void SetRenderMode(PolygonMode mode)
    {
        _renderMode = mode;
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

    private int _currentRenderMode;
    protected override void RenderSelf(float delta)
    {
        _currentRenderMode = Context.Gl.GetInteger(GetPName.PolygonMode);
        if (_currentRenderMode != (int)_renderMode)
        {
            Context.Gl.PolygonMode(TriangleFace.FrontAndBack, _renderMode);
        }
        
        var modelLoc = Context.CurrentPass.Program.GetUniformLocation("StartModel");
        Context.Gl.UniformMatrix4(modelLoc, false, RenderTransform.Values1D);
    }

    public override void EndRender()
    {
        if (_currentRenderMode != (int)PolygonMode.Fill)
        {
            Context.Gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        }

        base.EndRender();
    }
}