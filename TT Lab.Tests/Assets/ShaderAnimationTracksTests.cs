using Newtonsoft.Json;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Rendering.Materials;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace TT_Lab.Tests.Assets;

// The material editor's animation edits keep the game's layout: static tracks read the next static value, animated ones the next value of
// every frame, from the first settings' indexes
public class ShaderAnimationTracksTests
{
    private static byte[] Bytes(TwinShaderAnimation animation)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        animation.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    // Like some of the game's: its values start past leftovers and a second settings block the game never reads follows
    private static TwinShaderAnimation Leftovers()
    {
        var animation = new TwinShaderAnimation { TotalFrames = 2 };
        animation.TimedFrames = 2;
        animation.FramesPerSecond = 10;
        animation.AnimationSettings.Add(new AnimationSettings
        {
            TranslateX = TransformType.Animated, TranslateY = TransformType.Static, ColorR = TransformType.Static, ColorG = TransformType.Static,
            ColorB = TransformType.Static, ColorA = TransformType.Static, StaticTransformationIndex = 1, AnimationTransformationIndex = 1
        });
        animation.AnimationSettings.Add(new AnimationSettings { StaticTransformationIndex = 7, AnimationTransformationIndex = 3 });
        foreach (var value in new[] { 9.0f, 0.5f, 1.0f, 1.0f, 1.0f, 1.0f })
        {
            animation.StaticTransformations.Add(new Transformation { Value = value });
        }

        for (var frame = 0; frame < 2; frame++)
        {
            var values = new AnimatedTransformation(2);
            values.Transforms.Add(new Transformation { Value = 7.0f });
            values.Transforms.Add(new Transformation { Value = frame * 0.25f });
            animation.AnimatedTransformations.Add(values);
        }

        return animation;
    }

    [Fact]
    public void NewAnimationsAreStillAndWhite()
    {
        var animation = ShaderAnimationTracks.Create();

        Assert.Equal(1, ShaderAnimationTracks.FrameCount(animation));
        Assert.Equal(ShaderAnimationTracks.DefaultFramesPerSecond, animation.FramesPerSecond);
        Assert.All(Enumerable.Range(0, ShaderAnimationTracks.Count), track => Assert.False(ShaderAnimationTracks.IsAnimated(animation, track)));
        Assert.Equal([0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 1.0f], Enumerable.Range(0, ShaderAnimationTracks.Count).Select(track => ShaderAnimationTracks.ValueAt(animation, track, 0)));
        var sample = ShaderAnimationSampler.At(animation, 0.0);
        Assert.Equal((0.0f, 0.0f, 1.0f, 1.0f), (sample.Uv.x, sample.Uv.y, sample.Color.x, sample.Color.w));
    }

    [Fact]
    public void ValuesAreReadAndWrittenWhereTheGameReadsThem()
    {
        var animation = Leftovers();

        Assert.Equal(0.25f, ShaderAnimationTracks.ValueAt(animation, 0, 1));
        Assert.Equal(0.5f, ShaderAnimationTracks.ValueAt(animation, 1, 0));

        var edited = ShaderAnimationTracks.SetValue(animation, 0, 1, 0.75f);

        Assert.Equal(0.75f, ShaderAnimationTracks.ValueAt(edited, 0, 1));
        // Only the frame's value changed, the leftovers stay
        var before = Bytes(animation);
        var after = Bytes(edited);
        Assert.Equal(before.Length, after.Length);
        Assert.Single(Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).Select(i => i / 2).Distinct());
        Assert.Equal(0.25f, ShaderAnimationTracks.ValueAt(animation, 0, 1));
    }

    [Fact]
    public void TracksTurnAnimatedAndBackKeepingTheirValue()
    {
        var animation = ShaderAnimationTracks.InsertFrame(ShaderAnimationTracks.InsertFrame(ShaderAnimationTracks.Create(), 0), 0);

        var animated = ShaderAnimationTracks.SetAnimated(animation, 3, true, 0);

        Assert.True(ShaderAnimationTracks.IsAnimated(animated, 3));
        Assert.All(Enumerable.Range(0, 3), frame => Assert.Equal(1.0f, ShaderAnimationTracks.ValueAt(animated, 3, frame)));
        animated = ShaderAnimationTracks.SetValue(animated, 3, 2, 0.25f);
        // The other tracks still read their own values
        Assert.Equal([0.0f, 0.0f, 1.0f, 1.0f, 1.0f], new[] { 0, 1, 2, 4, 5 }.Select(track => ShaderAnimationTracks.ValueAt(animated, track, 2)));
        var sample = ShaderAnimationSampler.At(animated, 2.0 / ShaderAnimationTracks.DefaultFramesPerSecond);
        Assert.Equal(0.25f, sample.Color.y, 1e-4f);

        var still = ShaderAnimationTracks.SetAnimated(animated, 3, false, 2);

        Assert.False(ShaderAnimationTracks.IsAnimated(still, 3));
        Assert.Equal(0.25f, ShaderAnimationTracks.ValueAt(still, 3, 0));
        Assert.Equal(1.0f, ShaderAnimationTracks.ValueAt(still, 4, 0));
    }

    [Fact]
    public void FramesGoInAsCopiesAndTheGamesCountFollows()
    {
        var animation = ShaderAnimationTracks.SetAnimated(ShaderAnimationTracks.Create(), 0, true, 0);
        animation = ShaderAnimationTracks.SetValue(animation, 0, 0, 0.5f);

        var longer = ShaderAnimationTracks.InsertFrame(animation, 0);

        Assert.Equal(2, ShaderAnimationTracks.FrameCount(longer));
        Assert.Equal((2, (ushort)2), (longer.TimedFrames, longer.TotalFrames));
        Assert.Equal(0.5f, ShaderAnimationTracks.ValueAt(longer, 0, 1));
        Assert.True(ShaderAnimationTracks.CanRemoveFrame(longer));

        var shorter = ShaderAnimationTracks.RemoveFrame(longer, 0);

        Assert.Equal(1, ShaderAnimationTracks.FrameCount(shorter));
        Assert.Equal(1, shorter.TimedFrames);
        Assert.False(ShaderAnimationTracks.CanRemoveFrame(shorter));
        Assert.Same(shorter, ShaderAnimationTracks.RemoveFrame(shorter, 0));
    }

    [Fact]
    public void ValuesStayWithinTheGamesFixedPoint()
    {
        Assert.Equal(short.MaxValue, ShaderAnimationTracks.ToRaw(100.0f));
        Assert.Equal(short.MinValue, ShaderAnimationTracks.ToRaw(-100.0f));
        Assert.Equal(2048, ShaderAnimationTracks.ToRaw(0.5f));
        Assert.Equal(31, ShaderAnimationTracks.SetFramesPerSecond(ShaderAnimationTracks.Create(), 60).FramesPerSecond);
    }

    // The material's data file keeps every frame's values, which reading it back (replacing what the data had) left out
    [Fact]
    public void AnAnimationComesBackFromTheDataFileAsItWas()
    {
        var material = new MaterialData(null);
        material.Shaders[0].Animation = Leftovers();

        var read = new MaterialData(null);
        read.PopulateFrom(JsonConvert.SerializeObject(material));

        Assert.Equal(Bytes(Leftovers()), Bytes(read.Shaders[0].Animation!));
    }
}
