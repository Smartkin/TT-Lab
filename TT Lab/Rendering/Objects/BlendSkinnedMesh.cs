using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;

namespace TT_Lab.Rendering.Objects;

public class BlendSkinnedMesh(RenderContext context, List<ModelBufferBlendSkin> models, TextureBuffer vertexOffsets, vec3 blendShape, int blendShapesAmount, float[] weights) : SkinnedMesh(context, models.Cast<ModelBuffer>().ToList())
{
    private readonly RenderContext _context = context;
    private static readonly float[] ZeroWeights = new float[15];

    public override Mesh Clone()
    {
        var mesh = new BlendSkinnedMesh(Context, models, vertexOffsets, blendShape, blendShapesAmount, weights);
        return mesh;
    }

    public void SetShapeWeight(int shapeIndex, float weight)
    {
        if (shapeIndex >= weights.Length)
        {
            return;
        }
        
        weights[shapeIndex] = weight;
    }

    protected override void RenderSelf(float delta)
    {
        vertexOffsets.Bind(TextureUnit.Texture6);

        var program = _context.CurrentPass.Program;
        var blendShapesAmountLoc = program.GetUniformLocation("BlendShapesAmount");
        var blendShapeLoc = program.GetUniformLocation("BlendShape");
        _context.Gl.Uniform1(blendShapesAmountLoc, blendShapesAmount);
        _context.Gl.Uniform3(blendShapeLoc, blendShape.Values);

        var morphWeightLoc = program.GetUniformLocation("MorphWeights");
        _context.Gl.Uniform1(morphWeightLoc, weights);
        
        base.RenderSelf(delta);
        
        _context.Gl.Uniform1(morphWeightLoc, ZeroWeights);
    }
}