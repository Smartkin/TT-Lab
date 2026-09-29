using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TT_Lab.Controls;

/// <summary>
/// A sound's waveform with its loop shaded between two handles, the samples after the loop (never played) dimmed and where playback is.
/// Dragging a handle moves that end of the loop, clicking elsewhere moves playback there
/// </summary>
public class SoundWaveform : Control
{
    public static readonly StyledProperty<short[]?> SamplesProperty = AvaloniaProperty.Register<SoundWaveform, short[]?>(nameof(Samples));
    public static readonly StyledProperty<int> LoopStartProperty = AvaloniaProperty.Register<SoundWaveform, int>(nameof(LoopStart), -1);
    public static readonly StyledProperty<int> LoopEndProperty = AvaloniaProperty.Register<SoundWaveform, int>(nameof(LoopEnd), -1);
    public static readonly StyledProperty<double> PlayheadProperty = AvaloniaProperty.Register<SoundWaveform, double>(nameof(Playhead));

    private const double HandleReach = 6;

    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x22, 0x28));
    private static readonly IBrush Wave = new SolidColorBrush(Color.FromRgb(0x7C, 0xB4, 0xFF));
    private static readonly IBrush LoopFill = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xC8, 0x3C));
    private static readonly IBrush NeverPlayed = new SolidColorBrush(Color.FromArgb(0xA0, 0x10, 0x10, 0x10));
    private static readonly IPen LoopHandle = new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x3C)), 2);
    private static readonly IPen Playback = new Pen(Brushes.White, 1);
    private static readonly IPen Center = new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x40, 0x4A)), 1);

    private enum Drag
    {
        None,
        LoopStart,
        LoopEnd
    }

    private Drag _drag;
    private Cursor? _resizeCursor;
    // The lowest and highest sample under each pixel column, worked out again when the samples or the width change
    private float[] _minimums = [];
    private float[] _maximums = [];

    static SoundWaveform()
    {
        AffectsRender<SoundWaveform>(SamplesProperty, LoopStartProperty, LoopEndProperty, PlayheadProperty);
    }

    public short[]? Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    /// <summary>
    /// The loop's first sample, -1 without a loop
    /// </summary>
    public int LoopStart
    {
        get => GetValue(LoopStartProperty);
        set => SetValue(LoopStartProperty, value);
    }

    /// <summary>
    /// The sample after the loop's last one, -1 without a loop
    /// </summary>
    public int LoopEnd
    {
        get => GetValue(LoopEndProperty);
        set => SetValue(LoopEndProperty, value);
    }

    /// <summary>
    /// The sample playback is at
    /// </summary>
    public double Playhead
    {
        get => GetValue(PlayheadProperty);
        set => SetValue(PlayheadProperty, value);
    }

    public event Action? LoopDragStarted;
    public event Action<int>? LoopStartDragged;
    public event Action<int>? LoopEndDragged;
    public event Action? LoopDragEnded;
    public event Action<int>? Seeked;

    private bool HasLoop => LoopStart >= 0 && LoopEnd > LoopStart;

    private int SampleCount => Samples?.Length ?? 0;

    private double ToX(double sample) => SampleCount == 0 ? 0 : sample / SampleCount * Bounds.Width;

    private int ToSample(double x) => SampleCount == 0 ? 0 : (int)Math.Clamp(Math.Round(x / Bounds.Width * SampleCount), 0, SampleCount);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SamplesProperty || change.Property == BoundsProperty)
        {
            BuildColumns();
        }
    }

    private void BuildColumns()
    {
        var width = Math.Max(1, (int)Bounds.Width);
        _minimums = new float[width];
        _maximums = new float[width];
        var samples = Samples;
        if (samples == null || samples.Length == 0)
        {
            return;
        }

        for (var column = 0; column < width; column++)
        {
            var from = (int)((long)column * samples.Length / width);
            var to = Math.Max(from + 1, (int)((long)(column + 1) * samples.Length / width));
            short min = 0;
            short max = 0;
            for (var i = from; i < Math.Min(to, samples.Length); i++)
            {
                min = Math.Min(min, samples[i]);
                max = Math.Max(max, samples[i]);
            }

            _minimums[column] = min / 32768f;
            _maximums[column] = max / 32767f;
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Background, bounds);
        var middle = bounds.Height / 2;
        context.DrawLine(Center, new Point(0, middle), new Point(bounds.Width, middle));
        for (var column = 0; column < _minimums.Length; column++)
        {
            var top = middle - _maximums[column] * (middle - 2);
            var bottom = middle - _minimums[column] * (middle - 2);
            context.FillRectangle(Wave, new Rect(column, top, 1, Math.Max(1, bottom - top)));
        }

        if (HasLoop)
        {
            var start = ToX(LoopStart);
            var end = ToX(LoopEnd);
            context.FillRectangle(LoopFill, new Rect(start, 0, Math.Max(1, end - start), bounds.Height));
            if (end < bounds.Width)
            {
                context.FillRectangle(NeverPlayed, new Rect(end, 0, bounds.Width - end, bounds.Height));
            }

            DrawHandle(context, start, true);
            DrawHandle(context, end, false);
        }

        var playhead = ToX(Playhead);
        context.DrawLine(Playback, new Point(playhead, 0), new Point(playhead, bounds.Height));
    }

    // A line with a flag pointing into the loop
    private void DrawHandle(DrawingContext context, double x, bool start)
    {
        context.DrawLine(LoopHandle, new Point(x, 0), new Point(x, Bounds.Height));
        var direction = start ? 1 : -1;
        var flag = new StreamGeometry();
        using (var geometry = flag.Open())
        {
            geometry.BeginFigure(new Point(x, 0), true);
            geometry.LineTo(new Point(x + direction * 9, 0));
            geometry.LineTo(new Point(x, 9));
            geometry.EndFigure(true);
        }

        context.DrawGeometry(LoopHandle.Brush, null, flag);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || SampleCount == 0)
        {
            return;
        }

        var x = e.GetPosition(this).X;
        _drag = Drag.None;
        if (HasLoop)
        {
            var toStart = Math.Abs(x - ToX(LoopStart));
            var toEnd = Math.Abs(x - ToX(LoopEnd));
            if (Math.Min(toStart, toEnd) <= HandleReach)
            {
                _drag = toStart <= toEnd ? Drag.LoopStart : Drag.LoopEnd;
            }
        }

        if (_drag == Drag.None)
        {
            Seeked?.Invoke(ToSample(x));
            return;
        }

        e.Pointer.Capture(this);
        LoopDragStarted?.Invoke();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var x = e.GetPosition(this).X;
        if (_drag == Drag.None)
        {
            Cursor = HasLoop && (Math.Abs(x - ToX(LoopStart)) <= HandleReach || Math.Abs(x - ToX(LoopEnd)) <= HandleReach)
                ? _resizeCursor ??= new Cursor(StandardCursorType.SizeWestEast)
                : Cursor.Default;
            return;
        }

        if (_drag == Drag.LoopStart)
        {
            LoopStartDragged?.Invoke(ToSample(x));
        }
        else
        {
            LoopEndDragged?.Invoke(ToSample(x));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag == Drag.None)
        {
            return;
        }

        _drag = Drag.None;
        e.Pointer.Capture(null);
        LoopDragEnded?.Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_drag == Drag.None)
        {
            return;
        }

        _drag = Drag.None;
        LoopDragEnded?.Invoke();
    }
}
