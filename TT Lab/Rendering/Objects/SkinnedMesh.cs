using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.Rendering.Buffers;

namespace TT_Lab.Rendering.Objects;

public class SkinnedMesh : Mesh
{
    private readonly RenderContext _context;
    protected mat4[] BoneMatrices = new mat4[64];

    public SkinnedMesh(RenderContext context, List<ModelBuffer> models) : base(context, models)
    {
        _context = context;
        
        for (var i = 0; i < 64; i++)
        {
            BoneMatrices[i] = mat4.Identity;
        }
    }

    public override Mesh Clone()
    {
        var mesh = new SkinnedMesh(Context, GetModels().ToList());
        return mesh;
    }

    public void SetBoneMatrix(int boneIndex, mat4 boneMatrix)
    {
        BoneMatrices[boneIndex] = boneMatrix;
    }
    
    protected override void RenderSelf(float delta)
    {
        var program = _context.CurrentPass.Program;
        var boneMatrixLoc = program.GetUniformLocation("BoneMatrices");
        _context.Gl.UniformMatrix4(boneMatrixLoc, false, BoneMatrices.SelectMany(m => m.Values1D).ToArray());
        base.RenderSelf(delta);
    }
}