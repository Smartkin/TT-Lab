using System;
using GlmSharp;
using Silk.NET.Maths;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;

namespace TT_Lab.Rendering.Objects;

public class DynamicSceneryMesh : Renderable
{
    private readonly Mesh _mesh;
    private readonly TwinDynamicSceneryAnimation _animation;
    private readonly int _frames;
    private double _time;

    // The game loops over the model's frames, reading its animation's values: no more than the animation has values of
    public DynamicSceneryMesh(RenderContext context, Mesh mesh, TwinDynamicSceneryAnimation animation, int frames) : base(context)
    {
        _mesh = mesh;
        _animation = animation;
        _frames = Math.Min(frames, animation.AnimatedTransformations.Count);
        AddChild(mesh);
    }

    // The model's box in its own space, which its animation moves along with it
    public vec3 BoundsMin { get; init; }
    public vec3 BoundsMax { get; init; }

    public mat4 ModelTransform => _mesh.WorldTransform;

    public override bool DoesUpdates => true;

    protected override void UpdateSelf(float delta)
    {
        base.UpdateSelf(delta);

        // At the game's rate whatever the viewport's: stepping a frame of 50 a second at most once an update and dropping the time past it
        // played 1.2 times the game's rate at 60 FPS, and slower as the viewport slowed down
        var playback = DynamicSceneryPlayback.At(_time + delta, _frames);
        _time = playback.Seconds;
        var translationRotation = GetTransformFromAnimation(playback.Frame, playback.Next, playback.Share);
        _mesh.SetPosition(vec3.Zero);
        _mesh.LocalTransform = mat4.Translate(translationRotation.Item1) * translationRotation.Item2.ToMat4;
    }

    private (vec3, quat) GetTransformFromAnimation(int frame, int nextFrame, float share)
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
            currentTranslation.x = _animation.AnimatedTransformations[frame].TransformationValues[animatedIndexCurrentFrame++];
            nextTranslation.x = _animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++];
        }
        else
        {
            currentTranslation.x = _animation.StaticTransformations[staticIndex].Value;
            nextTranslation.x = _animation.StaticTransformations[staticIndex++].Value;
        }
        
        if (transformChoiceY == Enums.TransformType.Animated)
        {
            currentTranslation.y = _animation.AnimatedTransformations[frame].TransformationValues[animatedIndexCurrentFrame++];
            nextTranslation.y = _animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++];
        }
        else
        {
            currentTranslation.y = _animation.StaticTransformations[staticIndex].Value;
            nextTranslation.y = _animation.StaticTransformations[staticIndex++].Value;
        }
        
        if (transformChoiceZ == Enums.TransformType.Animated)
        {
            currentTranslation.z = _animation.AnimatedTransformations[frame].TransformationValues[animatedIndexCurrentFrame++];
            nextTranslation.z = _animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++];
        }
        else
        {
            currentTranslation.z = _animation.StaticTransformations[staticIndex].Value;
            nextTranslation.z = _animation.StaticTransformations[staticIndex++].Value;
        }

        if (rotationChoiceX == Enums.TransformType.Animated)
        {
            currentRotation.x = (_animation.AnimatedTransformations[frame].TransformationValues[animatedIndexCurrentFrame++]);
            nextRotation.x = (_animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++]);
        }
        else
        {
            currentRotation.x = (_animation.StaticTransformations[staticIndex].Value);
            nextRotation.x = (_animation.StaticTransformations[staticIndex++].Value);
        }
        
        if (rotationChoiceY == Enums.TransformType.Animated)
        {
            currentRotation.y = (_animation.AnimatedTransformations[frame].TransformationValues[animatedIndexCurrentFrame++]);
            nextRotation.y = (_animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++]);
        }
        else
        {
            currentRotation.y = (_animation.StaticTransformations[staticIndex].Value);
            nextRotation.y = (_animation.StaticTransformations[staticIndex++].Value);
        }
        
        if (rotationChoiceZ == Enums.TransformType.Animated)
        {
            currentRotation.z = (_animation.AnimatedTransformations[frame].TransformationValues[animatedIndexCurrentFrame++]);
            nextRotation.z = (_animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame++]);
        }
        else
        {
            currentRotation.z = (_animation.StaticTransformations[staticIndex].Value);
            nextRotation.z = (_animation.StaticTransformations[staticIndex++].Value);
        }
        
        if (rotationChoiceW == Enums.TransformType.Animated)
        {
            currentRotation.w = (_animation.AnimatedTransformations[frame].TransformationValues[animatedIndexCurrentFrame]);
            nextRotation.w = (_animation.AnimatedTransformations[nextFrame].TransformationValues[animatedIndexNextFrame]);
        }
        else
        {
            currentRotation.w = (_animation.StaticTransformations[staticIndex].Value);
            nextRotation.w = (_animation.StaticTransformations[staticIndex].Value);
        }
        
        var currentRotQuat = new quat(currentRotation.xyz);
        var nextRotQuat = new quat(nextRotation.xyz);
        var resultRotation = GlmExtensions.SLerpSafe(currentRotQuat, nextRotQuat, share);
        var resultTranslation = MathExtension.Lerp(currentTranslation, nextTranslation, share);
        
        return (resultTranslation, resultRotation);
    }
}