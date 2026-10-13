using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using DefaultHashes = Twinsanity.TwinsanityInterchange.Enumerations.DefaultHashes;

namespace TT_Lab.AssetData.Code;

public struct JointAnimationSample
{
    public (float, System.Numerics.Vector3) Translation;
    public (float, System.Numerics.Quaternion) Rotation;
    public (float, System.Numerics.Vector3) Scale;
}

public struct MorphAnimationSample
{
    public float Time;
    public float[] Weights;
}

/// <summary>
/// One of an OGI's animations, kept the way the game stores it
/// </summary>
public class AnimationData
{
    public AnimationData()
    {
        Name = string.Empty;
        MainAnimation = new TwinAnimation();
        FacialAnimation = new TwinMorphAnimation();
    }

    public AnimationData(ITwinAnimation animation) : this()
    {
        ID = animation.GetID();
        Name = RetailNames.Of(DefaultHashes.Animations, animation.GetID(), animation.GetName());
        TotalFrames = animation.TotalFrames;
        DefaultFPS = animation.DefaultFPS;
        MainAnimation = CloneUtils.DeepClone(animation.MainAnimation);
        FacialAnimation = CloneUtils.DeepClone(animation.FacialAnimation);
    }

    /// <summary>
    /// The game's ID of the animation, game objects pick it by it in their OGI
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public UInt32 ID { get; set; }
    [JsonProperty(Required = Required.Always)]
    public String Name { get; set; }
    [JsonProperty(Required = Required.Always)]
    public UInt16 TotalFrames { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Byte DefaultFPS { get; set; }
    [JsonProperty(Required = Required.Always)]
    public TwinAnimation MainAnimation { get; set; }
    [JsonProperty(Required = Required.Always)]
    public TwinMorphAnimation FacialAnimation { get; set; }

    private (vec3, vec3, vec3) GetJointSettingsSampleForFrame(int jointIndex, int currentAnimationFrame)
    {
        var twinAnimation = MainAnimation;
        var jointSettings = MainAnimation.JointSettings[jointIndex];
        var transformIndex = jointSettings.TransformationIndex;
        var currentFrameTransformIndex = jointSettings.AnimationTransformationIndex;
        var currentTranslation = new vec3();
        var currentRotation = new vec3();
        var scale = new vec3();
        var translateXChoice = jointSettings.TranslateX;
        var translateYChoice = jointSettings.TranslateY;
        var translateZChoice = jointSettings.TranslateZ;
        var rotXChoice = jointSettings.RotateX;
        var rotYChoice = jointSettings.RotateY;
        var rotZChoice = jointSettings.RotateZ;
        var scaleXChoice = jointSettings.ScaleX;
        var scaleYChoice = jointSettings.ScaleY;
        var scaleZChoice = jointSettings.ScaleZ;

        if (translateXChoice == Enums.TransformType.Animated)
        {
            currentTranslation.x = twinAnimation.AnimatedTransformations[currentAnimationFrame].Transforms[currentFrameTransformIndex++].Value;
        }
        else
        {
            currentTranslation.x = twinAnimation.StaticTransformations[transformIndex++].Value;
        }
            
        if (translateYChoice == Enums.TransformType.Animated)
        {
            currentTranslation.y = twinAnimation.AnimatedTransformations[currentAnimationFrame].Transforms[currentFrameTransformIndex++].Value;
        }
        else
        {
            currentTranslation.y = twinAnimation.StaticTransformations[transformIndex++].Value;
        }
            
        if (translateZChoice == Enums.TransformType.Animated)
        {
            currentTranslation.z = twinAnimation.AnimatedTransformations[currentAnimationFrame].Transforms[currentFrameTransformIndex++].Value;
        }
        else
        {
            currentTranslation.z = twinAnimation.StaticTransformations[transformIndex++].Value;
        }

        if (rotXChoice == Enums.TransformType.Animated)
        {
            var rot1 = twinAnimation.AnimatedTransformations[currentAnimationFrame].Transforms[currentFrameTransformIndex++].RotationValue;
            currentRotation.x = rot1;
        }
        else
        {
            var rot = twinAnimation.StaticTransformations[transformIndex++].RotationValue;
            currentRotation.x = rot;
        }
            
        if (rotYChoice == Enums.TransformType.Animated)
        {
            var rot1 = twinAnimation.AnimatedTransformations[currentAnimationFrame].Transforms[currentFrameTransformIndex++].RotationValue;
            currentRotation.y = rot1;
        }
        else
        {
            var rot = twinAnimation.StaticTransformations[transformIndex++].RotationValue;
            currentRotation.y = rot;
        }
            
        if (rotZChoice == Enums.TransformType.Animated)
        {
            var rot1 = twinAnimation.AnimatedTransformations[currentAnimationFrame].Transforms[currentFrameTransformIndex++].RotationValue;
            currentRotation.z = rot1;
        }
        else
        {
            var rot = twinAnimation.StaticTransformations[transformIndex++].RotationValue;
            currentRotation.z = rot;
        }

        if (scaleXChoice == Enums.TransformType.Animated)
        {
            var val1 = twinAnimation.AnimatedTransformations[currentAnimationFrame].Transforms[currentFrameTransformIndex++].Value;
            scale.x = val1;
        }
        else
        {
            scale.x = twinAnimation.StaticTransformations[transformIndex++].Value;
        }
            
        if (scaleYChoice == Enums.TransformType.Animated)
        {
            var val1 = twinAnimation.AnimatedTransformations[currentAnimationFrame].Transforms[currentFrameTransformIndex++].Value;
            scale.y = val1;
        }
        else
        {
            scale.y = twinAnimation.StaticTransformations[transformIndex++].Value;
        }
            
        if (scaleZChoice == Enums.TransformType.Animated)
        {
            var val1 = twinAnimation.AnimatedTransformations[currentAnimationFrame].Transforms[currentFrameTransformIndex].Value;
            scale.z = val1;
        }
        else
        {
            scale.z = twinAnimation.StaticTransformations[transformIndex].Value;
        }

        return (currentTranslation, currentRotation, scale);
    }

    public JointAnimationSample GetAnimationSampleForMainAnimation(int jointIndex, int parentIndex, OGIData ogiData, int currentAnimationFrame)
    {
        var jointSettings = MainAnimation.JointSettings[jointIndex];
        var useAddRot = jointSettings.UseAdditionalRotation;
        var jointPosition = GetJointSettingsSampleForFrame(jointIndex, currentAnimationFrame);
        var currentTranslation = jointPosition.Item1;
        var currentRotation = jointPosition.Item2;
        var scale = jointPosition.Item3;
        
        var resultTranslation = currentTranslation;
        var quat1 = new quat(currentRotation);
        var lerpedQuat = quat1;

        var resRotationQuat = lerpedQuat;
        if (useAddRot)
        {
            var additionalRotation = ogiData.Joints[jointIndex].AdditionalAnimationRotation;
            var addRotQuat = new quat(additionalRotation.X, additionalRotation.Y, additionalRotation.Z, additionalRotation.W);
            resRotationQuat = addRotQuat * lerpedQuat;
        }

        var systemPosition = new System.Numerics.Vector3(resultTranslation.x, resultTranslation.y, resultTranslation.z);
        var systemQuaternion = new System.Numerics.Quaternion(resRotationQuat.x, resRotationQuat.y, resRotationQuat.z, resRotationQuat.w);
        var systemScale = new System.Numerics.Vector3(scale.x, scale.y, scale.z);
        var animationSample = new JointAnimationSample();
        var sampleTime = (float)currentAnimationFrame / DefaultFPS;
        animationSample.Translation = (sampleTime, systemPosition);
        animationSample.Rotation = (sampleTime, systemQuaternion);
        animationSample.Scale = (sampleTime, systemScale);
        return animationSample;
    }

    /// <summary>
    /// The joint's local transform a share of the way from a frame to the next, worked out the way the game's AnimateJoint does
    /// </summary>
    /// <remarks>
    /// Translations, scales and each of the three Euler angles go straight from a frame's value to the next's, an angle taken round to
    /// the next's side when they're more than half a turn apart (ReadJointAngle), then the rotation of the angles is turned by the joint's
    /// additional rotation. Two frames of nearby rotations whose angles are far apart (the other set of angles of a rotation) swing the
    /// joint around on the way
    /// </remarks>
    public (vec3 Translation, quat Rotation, vec3 Scale) GetJointPose(int jointIndex, OGIData ogiData, int frame, int nextFrame, float share)
    {
        const int turnShift = 4;
        const int halfTurn = 0x8000;
        const int fullTurn = 0x10000;
        const float angleToRadians = MathF.PI / halfTurn;
        var frames = MainAnimation.AnimatedTransformations.Count;
        var now = TlmAnimations.GetRawValues(this, jointIndex, frames == 0 ? -1 : Math.Clamp(frame, 0, frames - 1));
        var next = TlmAnimations.GetRawValues(this, jointIndex, frames == 0 ? -1 : Math.Clamp(nextFrame, 0, frames - 1));
        var angles = new float[3];
        for (var axis = 0; axis < angles.Length; axis++)
        {
            var current = now[3 + axis] << turnShift;
            var coming = next[3 + axis] << turnShift;
            var difference = current - coming;
            if (difference > halfTurn)
            {
                current -= fullTurn;
            }
            else if (difference < -halfTurn)
            {
                current += fullTurn;
            }

            angles[axis] = Toward(current * angleToRadians, coming * angleToRadians);
        }

        var rotation = new quat(new vec3(angles[0], angles[1], angles[2]));
        if (MainAnimation.JointSettings[jointIndex].UseAdditionalRotation && jointIndex < ogiData.Joints.Count)
        {
            var additional = ogiData.Joints[jointIndex].AdditionalAnimationRotation;
            rotation = new quat(additional.X, additional.Y, additional.Z, additional.W) * rotation;
        }

        return (Values(0), rotation, Values(6));

        float Toward(float from, float to) => from + (to - from) * share;

        vec3 Values(int channel) => new(Toward(now[channel] / 4096f, next[channel] / 4096f), Toward(now[channel + 1] / 4096f, next[channel + 1] / 4096f),
            Toward(now[channel + 2] / 4096f, next[channel + 2] / 4096f));
    }

    public MorphAnimationSample GetAnimationSampleForMorphAnimation(int animationFrame)
    {
        var morphSettings = FacialAnimation.JointSettings[0];
        var shapesAmount = morphSettings.FacialShapesAmount;
        var weights = new float[shapesAmount];
        var transformIndex = morphSettings.TransformationIndex;
        var animatedTransformIndex = morphSettings.AnimationTransformationIndex;

        for (var i = 0; i < shapesAmount; i++)
        {
            if (morphSettings.AnimationMorph[i] == Enums.TransformType.Animated)
            {
                var f1 = FacialAnimation.AnimatedTransformations[animationFrame].Transforms[animatedTransformIndex++].Value;
                weights[i] = f1;
            }
            else
            {
                weights[i] = FacialAnimation.StaticTransformations[transformIndex++].Value;
            }
        }

        var sampleTime = (float)animationFrame / DefaultFPS;
        return new MorphAnimationSample { Weights = weights, Time = sampleTime };
    }

    public List<JointAnimationSample> GetAnimationKeyframesForMainAnimation(int jointIndex, OGIData ogiData, int parentIndex)
    {
        var keyframes = new List<JointAnimationSample>();
        for (var i = 0; i < TotalFrames; ++i)
        {
            keyframes.Add(GetAnimationSampleForMainAnimation(jointIndex, parentIndex, ogiData, i));
        }

        return keyframes;
    }

    public List<MorphAnimationSample> GetAnimationKeyframesForMorphAnimation()
    {
        var keyframes = new List<MorphAnimationSample>();
        for (var i = 0; i < TotalFrames; ++i)
        {
            keyframes.Add(GetAnimationSampleForMorphAnimation(i));
        }
            
        return keyframes;
    }

    public ITwinAnimation Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(TotalFrames);
        writer.Write(DefaultFPS);
        MainAnimation.Write(writer);
        FacialAnimation.Write(writer);

        writer.Flush();
        ms.Position = 0;
        var animation = factory.GenerateAnimation(ms);
        animation.SetID(ID);
        return animation;
    }
}