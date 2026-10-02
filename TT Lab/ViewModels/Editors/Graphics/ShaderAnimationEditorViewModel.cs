using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Controls;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TwinShader = Twinsanity.TwinsanityInterchange.Common.TwinShader;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;

namespace TT_Lab.ViewModels.Editors.Graphics;

/// <summary>
/// A track of the animation at the playhead: whether it has a key on every frame and its value there, typed in
/// </summary>
public sealed class ShaderAnimationTrackRow(ShaderAnimationEditorViewModel owner, int index, string name) : ReactiveObject
{
    private bool _isAnimated;
    private string _valueText = string.Empty;

    public int Index => index;
    public string Name => name;

    public bool IsAnimated
    {
        get => _isAnimated;
        set
        {
            if (_isAnimated == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _isAnimated, value);
            if (!owner.IsShowing)
            {
                owner.SetAnimated(index, value);
            }
        }
    }

    public string ValueText
    {
        get => _valueText;
        set
        {
            if (_valueText == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _valueText, value);
            if (!owner.IsShowing)
            {
                owner.TypeValue(index, value);
            }
        }
    }

    /// <summary>
    /// Whether the shader takes the track: U and V with their scroll settings from the animation, the color with 'animation drives color'
    /// </summary>
    public bool IsUsed => owner.IsTrackUsed(index);

    /// <summary>
    /// What turns the track on, while the shader doesn't take it and its row is grayed out
    /// </summary>
    public string? UnusedHint => IsUsed ? null : ShaderAnimationEditorViewModel.UnusedHint(index);

    internal void Show(bool animated, string valueText)
    {
        this.RaiseAndSetIfChanged(ref _isAnimated, animated, nameof(IsAnimated));
        this.RaiseAndSetIfChanged(ref _valueText, valueText, nameof(ValueText));
        this.RaisePropertyChanged(nameof(IsUsed));
        this.RaisePropertyChanged(nameof(UnusedHint));
    }
}

/// <summary>
/// Edits a shader's animation on a timeline: its frames' keys per track, which the game plays at its frames per second and the viewport's
/// preview plays as well. Tracks the shader doesn't take are grayed out and left as they are. Every edit replaces the shader's animation
/// with an edited copy (<see cref="ShaderAnimationTracks"/>), so one undo takes a drag or a typed value back
/// </summary>
public partial class ShaderAnimationEditorViewModel : DocumentDataViewModel<TwinShaderAnimation>
{
    [Reactive]
    private int _frame;

    [Reactive]
    private int _selectedTrack;

    [Reactive]
    private string _framesPerSecondText = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<TimelineTrack> _tracks = [];

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _hasAnimation;

    [Reactive(SetModifier = AccessModifier.Private)]
    private int _frameCount;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _frameLabel = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private IBrush _swatch = Brushes.White;

    private IDisposable? _drag;

    public ShaderAnimationEditorViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        Rows = Enumerable.Range(0, ShaderAnimationTracks.Count).Select(track => new ShaderAnimationTrackRow(this, track, ShaderAnimationTracks.Names[track])).ToList();
        AddAnimationCommand = ReactiveCommand.Create(() => Write(ShaderAnimationTracks.Create(), true));
        RemoveAnimationCommand = ReactiveCommand.Create(() => Write(null, true));
        InsertFrameCommand = ReactiveCommand.Create(InsertFrame);
        RemoveFrameCommand = ReactiveCommand.Create(RemoveFrame);
        PreviousFrameCommand = ReactiveCommand.Create(() => StepFrame(-1));
        NextFrameCommand = ReactiveCommand.Create(() => StepFrame(1));
        Refresh();
    }

    public IReadOnlyList<ShaderAnimationTrackRow> Rows { get; }
    public ReactiveCommand<Unit, Unit> AddAnimationCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveAnimationCommand { get; }
    public ReactiveCommand<Unit, Unit> InsertFrameCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveFrameCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousFrameCommand { get; }
    public ReactiveCommand<Unit, Unit> NextFrameCommand { get; }

    public string TimelineHint => "Click the ruler or a lane to move the playhead, drag the selected track's keys up and down. Animated tracks have a value on every frame, " +
                                  "static ones one value. Gray tracks aren't taken by the shader, tick them under 'The shader takes' to edit them. The game loops the " +
                                  "frames at the frames per second, the viewport's preview plays them";

    internal bool IsShowing { get; private set; }

    /// <summary>
    /// The shader takes the U track as its U offset: its X scroll settings are FromAnimation
    /// </summary>
    public bool MovesU
    {
        get => ShaderNode(nameof(LabShader.XScrollSettings))?.GetValue() is TwinShader.XScrollFormula.FromAnimation;
        set => SetShaderValue(nameof(LabShader.XScrollSettings), MovesU, value, value ? TwinShader.XScrollFormula.FromAnimation : TwinShader.XScrollFormula.Disabled);
    }

    /// <summary>
    /// The shader takes the V track as its V offset: its Y scroll settings are FromAnimation
    /// </summary>
    public bool MovesV
    {
        get => ShaderNode(nameof(LabShader.YScrollSettings))?.GetValue() is TwinShader.YScrollFormula.FromAnimation;
        set => SetShaderValue(nameof(LabShader.YScrollSettings), MovesV, value, value ? TwinShader.YScrollFormula.FromAnimation : TwinShader.YScrollFormula.Disabled);
    }

    /// <summary>
    /// The shader takes the color tracks: 'animation drives color'
    /// </summary>
    public bool DrivesColor
    {
        get => ShaderNode(nameof(LabShader.AnimationDrivesColor))?.GetValue() is true;
        set => SetShaderValue(nameof(LabShader.AnimationDrivesColor), DrivesColor, value, value);
    }

    internal bool IsTrackUsed(int track) => track switch
    {
        0 => MovesU,
        1 => MovesV,
        _ => DrivesColor
    };

    internal static string UnusedHint(int track) => track switch
    {
        0 => "The shader doesn't take U: tick U under 'The shader takes' (X Scroll Settings set to FromAnimation) to edit it",
        1 => "The shader doesn't take V: tick V under 'The shader takes' (Y Scroll Settings set to FromAnimation) to edit it",
        _ => "The shader doesn't take the color: tick Color under 'The shader takes' ('Animation drives color') to edit it"
    };

    // The swatch is grayed out with the color tracks
    public double SwatchOpacity => DrivesColor ? 1.0 : 0.35;

    private PropertyNode? ShaderNode(string name) => Property.Parent?.FindChild($".{name}");

    // One step of its own, like making a track animated
    private void SetShaderValue(string name, bool current, bool wanted, object value)
    {
        if (current == wanted || ShaderNode(name) is not { } node)
        {
            return;
        }

        Document.History.CloseStep();
        node.SetValue(value);
        Document.History.CloseStep();
        ShowShaderUse();
    }

    private void ShowShaderUse()
    {
        this.RaisePropertyChanged(nameof(MovesU));
        this.RaisePropertyChanged(nameof(MovesV));
        this.RaisePropertyChanged(nameof(DrivesColor));
        this.RaisePropertyChanged(nameof(SwatchOpacity));
        Refresh();
    }

    private void OnGraphChanged(PropertyChange change)
    {
        if (change.Node.Parent == Property.Parent && change.Node.Name is nameof(LabShader.XScrollSettings) or nameof(LabShader.YScrollSettings) or nameof(LabShader.AnimationDrivesColor))
        {
            ShowShaderUse();
        }
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        Document.PropertyGraph.Changed += OnGraphChanged;
        Disposable.Create(() => Document.PropertyGraph.Changed -= OnGraphChanged).DisposeWith(disposables);
        this.WhenAnyValue(x => x.Frame, x => x.SelectedTrack).Skip(1).Subscribe(_ =>
        {
            // Typing at another frame or track is a step of its own
            Document.History.CloseStep();
            ShowFrame();
        }).DisposeWith(disposables);
        this.WhenAnyValue(x => x.FramesPerSecondText).Skip(1).Where(_ => !IsShowing).Subscribe(text =>
        {
            if (CurrentValue != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var rate) && rate != CurrentValue.FramesPerSecond)
            {
                Write(ShaderAnimationTracks.SetFramesPerSecond(CurrentValue, rate), false);
            }
        }).DisposeWith(disposables);
        Refresh();
    }

    protected override void OnCurrentValueChanged()
    {
        base.OnCurrentValueChanged();
        Refresh();
    }

    internal void SetAnimated(int track, bool animated)
    {
        if (CurrentValue != null)
        {
            Write(ShaderAnimationTracks.SetAnimated(CurrentValue, track, animated, Frame), true);
        }
    }

    internal void TypeValue(int track, string text)
    {
        if (CurrentValue != null && TryParse(text, out var value))
        {
            Write(ShaderAnimationTracks.SetValue(CurrentValue, track, Frame, value), false);
        }
    }

    /// <summary>
    /// Makes everything a drag of a key changes one step, apart from what was typed before it: both change the same animation
    /// </summary>
    public void BeginDrag()
    {
        _drag?.Dispose();
        Document.History.CloseStep();
        _drag = Document.History.BeginGroup("Moved an animation key");
    }

    public void EndDrag()
    {
        _drag?.Dispose();
        _drag = null;
        Document.History.CloseStep();
    }

    public void DragKey(int track, int frame, float value)
    {
        if (CurrentValue != null)
        {
            Write(ShaderAnimationTracks.SetValue(CurrentValue, track, frame, value), false);
        }
    }

    private void InsertFrame()
    {
        if (CurrentValue == null)
        {
            return;
        }

        Write(ShaderAnimationTracks.InsertFrame(CurrentValue, Frame), true);
        Frame = Math.Min(Frame + 1, FrameCount - 1);
    }

    private void RemoveFrame()
    {
        if (CurrentValue == null || !ShaderAnimationTracks.CanRemoveFrame(CurrentValue))
        {
            return;
        }

        Write(ShaderAnimationTracks.RemoveFrame(CurrentValue, Frame), true);
    }

    private void StepFrame(int by)
    {
        if (FrameCount > 0)
        {
            Frame = ((Frame + by) % FrameCount + FrameCount) % FrameCount;
        }
    }

    // Steps that change what the animation is made of (a track's kind, frames, the animation itself) are steps of their own, typed and
    // dragged values go on changing the step they're in
    private void Write(TwinShaderAnimation? animation, bool ownStep)
    {
        if (ownStep)
        {
            Document.History.CloseStep();
        }

        SetCurrentValue(animation);
        if (ownStep)
        {
            Document.History.CloseStep();
        }

        Refresh();
    }

    private void Refresh()
    {
        var animation = CurrentValue;
        HasAnimation = animation != null;
        FrameCount = animation == null ? 0 : Math.Max(1, ShaderAnimationTracks.FrameCount(animation));
        if (Frame >= FrameCount)
        {
            Frame = Math.Max(FrameCount - 1, 0);
        }

        if (animation == null)
        {
            Tracks = [];
            return;
        }

        Tracks = Enumerable.Range(0, ShaderAnimationTracks.Count)
            .Select(track => new TimelineTrack(ShaderAnimationTracks.Names[track], ShaderAnimationTracks.IsAnimated(animation, track),
                Enumerable.Range(0, FrameCount).Select(frame => ShaderAnimationTracks.ValueAt(animation, track, frame)).ToArray(), IsTrackUsed(track)))
            .ToList();
        IsShowing = true;
        try
        {
            if (!int.TryParse(FramesPerSecondText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var typed) || typed != animation.FramesPerSecond)
            {
                FramesPerSecondText = animation.FramesPerSecond.ToString(CultureInfo.CurrentCulture);
            }
        }
        finally
        {
            IsShowing = false;
        }

        ShowFrame();
    }

    private void ShowFrame()
    {
        var animation = CurrentValue;
        if (animation == null)
        {
            return;
        }

        FrameLabel = $"Frame {Frame + 1} of {FrameCount}";
        IsShowing = true;
        try
        {
            foreach (var row in Rows)
            {
                var value = ShaderAnimationTracks.ValueAt(animation, row.Index, Frame);
                // Keeps what's being typed while it still means the value, "0." doesn't turn into "0" under the caret
                var text = TryParse(row.ValueText, out var typed) && ShaderAnimationTracks.ToRaw(typed) == ShaderAnimationTracks.ToRaw(value) ? row.ValueText : Format(value);
                row.Show(ShaderAnimationTracks.IsAnimated(animation, row.Index), text);
            }
        }
        finally
        {
            IsShowing = false;
        }

        Swatch = new ImmutableSolidColorBrush(Color.FromArgb(ToByte(ShaderAnimationTracks.ValueAt(animation, 5, Frame)), ToByte(ShaderAnimationTracks.ValueAt(animation, 2, Frame)),
            ToByte(ShaderAnimationTracks.ValueAt(animation, 3, Frame)), ToByte(ShaderAnimationTracks.ValueAt(animation, 4, Frame))));
    }

    private static byte ToByte(float value) => (byte)Math.Clamp(MathF.Round(value * 255.0f), 0.0f, 255.0f);

    private static string Format(float value) => value.ToString("0.####", CultureInfo.CurrentCulture);

    private static bool TryParse(string? text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && float.IsFinite(value);
    }
}
