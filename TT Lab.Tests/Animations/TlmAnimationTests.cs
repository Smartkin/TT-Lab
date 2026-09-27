using System.Text.Json.Nodes;
using GlmSharp;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace TT_Lab.Tests.Animations;

public class TlmAnimationTests
{
    private const int Frames = 8;
    private const int JointsAmount = 3;
    private static readonly quat AdditionalRotation = new(new vec3(0, 0.5f, 0));

    private static readonly List<TwinJoint> Joints =
    [
        new() { Index = 0, ParentIndex = 255, LocalTranslation = new Vector4(0, 0, 0, 1), LocalRotation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) },
        new() { Index = 1, ParentIndex = 0, LocalTranslation = new Vector4(0, 1, 0, 1), LocalRotation = new Vector4(0, 0, 0, 1), AdditionalAnimationRotation = new Vector4(0, 0, 0, 1) },
        new()
        {
            Index = 2, ParentIndex = 1, LocalTranslation = new Vector4(0, 0, 1, 1), LocalRotation = new Vector4(0, 0, 0, 1),
            AdditionalAnimationRotation = new Vector4(AdditionalRotation.x, AdditionalRotation.y, AdditionalRotation.z, AdditionalRotation.w)
        }
    ];

    [Theory]
    [InlineData(0f, (short)0)]
    [InlineData(1f, (short)4096)]
    [InlineData(-0.5f, (short)-2048)]
    [InlineData(1.00012f, (short)4096)]
    [InlineData(1.00013f, (short)4097)]
    [InlineData(100f, short.MaxValue)]
    [InlineData(-100f, short.MinValue)]
    public void ValuesAreRoundedToFixedPointAndClamped(float value, short raw)
    {
        Assert.Equal(raw, TlmAnimations.ToRawValue(value));
    }

    [Theory]
    [InlineData(0f, (short)0)]
    [InlineData(MathF.PI / 2, (short)1024)]
    [InlineData(-MathF.PI, (short)-2048)]
    [InlineData(MathF.PI * 2, (short)4096)]
    public void RotationsUse4096UnitsPerTurn(float angle, short raw)
    {
        Assert.Equal(raw, TlmAnimations.ToRawRotation(angle));
    }

    [Fact]
    public void EulerAnglesReproduceTheRotation()
    {
        var random = new Random(42);
        for (var i = 0; i < 2000; i++)
        {
            var rotation = RandomRotation(random);

            var angles = TlmAnimations.ToContinuousEulerAngles(rotation, null);

            AssertSameRotation(rotation, new quat(angles));
        }
    }

    // Close to gimbal lock reading all three angles off the quaternion is imprecise, the rotation still has to come out exact
    [Theory]
    [InlineData(0.3f, MathF.PI / 2, -0.7f)]
    [InlineData(1.2f, -MathF.PI / 2, 0.4f)]
    [InlineData(-2f, MathF.PI / 2 - 1e-4f, 2.5f)]
    public void EulerAnglesAtGimbalLockReproduceTheRotation(float x, float y, float z)
    {
        var rotation = new quat(new vec3(x, y, z));

        var angles = TlmAnimations.ToContinuousEulerAngles(rotation, new vec3(x, y, z));

        AssertSameRotation(rotation, new quat(angles));
    }

    // Rounding put the sine of the middle angle of a turn of exactly 90 degrees past 1, which made the angles NaN and broke the
    // frames after it too
    [Fact]
    public void TurningThroughGimbalLockStaysOnTheSameAxis()
    {
        var rotations = new[] { new quat(new vec3(0, glm.Radians(88.0f), 0)), new quat(0, 0.70710683f, 0, 0.70710683f), new quat(new vec3(0, glm.Radians(92.0f), 0)) };
        vec3? previous = null;
        foreach (var (rotation, degrees) in rotations.Zip(new[] { 88.0f, 90.0f, 92.0f }))
        {
            var angles = TlmAnimations.ToContinuousEulerAngles(rotation, previous);

            Assert.Equal((0, TlmAnimations.ToRawRotation(glm.Radians(degrees)), 0),
                (TlmAnimations.ToRawRotation(angles.x), TlmAnimations.ToRawRotation(angles.y), TlmAnimations.ToRawRotation(angles.z)));
            previous = angles;
        }
    }

    // The game interpolates each angle separately, a wrap from +PI to -PI would spin the joint around the other way
    [Fact]
    public void ConsecutiveFramesDontJump()
    {
        vec3? previous = null;
        for (var frame = 0; frame < 400; frame++)
        {
            var expected = new vec3(frame * 0.05f, MathF.Sin(frame * 0.02f) * 1.4f, -frame * 0.03f);
            var rotation = new quat(expected);

            var angles = TlmAnimations.ToContinuousEulerAngles(rotation, previous);

            AssertSameRotation(rotation, new quat(angles));
            if (previous != null)
            {
                Assert.True((angles - previous.Value).Length < 0.2f, $"Frame {frame} jumped from {previous} to {angles}");
            }

            previous = angles;
        }
    }

    [Fact]
    public void AnimationsComeBackAsTheGameStoresThem()
    {
        // Animated channels that never change aren't what TT Lab makes out of keys, the game has some
        var main = new TwinAnimation { TotalFrames = 2 };
        main.JointSettings.Add(new JointSettings
        {
            TranslateX = TransformType.Animated, TranslateY = TransformType.Animated, TranslateZ = TransformType.Animated,
            RotateX = TransformType.Animated, RotateY = TransformType.Animated, RotateZ = TransformType.Animated,
            ScaleX = TransformType.Animated, ScaleY = TransformType.Animated, ScaleZ = TransformType.Animated
        });
        for (var frame = 0; frame < 2; frame++)
        {
            var transformation = new AnimatedTransformation(9);
            transformation.Transforms.AddRange(TestAnimations.RawValues(0, 0).Select(value => new Transformation { PureValue = value }));
            main.AnimatedTransformations.Add(transformation);
        }

        var animation = new AnimationData { ID = 0x1, Name = "Idle", TotalFrames = 2, DefaultFPS = 30, MainAnimation = main };
        var bytes = TlmAnimations.GetExactBytes(animation);

        var read = RoundTrip(animation, out var edited);

        Assert.False(edited);
        Assert.Equal(bytes, TlmAnimations.GetExactBytes(read));
        Assert.Equal((animation.ID, animation.Name, animation.DefaultFPS, animation.TotalFrames), (read.ID, read.Name, read.DefaultFPS, read.TotalFrames));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AnimationsAreMadeFromTheirKeysWithoutTheGamesBytes(int variant)
    {
        var animation = Create("Walk", 0x1, variant);

        var read = RoundTrip(animation, out var edited, file => Animation(file).Remove("exact"));

        Assert.True(edited);
        AssertSameKeyframes(animation, read);
    }

    // Blender moves the keys by rounding errors, far less than the game's smallest step
    [Fact]
    public void KeysThatStillHoldTheGamesValuesKeepItsBytes()
    {
        var animation = Create("Run", 0x2, 1);
        animation.FacialAnimation = CreateFacialAnimation();

        var read = RoundTrip(animation, out var edited, file => Nudge(file, 2e-6f));

        Assert.False(edited);
        Assert.Equal(TlmAnimations.GetExactBytes(animation), TlmAnimations.GetExactBytes(read));
    }

    // An edited joint doesn't change the others, the game's values of those are kept as they are
    [Fact]
    public void EditingAJointKeepsTheOthersAsTheGameHasThem()
    {
        var animation = Create("Run", 0x2, 1);

        var read = RoundTrip(animation, out var edited, file =>
        {
            var keys = Animation(file)["joints"]!.AsArray().OfType<JsonObject>().Single(joint => joint.GetInt("joint") == 1);
            var translations = file.Read<float>(keys["translation"]);
            translations[3 * 3 + 1] += 0.5f;
            keys["translation"] = file.Write(translations.AsSpan());
        });

        Assert.True(edited);
        for (var joint = 0; joint < JointsAmount; joint++)
        {
            for (var frame = 0; frame < Frames; frame++)
            {
                var expected = TlmAnimations.GetRawValues(animation, joint, frame);
                if (joint == 1 && frame == 3)
                {
                    expected[1] += 2048;
                }

                Assert.Equal(expected, TlmAnimations.GetRawValues(read, joint, frame));
            }
        }
    }

    // The game's animations are made in joint order, reading them from their keys gives their bytes back
    [Fact]
    public void AnimationsMadeFromUneditedKeysAreTheGamesAnimation()
    {
        var animation = Create("Walk", 0x1, 0);

        var read = RoundTrip(animation, out _, file => Animation(file).Remove("exact"));

        Assert.Equal(TlmAnimations.GetExactBytes(animation), TlmAnimations.GetExactBytes(read));
    }

    [Fact]
    public void KeysAreLocalTransformsWithTheAdditionalRotation()
    {
        var animation = Create("Run", 0x2, 1);
        var file = new TlmFile(OGIData.TlmAssetType, "Test");

        var json = TlmAnimations.Write(file, animation, Joints);

        var joint = json["joints"]!.AsArray().OfType<JsonObject>().Single(keys => keys.GetInt("joint") == 2);
        Assert.True(joint.GetBool("additional_rotation"));
        var rotations = file.Read<float>(joint["rotation"]);
        Assert.Equal(Frames * 4, rotations.Length);
        for (var frame = 0; frame < Frames; frame++)
        {
            var raw = TestAnimations.RawValues(2, frame, 1);
            var expected = AdditionalRotation * new quat(new vec3(raw[3], raw[4], raw[5]) / 4096.0f * MathF.PI * 2);
            AssertSameRotation(expected, new quat(rotations[frame * 4], rotations[frame * 4 + 1], rotations[frame * 4 + 2], rotations[frame * 4 + 3]));
        }

        var translations = file.Read<float>(joint["translation"]);
        Assert.Equal(TestAnimations.RawValues(2, 3, 1)[0] / 4096.0f, translations[3 * 3]);
    }

    // Most joints don't move in most animations
    [Fact]
    public void TracksThatNeverChangeAreOneKey()
    {
        var animation = Create("Walk", 0x1, 0);
        var file = new TlmFile(OGIData.TlmAssetType, "Test");

        var json = TlmAnimations.Write(file, animation, Joints);

        var root = json["joints"]!.AsArray().OfType<JsonObject>().Single(keys => keys.GetInt("joint") == 0);
        Assert.Equal(3, file.Read<float>(root["scale"]).Length);
        Assert.Equal(Frames * 3, file.Read<float>(root["translation"]).Length);
    }

    // Keeping every frame's angle close to the last one adds a turn every turn, a joint spinning for long enough overflowed the raw value
    [Fact]
    public void JointsSpinningForLongStayInRange()
    {
        const int frames = 60;
        var spin = new AnimationData
        {
            ID = 0x3,
            Name = "Spin",
            TotalFrames = frames,
            DefaultFPS = 25,
            MainAnimation = TestAnimations.CreateTwinAnimation(frames, JointsAmount, rawValues: (joint, frame) => joint == 1
                ? [0, 2048, 0, (short)(frame * 682 % 4096), 0, 0, 4096, 4096, 4096]
                : TestAnimations.RawValues(joint, frame))
        };

        var read = RoundTrip(spin, out _, file => Animation(file).Remove("exact"));

        for (var frame = 0; frame < frames; frame++)
        {
            var angle = TlmAnimations.GetRawValues(read, 1, frame)[3];
            var difference = ((angle - frame * 682) % 4096 + 4096) % 4096;
            Assert.True(Math.Min(difference, 4096 - difference) <= 1, $"Frame {frame} turned to {angle}");
        }
    }

    [Fact]
    public void FacialAnimationsAreMadeFromTheirWeights()
    {
        var animation = Create("Talk", 0x4, 0);
        animation.FacialAnimation = CreateFacialAnimation();

        var read = RoundTrip(animation, out _, file => Animation(file).Remove("exact"));

        var settings = read.FacialAnimation.JointSettings.Single();
        Assert.Equal((3, true, false), (settings.FacialShapesAmount, settings.UnusedFlag, settings.UseAdditionalRotation));
        Assert.Equal([TransformType.Animated, TransformType.Static, TransformType.Animated], settings.AnimationMorph.Take(3));
        for (var frame = 0; frame < Frames; frame++)
        {
            Assert.Equal(TlmAnimations.GetFacialRawValues(animation.FacialAnimation, frame), TlmAnimations.GetFacialRawValues(read.FacialAnimation, frame));
        }

        Assert.Equal(TlmAnimations.GetExactBytes(animation), TlmAnimations.GetExactBytes(read));
    }

    private static TwinMorphAnimation CreateFacialAnimation()
    {
        var settings = new MorphJointSettings { FacialShapesAmount = 3, UnusedFlag = true };
        settings.AnimationMorph[0] = TransformType.Animated;
        settings.AnimationMorph[1] = TransformType.Static;
        settings.AnimationMorph[2] = TransformType.Animated;
        var animation = new TwinMorphAnimation { TotalFrames = Frames, JointSettings = [settings], StaticTransformations = [new Transformation { PureValue = 1024 }] };
        for (var frame = 0; frame < Frames; frame++)
        {
            var transformation = new AnimatedTransformation(2);
            transformation.Transforms.AddRange([new Transformation { PureValue = (short)(frame * 500) }, new Transformation { PureValue = (short)(4096 - frame * 300) }]);
            animation.AnimatedTransformations.Add(transformation);
        }

        return animation;
    }

    private static AnimationData Create(string name, uint id, int variant)
    {
        return new AnimationData
        {
            ID = id,
            Name = name,
            TotalFrames = Frames,
            DefaultFPS = 25,
            MainAnimation = TestAnimations.CreateTwinAnimation(Frames, JointsAmount, variant, joint => joint == 1, joint => variant == 1 && joint == 2)
        };
    }

    // Written into a file, saved, loaded and read back
    private static AnimationData RoundTrip(AnimationData animation, out bool fromKeys, Action<TlmFile>? edit = null)
    {
        var file = new TlmFile(OGIData.TlmAssetType, "Test");
        file.Root = new JsonObject { ["animations"] = new JsonArray(TlmAnimations.Write(file, animation, Joints)) };
        edit?.Invoke(file);
        using var stream = new MemoryStream();
        file.WriteTo(stream);
        stream.Position = 0;
        var read = TlmFile.Read(stream);
        return TlmAnimations.Read(read, Animation(read), Joints, out fromKeys);
    }

    private static JsonObject Animation(TlmFile file) => file.Root!["animations"]![0]!.AsObject();

    // Moves every key a bit, like Blender's rounding does
    private static void Nudge(TlmFile file, float amount)
    {
        foreach (var keys in Animation(file)["joints"]!.AsArray().OfType<JsonObject>())
        {
            foreach (var track in new[] { "translation", "rotation", "scale" })
            {
                keys[track] = file.Write(file.Read<float>(keys[track]).Select(value => value + amount).ToArray().AsSpan());
            }
        }

        var facial = Animation(file)["facial"]!.AsObject();
        facial["weights"] = file.Write(file.Read<float>(facial["weights"]).Select(value => value + amount).ToArray().AsSpan());
    }

    private static void AssertSameKeyframes(AnimationData expected, AnimationData actual)
    {
        Assert.Equal(expected.TotalFrames, actual.TotalFrames);
        Assert.Equal(expected.DefaultFPS, actual.DefaultFPS);
        for (var joint = 0; joint < JointsAmount; joint++)
        {
            var expectedSettings = expected.MainAnimation.JointSettings[joint];
            var actualSettings = actual.MainAnimation.JointSettings[joint];
            Assert.Equal(expectedSettings.IndependentScaling, actualSettings.IndependentScaling);
            Assert.Equal(expectedSettings.UseAdditionalRotation, actualSettings.UseAdditionalRotation);
            for (var frame = 0; frame < expected.TotalFrames; frame++)
            {
                var expectedValues = TlmAnimations.GetRawValues(expected, joint, frame);
                var actualValues = TlmAnimations.GetRawValues(actual, joint, frame);
                // Translations and scales are exact, rotations go through quaternions and can be a unit off
                Assert.Equal(expectedValues[..3], actualValues[..3]);
                Assert.Equal(expectedValues[6..], actualValues[6..]);
                for (var channel = 3; channel < 6; channel++)
                {
                    Assert.InRange(actualValues[channel], expectedValues[channel] - 1, expectedValues[channel] + 1);
                }
            }
        }
    }

    private static quat RandomRotation(Random random)
    {
        return new quat(new vec3((float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble() * 2 - 1)) * MathF.PI * 2);
    }

    private static void AssertSameRotation(quat expected, quat actual)
    {
        Assert.True(Math.Abs(quat.Dot(expected.Normalized, actual.Normalized)) > 0.99999f, $"{actual} isn't {expected}");
    }
}
