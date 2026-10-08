using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Threading;
using GlmSharp;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData.Code;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors.Code;

/// <summary>
/// Plays the OGI's animations on its model in the document's viewport
/// </summary>
public partial class OgiAnimationsViewModel : DocumentDataViewModel<List<AnimationData>>
{
    private readonly Stopwatch _clock = new();
    private DispatcherTimer? _timer;
    // What the model was last shown in, the bind pose or the frame, which a model made again gets
    private bool _showsBindPose;

    [Reactive]
    private AnimationData? _selectedAnimation;

    [Reactive]
    private double _frame;

    [Reactive]
    private bool _isPlaying;

    [Reactive]
    private bool _loop = true;

    [Reactive]
    private double _speed = 1;

    public OgiAnimationsViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
    }

    public IReadOnlyList<AnimationData> Animations => CurrentValue ?? [];

    // The viewport keeps it: the panel is made again with the inspector, the model's objects when its materials change. Only the OGI's
    // own viewer has it, a chunk's scene would draw every instance's
    public bool ShowsSkeleton
    {
        get => CanShowSkeleton && Document.Viewport!.ShowsSkeletons;
        set
        {
            if (!CanShowSkeleton || Document.Viewport!.ShowsSkeletons == value)
            {
                return;
            }

            Document.Viewport.ShowsSkeletons = value;
            this.RaisePropertyChanged();
        }
    }

    public bool CanShowSkeleton => Document.Viewport != null && Document.DocumentModel is Assets.Code.OGI;

    public bool HasAnimations => Animations.Count > 0;

    public double LastFrame => Math.Max((SelectedAnimation?.TotalFrames ?? 1) - 1, 0);

    // Where the frames' slider ends: looping, past the last frame, which blends into the first on the way there
    public double EndFrame => Loop && LastFrame > 0 ? LastFrame + 1 : LastFrame;

    public string FrameText => SelectedAnimation == null ? string.Empty : $"Frame {(Loop ? Math.Floor(Frame) % (LastFrame + 1) : Math.Floor(Frame))} of {LastFrame}";

    public void PlayAnimation()
    {
        SelectedAnimation ??= Animations.FirstOrDefault();
        if (SelectedAnimation == null)
        {
            return;
        }

        if (!Loop && Frame >= LastFrame)
        {
            Frame = 0;
        }

        _timer ??= new DispatcherTimer(TimeSpan.FromSeconds(1.0 / 60), DispatcherPriority.Render, (_, _) => Advance());
        _clock.Restart();
        _timer.Start();
        IsPlaying = true;
    }

    public void PauseAnimation()
    {
        _timer?.Stop();
        _clock.Reset();
        IsPlaying = false;
    }

    public void StopAnimation()
    {
        PauseAnimation();
        Frame = 0;
    }

    // Playing any animation leaves the model in its pose, this shows how the model is bound until a frame gets picked again
    public void ShowBindPose()
    {
        PauseAnimation();
        ApplyPose(true);
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);

        // The model made again (another material picked for a slot) is at rest
        if (Document.Viewport is { } viewport && Document.DocumentModel is Assets.Code.OGI)
        {
            void PoseAgain(PropertyNode _) => ApplyPose(_showsBindPose);
            viewport.ObjectsRebuilt += PoseAgain;
            Disposable.Create(() => viewport.ObjectsRebuilt -= PoseAgain).DisposeWith(disposables);
            // The viewport takes the switch from the preferences, which another viewer can change
            viewport.WhenAnyValue(x => x.ShowsSkeletons).Skip(1).Subscribe(_ => this.RaisePropertyChanged(nameof(ShowsSkeleton))).DisposeWith(disposables);
        }

        this.WhenAnyValue(x => x.SelectedAnimation)
            .Skip(1)
            .Subscribe(_ =>
            {
                Frame = 0;
                if (SelectedAnimation == null)
                {
                    PauseAnimation();
                }

                this.RaisePropertyChanged(nameof(LastFrame));
                this.RaisePropertyChanged(nameof(EndFrame));
                ApplyPose();
            })
            .DisposeWith(disposables);
        this.WhenAnyValue(x => x.Loop)
            .Skip(1)
            .Subscribe(_ =>
            {
                if (Frame > LastFrame)
                {
                    Frame = LastFrame;
                }

                this.RaisePropertyChanged(nameof(EndFrame));
                this.RaisePropertyChanged(nameof(FrameText));
                ApplyPose();
            })
            .DisposeWith(disposables);
        this.WhenAnyValue(x => x.Frame)
            .Skip(1)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(FrameText));
                ApplyPose();
            })
            .DisposeWith(disposables);
    }

    protected override void OnDeactivated(CompositeDisposable disposables)
    {
        PauseAnimation();

        base.OnDeactivated(disposables);
    }

    protected override void OnClosed(CompositeDisposable disposables)
    {
        PauseAnimation();
    }

    protected override void OnCurrentValueChanged()
    {
        // Loading the OGI's file again replaces the edited animations, the one that was picked stays picked
        var selectedId = SelectedAnimation?.ID;
        this.RaisePropertyChanged(nameof(Animations));
        this.RaisePropertyChanged(nameof(HasAnimations));
        SelectedAnimation = Animations.FirstOrDefault(animation => animation.ID == selectedId);
    }

    internal void Advance()
    {
        var animation = SelectedAnimation;
        if (animation == null)
        {
            PauseAnimation();
            return;
        }

        // A single frame has nothing to play, looping keeps playing so picking another animation carries on playing it
        if (LastFrame == 0)
        {
            if (!Loop)
            {
                PauseAnimation();
            }

            return;
        }

        // The game plays an animation for its frames over its rate (ConstructAnimationSettings): looping, a frame each 1/rate and
        // the last one blending into the first, once, its steps between the first and the last frame over that time (SetAnimationData)
        var elapsed = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        var frames = LastFrame + 1;
        var steps = elapsed * animation.DefaultFPS * Speed;
        if (Loop)
        {
            Frame = (Frame + steps) % frames;
            return;
        }

        var frame = Frame + steps * LastFrame / frames;
        if (frame >= LastFrame)
        {
            Frame = LastFrame;
            PauseAnimation();
            return;
        }

        Frame = frame;
    }

    private void ApplyPose(bool bindPose = false)
    {
        _showsBindPose = bindPose;
        var context = Document.Viewport?.GetRenderContext();
        if (context == null || Property.Target is not OGIData ogi)
        {
            return;
        }

        var animation = SelectedAnimation;
        var pose = animation != null && !bindPose ? SamplePose(animation, ogi, Frame, Loop) : null;
        context.QueueRenderAction(() =>
        {
            var render = Document.Viewport?.GetViewportObjects().Select(viewportObject => viewportObject.UserData).OfType<Rendering.Objects.OGI>().FirstOrDefault();
            if (render == null)
            {
                return;
            }

            render.ResetPose();
            if (pose == null)
            {
                return;
            }

            foreach (var joint in pose.Joints)
            {
                render.SetInheritScaleForJoint(joint.Index, joint.InheritScale);
                render.ApplyTransformToJoint(joint.Index, joint.Translation, joint.Scale, joint.Rotation);
            }

            if (pose.ShapeWeights != null)
            {
                render.ApplyWeightsToBlendSkin(pose.ShapeWeights);
            }
        });
    }

    // Looping, the last frame blends into the first like the game's (SetAnimationData), once, the last frame holds
    internal static Pose SamplePose(AnimationData animation, OGIData ogi, double frame, bool loops = false)
    {
        var lastFrame = Math.Max(animation.TotalFrames - 1, 0);
        if (loops)
        {
            frame %= lastFrame + 1;
        }

        var first = Math.Clamp((int)Math.Floor(frame), 0, lastFrame);
        var second = first < lastFrame ? first + 1 : loops ? 0 : lastFrame;
        var t = (float)Math.Clamp(frame - first, 0, 1);

        var joints = new List<JointPose>();
        var jointCount = Math.Min(animation.MainAnimation.JointSettings.Count, ogi.Joints.Count);
        var mainFrames = animation.MainAnimation.AnimatedTransformations.Count;
        for (var i = 0; i < jointCount; i++)
        {
            var parent = ogi.Joints[i].ParentIndex;
            var from = animation.GetAnimationSampleForMainAnimation(i, parent, ogi, Math.Min(first, Math.Max(mainFrames - 1, 0)));
            var to = animation.GetAnimationSampleForMainAnimation(i, parent, ogi, Math.Min(second, Math.Max(mainFrames - 1, 0)));
            var translation = Vector3.Lerp(from.Translation.Item2, to.Translation.Item2, t);
            var rotation = Quaternion.Slerp(from.Rotation.Item2, to.Rotation.Item2, t);
            var scale = Vector3.Lerp(from.Scale.Item2, to.Scale.Item2, t);
            joints.Add(new JointPose(i, !animation.MainAnimation.JointSettings[i].IndependentScaling,
                new vec3(translation.X, translation.Y, translation.Z),
                new quat(rotation.X, rotation.Y, rotation.Z, rotation.W),
                new vec3(scale.X, scale.Y, scale.Z)));
        }

        Single[]? weights = null;
        if (animation.FacialAnimation.JointSettings.Count > 0)
        {
            var facialFrames = Math.Max(animation.FacialAnimation.AnimatedTransformations.Count - 1, 0);
            var from = animation.GetAnimationSampleForMorphAnimation(Math.Min(first, facialFrames)).Weights;
            var to = animation.GetAnimationSampleForMorphAnimation(Math.Min(second, facialFrames)).Weights;
            weights = from.Select((weight, index) => weight + (to[index] - weight) * t).ToArray();
        }

        return new Pose(joints, weights);
    }

    internal record JointPose(int Index, bool InheritScale, vec3 Translation, quat Rotation, vec3 Scale);

    internal record Pose(List<JointPose> Joints, Single[]? ShapeWeights);
}
