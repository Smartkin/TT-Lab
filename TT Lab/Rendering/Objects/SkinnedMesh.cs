using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Shaders;

namespace TT_Lab.Rendering.Objects;

public class SkinnedMesh : Mesh
{
    private const int MaxBones = 64;

    private readonly mat4[] _boneMatrices = new mat4[MaxBones];
    private readonly float[] _renderBoneMatrices = new float[MaxBones * 16];

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

    public override void UpdateRenderTransform()
    {
        CopyBoneMatrices();

        base.UpdateRenderTransform();
    }

    public void SetBoneMatrix(int boneIndex, mat4 boneMatrix)
    {
        _boneMatrices[boneIndex] = boneMatrix;
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
        for (var i = 0; i < MaxBones; i++)
        {
            StreamStorageBuffer.Write(_renderBoneMatrices.AsSpan(i * 16, 16), _boneMatrices[i]);
        }
    }
}
