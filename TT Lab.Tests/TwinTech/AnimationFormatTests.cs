using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using Twinsanity.TwinsanityInterchange.Interfaces;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace TT_Lab.Tests.TwinTech;

public class AnimationFormatTests
{
    [Theory]
    [InlineData((short)0, 0f)]
    [InlineData((short)4096, 1f)]
    [InlineData((short)-2048, -0.5f)]
    [InlineData(short.MaxValue, 32767f / 4096f)]
    [InlineData(short.MinValue, -8f)]
    public void TransformationValueIsFixedPoint(short raw, float value)
    {
        var transformation = new Transformation { PureValue = raw };

        Assert.Equal(value, transformation.Value);
    }

    [Fact]
    public void RotationUses4096UnitsPerTurn()
    {
        var quarterTurn = new Transformation { PureValue = 1024 };

        Assert.Equal(MathF.PI / 2, quarterTurn.RotationValue, 5);
    }

    // Raw values have to be kept as they are, going through the float properties loses precision
    [Fact]
    public void PureValueRoundTripsEveryRawValue()
    {
        for (int raw = short.MinValue; raw <= short.MaxValue; raw += 7)
        {
            var transformation = new Transformation { PureValue = (short)raw };
            var copy = RoundTrip(transformation, new Transformation());

            Assert.Equal((short)raw, copy.PureValue);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void JointSettingsKeepTheirFlags(bool independentScaling, bool useAdditionalRotation)
    {
        var settings = new JointSettings
        {
            IndependentScaling = independentScaling,
            UseAdditionalRotation = useAdditionalRotation,
            TranslateX = TransformType.Static,
            TranslateY = TransformType.Animated,
            TranslateZ = TransformType.Static,
            RotateX = TransformType.Animated,
            RotateY = TransformType.Animated,
            RotateZ = TransformType.Static,
            ScaleX = TransformType.Static,
            ScaleY = TransformType.Static,
            ScaleZ = TransformType.Animated,
            TransformationIndex = 0x1234,
            AnimationTransformationIndex = 0x4321
        };

        var bytes = Write(settings);
        var copy = RoundTrip(settings, new JointSettings());

        // Independent scaling is bit 0xD and additional rotation bit 0xC of the first half-word
        var flags = BitConverter.ToUInt16(bytes, 0);
        Assert.Equal(independentScaling, (flags >> 0xD & 0x1) != 0);
        Assert.Equal(useAdditionalRotation, (flags >> 0xC & 0x1) != 0);
        // 9 channels, 4 of them animated and 5 static
        Assert.Equal(0x0945, flags & 0x0FFF);
        Assert.Equal(independentScaling, copy.IndependentScaling);
        Assert.Equal(useAdditionalRotation, copy.UseAdditionalRotation);
        Assert.Equal(
            new[] { settings.TranslateX, settings.TranslateY, settings.TranslateZ, settings.RotateX, settings.RotateY, settings.RotateZ, settings.ScaleX, settings.ScaleY, settings.ScaleZ },
            new[] { copy.TranslateX, copy.TranslateY, copy.TranslateZ, copy.RotateX, copy.RotateY, copy.RotateZ, copy.ScaleX, copy.ScaleY, copy.ScaleZ });
        Assert.Equal(settings.TransformationIndex, copy.TransformationIndex);
        Assert.Equal(settings.AnimationTransformationIndex, copy.AnimationTransformationIndex);
    }

    // Both versions of the game count each joint's channels in its flags and the animation's header
    [Fact]
    public void AnimationsCountTheirChannels()
    {
        var facial = new TwinMorphAnimation();
        Assert.Equal(0u, BitConverter.ToUInt32(Write(facial), 0));
        var morph = new MorphJointSettings { FacialShapesAmount = 13 };
        Array.Fill(morph.AnimationMorph, TransformType.Static);
        morph.AnimationMorph[0] = TransformType.Animated;
        morph.AnimationMorph[12] = TransformType.Animated;
        // Shapes past the amount aren't stored
        morph.AnimationMorph[14] = TransformType.Animated;
        facial.JointSettings.Add(morph);
        var main = new TwinAnimation();
        main.JointSettings.Add(new JointSettings { RotateY = TransformType.Static });

        var facialBytes = Write(facial);
        var mainBytes = Write(main);

        Assert.Equal(0x680u, BitConverter.ToUInt32(facialBytes, 0) & 0x780);
        Assert.Equal(0x0D2B, BitConverter.ToUInt16(facialBytes, 6));
        Assert.Equal(0x0FFE, BitConverter.ToUInt16(facialBytes, 8));
        Assert.Equal(0x480u, BitConverter.ToUInt32(mainBytes, 0) & 0x780);
        Assert.Equal(0x0981, BitConverter.ToUInt16(mainBytes, 6));
    }

    [Fact]
    public void AnimationRoundTripsAndMatchesItsLength()
    {
        var animation = TestAnimations.CreateTwinAnimation(frames: 5, joints: 3);

        var bytes = Write(animation);
        var copy = RoundTrip(animation, new TwinAnimation());

        Assert.Equal(animation.GetLength(), bytes.Length);
        Assert.Equal(animation.TotalFrames, copy.TotalFrames);
        Assert.Equal(animation.JointSettings.Count, copy.JointSettings.Count);
        Assert.Equal(animation.StaticTransformations.Select(t => t.PureValue), copy.StaticTransformations.Select(t => t.PureValue));
        Assert.Equal(animation.AnimatedTransformations.Count, copy.AnimatedTransformations.Count);
        for (var frame = 0; frame < animation.TotalFrames; frame++)
        {
            Assert.Equal(animation.AnimatedTransformations[frame].Transforms.Select(t => t.PureValue), copy.AnimatedTransformations[frame].Transforms.Select(t => t.PureValue));
        }

        Assert.Equal(bytes, Write(copy));
    }

    private static byte[] Write(ITwinSerializable item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static T RoundTrip<T>(ITwinSerializable item, T target) where T : ITwinSerializable
    {
        var bytes = Write(item);
        using var reader = new BinaryReader(new MemoryStream(bytes));
        target.Read(reader, bytes.Length);
        Assert.Equal(bytes.Length, reader.BaseStream.Position);
        return target;
    }
}
