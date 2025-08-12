using System;
using GlmSharp;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;

namespace TT_Lab.Rendering.Objects;

public class DynamicSceneryMesh : Renderable
{
    private readonly Mesh _mesh;
    private readonly TwinDynamicSceneryAnimation _animation;
    private int _currentAnimationFrame = 0;
    private readonly float _animationFps;
    private float _frameFraction = 0.0f;
    private float _frameDisplacement = 0.0f;

    public DynamicSceneryMesh(RenderContext context, Mesh mesh, TwinDynamicSceneryAnimation animation, float fps = 50.0f) : base(context)
    {
        _mesh = mesh;
        _animation = animation;
        _animationFps = fps;
        AddChild(mesh);
    }

    public override bool DoesUpdates => true;

    protected override void UpdateSelf(float delta)
    {
        base.UpdateSelf(delta);
        
        var nextFrame = (ushort)Math.Min(_currentAnimationFrame + 1, _animation.TotalFrames - 1);
        if (_currentAnimationFrame == _animation.TotalFrames - 1)
        {
            nextFrame = 0;
        }
        
        var interpolationLength = 1.0f / _animationFps;
        _frameFraction += delta;
        _frameDisplacement = _frameFraction / interpolationLength;
        if (_frameFraction >= interpolationLength)
        {
            _frameDisplacement = 0;
            _frameFraction = 0;
            if (nextFrame == 0)
            {
                _currentAnimationFrame = 0;
                nextFrame = 1;
            }
            else
            {
                _currentAnimationFrame = (ushort)Math.Min(_currentAnimationFrame + 1, _animation.TotalFrames - 1);
                nextFrame = (ushort)Math.Min(_currentAnimationFrame + 1, _animation.TotalFrames - 1);
            }
        }

        if (nextFrame == _currentAnimationFrame)
        {
            nextFrame = 0;
        }

        var translationRotation = GetTransformFromAnimation(nextFrame);
        _mesh.SetPosition(vec3.Zero);
        _mesh.LocalTransform = mat4.Translate(translationRotation.Item1) * translationRotation.Item2.ToMat4;
    }

    private (vec3, quat) GetTransformFromAnimation(int nextFrame)
    {
        if (_animation.ModelSettings.Count <= 0)
        {
            return (vec3.Zero, quat.Identity);
        }
        var model = _animation.ModelSettings[0];
        var staticIndex = model.StaticTransformationIndex;
        var animatedIndexCurrentFrame = model.AnimationTransformationIndex;
        var animatedIndexNextFrame = animatedIndexCurrentFrame;
        var currentTranslation = vec3.Zero;
        var nextTranslation = vec3.Zero;
        var currentRotation = vec4.Zero;
        var nextRotation = vec4.Zero;
        var transformChoiceX = model.TranslateX;
        var transformChoiceY = model.TranslateY;
        var transformChoiceZ = model.TranslateZ;
        var rotationChoiceX = model.RotateX;
        var rotationChoiceY = model.RotateY;
        var rotationChoiceZ = model.RotateZ;
        var rotationChoiceW = model.RotateW;
        if (transformChoiceX == Enums.TransformType.Animated)
        {
            currentTranslation.x = _animation.AnimatedTransformations[_currentAnimationFrame]
                .TransformationValues[animatedIndexCurrentFrame++];
            nextTranslation.x = _animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++];
        }
        else
        {
            currentTranslation.x = _animation.StaticTransformations[staticIndex].Value;
            nextTranslation.x = _animation.StaticTransformations[staticIndex++].Value;
        }
        
        if (transformChoiceY == Enums.TransformType.Animated)
        {
            currentTranslation.y = _animation.AnimatedTransformations[_currentAnimationFrame]
                .TransformationValues[animatedIndexCurrentFrame++];
            nextTranslation.y = _animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++];
        }
        else
        {
            currentTranslation.y = _animation.StaticTransformations[staticIndex].Value;
            nextTranslation.y = _animation.StaticTransformations[staticIndex++].Value;
        }
        
        if (transformChoiceZ == Enums.TransformType.Animated)
        {
            currentTranslation.z = _animation.AnimatedTransformations[_currentAnimationFrame]
                .TransformationValues[animatedIndexCurrentFrame++];
            nextTranslation.z = _animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++];
        }
        else
        {
            currentTranslation.z = _animation.StaticTransformations[staticIndex].Value;
            nextTranslation.z = _animation.StaticTransformations[staticIndex++].Value;
        }

        if (rotationChoiceX == Enums.TransformType.Animated)
        {
            currentRotation.x = (_animation.AnimatedTransformations[_currentAnimationFrame].TransformationValues[animatedIndexCurrentFrame++]);
            nextRotation.x = (_animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++]);
        }
        else
        {
            currentRotation.x = (_animation.StaticTransformations[staticIndex].Value);
            nextRotation.x = (_animation.StaticTransformations[staticIndex++].Value);
        }
        
        if (rotationChoiceY == Enums.TransformType.Animated)
        {
            currentRotation.y = (_animation.AnimatedTransformations[_currentAnimationFrame].TransformationValues[animatedIndexCurrentFrame++]);
            nextRotation.y = (_animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++]);
        }
        else
        {
            currentRotation.y = (_animation.StaticTransformations[staticIndex].Value);
            nextRotation.y = (_animation.StaticTransformations[staticIndex++].Value);
        }
        
        if (rotationChoiceZ == Enums.TransformType.Animated)
        {
            currentRotation.z = (_animation.AnimatedTransformations[_currentAnimationFrame].TransformationValues[animatedIndexCurrentFrame++]);
            nextRotation.z = (_animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++]);
        }
        else
        {
            currentRotation.z = (_animation.StaticTransformations[staticIndex].Value);
            nextRotation.z = (_animation.StaticTransformations[staticIndex++].Value);
        }
        
        if (rotationChoiceW == Enums.TransformType.Animated)
        {
            currentRotation.w = (_animation.AnimatedTransformations[_currentAnimationFrame].TransformationValues[animatedIndexCurrentFrame]);
            nextRotation.w = (_animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame]);
        }
        else
        {
            currentRotation.w = (_animation.StaticTransformations[staticIndex].Value);
            nextRotation.w = (_animation.StaticTransformations[staticIndex].Value);
        }
        
        var currentRotQuat = new quat(currentRotation.xyz);
        var nextRotQuat = new quat(nextRotation.xyz);
        var resultRotation = GlmExtensions.SLerpSafe(currentRotQuat, nextRotQuat, _frameDisplacement);
        var resultTranslation = MathExtension.Lerp(currentTranslation, nextTranslation, _frameDisplacement);
        
        return (resultTranslation, resultRotation);
    }
}