using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.Assets;
using TT_Lab.Controls;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Global;
using TT_Lab.Views.Editors.Global;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;

namespace TT_Lab.Tests.Editor;

// The save icon's editor plays its animation like the OGI editor plays its animations: it starts by itself (the console's browser plays
// the icon all along), 60 frames a second times the icon's speed, looping over its frame length, with a slider, pausing and a speed
[Collection(ProjectCollection.Name)]
public sealed class SaveIconAnimationEditorTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public SaveIconAnimationEditorTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    private DocumentViewModel Open(PS2SaveIcon icon)
    {
        var document = new DocumentViewModel(_assets.AddSaveIcon("Crash", icon));
        document.Initialize();
        return document;
    }

    private static SaveIconAnimationViewModel Editor(DocumentViewModel document)
    {
        var editor = Assert.IsType<SaveIconAnimationViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.Icon")!).Construct());
        editor.Activator.Activate();
        return editor;
    }

    // The game's own: one shape, a frame length of 1
    private static PS2SaveIcon StillIcon()
    {
        var icon = TestAssets.MakeSaveIcon();
        icon.ShapeCount = 1;
        icon.FrameLength = 1;
        icon.Frames = [new SaveIconFrame { Shape = 0, Keys = [new SaveIconKey(0, 1)] }];
        return icon;
    }

    [AvaloniaFact]
    public void TheAnimationPlaysByItselfAndLoops()
    {
        // 60 frames long at 1.5 times the speed, the first shape fading into the second by frame 30 and back
        var editor = Editor(Open(TestAssets.MakeSaveIcon()));

        Assert.True(editor.HasAnimation);
        Assert.True(editor.IsPlaying);
        Assert.Equal($"2 shapes, 60 frames played at {90:0.##} a second", editor.Summary);
        editor.PauseAnimation();
        editor.Frame = 0;

        editor.AdvanceBy(0.5);
        Assert.Equal("Frame 45 of 59", editor.FrameText);
        editor.AdvanceBy(0.5);
        Assert.Equal(30.0, editor.Frame, 6);
        Assert.Equal($"Shape weights: 0 {0:0.##}, 1 {1:0.##}", editor.WeightsText);
        editor.Speed = 2;
        editor.AdvanceBy(0.25);
        Assert.Equal(15.0, editor.Frame, 6);
        Assert.Equal($"Shape weights: 0 {0.5f:0.##}, 1 {0.5f:0.##}", editor.WeightsText);

        // Without looping it stops on the last frame
        editor.Loop = false;
        editor.PlayAnimation();
        editor.AdvanceBy(1.0);
        Assert.Equal(59.0, editor.Frame);
        Assert.False(editor.IsPlaying);
        editor.PlayAnimation();
        Assert.Equal(0.0, editor.Frame);
        editor.StopAnimation();
        Assert.False(editor.IsPlaying);
    }

    [AvaloniaFact]
    public void AStillIconSaysWhyNothingPlays()
    {
        var editor = Editor(Open(StillIcon()));

        Assert.False(editor.HasAnimation);
        Assert.False(editor.IsPlaying);
        Assert.Equal("1 shape, 1 frame", editor.Summary);
        Assert.Contains("one shape", editor.NoAnimationText);
        editor.PlayAnimation();
        Assert.False(editor.IsPlaying);
    }

    [AvaloniaFact]
    public void TheEditorShowsInTheIconsDocument()
    {
        var document = Open(TestAssets.MakeSaveIcon());
        document.OpenInspector(document.PropertyGraph.Root, document.PropertyGraph.Find("Root.AssetData.Icon"));
        var window = new Window { Content = new DocumentScrollViewer { Content = document.Inspector }, Width = 800, Height = 600 };
        window.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        var view = window.GetVisualDescendants().OfType<SaveIconAnimationView>().Single();
        var editor = Assert.IsType<SaveIconAnimationViewModel>(view.ViewModel);
        Assert.True(editor.IsPlaying);
        Assert.Contains(window.GetVisualDescendants().OfType<Slider>(), slider => slider.Maximum == 59);
        window.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.False(editor.IsPlaying);
    }
}
