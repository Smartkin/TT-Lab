using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Rendering.Objects;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;

namespace TT_Lab.ViewModels.Editors.Global;

/// <summary>
/// Plays the save icon's animation on its preview in the document's viewport, like the OGI editor plays its animations. It starts
/// playing by itself, the console's browser plays the icon all along
/// </summary>
public partial class SaveIconAnimationViewModel : DocumentDataViewModel<PS2SaveIcon>
{
    private readonly Stopwatch _clock = new();
    private DispatcherTimer? _timer;
    private bool _hasStarted;
    private bool _resumes;

    [Reactive]
    private double _frame;

    [Reactive]
    private bool _isPlaying;

    [Reactive]
    private bool _loop = true;

    [Reactive]
    private double _speed = 1;

    public SaveIconAnimationViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
    }

    public bool HasAnimation => CurrentValue is { ShapeCount: > 1, FrameLength: > 1 };

    public int LastFrame => Math.Max((int)(CurrentValue?.FrameLength ?? 1) - 1, 0);

    public string FrameText => $"Frame {ShownFrame} of {LastFrame}";

    public string Summary => CurrentValue is { } icon
        ? $"{Count(icon.ShapeCount, "shape")}, {Count(icon.FrameLength, "frame")}" +
          (HasAnimation ? $" played at {SaveIconPreview.FramesPerSecond * icon.AnimationSpeed:0.##} a second" : string.Empty)
        : string.Empty;

    // Why nothing plays
    public string NoAnimationText => CurrentValue switch
    {
        { ShapeCount: <= 1 } => "The icon has one shape, so there's nothing to play (the game's own icon is still). Give it more in Blender: " +
                                "shape keys on its mesh, their values keyed over the frames",
        { FrameLength: <= 1 } => "The icon loops over one frame, so its shapes don't play. Set its Frame Length in Blender's Twin Tech panel",
        _ => string.Empty
    };

    public string WeightsText => CurrentValue is { } icon
        ? "Shape weights: " + string.Join(", ", SaveIconPreview.ShapeWeightsAt(icon, ShownFrame).Select((weight, shape) => $"{shape} {weight:0.##}"))
        : string.Empty;

    // The browser shows whole frames
    private int ShownFrame => (int)Math.Floor(Frame);

    private static string Count(long count, string thing) => count == 1 ? $"1 {thing}" : $"{count} {thing}s";

    public void PlayAnimation()
    {
        if (!HasAnimation)
        {
            return;
        }

        if (!Loop && ShownFrame >= LastFrame)
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

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);

        this.WhenAnyValue(x => x.Frame)
            .Skip(1)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(FrameText));
                this.RaisePropertyChanged(nameof(WeightsText));
                ShowFrame();
            })
            .DisposeWith(disposables);
        if (_resumes || (!_hasStarted && HasAnimation))
        {
            _hasStarted = true;
            PlayAnimation();
        }
    }

    protected override void OnDeactivated(CompositeDisposable disposables)
    {
        _resumes = IsPlaying;
        PauseAnimation();

        base.OnDeactivated(disposables);
    }

    protected override void OnClosed(CompositeDisposable disposables)
    {
        PauseAnimation();
    }

    protected override void OnCurrentValueChanged()
    {
        if (!HasAnimation)
        {
            StopAnimation();
        }

        Frame = Math.Min(Frame, LastFrame);
        this.RaisePropertyChanged(nameof(HasAnimation));
        this.RaisePropertyChanged(nameof(LastFrame));
        this.RaisePropertyChanged(nameof(Summary));
        this.RaisePropertyChanged(nameof(NoAnimationText));
        this.RaisePropertyChanged(nameof(FrameText));
        this.RaisePropertyChanged(nameof(WeightsText));
    }

    private void Advance()
    {
        var elapsed = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        AdvanceBy(elapsed);
    }

    // 60 frames a second times the icon's speed, the panel's speed on top
    internal void AdvanceBy(double seconds)
    {
        if (CurrentValue is not { } icon || !HasAnimation)
        {
            PauseAnimation();
            return;
        }

        var length = (double)icon.FrameLength;
        var frame = Frame + seconds * SaveIconPreview.FramesPerSecond * icon.AnimationSpeed * Speed;
        if (!double.IsFinite(frame))
        {
            frame = 0;
        }

        if (frame >= length || frame < 0)
        {
            if (!Loop)
            {
                Frame = frame < 0 ? 0 : LastFrame;
                PauseAnimation();
                return;
            }

            frame -= Math.Floor(frame / length) * length;
        }

        Frame = frame;
    }

    private void ShowFrame()
    {
        Document.Viewport?.GetViewportObjects().Select(viewportObject => viewportObject.UserData).OfType<SaveIconPreview>().FirstOrDefault()?.ShowFrame(ShownFrame);
    }
}
