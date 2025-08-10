using System;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Rendering.Factories;

namespace TT_Lab.Rendering.Buffers;

public class ModelBufferBlendSkin(RenderContext context, BlendSkinModelBufferBuild build, MaterialFactory materialFactory, MaterialData material) : ModelBuffer(context, build.Model, materialFactory, material)
{
    private readonly RenderContext _context = context;

    public override bool Bind()
    {
        var initialBind = base.Bind();
        if (!initialBind)
        {
            return false;
        }

        var program = _context.CurrentPass.Program;
        var shapeStartLoc = program.GetUniformLocation("ShapeStart");
        _context.Gl.Uniform1(shapeStartLoc, build.ShapeBuild.ShapeStart);
        var shapeOffsetLoc = program.GetUniformLocation("ShapeOffset");
        _context.Gl.Uniform1(shapeOffsetLoc, build.ShapeBuild.ShapesOffsets);
        
        return true;
    }
}