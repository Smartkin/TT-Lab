using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.Rendering.Buffers;

namespace TT_Lab.Rendering.Objects;

public class SkinnedMesh : Mesh
{
    private readonly RenderContext _context;
    private readonly mat4[] _boneMatrices = new mat4[64];
    private readonly mat4[] _renderBoneMatrices = new mat4[64];

    public SkinnedMesh(RenderContext context, List<ModelBuffer> models) : base(context, models)
    {
        _context = context;
        
        for (var i = 0; i < 64; i++)
        {
            _boneMatrices[i] = mat4.Identity;
            _renderBoneMatrices[i] = mat4.Identity;
        }
    }

    public override Mesh Clone()
    {
        var mesh = new SkinnedMesh(Context, GetModels().ToList());
        return mesh;
    }

    public override void UpdateRenderTransform()
    {
        for (var i = 0; i < 64; i++)
        {
            _renderBoneMatrices[i] = _boneMatrices[i];
        }
        
        base.UpdateRenderTransform();
    }

    public void SetBoneMatrix(int boneIndex, mat4 boneMatrix)
    {
        _boneMatrices[boneIndex] = boneMatrix;
    }
    
    protected override void RenderSelf(float delta)
    {
        var program = _context.CurrentPass.Program;
        var boneMatrixLoc = program.GetUniformLocation("BoneMatrices");
        _context.Gl.UniformMatrix4(boneMatrixLoc, false, _renderBoneMatrices.SelectMany(m => m.Values1D).ToArray());
        base.RenderSelf(delta);
    }
}