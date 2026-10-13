using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using GlmSharp;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Shaders;

namespace TT_Lab.Rendering.Objects;

public class SkinnedMesh : Mesh
{
    private const int MaxBones = 64;

    private readonly mat4[] _boneMatrices = new mat4[MaxBones];
    private readonly float[] _renderBoneMatrices = new float[MaxBones * 16];
    // Bones are posed on the render thread, the matrices drawn with are only copied again after that
    private bool _bonesChanged;

    public SkinnedMesh(RenderContext context, List<ModelBuffer> models) : base(context, models)
    {
        for (var i = 0; i < MaxBones; i++)
        {
            _boneMatrices[i] = mat4.Identity;
        }

        CopyBoneMatrices();
    }

    public override Mesh Clone()
    {
        var mesh = new SkinnedMesh(Context, GetModels().ToList());
        return mesh;
    }

    public override bool RequiresIndividualDraw => true;

    protected override void TakeRenderState()
    {
        if (_bonesChanged)
        {
            _bonesChanged = false;
            CopyBoneMatrices();
        }

        base.TakeRenderState();
    }

    public void SetBoneMatrix(int boneIndex, mat4 boneMatrix)
    {
        _boneMatrices[boneIndex] = boneMatrix;
        _bonesChanged = true;
        QueueRenderTransform();
    }

    public override void BeginIndividualDraw(ModelBuffer model)
    {
        var program = Context.CurrentPass.Program;
        Context.Gl.UniformMatrix4(program[KnownUniform.BoneMatrices], false, _renderBoneMatrices);
        program.SetUniform(KnownUniform.UseSkinning, true);

        base.BeginIndividualDraw(model);
    }

    public override void EndIndividualDraw(ModelBuffer model)
    {
        Context.CurrentPass.Program.SetUniform(KnownUniform.UseSkinning, false);

        base.EndIndividualDraw(model);
    }

    private void CopyBoneMatrices()
    {
        MemoryMarshal.Cast<mat4, float>(_boneMatrices).CopyTo(_renderBoneMatrices);
    }
}
