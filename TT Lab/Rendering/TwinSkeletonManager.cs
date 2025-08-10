using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using Silk.NET.Maths;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Scene;
using TT_Lab.Util;

namespace TT_Lab.Rendering;

public sealed class TwinBone : Node
{
    private mat4 inverseBindMatrix = mat4.Identity;
    private mat4 bindingMatrix = mat4.Identity;

    public TwinBone(RenderContext context, Renderable parent) : base(context)
    {
        parent.AddChild(this);
    }
    
    public void SetBindingAndInverseMatrix(mat4 mat)
    {
        bindingMatrix = mat;
        inverseBindMatrix = mat.Inverse;
    }

    public mat4 GetBoneMatrix()
    {
        return WorldTransform * inverseBindMatrix;
    }
}

public sealed class TwinSkeleton
{
    public Dictionary<int, TwinBone> Bones = [];
}

public class TwinSkeletonManager(RenderContext context)
{
    public TwinSkeleton CreateSceneNodeSkeleton(Renderable parentNode, LabURI ogiData)
    {
        var skeletonData = AssetManager.Get().GetAssetData<OGIData>(ogiData);
        return CreateSceneNodeSkeleton(parentNode, skeletonData);
    }
    
    public TwinSkeleton CreateSceneNodeSkeleton(Renderable parentNode, OGIData ogiData)
    {
        var skeleton = new TwinSkeleton();
        var boneMap = new Dictionary<int, TwinBone>();
        var rootBone = new TwinBone(context, parentNode);
        rootBone.SetBindingAndInverseMatrix(mat4.Identity);
        rootBone.SetLocalTransform(mat4.Identity);
        boneMap.Add(ogiData.Joints[0].Index, rootBone);
        var allOtherJoints = ogiData.Joints.Skip(1);
        
        foreach (var joint in allOtherJoints)
        {
            var parentBone = boneMap[joint.ParentIndex];
            var position = new vec3(-joint.LocalTranslation.X, joint.LocalTranslation.Y, joint.LocalTranslation.Z);
            var quat = new quat(-joint.LocalRotation.X, joint.LocalRotation.Y, joint.LocalRotation.Z, -joint.LocalRotation.W);
            var localTransform = mat4.Translate(position) * quat.ToMat4;
            var bone = new TwinBone(context, parentBone);
            bone.Transform(localTransform);
            bone.SetInheritScale(false);
            bone.SetBindingAndInverseMatrix(bone.WorldTransform);
            boneMap.TryAdd(joint.Index, bone);
        }
        skeleton.Bones = boneMap;

        return skeleton;
    }
}