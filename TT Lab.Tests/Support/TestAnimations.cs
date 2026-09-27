using Twinsanity.TwinsanityInterchange.Common.Animation;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace TT_Lab.Tests.Support;

/// <summary>
/// Made up animations for small skeletons, the rotations stay far away from gimbal lock
/// </summary>
public static class TestAnimations
{
    private const int ChannelsPerJoint = 9;

    /// <summary>
    /// Raw translation XYZ, rotation XYZ and scale XYZ of a joint in a frame
    /// </summary>
    public static short[] RawValues(int joint, int frame, int variant = 0)
    {
        var direction = joint % 2 == 0 ? 1 : -1;
        return
        [
            (short)(2048 * (joint + 1) + frame * 100 * direction + variant * 7),
            2048,
            (short)(-1024 * joint),
            (short)(frame * 40 + joint * 100 + variant * 3),
            (short)(joint == 2 ? frame * -30 : 200),
            (short)(300 - frame * 25),
            4096,
            (short)(joint == 1 ? 4096 + frame * 200 : 4096),
            4096
        ];
    }

    public static TwinAnimation CreateTwinAnimation(int frames, int joints, int variant = 0, Func<int, bool>? independentScaling = null,
        Func<int, bool>? additionalRotation = null, Func<int, int, short[]>? rawValues = null)
    {
        var animation = new TwinAnimation { TotalFrames = (ushort)frames };
        var animatedPerFrame = Enumerable.Range(0, frames).Select(_ => new List<Transformation>()).ToList();
        for (var joint = 0; joint < joints; joint++)
        {
            var values = Enumerable.Range(0, frames).Select(frame => rawValues?.Invoke(joint, frame) ?? RawValues(joint, frame, variant)).ToList();
            var settings = new JointSettings
            {
                IndependentScaling = independentScaling?.Invoke(joint) ?? false,
                UseAdditionalRotation = additionalRotation?.Invoke(joint) ?? false,
                TransformationIndex = (ushort)animation.StaticTransformations.Count,
                AnimationTransformationIndex = (ushort)animatedPerFrame[0].Count
            };
            var choices = new TransformType[ChannelsPerJoint];
            for (var channel = 0; channel < ChannelsPerJoint; channel++)
            {
                if (values.All(frameValues => frameValues[channel] == values[0][channel]))
                {
                    choices[channel] = TransformType.Static;
                    animation.StaticTransformations.Add(new Transformation { PureValue = values[0][channel] });
                    continue;
                }

                choices[channel] = TransformType.Animated;
                for (var frame = 0; frame < frames; frame++)
                {
                    animatedPerFrame[frame].Add(new Transformation { PureValue = values[frame][channel] });
                }
            }

            settings.TranslateX = choices[0];
            settings.TranslateY = choices[1];
            settings.TranslateZ = choices[2];
            settings.RotateX = choices[3];
            settings.RotateY = choices[4];
            settings.RotateZ = choices[5];
            settings.ScaleX = choices[6];
            settings.ScaleY = choices[7];
            settings.ScaleZ = choices[8];
            animation.JointSettings.Add(settings);
        }

        foreach (var frameValues in animatedPerFrame)
        {
            var transformation = new AnimatedTransformation((ushort)frameValues.Count);
            transformation.Transforms.AddRange(frameValues);
            animation.AnimatedTransformations.Add(transformation);
        }

        return animation;
    }
}
