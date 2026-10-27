using TT_Lab.Rendering.Objects;

namespace TT_Lab.Tests.Rendering;

// Dynamic scenery plays at the game's 25 frames a second whatever the viewport's rate (the decomp's DynamicSceneryNode::Animate): it stepped a
// frame of 50 a second at most once an update and dropped the time past it, 1.2 times the game's rate at 60 FPS and slower as frames took longer
public class DynamicSceneryPlaybackTests
{
    private const int Frames = 10;

    private static DynamicSceneryPlayback Play(IEnumerable<double> steps, int frames = Frames)
    {
        var playback = DynamicSceneryPlayback.At(0.0, frames);
        foreach (var step in steps)
        {
            playback = DynamicSceneryPlayback.At(playback.Seconds + step, frames);
        }

        return playback;
    }

    private static IEnumerable<double> ASecondOf(double step) => Enumerable.Repeat(step, (int)Math.Round(1.0 / step));

    // Where in the loop it is, a frame's share past a frame that ends just short of the next one is the same place
    private static void AssertAt(double expected, DynamicSceneryPlayback playback, int frames = Frames)
    {
        var at = playback.Frame + playback.Share;
        var off = Math.Abs(at - expected) % frames;
        Assert.True(Math.Min(off, frames - off) < 1e-4, $"At frame {playback.Frame} and {playback.Share} of the next, not {expected}");
        Assert.InRange(playback.Frame, 0, frames - 1);
        Assert.InRange(playback.Share, 0.0f, 1.0f);
        Assert.Equal((playback.Frame + 1) % frames, playback.Next);
    }

    [Theory]
    [InlineData(1.0 / 60)]
    [InlineData(1.0 / 144)]
    [InlineData(1.0 / 30)]
    [InlineData(0.001)]
    [InlineData(1.0)]
    public void ASecondIsTwentyFiveFramesWhateverTheSteps(double step)
    {
        AssertAt(25 % Frames, Play(ASecondOf(step)));
        AssertAt(25 % 30, Play(ASecondOf(step), 30), 30);
    }

    // The viewport's steps are whole milliseconds (Renderer.DoUpdate), 16 and 17 of them at about 60 FPS
    [Fact]
    public void ASecondOfTheViewportsStepsIsTwentyFiveFrames()
    {
        var steps = Enumerable.Repeat(new[] { 0.017, 0.017, 0.016 }, 20).SelectMany(step => step).ToList();
        Assert.Equal(1.0, steps.Sum(), 1e-9);

        AssertAt(25 % Frames, Play(steps));
    }

    [Fact]
    public void HalfASecondOfTenFramesIsHalfwayToTheThird()
    {
        var playback = DynamicSceneryPlayback.At(0.5, Frames);

        Assert.Equal(2, playback.Frame);
        Assert.Equal(0.5f, playback.Share, 1e-4f);
        Assert.Equal(3, playback.Next);
        Assert.Equal(0.1, playback.Seconds, 1e-9);
    }

    [Fact]
    public void TheLastFrameBlendsIntoTheFirst()
    {
        var last = DynamicSceneryPlayback.At(9.5 / DynamicSceneryPlayback.FramesPerSecond, Frames);
        Assert.Equal(9, last.Frame);
        Assert.Equal(0, last.Next);

        var after = DynamicSceneryPlayback.At(last.Seconds + 1.0 / DynamicSceneryPlayback.FramesPerSecond, Frames);
        Assert.Equal(0, after.Frame);
        Assert.Equal(1, after.Next);
        AssertAt(0.5, after);
    }

    // A tab's viewport stops updating while it's hidden, and the step it takes when it's shown again is the whole time
    [Fact]
    public void ALongStepTakesItsLoopsBackAtOnce()
    {
        var playback = DynamicSceneryPlayback.At(36000.5 * Frames / DynamicSceneryPlayback.FramesPerSecond, Frames);

        AssertAt(5, playback);
        Assert.InRange(playback.Seconds, 0.0, Frames / DynamicSceneryPlayback.FramesPerSecond);
    }

    // The game takes the time of a model of no frames back for ever
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AModelOfAFrameOrNoneStandsStill(int frames)
    {
        var playback = DynamicSceneryPlayback.At(12.3, frames);

        Assert.Equal((0, 0, 0.0f), (playback.Frame, playback.Next, playback.Share));
    }
}
