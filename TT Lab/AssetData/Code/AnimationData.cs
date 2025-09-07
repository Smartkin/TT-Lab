using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlmSharp;
using SharpGLTF.Schema2;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

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
    
public class AnimationData : AbstractAssetData
{
    private class GltfAnimation
    {
        public int TotalFrames { get; private set; }
        public int JointIndex { get; private set; }
        public List<vec3> Translations = [];
        public List<quat> Rotations = [];
        public List<vec3> Scales = [];

        public static GltfAnimation GenerateFromGltfSamplers(
            int jointIndex,
            IEnumerable<(float key, System.Numerics.Vector3 translation)> translations,
            IEnumerable<(float key, System.Numerics.Quaternion rotation)> rotations,
            IEnumerable<(float key, System.Numerics.Vector3 scale)> scales)
        {
            var translationList = translations.ToList();
            var rotationList = rotations.ToList();
            var scalesList = scales.ToList();

            var maxTime = Math.Max(translationList.Max(tr => tr.key), Math.Max(rotationList.Max(r => r.key), scalesList.Max(s => s.key)));
            const float sampleRate = 1.0f / 25.0f;
            var framesAmount = (int)Math.Floor(maxTime / sampleRate) + 1;
            var result = new GltfAnimation
            {
                JointIndex = jointIndex,
                TotalFrames = framesAmount,
            };
            var time = 0.0f;
            do
            {
                
                time += sampleRate;
            } while (time < maxTime);
            
            return result;
        }
    }
    
    public AnimationData(IAsset asset) : base(asset)
    {
        MainAnimation = new TwinAnimation();
        FacialAnimation = new TwinMorphAnimation();
    }

    public AnimationData(IAsset asset, ITwinAnimation animation) : this(asset)
    {
        SetTwinItem(animation);
    }

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

    public void LoadFromGltf(Animation gltfAnim)
    {
        DefaultFPS = 25;
        var jointList = gltfAnim.Channels.ToList();
        jointList.Sort((ac1, ac2) => ac1.TargetNode.LogicalIndex - ac2.TargetNode.LogicalIndex);
        var mainAnimation = new TwinAnimation();
        var jointSettings = new List<JointSettings>(jointList.Count);
        var staticTransforms = new List<Transformation>();
        var animatedTransforms = new List<AnimatedTransformation>();
        mainAnimation.JointSettings = jointSettings;
        mainAnimation.StaticTransformations = staticTransforms;
        mainAnimation.AnimatedTransformations = animatedTransforms;
        for (var i = 0; i < jointList.Count; i++)
        {
            jointSettings[i] = new JointSettings
            {
                TranslateX = Enums.TransformType.Static,
                TranslateY = Enums.TransformType.Static,
                TranslateZ = Enums.TransformType.Static,
                RotateX = Enums.TransformType.Static,
                RotateY = Enums.TransformType.Static,
                RotateZ = Enums.TransformType.Static,
                ScaleX = Enums.TransformType.Static,
                ScaleY = Enums.TransformType.Static,
                ScaleZ = Enums.TransformType.Static,
                UseAdditionalRotation = false,
                IndependentScaling = false,
            };
        }

        TotalFrames = (ushort)gltfAnim.Channels.Max(c => Math.Max(c.GetScaleSampler().GetLinearKeys().Count(), Math.Max(c.GetTranslationSampler().GetLinearKeys().Count(), c.GetRotationSampler().GetLinearKeys().Count())));
        
        for (var i = 0; i < jointList.Count; ++i)
        {
            var channel = jointList[i];
            var jointSetting = jointSettings[i];
            jointSetting.TransformationIndex = (ushort)staticTransforms.Count;
            jointSetting.AnimationTransformationIndex = (ushort)animatedTransforms.Count;
            var translationSampler = channel.GetTranslationSampler().GetLinearKeys().ToList();
            var rotationSampler = channel.GetRotationSampler().GetLinearKeys().ToList();
            var scaleSampler = channel.GetScaleSampler().GetLinearKeys().ToList();
            var initialTranslation = translationSampler[0].Value;
            var translationX = initialTranslation.X;
            var translationY = initialTranslation.Y;
            var translationZ = initialTranslation.Z;
            foreach (var kv in translationSampler.Skip(1))
            {
                var newTranslationX = kv.Value.X;
                if (!(Math.Abs(translationX - newTranslationX) > 0.0001f))
                {
                    continue;
                }
                
                jointSetting.TranslateX = Enums.TransformType.Animated;
                break;
            }
            
            foreach (var kv in translationSampler.Skip(1))
            {
                var newTranslationY = kv.Value.Y;
                if (!(Math.Abs(translationY - newTranslationY) > 0.0001f))
                {
                    continue;
                }
                
                jointSetting.TranslateY = Enums.TransformType.Animated;
                break;
            }
            
            foreach (var kv in translationSampler.Skip(1))
            {
                var newTranslationZ = kv.Value.Z;
                if (!(Math.Abs(translationZ - newTranslationZ) > 0.0001f))
                {
                    continue;
                }
                
                jointSetting.TranslateZ = Enums.TransformType.Animated;
                break;
            }

            var rotationEuler = rotationSampler[0].Value.ToTwin().ToEulerAngles();
            var rotationX = rotationEuler.X;
            var rotationY = rotationEuler.Y;
            var rotationZ = rotationEuler.Z;
            foreach (var kv in rotationSampler.Skip(1))
            {
                var newRotationEuler = kv.Value.ToTwin().ToEulerAngles();
                if (Math.Abs(rotationX - newRotationEuler.X) > 0.0001f)
                {
                    jointSetting.RotateX = Enums.TransformType.Animated;
                }
                if (Math.Abs(rotationY - newRotationEuler.Y) > 0.0001f)
                {
                    jointSetting.RotateY = Enums.TransformType.Animated;
                }
                if (Math.Abs(rotationZ - newRotationEuler.Z) > 0.0001f)
                {
                    jointSetting.RotateZ = Enums.TransformType.Animated;
                }
            }
            
            var initialScale = scaleSampler[0].Value;
            var scaleX = initialScale.X;
            var scaleY = initialScale.Y;
            var scaleZ = initialScale.Z;
            foreach (var kv in scaleSampler.Skip(1))
            {
                var newScaleX = kv.Value.X;
                if (!(Math.Abs(scaleX - newScaleX) > 0.0001f))
                {
                    continue;
                }
                
                jointSetting.ScaleX = Enums.TransformType.Animated;
                break;
            }
            
            foreach (var kv in scaleSampler.Skip(1))
            {
                var newScaleY = kv.Value.Y;
                if (!(Math.Abs(scaleY - newScaleY) > 0.0001f))
                {
                    continue;
                }
                
                jointSetting.ScaleY = Enums.TransformType.Animated;
                break;
            }
            
            foreach (var kv in scaleSampler.Skip(1))
            {
                var newScaleZ = kv.Value.Z;
                if (!(Math.Abs(scaleZ - newScaleZ) > 0.0001f))
                {
                    continue;
                }
                
                jointSetting.ScaleZ = Enums.TransformType.Animated;
                break;
            }

            if (jointSetting.TranslateX == Enums.TransformType.Static)
            {
                var transformation = new Transformation
                {
                    Value = initialTranslation.X
                };
                staticTransforms.Add(transformation);
            }
            else
            {
                var animatedTransform = new AnimatedTransformation((ushort)translationSampler.Count);
                foreach (var sample in translationSampler)
                {
                }
            }
            
            if (jointSetting.TranslateY == Enums.TransformType.Static)
            {
                var transformation = new Transformation
                {
                    Value = initialTranslation.Y
                };
                staticTransforms.Add(transformation);
            }
            else
            {
                
            }
            
            if (jointSetting.TranslateZ == Enums.TransformType.Static)
            {
                var transformation = new Transformation
                {
                    Value = initialTranslation.Z
                };
                staticTransforms.Add(transformation);
            }
            else
            {
                
            }

            if (jointSetting.RotateX == Enums.TransformType.Static)
            {
                var transformation = new Transformation
                {
                    RotationValue = rotationEuler.X
                };
                staticTransforms.Add(transformation);
            }
            else
            {
                
            }
            
            if (jointSetting.RotateY == Enums.TransformType.Static)
            {
                var transformation = new Transformation
                {
                    RotationValue = rotationEuler.Y
                };
                staticTransforms.Add(transformation);
            }
            else
            {
                
            }
            
            if (jointSetting.RotateZ == Enums.TransformType.Static)
            {
                var transformation = new Transformation
                {
                    RotationValue = rotationEuler.Z
                };
                staticTransforms.Add(transformation);
            }
            else
            {
                
            }
            
            if (jointSetting.ScaleX == Enums.TransformType.Static)
            {
                var transformation = new Transformation
                {
                    Value = initialScale.X
                };
                staticTransforms.Add(transformation);
            }
            else
            {
                
            }
            
            if (jointSetting.ScaleY == Enums.TransformType.Static)
            {
                var transformation = new Transformation
                {
                    Value = initialScale.Y
                };
                staticTransforms.Add(transformation);
            }
            else
            {
                
            }
            
            if (jointSetting.ScaleZ == Enums.TransformType.Static)
            {
                var transformation = new Transformation
                {
                    Value = initialScale.Z
                };
                staticTransforms.Add(transformation);
            }
            else
            {
                
            }
        }
    }
    
    protected override void Dispose(Boolean disposing)
    {
        return;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var twinAnimation = GetTwinItem<ITwinAnimation>();
        TotalFrames = twinAnimation.TotalFrames;
        DefaultFPS = twinAnimation.DefaultFPS;
        MainAnimation = CloneUtils.DeepClone(twinAnimation.MainAnimation);
        FacialAnimation = CloneUtils.DeepClone(twinAnimation.FacialAnimation);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(TotalFrames);
        writer.Write(DefaultFPS);
        MainAnimation.Write(writer);
        FacialAnimation.Write(writer);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateAnimation(ms);
    }
}