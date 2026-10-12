using Avalonia.Headless.XUnit;
using TT_Lab.ViewModels;

namespace TT_Lab.Tests.Editor;

public sealed class ViewportSnappingTests
{
    [AvaloniaFact]
    public void TypedStepsApplyOnceTheyAreNumbers()
    {
        var step = new SnapStep(1.0f, 0.5f, 1.0f);

        // Halfway through typing 0.25
        step.Text = "0";
        Assert.Equal(1.0f, step.Value);
        Assert.Equal("0", step.Text);

        step.Text = "0.25";
        Assert.Equal(0.25f, step.Value);
        Assert.Equal("0.25", step.Text);

        step.Value = 2.0f;
        Assert.Equal(SnapStep.Format(2.0f), step.Text);
    }

    // Snapping is kept in the preferences, which every viewport follows
    [AvaloniaFact]
    public void SnappingIsTheSameInEveryViewport()
    {
        var first = new ViewportViewModel();
        var second = new ViewportViewModel();
        var wasSnapping = first.IsSnapping;
        var rotationStep = first.RotationSnap.Value;
        try
        {
            first.ToggleSnappingCommand.Execute().Subscribe();
            first.RotationSnap.Text = "45";

            Assert.Equal(!wasSnapping, second.IsSnapping);
            Assert.Equal(45.0f, second.RotationSnap.Value);
            Assert.Equal("45", second.RotationSnap.Text);
        }
        finally
        {
            first.IsSnapping = wasSnapping;
            first.RotationSnap.Value = rotationStep;
            first.Close();
            second.Close();
        }
    }

    // Leaving out the scenery's LODs past their distances like the game is the toolbar's choice, kept in the preferences every viewport follows
    [AvaloniaFact]
    public void LodCullingIsTheSameInEveryViewport()
    {
        var first = new ViewportViewModel();
        var second = new ViewportViewModel();
        var wasCulling = first.CullsLods;
        try
        {
            Assert.Equal(Preferences.GetPreference<bool>(Preferences.ViewportLodCulling), wasCulling);
            first.ToggleLodCullingCommand.Execute().Subscribe();

            Assert.Equal(!wasCulling, second.CullsLods);
            Assert.Equal(!wasCulling, Preferences.GetPreference<bool>(Preferences.ViewportLodCulling));
        }
        finally
        {
            first.CullsLods = wasCulling;
            first.Close();
            second.Close();
        }
    }
}
