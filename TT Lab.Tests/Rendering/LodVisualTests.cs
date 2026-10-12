using TT_Lab.Rendering.Objects;

namespace TT_Lab.Tests.Rendering;

// The game picks a level of detail's mesh by the camera's distance squared (LodMeshAt): the first up to the first switch, the next past each
// one, and draws none nearer than the nearest or farther than the farthest distance. Unused switches hold the tools' leftovers
public class LodVisualTests
{
    private static readonly int[] Switches = [110 * 110, 170 * 170, 1641631945];

    [Fact]
    public void TheDistancePicksTheLevel()
    {
        Assert.Equal(0, LodVisual.LevelAt(Switches, 3, 50.0f * 50.0f));
        Assert.Equal(1, LodVisual.LevelAt(Switches, 3, 120.0f * 120.0f));
        Assert.Equal(2, LodVisual.LevelAt(Switches, 3, 200.0f * 200.0f));
        // Past the farthest distance the last level is the one the editor shows
        Assert.Equal(2, LodVisual.LevelAt(Switches, 3, 260.0f * 260.0f));
    }

    [Fact]
    public void TheGameDrawsNothingPastTheFarthestDistance()
    {
        Assert.True(LodVisual.IsDrawnAt(0, 250 * 250, 200.0f * 200.0f));
        Assert.False(LodVisual.IsDrawnAt(0, 250 * 250, 260.0f * 260.0f));
        Assert.False(LodVisual.IsDrawnAt(400, -1, 10.0f));
        // -1 has no limit
        Assert.True(LodVisual.IsDrawnAt(0, -1, 1.0e7f));
    }

    [Fact]
    public void ALodOfOneMeshNeverSwitches()
    {
        Assert.Equal(0, LodVisual.LevelAt([1641631945, 1641631945, 1641631945], 1, 99.0f * 99.0f));
        Assert.Equal(1, LodVisual.LevelAt([90 * 90, 1641631945, 1641631945], 2, 1.0e7f));
    }
}
