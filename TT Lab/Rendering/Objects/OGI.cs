using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering.Objects;

public class OGI : Renderable
{
    private SkinnedMesh? skinBuffer;
    private BlendSkinnedMesh? blendSkinBuffer;
    private TwinSkeleton defaultSkeleton = new();
    
    public OGI(RenderContext context, TwinSkeletonManager skeletonManager, MeshService meshService, OGIData ogiData, string name = "") : base(context, name)
    {
        BuildSkeleton(skeletonManager, meshService, ogiData);
    }

    public void ApplyTransformToJoint(int jointIndex, vec3 position, vec3 scale, quat rotation)
    {
        var jointNode = defaultSkeleton.Bones[jointIndex];
        var transform = mat4.Translate(position) * glm.ToMat4(rotation) * mat4.Scale(scale);
        jointNode.SetLocalTransform(transform);
        skinBuffer?.SetBoneMatrix(jointIndex, defaultSkeleton.Bones[jointIndex].GetBoneMatrix());
        blendSkinBuffer?.SetBoneMatrix(jointIndex, defaultSkeleton.Bones[jointIndex].GetBoneMatrix());
    }

    public void ApplyWeightsToBlendSkin(float[] weights)
    {
        if (blendSkinBuffer == null)
        {
            return;
        }

        var idx = 0;
        foreach (var weight in weights)
        {
            blendSkinBuffer.SetShapeWeight(idx++, weight);
        }
    }

    private void BuildSkeleton(TwinSkeletonManager skeletonManager, MeshService meshService, OGIData ogiData)
    {
        defaultSkeleton = skeletonManager.CreateSceneNodeSkeleton(this, ogiData);
        var jointIndex = 0;
        foreach (var rigidModelUri in ogiData.RigidModelIds)
        {
            var node = defaultSkeleton.Bones[ogiData.JointIndices[jointIndex++]];
            if (rigidModelUri == LabURI.Empty)
            {
                continue;
            }

            var mesh = meshService.GetMesh(rigidModelUri, true);
            if (mesh.Model == null)
            {
                continue;
            }
            node.AddChild(mesh.Model);
        }

        if (ogiData.Skin != LabURI.Empty)
        {
            var skin = meshService.GetMesh(ogiData.Skin, true);
            if (skin.Model != null)
            {
                skinBuffer = (SkinnedMesh)skin.Model;
                AddChild(skinBuffer);
            }
        }

        if (ogiData.BlendSkin != LabURI.Empty)
        {
            var blendSkin = meshService.GetMesh(ogiData.BlendSkin, true);
            if (blendSkin.Model != null)
            {
                blendSkinBuffer = (BlendSkinnedMesh)blendSkin.Model;
                AddChild(blendSkinBuffer);
            }
        }
    }
    
}