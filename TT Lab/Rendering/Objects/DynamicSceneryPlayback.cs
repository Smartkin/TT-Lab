using System;

namespace TT_Lab.Rendering.Objects;

// Where a dynamic scenery model's animation is the way the game plays it (the decomp's DynamicSceneryNode::Animate): 25 frames a second,
// the time taken back a loop at a time once it's past the last frame, which blends into the first. Seconds is the time taken back,
// what the next step adds to
public readonly record struct DynamicSceneryPlayback(int Frame, float Share, int Next, double Seconds)
{
    public const double FramesPerSecond = 25.0;
    public const double SecondsPerFrame = 1.0 / FramesPerSecond;

    public static DynamicSceneryPlayback At(double seconds, int frames)
    {
        // A model of one frame stands still, the game takes the time of one of none back for ever
        if (frames <= 1)
        {
            return new DynamicSceneryPlayback(0, 0.0f, 0, 0.0);
        }

        var at = seconds * FramesPerSecond;
        // A step of more than a loop (a hidden tab's viewport stops updating, the step it takes once it's shown again is the whole time)
        // takes back all but one at once
        if (at >= 2 * frames)
        {
            seconds -= (Math.Floor(at / frames) - 1) * frames * SecondsPerFrame;
            at = seconds * FramesPerSecond;
        }

        while (at >= frames)
        {
            seconds -= frames * SecondsPerFrame;
            at = seconds * FramesPerSecond;
        }

        var frame = (int)at;
        return new DynamicSceneryPlayback(frame, (float)(at - frame), frame + 1 < frames ? frame + 1 : 0, seconds);
    }
}
