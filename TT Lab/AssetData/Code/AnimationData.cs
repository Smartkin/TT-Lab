using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.AssetData.Code
{
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
        public AnimationData()
        {
            MainAnimation = new();
            FacialAnimation = new();
        }

        public AnimationData(ITwinAnimation animation) : this()
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

        public JointAnimationSample GetAnimationSampleForMainAnimation(int jointIndex, OGIData ogiData, int currentAnimationFrame)
        {
            var twinAnimation = MainAnimation;
            var jointSettings = MainAnimation.JointSettings[jointIndex];
            var useAddRot = jointSettings.UseAdditionalRotation;
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

        public List<JointAnimationSample> GetAnimationKeyframesForMainAnimation(int jointIndex, OGIData ogiData)
        {
            var keyframes = new List<JointAnimationSample>();
            for (var i = 0; i < TotalFrames; ++i)
            {
                keyframes.Add(GetAnimationSampleForMainAnimation(jointIndex, ogiData, i));
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
}
