using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TT_Lab.Controls;

public record GradientBarStop(double Time, Color Color);

/// <summary>
/// A gradient's colors over time from 0 to 1 with a marker for each stop: dragging a marker moves it, double clicking the bar adds a stop
/// and right clicking a marker removes it. What happens to the stops is up to whoever handles the requests
/// </summary>
public class GradientBar : Control
{
    public static readonly StyledProperty<IReadOnlyList<GradientBarStop>?> StopsProperty =
        AvaloniaProperty.Register<GradientBar, IReadOnlyList<GradientBarStop>?>(nameof(Stops));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<GradientBar, int>(nameof(SelectedIndex), -1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private const double Margin = 8.0;
    private const double MarkerHeight = 12.0;
    private const double MarkerHalfWidth = 6.0;
    private static readonly IBrush Background = new ImmutableSolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22));
    private static readonly IPen MarkerPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), 1.0);
    private static readonly IPen SelectedMarkerPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x30)), 2.0);
    private static readonly IPen BarPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0x55, 0x55, 0x5E)), 1.0);

    private int _dragged = -1;

    static GradientBar()
    {
        AffectsRender<GradientBar>(StopsProperty, SelectedIndexProperty);
        FocusableProperty.OverrideDefaultValue<GradientBar>(true);
    }

    public IReadOnlyList<GradientBarStop>? Stops
    {
        get => GetValue(StopsProperty);
        set => SetValue(StopsProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public event Action<int, double>? StopDragged;
    public event Action<double>? StopAddRequested;
    public event Action<int>? StopRemoveRequested;
    public event Action? DragStarted;
    public event Action? DragEnded;

    private Rect BarRect => new(Margin, Margin, Math.Max(Bounds.Width - Margin * 2.0, 1.0), Math.Max(Bounds.Height - Margin * 2.0 - MarkerHeight, 1.0));

    public double ToScreen(double time) => BarRect.X + time * BarRect.Width;

    public double FromScreen(double x) => Math.Clamp((x - BarRect.X) / BarRect.Width, 0.0, 1.0);

    public int HitTestStop(Point position)
    {
        var stops = Stops ?? [];
        var hit = -1;
        var closest = MarkerHalfWidth + 2.0;
        for (var i = 0; i < stops.Count; i++)
        {
            var distance = Math.Abs(ToScreen(stops[i].Time) - position.X);
            if (distance <= closest)
            {
                closest = distance;
                hit = i;
            }
        }

        return hit;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background, new Rect(Bounds.Size), 3.0f);
        var stops = Stops ?? [];
        var bar = BarRect;
        if (stops.Count > 0)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1.0, 0.5, RelativeUnit.Relative)
            };
            foreach (var stop in stops)
            {
                brush.GradientStops.Add(new GradientStop(stop.Color, Math.Clamp(stop.Time, 0.0, 1.0)));
            }

            context.FillRectangle(brush, bar);
        }

        context.DrawRectangle(BarPen, bar);
        for (var i = 0; i < stops.Count; i++)
        {
            var x = ToScreen(stops[i].Time);
            var top = bar.Bottom + 2.0;
            var marker = new StreamGeometry();
            using (var geometry = marker.Open())
            {
                geometry.BeginFigure(new Point(x, top), true);
                geometry.LineTo(new Point(x + MarkerHalfWidth, top + MarkerHeight - 2.0));
                geometry.LineTo(new Point(x - MarkerHalfWidth, top + MarkerHeight - 2.0));
                geometry.EndFigure(true);
            }

            context.DrawGeometry(new ImmutableSolidColorBrush(stops[i].Color), i == SelectedIndex ? SelectedMarkerPen : MarkerPen, marker);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var position = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        var hit = HitTestStop(position);
        if (properties.IsRightButtonPressed)
        {
            if (hit != -1)
            {
                StopRemoveRequested?.Invoke(hit);
            }

            e.Handled = true;
            return;
        }

        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        if (hit == -1)
        {
            if (e.ClickCount == 2)
            {
                StopAddRequested?.Invoke(FromScreen(position.X));
            }

            e.Handled = true;
            return;
        }

        SelectedIndex = hit;
        _dragged = hit;
        DragStarted?.Invoke();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragged != -1)
        {
            StopDragged?.Invoke(_dragged, FromScreen(e.GetPosition(this).X));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragged == -1)
        {
            return;
        }

        _dragged = -1;
        e.Pointer.Capture(null);
        DragEnded?.Invoke();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragged != -1)
        {
            DragEnded?.Invoke();
        }

        _dragged = -1;
    }
}
