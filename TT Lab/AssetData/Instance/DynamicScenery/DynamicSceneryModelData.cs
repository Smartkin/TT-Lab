using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;
using Twinsanity.TwinsanityInterchange.Common.Animation;

namespace TT_Lab.AssetData.Instance.DynamicScenery;

public struct DynamicModelAnimationSample
{
    public (float, System.Numerics.Vector3) Translation;
    public (float, System.Numerics.Quaternion) Rotation;
}

[ReferencesAssets]
public class DynamicSceneryModelData
{
    public List<TwinBoundingBoxBuilder> BoundingBoxBuilders { get; set; }
    public Int32 AnimatedFrames { get; set; }
    public TwinDynamicSceneryAnimation Animation { get; set; }
    public Byte LodFlag { get; set; }
    public LabURI Mesh { get; set; }
    public Vector4[] BoundingBox { get; set; }

    public DynamicSceneryModelData()
    {
        BoundingBoxBuilders = new List<TwinBoundingBoxBuilder>();
        Animation = new TwinDynamicSceneryAnimation();
        Mesh = LabURI.Empty;
        BoundingBox = [new Vector4(0, 0, 0, 1), new Vector4(10, 10, 10, 1)];
    }

    public DynamicSceneryModelData(IAsset owner, TwinDynamicSceneryModel model)
    {
        BoundingBoxBuilders = CloneUtils.DeepClone(model.BoundingBoxBuilders);
        AnimatedFrames = model.AnimatedFrames;
        Animation = CloneUtils.DeepClone(model.Animation);
        LodFlag = model.LodFlag;
        Mesh = AssetManager.Get().GetUriByTwinId<Mesh>(owner, model.MeshID);
        BoundingBox = new Vector4[2];
        for (Int32 i = 0; i < BoundingBox.Length; i++)
        {
            BoundingBox[i] = CloneUtils.Clone(model.BoundingBox[i]);
        }
    }

    public List<DynamicModelAnimationSample> GetAnimationSamples()
    {
        var result = new List<DynamicModelAnimationSample>();

        for (var i = 0; i < AnimatedFrames; i++)
        {
            result.Add(GetTransformFromAnimation(i));
        }
        
        return result;
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(8); // Dynamic scenery model header
        writer.Write(BoundingBoxBuilders.Count);
        foreach (var bbBuilder in BoundingBoxBuilders)
        {
            bbBuilder.Write(writer);
        }
        writer.Write(AnimatedFrames);
        Animation.Write(writer);
        writer.Write(LodFlag);
        writer.Write(AssetManager.Get().GetAsset(Mesh).ID);
        foreach (var v in BoundingBox)
        {
            v.Write(writer);
        }
    }
    
    private DynamicModelAnimationSample GetTransformFromAnimation(int frame)
    {
        var sampleTime = frame / 25.0f;
        if (Animation.ModelSettings.Count <= 0)
        {
            return new DynamicModelAnimationSample
            {
                Translation = (sampleTime, System.Numerics.Vector3.Zero),
                Rotation = (sampleTime, System.Numerics.Quaternion.Identity),
            };
        }
        
        var model = Animation.ModelSettings[0];
        var staticIndex = model.StaticTransformationIndex;
        var animatedIndexCurrentFrame = model.AnimationTransformationIndex;
        var currentTranslation = new System.Numerics.Vector3();
        var currentRotation = new vec4();
        var transformChoiceX = model.TranslateX;
        var transformChoiceY = model.TranslateY;
        var transformChoiceZ = model.TranslateZ;
        var rotationChoiceX = model.RotateX;
        var rotationChoiceY = model.RotateY;
        var rotationChoiceZ = model.RotateZ;
        var rotationChoiceW = model.RotateW;
        if (transformChoiceX == Enums.TransformType.Animated)
        {
            currentTranslation.X = Animation.AnimatedTransformations[frame]
                .TransformationValues[animatedIndexCurrentFrame++];
        }
        else
        {
            currentTranslation.X = Animation.StaticTransformations[staticIndex++].Value;
        }
        
        if (transformChoiceY == Enums.TransformType.Animated)
        {
            currentTranslation.Y = Animation.AnimatedTransformations[frame]
                .TransformationValues[animatedIndexCurrentFrame++];
        }
        else
        {
            currentTranslation.Y = Animation.StaticTransformations[staticIndex++].Value;
        }
        
        if (transformChoiceZ == Enums.TransformType.Animated)
        {
            currentTranslation.Z = Animation.AnimatedTransformations[frame]
                .TransformationValues[animatedIndexCurrentFrame++];
        }
        else
        {
            currentTranslation.Z = Animation.StaticTransformations[staticIndex++].Value;
        }

        if (rotationChoiceX == Enums.TransformType.Animated)
        {
            currentRotation.x = (Animation.AnimatedTransformations[frame].TransformationValues[animatedIndexCurrentFrame++]);
        }
        else
        {
            currentRotation.x = (Animation.StaticTransformations[staticIndex++].Value);
        }
        
        if (rotationChoiceY == Enums.TransformType.Animated)
        {
            currentRotation.y = (Animation.AnimatedTransformations[frame].TransformationValues[animatedIndexCurrentFrame++]);
        }
        else
        {
            currentRotation.y = (Animation.StaticTransformations[staticIndex++].Value);
        }
        
        if (rotationChoiceZ == Enums.TransformType.Animated)
        {
            currentRotation.z = (Animation.AnimatedTransformations[frame].TransformationValues[animatedIndexCurrentFrame++]);
        }
        else
        {
            currentRotation.z = (Animation.StaticTransformations[staticIndex++].Value);
        }
        
        if (rotationChoiceW == Enums.TransformType.Animated)
        {
            currentRotation.w = (Animation.AnimatedTransformations[frame].TransformationValues[animatedIndexCurrentFrame]);
        }
        else
        {
            currentRotation.w = (Animation.StaticTransformations[staticIndex].Value);
        }

        var quat = new quat(currentRotation.xyz);
        return new DynamicModelAnimationSample
        {
            Translation = (sampleTime, currentTranslation),
            Rotation = (sampleTime, new System.Numerics.Quaternion(quat.x, quat.y, quat.z, quat.w)),
        };
    }
}