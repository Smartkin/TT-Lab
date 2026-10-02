using System;
using System.IO;
using System.Linq;
using System.Text;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace TT_Lab.AssetData.Graphics.Shaders;

/// <summary>
/// Edits a shader animation's six tracks (U, V, red, green, blue, alpha) the way the game reads them (AnimateShader 0x297260): the first
/// settings say which tracks are static, each static track takes the next static value from the settings' static index, each animated one
/// the next value of every frame from its animated index. Every edit works on a copy made from the game's bytes, so what it doesn't touch
/// stays as it was
/// </summary>
public static class ShaderAnimationTracks
{
    public const int Count = 6;
    public const int DefaultFramesPerSecond = 30;
    // The values are 16 bit fixed point numbers of 4096ths
    public const float MinValue = Int16.MinValue / 4096.0f;
    public const float MaxValue = Int16.MaxValue / 4096.0f;
    public static readonly string[] Names = ["U", "V", "Red", "Green", "Blue", "Alpha"];

    /// <summary>
    /// A new animation of one frame: no UV offset and white, every track static
    /// </summary>
    public static TwinShaderAnimation Create()
    {
        var animation = new TwinShaderAnimation { TotalFrames = 1 };
        animation.TimedFrames = 1;
        animation.FramesPerSecond = DefaultFramesPerSecond;
        animation.AnimationSettings.Add(new AnimationSettings
        {
            TranslateX = TransformType.Static, TranslateY = TransformType.Static, ColorR = TransformType.Static, ColorG = TransformType.Static,
            ColorB = TransformType.Static, ColorA = TransformType.Static
        });
        foreach (var value in new[] { 0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 1.0f })
        {
            animation.StaticTransformations.Add(new Transformation { PureValue = ToRaw(value) });
        }

        animation.AnimatedTransformations.Add(new AnimatedTransformation());
        return animation;
    }

    public static int FrameCount(TwinShaderAnimation animation) => animation.AnimatedTransformations.Count;

    public static bool IsAnimated(TwinShaderAnimation animation, int track)
    {
        return animation.AnimationSettings.Count > 0 && KindOf(animation.AnimationSettings[0], track) == TransformType.Animated;
    }

    /// <summary>
    /// The track's value at a frame: its static value, or the frame's
    /// </summary>
    public static float ValueAt(TwinShaderAnimation animation, int track, int frame)
    {
        if (animation.AnimationSettings.Count == 0)
        {
            return 0.0f;
        }

        var index = IndexOf(animation.AnimationSettings[0], track);
        if (!IsAnimated(animation, track))
        {
            return index < animation.StaticTransformations.Count ? animation.StaticTransformations[index].Value : 0.0f;
        }

        if (frame < 0 || frame >= animation.AnimatedTransformations.Count || index >= animation.AnimatedTransformations[frame].Count)
        {
            return 0.0f;
        }

        return animation.AnimatedTransformations[frame][index].Value;
    }

    public static TwinShaderAnimation SetValue(TwinShaderAnimation animation, int track, int frame, float value)
    {
        var copy = WithSettings(animation);
        var settings = copy.AnimationSettings[0];
        var index = IndexOf(settings, track);
        if (IsAnimated(copy, track))
        {
            if (frame >= 0 && frame < copy.AnimatedTransformations.Count && index < copy.AnimatedTransformations[frame].Count)
            {
                copy.AnimatedTransformations[frame][index].PureValue = ToRaw(value);
            }
        }
        else if (index < copy.StaticTransformations.Count)
        {
            copy.StaticTransformations[index].PureValue = ToRaw(value);
        }

        return copy;
    }

    /// <summary>
    /// Makes the track animated (every frame takes its static value) or static (it keeps the value it had at the frame)
    /// </summary>
    public static TwinShaderAnimation SetAnimated(TwinShaderAnimation animation, int track, bool animated, int frame)
    {
        if (IsAnimated(animation, track) == animated)
        {
            return animation;
        }

        var value = ValueAt(animation, track, frame);
        var copy = WithSettings(animation);
        if (copy.AnimatedTransformations.Count == 0)
        {
            copy.AnimatedTransformations.Add(new AnimatedTransformation());
            copy.TotalFrames = 1;
        }

        var settings = copy.AnimationSettings[0];
        var staticIndex = settings.StaticTransformationIndex + Enumerable.Range(0, track).Count(other => KindOf(settings, other) == TransformType.Static);
        var animatedIndex = settings.AnimationTransformationIndex + Enumerable.Range(0, track).Count(other => KindOf(settings, other) == TransformType.Animated);
        if (animated)
        {
            if (staticIndex < copy.StaticTransformations.Count)
            {
                copy.StaticTransformations.RemoveAt(staticIndex);
            }

            foreach (var frameValues in copy.AnimatedTransformations)
            {
                frameValues.Transforms.Insert(Math.Min(animatedIndex, frameValues.Count), new Transformation { PureValue = ToRaw(value) });
            }
        }
        else
        {
            foreach (var frameValues in copy.AnimatedTransformations.Where(frameValues => animatedIndex < frameValues.Count))
            {
                frameValues.Transforms.RemoveAt(animatedIndex);
            }

            copy.StaticTransformations.Insert(Math.Min(staticIndex, copy.StaticTransformations.Count), new Transformation { PureValue = ToRaw(value) });
        }

        SetKind(settings, track, animated ? TransformType.Animated : TransformType.Static);
        return copy;
    }

    /// <summary>
    /// A copy of the frame put in after it. The game times the animation by its own frame count, which follows while it was the frames'
    /// </summary>
    public static TwinShaderAnimation InsertFrame(TwinShaderAnimation animation, int after)
    {
        var copy = Copy(animation);
        var frames = copy.AnimatedTransformations;
        var source = frames.Count == 0 ? new AnimatedTransformation() : frames[Math.Clamp(after, 0, frames.Count - 1)];
        var frame = new AnimatedTransformation();
        frame.Transforms.AddRange(source.Transforms.Select(value => new Transformation { PureValue = value.PureValue }));
        frames.Insert(Math.Clamp(after + 1, 0, frames.Count), frame);
        SetFrameCount(copy, animation);
        return copy;
    }

    public static bool CanRemoveFrame(TwinShaderAnimation animation) => animation.AnimatedTransformations.Count > 1;

    public static TwinShaderAnimation RemoveFrame(TwinShaderAnimation animation, int frame)
    {
        if (!CanRemoveFrame(animation) || frame < 0 || frame >= animation.AnimatedTransformations.Count)
        {
            return animation;
        }

        var copy = Copy(animation);
        copy.AnimatedTransformations.RemoveAt(frame);
        SetFrameCount(copy, animation);
        return copy;
    }

    public static TwinShaderAnimation SetFramesPerSecond(TwinShaderAnimation animation, int framesPerSecond)
    {
        var copy = Copy(animation);
        copy.FramesPerSecond = Math.Clamp(framesPerSecond, 0, 31);
        return copy;
    }

    public static Int16 ToRaw(float value)
    {
        return (Int16)Math.Clamp(MathF.Round(value * 4096.0f), Int16.MinValue, Int16.MaxValue);
    }

    // The frames written and the count the game times by, the latter only while it was the frames'
    private static void SetFrameCount(TwinShaderAnimation copy, TwinShaderAnimation original)
    {
        if (original.TimedFrames == original.AnimatedTransformations.Count)
        {
            copy.TimedFrames = copy.AnimatedTransformations.Count;
        }

        copy.TotalFrames = (UInt16)copy.AnimatedTransformations.Count;
    }

    // A copy with the settings the game reads, all static when it had none
    private static TwinShaderAnimation WithSettings(TwinShaderAnimation animation)
    {
        var copy = Copy(animation);
        if (copy.AnimationSettings.Count > 0)
        {
            return copy;
        }

        copy.AnimationSettings.Add(new AnimationSettings
        {
            TranslateX = TransformType.Static, TranslateY = TransformType.Static, ColorR = TransformType.Static, ColorG = TransformType.Static,
            ColorB = TransformType.Static, ColorA = TransformType.Static,
            StaticTransformationIndex = (UInt16)copy.StaticTransformations.Count,
            AnimationTransformationIndex = (UInt16)(copy.AnimatedTransformations.Count > 0 ? copy.AnimatedTransformations[0].Count : 0)
        });
        foreach (var value in new[] { 0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 1.0f })
        {
            copy.StaticTransformations.Add(new Transformation { PureValue = ToRaw(value) });
        }

        return copy;
    }

    private static int IndexOf(AnimationSettings settings, int track)
    {
        var kind = KindOf(settings, track);
        var before = Enumerable.Range(0, track).Count(other => KindOf(settings, other) == kind);
        return (kind == TransformType.Static ? settings.StaticTransformationIndex : settings.AnimationTransformationIndex) + before;
    }

    private static TransformType KindOf(AnimationSettings settings, int track) => track switch
    {
        0 => settings.TranslateX,
        1 => settings.TranslateY,
        2 => settings.ColorR,
        3 => settings.ColorG,
        4 => settings.ColorB,
        5 => settings.ColorA,
        _ => throw new ArgumentOutOfRangeException(nameof(track))
    };

    private static void SetKind(AnimationSettings settings, int track, TransformType kind)
    {
        switch (track)
        {
            case 0:
                settings.TranslateX = kind;
                break;
            case 1:
                settings.TranslateY = kind;
                break;
            case 2:
                settings.ColorR = kind;
                break;
            case 3:
                settings.ColorG = kind;
                break;
            case 4:
                settings.ColorB = kind;
                break;
            case 5:
                settings.ColorA = kind;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(track));
        }
    }

    // Through the game's bytes, which keep everything the editor doesn't know about
    private static TwinShaderAnimation Copy(TwinShaderAnimation animation)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Default, true))
        {
            animation.Write(writer);
        }

        stream.Position = 0;
        var copy = new TwinShaderAnimation();
        using var reader = new BinaryReader(stream);
        copy.Read(reader, (int)stream.Length);
        return copy;
    }
}
