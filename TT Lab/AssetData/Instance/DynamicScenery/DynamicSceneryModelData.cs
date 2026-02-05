using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlmSharp;
using SharpGLTF.Schema2;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using AnimatedTransformation = Twinsanity.TwinsanityInterchange.Common.DynamicScenery.AnimatedTransformation;
using Mesh = TT_Lab.Assets.Graphics.Mesh;
using Transformation = Twinsanity.TwinsanityInterchange.Common.DynamicScenery.Transformation;

namespace TT_Lab.AssetData.Instance.DynamicScenery;

public struct DynamicModelAnimationSample
{
    public (float, System.Numerics.Vector3) Translation;
    public (float, System.Numerics.Quaternion) Rotation;
}

[ReferencesAssets]
public class DynamicSceneryModelData
{
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonListConverter<TwinBoundingBoxBuilder>))]
    public List<TwinBoundingBoxBuilder> BoundingBoxBuilders { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public Int32 AnimatedFrames { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public TwinDynamicSceneryAnimation Animation { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public Byte LodFlag { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore]
    public LabURI Mesh { get; set; }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonListConverter<Vector4>))]
    public List<Vector4> BoundingBox { get; set; }

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
        BoundingBox = [new Vector4(), new Vector4()];
        for (Int32 i = 0; i < BoundingBox.Count; i++)
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

    private Enums.TransformType IsPropertyAnimated(List<(float, float)> propertyTimeline)
    {
        return propertyTimeline.All(tuple => Math.Abs(tuple.Item2 - propertyTimeline[0].Item2) < 0.000001)
            ? Enums.TransformType.Static
            : Enums.TransformType.Animated;
    }

    public void ReadAnimationFromGltf(NodeCurveSamplers gltfAnimation)
    {
        Animation = new TwinDynamicSceneryAnimation();
        var settings = new DynamicModelSettings
        {
            UnknownValue = 22,
            UnusedRotationRelatedParameter = 7,
            StaticTransformationIndex = 0,
            AnimationTransformationIndex = 0
        };
        Animation.ModelSettings.Add(settings);
        var translations = gltfAnimation.Translation.GetLinearKeys().ToList();
        var rotations = gltfAnimation.Rotation.GetLinearKeys().ToList();
        var rotationsEuler = rotations.Select(tuple => (tuple.Key, tuple.Value.ToTwin().ToEulerAngles())).ToList();
        AnimatedFrames = translations.Count;
        Animation.TotalFrames = (ushort)AnimatedFrames;

        var translationsX = translations.Select(tuple => (tuple.Key, tuple.Value.X)).ToList();
        var translationsY = translations.Select(tuple => (tuple.Key, tuple.Value.Y)).ToList();
        var translationsZ = translations.Select(tuple => (tuple.Key, tuple.Value.Z)).ToList();
        var rotationsX = rotationsEuler.Select(tuple => (tuple.Key, tuple.Item2.X)).ToList();
        var rotationsY = rotationsEuler.Select(tuple => (tuple.Key, tuple.Item2.Y)).ToList();
        var rotationsZ = rotationsEuler.Select(tuple => (tuple.Key, tuple.Item2.Z)).ToList();
        settings.TranslateX = IsPropertyAnimated(translationsX);
        settings.TranslateY = IsPropertyAnimated(translationsY);
        settings.TranslateZ = IsPropertyAnimated(translationsZ);
        settings.RotateX = IsPropertyAnimated(rotationsX);
        settings.RotateY = IsPropertyAnimated(rotationsY);
        settings.RotateZ = IsPropertyAnimated(rotationsZ);
        settings.RotateW = Enums.TransformType.Static;

        ushort animatedPropertiesAmount = 0;
        
        if (settings.TranslateX == Enums.TransformType.Static)
        {
            Animation.StaticTransformations.Add(new Transformation
            {
                Value = translationsX[0].X
            });
        }
        else
        {
            animatedPropertiesAmount++;
        }

        if (settings.TranslateY == Enums.TransformType.Static)
        {
            Animation.StaticTransformations.Add(new Transformation
            {
                Value = translationsY[0].Y
            });
        }
        else
        {
            animatedPropertiesAmount++;
        }

        if (settings.TranslateZ == Enums.TransformType.Static)
        {
            Animation.StaticTransformations.Add(new Transformation
            {
                Value = translationsZ[0].Z
            });
        }
        else
        {
            animatedPropertiesAmount++;
        }

        if (settings.RotateX == Enums.TransformType.Static)
        {
            Animation.StaticTransformations.Add(new Transformation
            {
                Value = rotationsX[0].X
            });
        }
        else
        {
            animatedPropertiesAmount++;
        }

        if (settings.RotateY == Enums.TransformType.Static)
        {
            Animation.StaticTransformations.Add(new Transformation
            {
                Value = rotationsY[0].Y
            });
        }
        else
        {
            animatedPropertiesAmount++;
        }

        if (settings.RotateZ == Enums.TransformType.Static)
        {
            Animation.StaticTransformations.Add(new Transformation
            {
                Value = rotationsZ[0].Z
            });
        }
        else
        {
            animatedPropertiesAmount++;
        }
        
        Animation.StaticTransformations.Add(new Transformation
        {
            Value = 1.0f
        });

        for (var i = 0; i < AnimatedFrames; i++)
        {
            var transformIndex = 0;
            Animation.AnimatedTransformations.Add(new AnimatedTransformation(animatedPropertiesAmount));
            for (var j = 0; j < animatedPropertiesAmount; j++)
            {
                Animation.AnimatedTransformations[i].TransformationValues.Add(0.0f);
            }

            if (settings.TranslateX == Enums.TransformType.Animated)
            {
                Animation.AnimatedTransformations[i].TransformationValues[transformIndex++] = translationsX[i].X;
            }
            
            if (settings.TranslateY == Enums.TransformType.Animated)
            {
                Animation.AnimatedTransformations[i].TransformationValues[transformIndex++] = translationsY[i].Y;
            }
            
            if (settings.TranslateZ == Enums.TransformType.Animated)
            {
                Animation.AnimatedTransformations[i].TransformationValues[transformIndex++] = translationsZ[i].Z;
            }

            if (settings.RotateX == Enums.TransformType.Animated)
            {
                Animation.AnimatedTransformations[i].TransformationValues[transformIndex++] = rotationsX[i].X;
            }

            if (settings.RotateY == Enums.TransformType.Animated)
            {
                Animation.AnimatedTransformations[i].TransformationValues[transformIndex++] = rotationsY[i].Y;
            }

            if (settings.RotateZ == Enums.TransformType.Animated)
            {
                Animation.AnimatedTransformations[i].TransformationValues[transformIndex] = rotationsZ[i].Z;
            }
        }
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
        writer.Write(AssetManager.Get().GetAsset(Mesh).ExportTwinID);
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