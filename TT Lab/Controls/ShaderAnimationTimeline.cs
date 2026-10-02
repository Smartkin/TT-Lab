using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TT_Lab.Controls;

/// <summary>
/// A track of the timeline: its name, whether it has a key on every frame, its value on every frame and whether it's used (dimmed if not)
/// </summary>
public sealed record TimelineTrack(string Name, bool Animated, IReadOnlyList<float> Values, bool Used = true);

/// <summary>
/// A shader animation's frames: a ruler with the playhead, a lane per track with its keys and the selected track's curve. Clicking the ruler or
/// a lane moves the playhead (and picks the lane's track), dragging the curve's keys changes the values, a static track's whole line at once.
/// Tracks that aren't used are gray and their keys don't drag. What happens to the values is up to whoever handles the requests
/// </summary>
public class ShaderAnimationTimeline : Control
{
    public static readonly StyledProperty<IReadOnlyList<TimelineTrack>?> TracksProperty =
        AvaloniaProperty.Register<ShaderAnimationTimeline, IReadOnlyList<TimelineTrack>?>(nameof(Tracks));

    public static readonly StyledProperty<int> FrameProperty =
        AvaloniaProperty.Register<ShaderAnimationTimeline, int>(nameof(Frame), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> SelectedTrackProperty =
        AvaloniaProperty.Register<ShaderAnimationTimeline, int>(nameof(SelectedTrack), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private const double Gutter = 52.0;
    private const double Inset = 6.0;
    private const double RulerHeight = 18.0;
    private const double LaneHeight = 18.0;
    private const double CurveTop = 8.0;
    private const double HitDistance = 8.0;
    private static readonly IBrush Background = new ImmutableSolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22));
    private static readonly IBrush LaneBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x26, 0x26, 0x2B));
    private static readonly IBrush SelectedLaneBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x33, 0x3A, 0x48));
    private static readonly IPen GridPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3E)), 1.0);
    private static readonly IPen ZeroPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0x55, 0x55, 0x5E)), 1.0);
    private static readonly IPen PlayheadPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x30)), 1.5);
    private static readonly IBrush LabelBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x90, 0x90, 0x98));
    private static readonly IBrush NameBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD6));
    private static readonly IBrush UnusedNameBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x70, 0x70, 0x76));
    private static readonly Color UnusedTrackColor = Color.FromRgb(0x5E, 0x5E, 0x64);
    private static readonly IBrush SelectedKeyBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x30));
    private static readonly Color[] TrackColors =
    [
        Color.FromRgb(0x6C, 0xB4, 0xEE), Color.FromRgb(0x9C, 0xD6, 0x8A), Color.FromRgb(0xE0, 0x5A, 0x5A),
        Color.FromRgb(0x5A, 0xC0, 0x5A), Color.FromRgb(0x5A, 0x7A, 0xE8), Color.FromRgb(0xC8, 0xC8, 0xC8)
    ];

    private (double Min, double Max)? _dragRange;
    private int _draggedFrame = -1;
    private bool _isScrubbing;

    static ShaderAnimationTimeline()
    {
        AffectsRender<ShaderAnimationTimeline>(TracksProperty, FrameProperty, SelectedTrackProperty);
        FocusableProperty.OverrideDefaultValue<ShaderAnimationTimeline>(true);
    }

    public IReadOnlyList<TimelineTrack>? Tracks
    {
        get => GetValue(TracksProperty);
        set => SetValue(TracksProperty, value);
    }

    public int Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public int SelectedTrack
    {
        get => GetValue(SelectedTrackProperty);
        set => SetValue(SelectedTrackProperty, value);
    }

    public event Action<int, int, float>? KeyDragged;
    public event Action? DragStarted;
    public event Action? DragEnded;

    private int FrameCount => Math.Max(1, Tracks?.FirstOrDefault()?.Values.Count ?? 1);

    private TimelineTrack? Selected => Tracks is { } tracks && SelectedTrack >= 0 && SelectedTrack < tracks.Count ? tracks[SelectedTrack] : null;

    private double LanesBottom => RulerHeight + LaneHeight * (Tracks?.Count ?? 0);

    private double CellWidth => Math.Max(Bounds.Width - Gutter - Inset, 1.0) / FrameCount;

    public double FrameX(int frame) => Gutter + (frame + 0.5) * CellWidth;

    public int FrameAt(double x) => Math.Clamp((int)Math.Floor((x - Gutter) / CellWidth), 0, FrameCount - 1);

    private Rect CurveRect => new(Gutter, LanesBottom + CurveTop, Math.Max(Bounds.Width - Gutter - Inset, 1.0), Math.Max(Bounds.Height - LanesBottom - CurveTop - Inset, 1.0));

    /// <summary>
    /// The selected track's values from the bottom of the curve to its top. It stays the same while dragging, following the key would move
    /// the curve under the mouse
    /// </summary>
    public (double Min, double Max) GetValueRange()
    {
        if (_dragRange.HasValue)
        {
            return _dragRange.Value;
        }

        var values = Selected?.Values ?? [];
        var min = values.Count == 0 ? 0.0 : Math.Min(0.0, values.Min());
        var max = values.Count == 0 ? 1.0 : Math.Max(1.0, values.Max());
        var padding = (max - min) * 0.1;
        return (min - padding, max + padding);
    }

    public double ValueY(double value)
    {
        var (min, max) = GetValueRange();
        var curve = CurveRect;
        return curve.Bottom - (value - min) / (max - min) * curve.Height;
    }

    public double ValueAt(double y)
    {
        var (min, max) = GetValueRange();
        var curve = CurveRect;
        return min + (curve.Bottom - y) / curve.Height * (max - min);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Background, bounds, 3.0f);
        var tracks = Tracks ?? [];
        var frames = FrameCount;

        // Ruler: a tick every frame, numbered from 1 like the editor's frame label, numbers as far apart as they fit
        var labelEvery = Math.Max(1, (int)Math.Ceiling(28.0 / CellWidth));
        for (var frame = 0; frame < frames; frame++)
        {
            var x = FrameX(frame);
            context.DrawLine(GridPen, new Point(x, RulerHeight - 4.0), new Point(x, RulerHeight));
            if (frame == 0 || (frame + 1) % labelEvery == 0)
            {
                DrawText(context, (frame + 1).ToString(CultureInfo.InvariantCulture), new Point(x - 4.0, 1.0), LabelBrush);
            }
        }

        DrawText(context, "Frame", new Point(Inset, 1.0), LabelBrush);
        for (var track = 0; track < tracks.Count; track++)
        {
            var top = RulerHeight + track * LaneHeight;
            var lane = new Rect(0.0, top, bounds.Width, LaneHeight - 1.0);
            context.FillRectangle(track == SelectedTrack ? SelectedLaneBrush : LaneBrush, lane);
            DrawText(context, tracks[track].Name, new Point(Inset, top + 2.0), tracks[track].Used ? NameBrush : UnusedNameBrush);
            var used = tracks[track].Used;
            var brush = new ImmutableSolidColorBrush(used ? TrackColors[track % TrackColors.Length] : UnusedTrackColor);
            var middle = top + LaneHeight / 2.0;
            if (!tracks[track].Animated)
            {
                var value = tracks[track].Values.Count > 0 ? tracks[track].Values[0] : 0.0f;
                var label = Text($"static {Format(value)}", used ? LabelBrush : UnusedNameBrush);
                context.DrawText(label, new Point(Gutter + 4.0, top + 2.0));
                var barStart = Gutter + 4.0 + label.Width + 6.0;
                context.FillRectangle(brush, new Rect(barStart, middle - 1.0, Math.Max(Gutter + CurveRect.Width - barStart, 0.0), 2.0));
                continue;
            }

            for (var frame = 0; frame < frames; frame++)
            {
                DrawDiamond(context, used && frame == Frame && track == SelectedTrack ? SelectedKeyBrush : brush, new Point(FrameX(frame), middle), 4.0);
            }
        }

        DrawCurve(context);
        var playhead = FrameX(Math.Clamp(Frame, 0, frames - 1));
        context.DrawLine(PlayheadPen, new Point(playhead, 0.0), new Point(playhead, bounds.Height - Inset));
    }

    private void DrawCurve(DrawingContext context)
    {
        var curve = CurveRect;
        context.DrawRectangle(null, GridPen, curve);
        var selected = Selected;
        if (selected == null || selected.Values.Count == 0)
        {
            return;
        }

        var (min, max) = GetValueRange();
        if (min < 0.0 && max > 0.0)
        {
            var zero = ValueY(0.0);
            context.DrawLine(ZeroPen, new Point(curve.Left, zero), new Point(curve.Right, zero));
        }

        DrawText(context, Format(max), new Point(Inset, curve.Top), LabelBrush);
        DrawText(context, Format(min), new Point(Inset, curve.Bottom - 14.0), LabelBrush);
        var color = selected.Used ? TrackColors[SelectedTrack % TrackColors.Length] : UnusedTrackColor;
        var keyHighlight = selected.Used ? SelectedKeyBrush : new ImmutableSolidColorBrush(UnusedTrackColor);
        var pen = new ImmutablePen(new ImmutableSolidColorBrush(color), 2.0);
        var keyBrush = new ImmutableSolidColorBrush(color);
        if (!selected.Animated)
        {
            var y = ValueY(selected.Values[0]);
            context.DrawLine(pen, new Point(curve.Left, y), new Point(curve.Right, y));
            context.DrawEllipse(keyHighlight, null, new Point(FrameX(Math.Clamp(Frame, 0, FrameCount - 1)), y), 4.5, 4.5);
            return;
        }

        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(new Point(FrameX(0), ValueY(selected.Values[0])), false);
            for (var frame = 1; frame < selected.Values.Count; frame++)
            {
                geometryContext.LineTo(new Point(FrameX(frame), ValueY(selected.Values[frame])));
            }

            geometryContext.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
        for (var frame = 0; frame < selected.Values.Count; frame++)
        {
            context.DrawEllipse(frame == Frame ? keyHighlight : keyBrush, null, new Point(FrameX(frame), ValueY(selected.Values[frame])), 4.0, 4.0);
        }
    }

    private static void DrawDiamond(DrawingContext context, IBrush brush, Point center, double size)
    {
        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(new Point(center.X, center.Y - size), true);
            geometryContext.LineTo(new Point(center.X + size, center.Y));
            geometryContext.LineTo(new Point(center.X, center.Y + size));
            geometryContext.LineTo(new Point(center.X - size, center.Y));
            geometryContext.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geometry);
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.CurrentCulture);

    private static FormattedText Text(string text, IBrush brush) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10.0, brush);

    private static void DrawText(DrawingContext context, string text, Point at, IBrush brush) => context.DrawText(Text(text, brush), at);

    // The key of the selected track under the point: an animated track's key at its frame, a static track's line anywhere along it. An unused
    // track's don't drag
    private int HitTestKey(Point position)
    {
        var selected = Selected;
        if (selected == null || !selected.Used || selected.Values.Count == 0 || !CurveRect.Inflate(HitDistance).Contains(position))
        {
            return -1;
        }

        if (!selected.Animated)
        {
            return Math.Abs(position.Y - ValueY(selected.Values[0])) <= HitDistance ? Math.Clamp(Frame, 0, FrameCount - 1) : -1;
        }

        var frame = FrameAt(position.X);
        var key = new Point(FrameX(frame), ValueY(selected.Values[frame]));
        return Point.Distance(key, position) <= HitDistance ? frame : -1;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Tracks == null)
        {
            return;
        }

        var position = e.GetPosition(this);
        var key = HitTestKey(position);
        if (key != -1)
        {
            Frame = key;
            _draggedFrame = key;
            _dragRange = GetValueRange();
            DragStarted?.Invoke();
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (position.Y < LanesBottom)
        {
            if (position.Y >= RulerHeight)
            {
                SelectedTrack = Math.Clamp((int)((position.Y - RulerHeight) / LaneHeight), 0, Tracks.Count - 1);
            }

            Frame = FrameAt(position.X);
            _isScrubbing = true;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        Frame = FrameAt(position.X);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (_isScrubbing)
        {
            Frame = FrameAt(position.X);
            return;
        }

        if (_draggedFrame == -1)
        {
            return;
        }

        var (min, max) = GetValueRange();
        KeyDragged?.Invoke(SelectedTrack, _draggedFrame, (float)Math.Clamp(ValueAt(position.Y), min, max));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_draggedFrame != -1)
        {
            DragEnded?.Invoke();
        }

        _draggedFrame = -1;
        _isScrubbing = false;
        _dragRange = null;
        InvalidateVisual();
    }

    private void EndDrag(IPointer pointer)
    {
        _isScrubbing = false;
        if (_draggedFrame == -1)
        {
            pointer.Capture(null);
            return;
        }

        _draggedFrame = -1;
        _dragRange = null;
        pointer.Capture(null);
        DragEnded?.Invoke();
        InvalidateVisual();
    }
}
