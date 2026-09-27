using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Shaders;

namespace TT_Lab.Rendering.Objects;

public class BlendSkinnedMesh(RenderContext context, List<ModelBufferBlendSkin> models, TextureBuffer vertexOffsets, vec3 blendShape, int blendShapesAmount, float[] weights) : SkinnedMesh(context, models.Cast<ModelBuffer>().ToList())
{
    private const int MaxBlends = 15;

    private readonly RenderContext _context = context;
    private readonly float[] _weights = weights;
    // Always uploaded whole so weights of other blend skins drawn before can't be left over
    private readonly float[] _renderWeights = new float[MaxBlends];

    public override Mesh Clone()
    {
        var mesh = new BlendSkinnedMesh(Context, models, vertexOffsets, blendShape, blendShapesAmount, _weights);
        return mesh;
    }

    public void SetShapeWeight(int shapeIndex, float weight)
    {
        if (shapeIndex >= _weights.Length)
        {
            return;
        }

        _weights[shapeIndex] = weight;
    }

    public void ResetShapeWeights()
    {
        Array.Clear(_weights);
    }

    public override void UpdateRenderTransform()
    {
        for (var i = 0; i < _renderWeights.Length; i++)
        {
            _renderWeights[i] = i < _weights.Length ? _weights[i] : 0.0f;
        }

        base.UpdateRenderTransform();
    }

    public override void BeginIndividualDraw(ModelBuffer model)
    {
        vertexOffsets.Bind(TextureUnit.Texture6);

        var program = _context.CurrentPass.Program;
        program.SetUniform(KnownUniform.BlendShapesAmount, blendShapesAmount);
        program.SetUniform(KnownUniform.BlendShape, blendShape);
        _context.Gl.Uniform1(program[KnownUniform.MorphWeights], _renderWeights);
        program.SetUniform(KnownUniform.UseMorphs, true);

        base.BeginIndividualDraw(model);
    }

    public override void EndIndividualDraw(ModelBuffer model)
    {
        _context.CurrentPass.Program.SetUniform(KnownUniform.UseMorphs, false);

        base.EndIndividualDraw(model);
    }
}
