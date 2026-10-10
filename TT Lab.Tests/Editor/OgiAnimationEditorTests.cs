using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Code;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.Views.Editors;
using TT_Lab.Views.Editors.Code;
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

    // The dropdown's search shows the animations with every word typed in their name or ID (hex, 0x or not) and highlights the first,
    // which Enter picks; opened, it shows them all with the slot's highlighted
    [AvaloniaFact]
    public void TheSearchShowsTheAnimationsWithTheWordsTyped()
    {
        var ogi = AddOgi("Skeleton", CreateAnimation(0x1, "Walk"), CreateAnimation(0x2, "Run"), CreateAnimation(0x1A, "Run fast"));
        var (document, slot) = OpenGameObject(new ModelSlot { Ogi = ogi.URI, Animation = 0x1 });
        var editor = Construct<OgiAnimationFieldViewModel>(document, "Root.AssetData.ModelSlots[0].Animation");
        string[] Shown() => editor.ShownAnimations.Select(animation => animation.Name).ToArray();

        editor.StartSearch();
        Assert.Equal(["None", "Walk", "Run", "Run fast"], Shown());
        Assert.Equal("Walk", editor.Highlighted!.Name);

        editor.Search = "RUN";
        Assert.Equal(["Run", "Run fast"], Shown());
        Assert.Equal("Run", editor.Highlighted!.Name);
        editor.Search = "fast run";
        Assert.Equal(["Run fast"], Shown());
        editor.Search = "0x1a";
        Assert.Equal(["Run fast"], Shown());
        editor.Search = "1";
        Assert.Equal(["Walk", "Run fast"], Shown());
        editor.Search = "jump";
        Assert.Empty(Shown());
        Assert.Null(editor.Highlighted);
        Assert.False(editor.PickHighlighted());
        Assert.False(document.IsDirty);

        editor.Search = "run";
        editor.MoveHighlight(1);
        editor.MoveHighlight(1);
        Assert.Equal("Run fast", editor.Highlighted!.Name);
        editor.MoveHighlight(-1);
        Assert.Equal("Run", editor.Highlighted!.Name);
        editor.MoveHighlight(1);
        Assert.True(editor.PickHighlighted());
        Pump();

        Assert.Equal(0x1A, slot.Animation);
        Assert.Equal("Run fast", editor.SelectedAnimation!.Name);
        document.Undo();
        Pump();
        Assert.Equal(0x1, slot.Animation);
        Assert.Same(document.History.Root, document.History.Current);
    }

    // Clicking the slot's combo box or pressing Enter on it opens the search below it, never its own list: typing goes into the search,
    // the arrows move through what it shows, Enter or a click picks and Escape leaves the slot as it was
    [AvaloniaFact]
    public void TheDropdownSearchesAndPicksWithTheKeyboardAndMouse()
    {
        var ogi = AddOgi("Skeleton", CreateAnimation(0x1, "Walk"), CreateAnimation(0x2, "Run"), CreateAnimation(0x1A, "Run fast"));
        var (document, slot) = OpenGameObject(new ModelSlot { Ogi = ogi.URI, Animation = 0x1 });
        var editor = Assert.IsType<OgiAnimationFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.ModelSlots[0].Animation")!).Construct());
        var window = new Window { Content = new ContentControl { Content = editor }, Width = 400, Height = 500 };
        window.Show();
        Render();
        var view = window.GetVisualDescendants().OfType<OgiAnimationFieldView>().Single();
        var combo = view.AnimationChoices;

        Click(window, combo.TranslatePoint(new Point(combo.Bounds.Width / 2, combo.Bounds.Height / 2), window)!.Value);

        Assert.True(view.SearchFlyout.IsOpen);
        Assert.False(combo.IsDropDownOpen);
        Assert.True(view.SearchBox.IsFocused);
        Assert.True(view.SearchPanel.Bounds.Width >= combo.Bounds.Width);
        foreach (var character in "run")
        {
            window.KeyTextInput(character.ToString());
        }

        Pump();
        Assert.Equal(["Run", "Run fast"], editor.ShownAnimations.Select(animation => animation.Name));
        Assert.Equal("Run", ((OgiAnimationChoice)view.SearchResults.SelectedItem!).Name);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal("Run fast", ((OgiAnimationChoice)view.SearchResults.SelectedItem!).Name);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();

        Assert.Equal(0x1A, slot.Animation);
        Assert.False(view.SearchFlyout.IsOpen);
        Assert.True(combo.IsFocused);
        Assert.Equal("Run fast", ((OgiAnimationChoice)combo.SelectedItem!).Name);

        // Enter on the focused combo box opens it again with everything shown, Escape changes nothing
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        Assert.True(view.SearchFlyout.IsOpen);
        Assert.False(combo.IsDropDownOpen);
        Assert.Equal(4, editor.ShownAnimations.Count);
        Assert.Equal("Run fast", ((OgiAnimationChoice)view.SearchResults.SelectedItem!).Name);
        window.KeyTextInput("w");
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();
        Assert.False(view.SearchFlyout.IsOpen);
        Assert.Equal(0x1A, slot.Animation);

        // A click on a row picks it
        window.KeyPressQwerty(PhysicalKey.F4, RawInputModifiers.None);
        Render();
        Assert.True(view.SearchFlyout.IsOpen);
        var row = view.SearchResults.GetVisualDescendants().OfType<ListBoxItem>().Single(item => ((OgiAnimationChoice)item.DataContext!).Name == "Walk");
        var popup = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(row));
        Click(popup, row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), popup)!.Value);

        Assert.Equal(0x1, slot.Animation);
        Assert.False(view.SearchFlyout.IsOpen);
        Assert.False(combo.IsDropDownOpen);
    }

    // Clicks are hit tested by what the compositor got last (see PrefabDragAndSearchTests)
    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(TopLevel topLevel, Point point)
    {
        Render();
        topLevel.MouseDown(point, MouseButton.Left);
        topLevel.MouseUp(point, MouseButton.Left);
        Render();
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

    private static (DocumentViewModel Document, ViewportViewModel Viewport) OpenViewer(IAsset asset)
    {
        var viewport = new ViewportViewModel();
        var document = new DocumentViewModel(asset, viewport);
        document.Initialize();
        viewport.Init(document);
        return (document, viewport);
    }

    // The skeleton's switch is a preference, the viewer's viewport keeps it while the panel is made again with the inspector and the
    // model's objects with its materials: the OGI viewers open follow it and the ones opened later start with it. A document without a
    // viewport of its own has none, nor does a viewer of something else (a chunk's scene would draw every instance's)
    [AvaloniaFact]
    public void TheSkeletonIsShownByAPreferenceOfTheViewers()
    {
        var before = Preferences.GetPreference<bool>(Preferences.OgiViewerSkeleton);
        Preferences.SetPreference(Preferences.OgiViewerSkeleton, false);
        try
        {
            var ogi = AddOgi("Skeleton", CreateAnimation(0x1, "Walk"));
            var (document, viewport) = OpenViewer(ogi);
            var (_, otherViewer) = OpenViewer(AddOgi("Other"));
            var player = Assert.IsType<OgiAnimationsViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.Animations")!).Construct());
            var window = new Window { Content = new ContentControl { Content = player }, Width = 800, Height = 600 };
            window.Show();
            Pump();
            var box = window.GetVisualDescendants().OfType<CheckBox>().Single(check => Equals(check.Content, "Skeleton"));
            Assert.True(box.IsVisible);
            Assert.False(box.IsChecked);

            box.IsChecked = true;
            Pump();

            Assert.True(viewport.ShowsSkeletons);
            Assert.True(player.ShowsSkeleton);
            Assert.True(Preferences.GetPreference<bool>(Preferences.OgiViewerSkeleton));
            Assert.True(otherViewer.ShowsSkeletons);
            Assert.True(Construct<OgiAnimationsViewModel>(document, "Root.AssetData.Animations").ShowsSkeleton);
            Assert.True(OpenViewer(AddOgi("Later")).Viewport.ShowsSkeletons);
            Assert.False(document.IsDirty);

            // Switched off in another viewer, this one's box follows
            otherViewer.ShowsSkeletons = false;
            Pump();
            Assert.False(viewport.ShowsSkeletons);
            Assert.False(box.IsChecked);
            window.Close();

            Preferences.SetPreference(Preferences.OgiViewerSkeleton, true);
            var (objectDocument, _) = OpenGameObject(new ModelSlot());
            Assert.False(OpenViewer((IAsset)objectDocument.DocumentModel).Viewport.ShowsSkeletons);

            var withoutViewport = new DocumentViewModel(ogi);
            withoutViewport.Initialize();
            var other = Construct<OgiAnimationsViewModel>(withoutViewport, "Root.AssetData.Animations");
            Assert.False(other.CanShowSkeleton);
            other.ShowsSkeleton = true;
            Assert.False(other.ShowsSkeleton);
        }
        finally
        {
            Preferences.SetPreference(Preferences.OgiViewerSkeleton, before);
        }
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

    // The game's looping playback blends the last frame into the first (SetAnimationData), played once the last frame holds
    [AvaloniaFact]
    public void LoopingBlendsTheLastFrameIntoTheFirst()
    {
        var animation = CreateAnimation(0x1, "Walk");
        var ogi = ((IAsset)AddOgi("Skeleton", animation)).GetData<OGIData>();

        var looping = OgiAnimationsViewModel.SamplePose(animation, ogi, Frames - 0.5, loops: true);
        var once = OgiAnimationsViewModel.SamplePose(animation, ogi, Frames - 0.5);

        for (var joint = 0; joint < Joints; joint++)
        {
            var parent = ogi.Joints[joint].ParentIndex;
            var last = animation.GetAnimationSampleForMainAnimation(joint, parent, ogi, Frames - 1).Translation.Item2;
            var first = animation.GetAnimationSampleForMainAnimation(joint, parent, ogi, 0).Translation.Item2;
            Assert.Equal((last.X + first.X) / 2, looping.Joints[joint].Translation.x, 4);
            Assert.Equal(last.X, once.Joints[joint].Translation.x, 4);
        }

        Assert.Equal(OgiAnimationsViewModel.SamplePose(animation, ogi, 0).Joints[0].Translation,
            OgiAnimationsViewModel.SamplePose(animation, ogi, Frames, loops: true).Joints[0].Translation);
    }

    // Playing on from the last frame went straight back to the first, and the frames' slider ended at the last one
    [AvaloniaFact]
    public void LoopingPlaysOnPastTheLastFrame()
    {
        var ogi = AddOgi("Skeleton", CreateAnimation(0x1, "Walk"));
        var document = new DocumentViewModel(ogi);
        document.Initialize();
        var player = Construct<OgiAnimationsViewModel>(document, "Root.AssetData.Animations");
        player.SelectedAnimation = player.Animations[0];
        Pump();
        Assert.Equal(Frames, player.EndFrame);

        player.Frame = player.LastFrame;
        player.PlayAnimation();
        player.Advance();

        Assert.True(player.Frame >= player.LastFrame, $"Playing went back to frame {player.Frame}");
        player.Frame = Frames - 0.5;
        Pump();
        Assert.Equal(Frames - 0.5, player.Frame);
        player.Loop = false;
        Pump();

        Assert.Equal(player.LastFrame, player.EndFrame);
        Assert.Equal(player.LastFrame, player.Frame);
        player.StopAnimation();
    }
}
