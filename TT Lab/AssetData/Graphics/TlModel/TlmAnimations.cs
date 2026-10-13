using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using GlmSharp;
using TT_Lab.AssetData.Code;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using TransformType = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType;

namespace TT_Lab.AssetData.Graphics.TlModel;

/// <summary>
/// The animations of an OGI in TT Lab model files
/// </summary>
/// <remarks>
/// Every animation has the game's bytes next to every frame's local transform of every joint, which Blender shows. The add-on writes
/// both back, TT Lab keeps the game's values of everything whose keys still hold them
/// </remarks>
public static class TlmAnimations
{
    private const Byte FallbackFps = 25;
    // GameAnimation keeps an animation's frames a second in 5 bits and plays it for its frames over them, 0 is a division by zero
    public const Byte MaxFps = 31;
    // A full turn is 0x10000 but the stored value is shifted down by 4 bits
    private const Single RawRotationUnitsPerTurn = 0x1000;
    private const Single RawValueScale = 4096;

    public static JsonObject Write(TlmFile file, AnimationData animation, IReadOnlyList<TwinJoint> joints)
    {
        var json = new JsonObject
        {
            ["id"] = TlmJson.ToJson(animation.ID),
            ["name"] = animation.Name,
            ["fps"] = (Int32)animation.DefaultFPS,
            ["frames"] = (Int32)animation.TotalFrames,
            ["joint_count"] = animation.MainAnimation.JointSettings.Count,
            ["exact"] = file.Write(GetExactBytes(animation).AsSpan())
        };

        var frames = animation.MainAnimation.AnimatedTransformations.Count;
        var jointKeys = new JsonArray();
        for (var joint = 0; joint < animation.MainAnimation.JointSettings.Count; joint++)
        {
            var settings = animation.MainAnimation.JointSettings[joint];
            var additionalRotation = GetAdditionalRotation(joints, joint);
            var translations = new List<Single>();
            var rotations = new List<Single>();
            var scales = new List<Single>();
            for (var frame = 0; frame < Math.Max(frames, 1); frame++)
            {
                var values = GetRawValues(animation, joint, frames == 0 ? -1 : frame);
                var rotation = ToRotation(values);
                if (settings.UseAdditionalRotation)
                {
                    rotation = additionalRotation * rotation;
                }

                translations.AddRange([values[0] / RawValueScale, values[1] / RawValueScale, values[2] / RawValueScale]);
                rotations.AddRange([rotation.x, rotation.y, rotation.z, rotation.w]);
                scales.AddRange([values[6] / RawValueScale, values[7] / RawValueScale, values[8] / RawValueScale]);
            }

            jointKeys.Add(new JsonObject
            {
                ["joint"] = joint,
                ["translation"] = file.Write(WithoutRepeatedKeys(translations, 3).AsSpan()),
                ["rotation"] = file.Write(WithoutRepeatedKeys(rotations, 4).AsSpan()),
                ["scale"] = file.Write(WithoutRepeatedKeys(scales, 3).AsSpan()),
                ["independent_scaling"] = settings.IndependentScaling,
                ["additional_rotation"] = settings.UseAdditionalRotation
            });
        }

        json["joints"] = jointKeys;
        var facialSettings = animation.FacialAnimation.JointSettings.FirstOrDefault();
        if (facialSettings != null)
        {
            var facialFrames = animation.FacialAnimation.AnimatedTransformations.Count;
            var weights = new List<Single>();
            for (var frame = 0; frame < facialFrames; frame++)
            {
                weights.AddRange(GetFacialRawValues(animation.FacialAnimation, frame).Select(value => value / RawValueScale));
            }

            json["facial"] = new JsonObject
            {
                ["frames"] = (Int32)animation.FacialAnimation.TotalFrames,
                ["shapes"] = (Int32)facialSettings.FacialShapesAmount,
                ["weights"] = file.Write(weights.ToArray().AsSpan()),
                ["unused_flag"] = facialSettings.UnusedFlag,
                ["additional_rotation"] = facialSettings.UseAdditionalRotation
            };
        }

        return json;
    }

    /// <summary>
    /// The animation from its keys. Joints and frames whose keys still hold the values of the game's bytes the file has keep those,
    /// an animation nobody edited is the game's bytes
    /// </summary>
    /// <param name="edited">Whether the keys aren't the ones of the game's bytes anymore, or the file has no bytes</param>
    public static AnimationData Read(TlmFile file, JsonObject json, IReadOnlyList<TwinJoint> joints, out Boolean edited)
    {
        var exact = file.Read<Byte>(json["exact"]);
        var original = exact.Length == 0 ? null : FromExactBytes(exact);
        var animation = FromKeys(file, json, joints, original, out edited);
        animation.ID = json.GetUInt("id");
        animation.Name = json.GetString("name") ?? string.Empty;
        animation.DefaultFPS = (Byte)Math.Clamp(json.GetInt("fps", FallbackFps), 1, MaxFps);
        animation.TotalFrames = (UInt16)json.GetInt("frames", animation.MainAnimation.TotalFrames);
        return animation;
    }

    public static Byte[] GetExactBytes(AnimationData animation)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        animation.MainAnimation.Write(writer);
        animation.FacialAnimation.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static AnimationData FromExactBytes(Byte[] exact)
    {
        using var stream = new MemoryStream(exact);
        using var reader = new BinaryReader(stream);
        var animation = new AnimationData();
        animation.MainAnimation.Read(reader, exact.Length);
        animation.FacialAnimation.Read(reader, exact.Length);
        return animation;
    }

    private static AnimationData FromKeys(TlmFile file, JsonObject json, IReadOnlyList<TwinJoint> joints, AnimationData? original, out Boolean edited)
    {
        var originalMain = original?.MainAnimation;
        var frames = (UInt16)json.GetInt("frames", originalMain?.TotalFrames ?? 1);
        if (originalMain == null)
        {
            frames = Math.Max(frames, (UInt16)1);
        }

        var jointCount = json.GetInt("joint_count", originalMain?.JointSettings.Count ?? joints.Count);
        var keysByJoint = (json["joints"] as JsonArray ?? []).OfType<JsonObject>().GroupBy(keys => keys.GetInt("joint", -1)).ToDictionary(group => group.Key, group => group.First());
        edited = originalMain == null || originalMain.TotalFrames != frames || originalMain.JointSettings.Count != jointCount;
        var mainAnimation = new TwinAnimation { TotalFrames = frames };
        var animatedPerFrame = Enumerable.Range(0, frames).Select(_ => new List<Transformation>()).ToList();
        for (var joint = 0; joint < jointCount; joint++)
        {
            var keys = keysByJoint.GetValueOrDefault(joint);
            var originalSettings = originalMain?.JointSettings.ElementAtOrDefault(joint);
            var settings = new JointSettings
            {
                IndependentScaling = keys?.GetBool("independent_scaling") ?? originalSettings?.IndependentScaling ?? false,
                UseAdditionalRotation = keys?.GetBool("additional_rotation") ?? originalSettings?.UseAdditionalRotation ?? false,
                TransformationIndex = (UInt16)mainAnimation.StaticTransformations.Count,
                AnimationTransformationIndex = (UInt16)(animatedPerFrame.FirstOrDefault()?.Count ?? 0)
            };
            if (originalSettings != null && (originalSettings.IndependentScaling != settings.IndependentScaling || originalSettings.UseAdditionalRotation != settings.UseAdditionalRotation))
            {
                edited = true;
            }

            var channels = SampleJoint(file, keys, joints, joint, settings.UseAdditionalRotation, frames, original, ref edited);
            var originalChoices = originalSettings == null ? null : GetChoices(originalSettings);
            var choices = new TransformType[channels.Length];
            for (var channel = 0; channel < channels.Length; channel++)
            {
                var values = channels[channel];
                // Channels of the game that are animated without changing stay that way
                if (frames == 0 || values.All(value => value == values[0]) && originalChoices?[channel] != TransformType.Animated)
                {
                    choices[channel] = TransformType.Static;
                    mainAnimation.StaticTransformations.Add(new Transformation { PureValue = values[0] });
                    continue;
                }

                choices[channel] = TransformType.Animated;
                for (var frame = 0; frame < frames; frame++)
                {
                    animatedPerFrame[frame].Add(new Transformation { PureValue = values[frame] });
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
            mainAnimation.JointSettings.Add(settings);
        }

        foreach (var frameValues in animatedPerFrame)
        {
            var animatedTransformation = new AnimatedTransformation((UInt16)frameValues.Count);
            animatedTransformation.Transforms.AddRange(frameValues);
            mainAnimation.AnimatedTransformations.Add(animatedTransformation);
        }

        var facialAnimation = FacialFromKeys(file, json["facial"] as JsonObject, frames, original?.FacialAnimation, ref edited);
        if (!edited)
        {
            return original!;
        }

        return new AnimationData
        {
            MainAnimation = mainAnimation,
            FacialAnimation = facialAnimation
        };
    }

    private static TransformType[] GetChoices(JointSettings settings)
    {
        return [settings.TranslateX, settings.TranslateY, settings.TranslateZ, settings.RotateX, settings.RotateY, settings.RotateZ, settings.ScaleX, settings.ScaleY, settings.ScaleZ];
    }

    private static TwinMorphAnimation FacialFromKeys(TlmFile file, JsonObject? json, UInt16 mainFrames, TwinMorphAnimation? original, ref Boolean edited)
    {
        var originalSettings = original?.JointSettings.FirstOrDefault();
        var facialAnimation = new TwinMorphAnimation();
        if (json == null)
        {
            edited |= originalSettings != null;
            return facialAnimation;
        }

        // The game reads the weights at the main animation's frame (SetAnimationData), fewer frames read past them
        var frames = mainFrames;
        var shapesAmount = (Byte)Math.Clamp(json.GetInt("shapes"), 0, 15);
        var weights = file.Read<Single>(json["weights"]);
        var settings = new MorphJointSettings
        {
            FacialShapesAmount = shapesAmount,
            UnusedFlag = json.GetBool("unused_flag"),
            UseAdditionalRotation = json.GetBool("additional_rotation")
        };
        var originalFrames = original?.AnimatedTransformations.Count ?? 0;
        edited |= originalSettings == null || originalSettings.FacialShapesAmount != shapesAmount || originalSettings.UnusedFlag != settings.UnusedFlag ||
                  originalSettings.UseAdditionalRotation != settings.UseAdditionalRotation || original!.TotalFrames != frames;
        facialAnimation.TotalFrames = frames;
        var animatedPerFrame = Enumerable.Range(0, frames).Select(_ => new List<Transformation>()).ToList();
        for (var shape = 0; shape < shapesAmount; shape++)
        {
            var values = new Int16[Math.Max((Int32)frames, 1)];
            for (var frame = 0; frame < values.Length; frame++)
            {
                values[frame] = Key(weights, shapesAmount, frame) is { } key ? ToRawValue(key[shape]) : (Int16)0;
                if (originalSettings == null || shape >= originalSettings.FacialShapesAmount)
                {
                    continue;
                }

                var originalValue = GetFacialRawValues(original!, originalFrames == 0 ? -1 : Math.Min(frame, originalFrames - 1))[shape];
                edited |= originalValue != values[frame];
            }

            if (frames == 0 || values.All(value => value == values[0]) && (originalSettings == null || originalSettings.AnimationMorph[shape] != TransformType.Animated))
            {
                settings.AnimationMorph[shape] = TransformType.Static;
                facialAnimation.StaticTransformations.Add(new Transformation { PureValue = values[0] });
                continue;
            }

            settings.AnimationMorph[shape] = TransformType.Animated;
            for (var frame = 0; frame < frames; frame++)
            {
                animatedPerFrame[frame].Add(new Transformation { PureValue = values[frame] });
            }
        }

        facialAnimation.JointSettings.Add(settings);
        foreach (var frameValues in animatedPerFrame)
        {
            var animatedTransformation = new AnimatedTransformation((UInt16)frameValues.Count);
            animatedTransformation.Transforms.AddRange(frameValues);
            facialAnimation.AnimatedTransformations.Add(animatedTransformation);
        }

        return facialAnimation;
    }

    // Raw values of translation XYZ, rotation XYZ and scale XYZ for every frame. A frame whose keys still hold the game's values keeps
    // them, joints without keys keep the game's values or stay at their rest. The angles of the other frames are chosen over the whole
    // track, so that the game's blends between them follow their turns (TlmAnimationFrames)
    private static Int16[][] SampleJoint(TlmFile file, JsonObject? keys, IReadOnlyList<TwinJoint> joints, Int32 jointIndex, Boolean useAdditionalRotation, Int32 frames,
        AnimationData? original, ref Boolean edited)
    {
        var joint = jointIndex < joints.Count ? joints[jointIndex] : null;
        var translations = keys != null ? file.Read<Single>(keys["translation"]) : [];
        var rotations = keys != null ? file.Read<Single>(keys["rotation"]) : [];
        var scales = keys != null ? file.Read<Single>(keys["scale"]) : [];
        var additionalRotation = GetAdditionalRotation(joints, jointIndex);
        var inverseAdditionalRotation = glm.Inverse(additionalRotation);
        var originalMain = original?.MainAnimation;
        var hasOriginal = originalMain != null && jointIndex < originalMain.JointSettings.Count;
        var originalFrames = originalMain?.AnimatedTransformations.Count ?? 0;
        var count = Math.Max(frames, 1);
        var values = new Int16[count][];
        var originals = new Int16[]?[count];
        var kept = new vec3?[count];
        var turns = new quat[count];
        for (var frame = 0; frame < count; frame++)
        {
            originals[frame] = hasOriginal ? GetRawValues(original!, jointIndex, originalFrames == 0 ? -1 : Math.Min(frame, originalFrames - 1)) : null;
            if (keys == null && originals[frame] != null)
            {
                values[frame] = originals[frame]!;
                continue;
            }

            var translation = Key(translations, 3, frame) is { } t ? new vec3(t[0], t[1], t[2]) :
                joint != null ? new vec3(joint.LocalTranslation.X, joint.LocalTranslation.Y, joint.LocalTranslation.Z) : vec3.Zero;
            var rotation = Key(rotations, 4, frame) is { } r ? new quat(r[0], r[1], r[2], r[3]) :
                joint != null ? new quat(joint.LocalRotation.X, joint.LocalRotation.Y, joint.LocalRotation.Z, joint.LocalRotation.W) : quat.Identity;
            var scale = Key(scales, 3, frame) is { } s ? new vec3(s[0], s[1], s[2]) : vec3.Ones;
            values[frame] =
            [
                ToRawValue(translation.x), ToRawValue(translation.y), ToRawValue(translation.z), 0, 0, 0,
                ToRawValue(scale.x), ToRawValue(scale.y), ToRawValue(scale.z)
            ];
            if (originals[frame] is { } originalValues && HoldsRotation(originalValues, rotation, useAdditionalRotation ? additionalRotation : quat.Identity))
            {
                var angles = new vec3(ToAngle(originalValues[3]), ToAngle(originalValues[4]), ToAngle(originalValues[5]));
                kept[frame] = angles;
                turns[frame] = new quat(angles);
                continue;
            }

            turns[frame] = useAdditionalRotation ? inverseAdditionalRotation * rotation : rotation;
        }

        if (keys != null || !hasOriginal)
        {
            // Most joints don't move in most animations
            var chosen = Array.TrueForAll(kept, angles => angles == null) && Array.TrueForAll(turns, turn => turn == turns[0])
                ? Enumerable.Repeat(TlmAnimationFrames.ChooseAngles([turns[0]], [null])[0], count).ToArray()
                : TlmAnimationFrames.ChooseAngles(turns, kept);
            for (var frame = 0; frame < count; frame++)
            {
                if (originals[frame] is { } originalValues && kept[frame] != null)
                {
                    Array.Copy(originalValues, 3, values[frame], 3, 3);
                }
                else
                {
                    var angles = WithinRawRange(chosen[frame]);
                    values[frame][3] = ToRawRotation(angles.x);
                    values[frame][4] = ToRawRotation(angles.y);
                    values[frame][5] = ToRawRotation(angles.z);
                }

                edited |= originals[frame] == null || !values[frame].SequenceEqual(originals[frame]!);
            }
        }

        var channels = Enumerable.Range(0, 9).Select(_ => new Int16[count]).ToArray();
        for (var frame = 0; frame < count; frame++)
        {
            for (var channel = 0; channel < channels.Length; channel++)
            {
                channels[channel][frame] = values[frame][channel];
            }
        }

        return channels;
    }

    // Whether the rotation is the game's one, closer than half of the smallest step the game turns joints by. Blender turns keys by
    // rounding errors far below that. Translations and scales are the game's values while they round to them
    private static Boolean HoldsRotation(Int16[] values, quat rotation, quat additionalRotation)
    {
        var gameRotation = (additionalRotation * ToRotation(values)).NormalizedSafe;
        rotation = rotation.NormalizedSafe;
        // Both of a rotation's quaternions are the same rotation. Turned by a small angle a quaternion moves by half of it
        var difference = Math.Min((gameRotation - rotation).Length, (gameRotation + rotation).Length);
        return difference * 2 < MathF.PI / RawRotationUnitsPerTurn;
    }

    // A track of one key holds for every frame, a shorter track holds its last key
    private static Single[]? Key(Single[] values, Int32 size, Int32 frame)
    {
        var count = size == 0 ? 0 : values.Length / size;
        if (count == 0)
        {
            return null;
        }

        var index = Math.Min(frame, count - 1) * size;
        return values[index..(index + size)];
    }

    // Most joints don't move in most animations, a track that never changes is kept as a single key
    private static Single[] WithoutRepeatedKeys(List<Single> values, Int32 size)
    {
        for (var i = size; i < values.Count; i++)
        {
            if (BitConverter.SingleToInt32Bits(values[i]) != BitConverter.SingleToInt32Bits(values[i % size]))
            {
                return values.ToArray();
            }
        }

        return values.Take(size).ToArray();
    }

    private static quat GetAdditionalRotation(IReadOnlyList<TwinJoint> joints, Int32 joint)
    {
        if (joint >= joints.Count)
        {
            return quat.Identity;
        }

        var rotation = joints[joint].AdditionalAnimationRotation;
        return new quat(rotation.X, rotation.Y, rotation.Z, rotation.W);
    }

    // The game builds a joint's rotation from its angles as qz * qy * qx
    private static quat ToRotation(Int16[] values)
    {
        return new quat(new vec3(ToAngle(values[3]), ToAngle(values[4]), ToAngle(values[5])));
    }

    private static Single ToAngle(Int16 raw)
    {
        return raw / RawRotationUnitsPerTurn * MathF.PI * 2;
    }

    /// <param name="animation">Facial animation to read</param>
    /// <param name="frame">Frame to read, -1 reads only the static values of an animation without frames</param>
    public static Int16[] GetFacialRawValues(TwinMorphAnimation animation, Int32 frame)
    {
        var settings = animation.JointSettings.FirstOrDefault();
        if (settings == null)
        {
            return [];
        }

        var staticIndex = settings.TransformationIndex;
        var animatedIndex = settings.AnimationTransformationIndex;
        var result = new Int16[settings.FacialShapesAmount];
        for (var shape = 0; shape < result.Length; shape++)
        {
            if (settings.AnimationMorph[shape] == TransformType.Animated)
            {
                result[shape] = frame >= 0 ? animation.AnimatedTransformations[frame].Transforms[animatedIndex++].PureValue : (Int16)0;
                continue;
            }

            result[shape] = animation.StaticTransformations[staticIndex++].PureValue;
        }

        return result;
    }

    /// <summary>
    /// Raw values of translation XYZ, rotation XYZ and scale XYZ of a joint in a frame
    /// </summary>
    /// <param name="frame">Frame to read, -1 reads only the static values of an animation without frames</param>
    public static Int16[] GetRawValues(AnimationData animation, Int32 jointIndex, Int32 frame)
    {
        var settings = animation.MainAnimation.JointSettings[jointIndex];
        var staticIndex = settings.TransformationIndex;
        var animatedIndex = settings.AnimationTransformationIndex;
        var choices = new[] { settings.TranslateX, settings.TranslateY, settings.TranslateZ, settings.RotateX, settings.RotateY, settings.RotateZ, settings.ScaleX, settings.ScaleY, settings.ScaleZ };
        var result = new Int16[choices.Length];
        for (var channel = 0; channel < choices.Length; channel++)
        {
            if (choices[channel] == TransformType.Animated)
            {
                result[channel] = frame >= 0 ? animation.MainAnimation.AnimatedTransformations[frame].Transforms[animatedIndex++].PureValue : (Int16)0;
                continue;
            }

            result[channel] = animation.MainAnimation.StaticTransformations[staticIndex++].PureValue;
        }

        return result;
    }

    // A joint that keeps spinning one way adds a turn to its angle every turn, whole turns come off before the raw value overflows.
    // The game's own animations wrap their angles every turn
    private static vec3 WithinRawRange(vec3 angles)
    {
        return new vec3(Wrap(angles.x), Wrap(angles.y), Wrap(angles.z));

        static Single Wrap(Single angle)
        {
            const Single turn = MathF.PI * 2;
            return Math.Abs(angle) < turn * 7 ? angle : angle - MathF.Round(angle / turn) * turn;
        }
    }

    public static Int16 ToRawValue(Single value) => (Int16)Math.Clamp(Math.Round(value * RawValueScale), Int16.MinValue, Int16.MaxValue);

    public static Int16 ToRawRotation(Single angle) => (Int16)Math.Clamp(Math.Round(angle / (MathF.PI * 2) * RawRotationUnitsPerTurn), Int16.MinValue, Int16.MaxValue);
}
