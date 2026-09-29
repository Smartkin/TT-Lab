using System;
using GlmSharp;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace TT_Lab.Rendering.Materials;

/// <summary>
/// Plays a shader animation the way the game's AnimateShader does: the frames loop at the animation's frames per second, each of
/// the six tracks (U, V, red, green, blue, alpha) is its static value or the value between the frame's and the next's, in 1/4096ths
/// </summary>
public static class ShaderAnimationSampler
{
    public readonly record struct Sample(vec2 Uv, vec4 Color);

    public static readonly Sample Still = new(vec2.Zero, vec4.Ones);

    public static Sample At(TwinShaderAnimation? animation, double seconds)
    {
        if (animation == null || animation.AnimationSettings.Count == 0)
        {
            return Still;
        }

        var frames = Math.Max(1, Math.Min(animation.TimedFrames, animation.AnimatedTransformations.Count));
        var rate = animation.FramesPerSecond > 0 ? animation.FramesPerSecond : 1;
        var position = (float)(seconds * rate % frames);
        if (position < 0)
        {
            position += frames;
        }

        var frame = Math.Min((int)position, frames - 1);
        var next = frame + 1 == frames ? 0 : frame + 1;
        var fraction = position - frame;
        var settings = animation.AnimationSettings[0];
        var values = new float[6];
        var staticIndex = settings.StaticTransformationIndex;
        var animatedIndex = settings.AnimationTransformationIndex;
        var kinds = new[] { settings.TranslateX, settings.TranslateY, settings.ColorR, settings.ColorG, settings.ColorB, settings.ColorA };
        for (var track = 0; track < kinds.Length; track++)
        {
            if (kinds[track] == TransformType.Static)
            {
                values[track] = ValueAt(animation.StaticTransformations, staticIndex++);
            }
            else
            {
                var from = ValueAt(animation.AnimatedTransformations, frame, animatedIndex);
                var to = ValueAt(animation.AnimatedTransformations, next, animatedIndex);
                animatedIndex++;
                values[track] = from * (1.0f - fraction) + to * fraction;
            }
        }

        return new Sample(new vec2(values[0], values[1]), new vec4(values[2], values[3], values[4], values[5]));
    }

    private static float ValueAt(System.Collections.Generic.List<Transformation> transforms, int index)
    {
        return index < transforms.Count ? transforms[index].Value : 0.0f;
    }

    private static float ValueAt(System.Collections.Generic.List<AnimatedTransformation> frames, int frame, int index)
    {
        if (frame >= frames.Count || index >= frames[frame].Count)
        {
            return 0.0f;
        }

        return frames[frame][index].Value;
    }
}
