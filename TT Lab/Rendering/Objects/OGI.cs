using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Lighting;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering.Objects;

// Its parts are lit by the lights where it is, as the game gathers them per object instance
public class OGI : Renderable, IPrimitiveRenderable, ILightAnchor
{
    private static readonly vec4 HullColor = new(0.25f, 0.9f, 0.7f, 0.85f);

    private SkinnedMesh? skinBuffer;
    private BlendSkinnedMesh? blendSkinBuffer;
    private TwinSkeleton defaultSkeleton = new();
    // The collision hulls' edges and the joint each is on, drawn where the joints are
    private readonly List<(int Joint, vec3[] Vertexes, (int, int)[] Edges)> _hulls = [];
    
    public OGI(RenderContext context, TwinSkeletonManager skeletonManager, MeshService meshService, OGIData ogiData, string name = "") : base(context, name)
    {
        BuildSkeleton(skeletonManager, meshService, ogiData);
        for (var i = 0; i < ogiData.CollisionHulls.Count; i++)
        {
            var hull = ogiData.CollisionHulls[i];
            _hulls.Add((i < ogiData.CollisionHullJoints.Count ? ogiData.CollisionHullJoints[i] : OGIData.NoJoint,
                hull.Vertexes.Select(vertex => new vec3(vertex.X, vertex.Y, vertex.Z)).ToArray(),
                hull.Edges.Where(edge => edge.Count == 2 && edge[0] < hull.Vertexes.Count && edge[1] < hull.Vertexes.Count).Select(edge => ((int)edge[0], (int)edge[1])).ToArray()));
        }
    }

    public bool ShowHulls { get; set; } = true;

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        if (!ShowHulls)
        {
            return;
        }

        foreach (var (joint, vertexes, edges) in _hulls)
        {
            var transform = joint != OGIData.NoJoint && defaultSkeleton.Bones.TryGetValue(joint, out var bone) ? bone.WorldTransform : WorldTransform;
            foreach (var (from, to) in edges)
            {
                renderer.DrawLine((transform * new vec4(vertexes[from], 1.0f)).xyz, (transform * new vec4(vertexes[to], 1.0f)).xyz, HullColor, 1.5f, PrimitiveLayer.WorldXRay);
            }
        }
    }

    public void ApplyTransformToJoint(int jointIndex, vec3 position, vec3 scale, quat rotation)
    {
        if (!defaultSkeleton.Bones.TryGetValue(jointIndex, out var jointNode))
        {
            return;
        }

        jointNode.SetPose(position, rotation, scale);
        skinBuffer?.SetBoneMatrix(jointIndex, defaultSkeleton.Bones[jointIndex].GetBoneMatrix());
        blendSkinBuffer?.SetBoneMatrix(jointIndex, defaultSkeleton.Bones[jointIndex].GetBoneMatrix());
    }

    public void SetInheritScaleForJoint(int jointIndex, bool inherit)
    {
        if (defaultSkeleton.Bones.TryGetValue(jointIndex, out var value))
        {
            value.SetInheritScale(inherit);
        }
    }

    public void ResetPose()
    {
        foreach (var bone in defaultSkeleton.Bones.Values)
        {
            bone.ResetPose();
        }

        foreach (var (jointIndex, bone) in defaultSkeleton.Bones)
        {
            skinBuffer?.SetBoneMatrix(jointIndex, bone.GetBoneMatrix());
            blendSkinBuffer?.SetBoneMatrix(jointIndex, bone.GetBoneMatrix());
        }

        blendSkinBuffer?.ResetShapeWeights();
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
            var node = defaultSkeleton.Bones[ogiData.RigidModelJointIndices[jointIndex++]];
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