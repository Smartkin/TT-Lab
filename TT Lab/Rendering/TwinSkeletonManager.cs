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

// A joint that doesn't inherit scale (an animation's independent scaling) still moves with its parent's whole transform, its offset
// gets scaled with the parent, only its own axes leave out the parent's scale. OGI 393's first animation scales the joint above
// everything to 0.3 and each part to 0.32, and ends on the bind pose at that size
public sealed class TwinBone : Node
{
    private mat4 inverseBindMatrix = mat4.Identity;
    private mat4 bindingMatrix = mat4.Identity;
    private vec3 restTranslation = vec3.Zero;
    private quat restRotation = quat.Identity;
    private vec3 translation = vec3.Zero;
    private quat rotation = quat.Identity;
    private vec3 scale = vec3.Ones;
    // Kept apart from the matrix, joints an animation scales to nothing to hide them have no rotation left in theirs
    private quat worldRotation = quat.Identity;

    public TwinBone(RenderContext context, Renderable parent) : base(context)
    {
        parent.AddChild(this);
    }
    
    public void SetBindingAndInverseMatrix(mat4 mat)
    {
        bindingMatrix = mat;
        inverseBindMatrix = mat.Inverse;
    }

    public void SetRest(vec3 restTranslation, quat restRotation)
    {
        this.restTranslation = restTranslation;
        this.restRotation = restRotation;
    }

    public void SetPose(vec3 translation, quat rotation, vec3 scale)
    {
        this.translation = translation;
        this.rotation = rotation;
        this.scale = scale;
        SetLocalTransform(mat4.Translate(translation) * rotation.ToMat4 * mat4.Scale(scale));
    }

    public void ResetPose()
    {
        SetInheritScale(true);
        SetPose(restTranslation, restRotation, vec3.Ones);
    }

    public mat4 GetBoneMatrix()
    {
        return WorldTransform * inverseBindMatrix;
    }

    protected override mat4 CalculateWorldTransform()
    {
        var parentRotation = Parent is TwinBone parentBone ? parentBone.worldRotation : Parent?.GetRotationQuat() ?? quat.Identity;
        worldRotation = parentRotation * rotation;
        if (InheritsScale || Parent == null)
        {
            return base.CalculateWorldTransform();
        }

        var position = (Parent.WorldTransform * new vec4(translation, 1)).xyz;
        return mat4.Translate(position) * worldRotation.ToMat4 * mat4.Scale(scale);
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
        rootBone.ResetPose();
        boneMap.Add(ogiData.Joints[0].Index, rootBone);
        var allOtherJoints = ogiData.Joints.Skip(1);
        
        foreach (var joint in allOtherJoints)
        {
            var parentBone = boneMap[joint.ParentIndex];
            var position = new vec3(joint.LocalTranslation.X, joint.LocalTranslation.Y, joint.LocalTranslation.Z);
            var quat = new quat(joint.LocalRotation.X, joint.LocalRotation.Y, joint.LocalRotation.Z, joint.LocalRotation.W);
            var bone = new TwinBone(context, parentBone);
            bone.SetRest(position, quat);
            bone.ResetPose();
            bone.SetBindingAndInverseMatrix(bone.WorldTransform);
            boneMap.TryAdd(joint.Index, bone);
        }
        skeleton.Bones = boneMap;

        return skeleton;
    }
}