using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Code;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.Views.Editors;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Editor;

[Collection(ProjectCollection.Name)]
public sealed class OgiAnimationEditorTests : IDisposable
{
    private const int Frames = 4;
    private const int Joints = 3;

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private static AnimationData CreateAnimation(uint id, string name, int variant = 0, int frames = Frames) => new()
    {
        ID = id,
        Name = name,
        TotalFrames = (ushort)frames,
        DefaultFPS = 25,
        MainAnimation = TestAnimations.CreateTwinAnimation(frames, Joints, variant)
    };

    private OGI AddOgi(string name, params AnimationData[] animations)
    {
        var ogi = _project.Add(new OGI(), name);
        var data = new OGIData(ogi)
        {
            Joints = Enumerable.Range(0, Joints).Select(joint => new TwinJoint
            {
                Index = joint, ParentIndex = joint == 0 ? 255 : joint - 1, LocalTranslation = new Vector4(0, joint, 0, 1), LocalRotation = new Vector4(0, 0, 0, 1),
                AdditionalAnimationRotation = new Vector4(0, 0, 0, 1)
            }).ToList()
        };
        data.SetAnimations([..animations]);
        ogi.SetData(data);
        return ogi;
    }

    private (DocumentViewModel Document, ModelSlot Slot) OpenGameObject(ModelSlot slot)
    {
        var gameObject = _project.Add(new GameObject(), "Crash");
        var data = new GameObjectData(gameObject);
        data.ModelSlots.Add(slot);
        gameObject.SetData(data);
        var document = new DocumentViewModel(gameObject);
        document.Initialize();
        return (document, slot);
    }

    private static T Construct<T>(DocumentViewModel document, string path) where T : DocumentNodeViewModel
    {
        var editor = Assert.IsType<T>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct());
        var window = new Window { Content = new ContentControl { Content = editor }, Width = 800, Height = 600 };
        window.Show();
        Pump();
        return editor;
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void SlotsPickFromTheAnimationsOfTheirOgi()
    {
        var ogi = AddOgi("Skeleton", CreateAnimation(0x1, "Walk"), CreateAnimation(0x2, "Run"));
        var (document, slot) = OpenGameObject(new ModelSlot { Ogi = ogi.URI, Animation = 0x1 });

        var editor = Construct<OgiAnimationFieldViewModel>(document, "Root.AssetData.ModelSlots[0].Animation");

        Assert.Equal([ModelSlot.NoAnimation, (ushort)0x1, (ushort)0x2], editor.Animations.Select(animation => animation.Id));
        Assert.Equal("Walk", editor.SelectedAnimation!.Name);
        Assert.True(editor.CanChooseAnimation);

        editor.SelectedAnimation = editor.Animations.Single(animation => animation.Name == "Run");
        Pump();

        Assert.Equal(0x2, slot.Animation);
        Assert.True(document.IsDirty);

        // Undo and redo show the value they put back. The combo box handed its old pick back while its list changed, which became the
        // value again, back and forth until the stack ran out
        document.Undo();
        Pump();
        Assert.Equal(0x1, slot.Animation);
        Assert.Equal("Walk", editor.SelectedAnimation!.Name);
        Assert.Same(document.History.Root, document.History.Current);
        document.Redo();
        Pump();
        Assert.Equal(0x2, slot.Animation);
        Assert.Equal("Run", editor.SelectedAnimation!.Name);
        Assert.False(document.CanRedo);
    }

    [AvaloniaFact]
    public void AnimationsFollowTheSlotsOgi()
    {
        var skeleton = AddOgi("Skeleton", CreateAnimation(0x1, "Walk"));
        var other = AddOgi("Other", CreateAnimation(0x3, "Jump"));
        var (document, _) = OpenGameObject(new ModelSlot { Ogi = skeleton.URI, Animation = 0x1 });
        var editor = Construct<OgiAnimationFieldViewModel>(document, "Root.AssetData.ModelSlots[0].Animation");

        document.PropertyGraph.Find("Root.AssetData.ModelSlots[0].Ogi")!.SetValue(other.URI);
        Pump();

        Assert.Contains(editor.Animations, animation => animation.Name == "Jump");
        // Walk isn't the other model's, it stays picked to be seen until another one gets picked
        Assert.True(editor.SelectedAnimation!.IsMissing);
        Assert.Equal(0x1, editor.SelectedAnimation.Id);
    }

    [AvaloniaFact]
    public void SlotsWithoutAnOgiHaveNoAnimationsToPick()
    {
        var (document, _) = OpenGameObject(new ModelSlot());

        var editor = Construct<OgiAnimationFieldViewModel>(document, "Root.AssetData.ModelSlots[0].Animation");

        Assert.Equal([ModelSlot.NoAnimation], editor.Animations.Select(animation => animation.Id));
        Assert.False(editor.CanChooseAnimation);
    }

    [AvaloniaFact]
    public void OgisPlayTheirAnimations()
    {
        var ogi = AddOgi("Skeleton", CreateAnimation(0x1, "Walk"), CreateAnimation(0x2, "Run"));
        var document = new DocumentViewModel(ogi);
        document.Initialize();

        var player = Construct<OgiAnimationsViewModel>(document, "Root.AssetData.Animations");

        Assert.Equal(["Walk", "Run"], player.Animations.Select(animation => animation.Name));
        player.PlayAnimation();
        Assert.True(player.IsPlaying);
        Assert.Equal("Walk", player.SelectedAnimation!.Name);
        Assert.Equal(Frames - 1, player.LastFrame);

        player.Frame = 2;
        player.StopAnimation();

        Assert.False(player.IsPlaying);
        Assert.Equal(0, player.Frame);
        Assert.False(document.IsDirty);
    }

    // A single frame has nothing to play, playback carried on into the next animation picked only while it did
    [AvaloniaFact]
    public void PlaybackCarriesOnPastAnimationsWithASingleFrame()
    {
        var ogi = AddOgi("Skeleton", CreateAnimation(0x1, "Pose", frames: 1), CreateAnimation(0x2, "Walk"));
        var document = new DocumentViewModel(ogi);
        document.Initialize();
        var player = Construct<OgiAnimationsViewModel>(document, "Root.AssetData.Animations");

        player.SelectedAnimation = player.Animations[0];
        player.PlayAnimation();
        player.Advance();
        Assert.True(player.IsPlaying);

        player.SelectedAnimation = player.Animations[1];
        Pump();
        player.Advance();

        Assert.True(player.IsPlaying);
        player.StopAnimation();
    }

    [AvaloniaFact]
    public void ShowingTheBindPoseStopsPlaying()
    {
        var ogi = AddOgi("Skeleton", CreateAnimation(0x1, "Walk"));
        var document = new DocumentViewModel(ogi);
        document.Initialize();
        var player = Construct<OgiAnimationsViewModel>(document, "Root.AssetData.Animations");
        player.PlayAnimation();

        player.ShowBindPose();

        Assert.False(player.IsPlaying);
        Assert.Equal("Walk", player.SelectedAnimation!.Name);
    }

    // The animations are what an OGI's editor is mostly opened for, an asset's data is laid out with its own properties
    [AvaloniaFact]
    public void OgisShowTheirAnimationsRightAway()
    {
        var ogi = AddOgi("Skeleton", CreateAnimation(0x1, "Walk"));
        var document = new DocumentViewModel(ogi);
        document.Initialize();
        new Window { Content = new DocumentView { DataContext = document }, Width = 800, Height = 600 }.Show();
        Pump();

        Assert.Contains(document.Root.Nodes, node => node is OgiAnimationsViewModel);
    }

    [AvaloniaFact]
    public void TheNoteAboutMissingAnimationsFitsTheInspector()
    {
        var ogi = AddOgi("Skeleton");
        var document = new DocumentViewModel(ogi);
        document.Initialize();
        var window = new Window { Content = new ContentControl { Content = document }, Width = 260, Height = 600 };
        window.Show();
        Pump();

        var note = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "No animations present. Create new ones in Blender");
        var inspector = note.GetVisualAncestors().OfType<ScrollViewer>().Last();

        Assert.True(note.IsEffectivelyVisible);
        Assert.True(note.Bounds.Width <= inspector.Viewport.Width, $"The note is {note.Bounds.Width} wide in a {inspector.Viewport.Width} wide inspector");
        Assert.True(note.Bounds.Height > note.FontSize * 2, "The note didn't wrap");
    }

    // Frames between the game's ones blend the two around them
    [AvaloniaFact]
    public void PosesBetweenFramesAreBlended()
    {
        var animation = CreateAnimation(0x1, "Walk");
        var ogi = ((IAsset)AddOgi("Skeleton", animation)).GetData<OGIData>();

        var pose = OgiAnimationsViewModel.SamplePose(animation, ogi, 1.5);

        Assert.Equal(Joints, pose.Joints.Count);
        Assert.Null(pose.ShapeWeights);
        for (var joint = 0; joint < Joints; joint++)
        {
            var parent = ogi.Joints[joint].ParentIndex;
            var from = animation.GetAnimationSampleForMainAnimation(joint, parent, ogi, 1).Translation.Item2;
            var to = animation.GetAnimationSampleForMainAnimation(joint, parent, ogi, 2).Translation.Item2;
            Assert.Equal((from.X + to.X) / 2, pose.Joints[joint].Translation.x, 4);
            Assert.Equal((from.Z + to.Z) / 2, pose.Joints[joint].Translation.z, 4);
        }
    }
}
