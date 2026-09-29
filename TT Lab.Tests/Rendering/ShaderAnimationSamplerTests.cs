using TT_Lab.Rendering.Materials;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;
using static Twinsanity.TwinsanityInterchange.Common.Animation.Enums;

namespace TT_Lab.Tests.Rendering;

// The viewport plays shader animations like the game's AnimateShader: looping frames at the header's rate, static tracks as they are,
// animated ones interpolated to the next frame
public class ShaderAnimationSamplerTests
{
    private static TwinShaderAnimation Animation()
    {
        var animation = new TwinShaderAnimation { TotalFrames = 4 };
        animation.TimedFrames = 4;
        animation.FramesPerSecond = 2;
        // U and alpha are static, V and the colors animated: 3 static values, 4 animated per frame wouldn't fit either, so 2 static (U, A)
        animation.AnimationSettings.Add(new AnimationSettings
        {
            TranslateX = TransformType.Static, TranslateY = TransformType.Animated, ColorR = TransformType.Animated, ColorG = TransformType.Animated,
            ColorB = TransformType.Animated, ColorA = TransformType.Static, StaticTransformationIndex = 0, AnimationTransformationIndex = 0
        });
        animation.StaticTransformations.Add(new Transformation { Value = 0.25f });
        animation.StaticTransformations.Add(new Transformation { Value = 0.5f });
        for (var frame = 0; frame < 4; frame++)
        {
            var values = new AnimatedTransformation(4);
            values.Transforms.Add(new Transformation { Value = frame });
            values.Transforms.Add(new Transformation { Value = 1.0f });
            values.Transforms.Add(new Transformation { Value = frame % 2 });
            values.Transforms.Add(new Transformation { Value = 0.0f });
            animation.AnimatedTransformations.Add(values);
        }

        return animation;
    }

    [Fact]
    public void TracksAreStaticOrInterpolatedBetweenFrames()
    {
        var animation = Animation();

        var start = ShaderAnimationSampler.At(animation, 0.0);
        var quarter = ShaderAnimationSampler.At(animation, 0.25);

        Assert.Equal((0.25f, 0.0f), (start.Uv.x, start.Uv.y));
        Assert.Equal((1.0f, 0.0f, 0.0f, 0.5f), (start.Color.x, start.Color.y, start.Color.z, start.Color.w));
        // Half a frame in at 2 frames a second: V halfway to 1, green halfway to 1
        Assert.Equal(0.5f, quarter.Uv.y, 1e-5f);
        Assert.Equal(0.5f, quarter.Color.y, 1e-5f);
    }

    [Fact]
    public void TheLastFrameLoopsToTheFirst()
    {
        var animation = Animation();

        var lastHalf = ShaderAnimationSampler.At(animation, 1.75);
        var wrapped = ShaderAnimationSampler.At(animation, 2.0);

        // Frame 3 (V 3) halfway to frame 0 (V 0)
        Assert.Equal(1.5f, lastHalf.Uv.y, 1e-5f);
        Assert.Equal(0.0f, wrapped.Uv.y, 1e-5f);
    }

    [Fact]
    public void NothingToPlayLeavesTheShaderStill()
    {
        Assert.Equal(ShaderAnimationSampler.Still, ShaderAnimationSampler.At(null, 3.0));
        Assert.Equal(ShaderAnimationSampler.Still, ShaderAnimationSampler.At(new TwinShaderAnimation(), 3.0));
    }
}
