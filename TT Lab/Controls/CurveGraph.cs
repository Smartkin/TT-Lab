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
/// A curve's keys over time from 0 to 1: dragging moves a key, double clicking adds one and right clicking removes one. What happens to the
/// keys is up to whoever handles the requests
/// </summary>
public class CurveGraph : Control
{
    public static readonly StyledProperty<IReadOnlyList<Point>?> KeysProperty =
        AvaloniaProperty.Register<CurveGraph, IReadOnlyList<Point>?>(nameof(Keys));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<CurveGraph, int>(nameof(SelectedIndex), -1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private const double Margin = 8.0;
    private const double HitDistance = 8.0;
    private static readonly IBrush Background = new ImmutableSolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22));
    private static readonly IPen GridPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3E)), 1.0);
    private static readonly IPen ZeroPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0x55, 0x55, 0x5E)), 1.0);
    private static readonly IPen CurvePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0x5B, 0x9B, 0xE0)), 2.0);
    private static readonly IBrush KeyBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
    private static readonly IBrush SelectedKeyBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x30));
    private static readonly IBrush LabelBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x90, 0x90, 0x98));

    private (double Min, double Max)? _dragRange;
    private int _dragged = -1;

    static CurveGraph()
    {
        AffectsRender<CurveGraph>(KeysProperty, SelectedIndexProperty);
        FocusableProperty.OverrideDefaultValue<CurveGraph>(true);
    }

    public IReadOnlyList<Point>? Keys
    {
        get => GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public event Action<int, Point>? KeyDragged;
    public event Action<Point>? KeyAddRequested;
    public event Action<int>? KeyRemoveRequested;

    /// <summary>
    /// Values shown from the bottom to the top. It stays the same while dragging, following the key would move the graph under the mouse
    /// </summary>
    public (double Min, double Max) GetValueRange()
    {
        if (_dragRange.HasValue)
        {
            return _dragRange.Value;
        }

        var keys = Keys ?? [];
        var min = keys.Count == 0 ? 0.0 : Math.Min(0.0, keys.Min(key => key.Y));
        var max = keys.Count == 0 ? 1.0 : Math.Max(0.0, keys.Max(key => key.Y));
        if (max - min < 1e-6)
        {
            max = min + 1.0;
        }

        var padding = (max - min) * 0.1;
        return (min - padding, max + padding);
    }

    public Point ToScreen(Point key)
    {
        var (min, max) = GetValueRange();
        var width = Math.Max(Bounds.Width - Margin * 2.0, 1.0);
        var height = Math.Max(Bounds.Height - Margin * 2.0, 1.0);
        return new Point(Margin + key.X * width, Margin + (1.0 - (key.Y - min) / (max - min)) * height);
    }

    public Point FromScreen(Point position)
    {
        var (min, max) = GetValueRange();
        var width = Math.Max(Bounds.Width - Margin * 2.0, 1.0);
        var height = Math.Max(Bounds.Height - Margin * 2.0, 1.0);
        return new Point(Math.Clamp((position.X - Margin) / width, 0.0, 1.0), min + (1.0 - (position.Y - Margin) / height) * (max - min));
    }

    public int HitTestKey(Point position)
    {
        var keys = Keys ?? [];
        var hit = -1;
        var closest = HitDistance;
        for (var i = 0; i < keys.Count; i++)
        {
            var distance = Point.Distance(ToScreen(keys[i]), position);
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
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Background, bounds, 3.0f);
        for (var quarter = 0; quarter <= 4; quarter++)
        {
            var x = ToScreen(new Point(quarter / 4.0, 0.0)).X;
            context.DrawLine(GridPen, new Point(x, Margin), new Point(x, bounds.Height - Margin));
        }

        var (min, max) = GetValueRange();
        if (min < 0.0 && max > 0.0)
        {
            var zero = ToScreen(new Point(0.0, 0.0)).Y;
            context.DrawLine(ZeroPen, new Point(Margin, zero), new Point(bounds.Width - Margin, zero));
        }

        DrawLabel(context, FormatValue(max), new Point(Margin + 2.0, Margin));
        DrawLabel(context, FormatValue(min), new Point(Margin + 2.0, bounds.Height - Margin - 14.0));

        var keys = Keys ?? [];
        if (keys.Count == 0)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(ToScreen(keys[0]), false);
            foreach (var key in keys.Skip(1))
            {
                geometryContext.LineTo(ToScreen(key));
            }

            geometryContext.EndFigure(false);
        }

        context.DrawGeometry(null, CurvePen, geometry);
        for (var i = 0; i < keys.Count; i++)
        {
            context.DrawEllipse(i == SelectedIndex ? SelectedKeyBrush : KeyBrush, null, ToScreen(keys[i]), 4.0, 4.0);
        }
    }

    private static string FormatValue(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private static void DrawLabel(DrawingContext context, string text, Point at)
    {
        context.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10.0, LabelBrush), at);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var position = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        var hit = HitTestKey(position);
        if (properties.IsRightButtonPressed)
        {
            if (hit != -1)
            {
                KeyRemoveRequested?.Invoke(hit);
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
                KeyAddRequested?.Invoke(FromScreen(position));
            }

            e.Handled = true;
            return;
        }

        SelectedIndex = hit;
        _dragRange = GetValueRange();
        _dragged = hit;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragged == -1)
        {
            return;
        }

        KeyDragged?.Invoke(_dragged, FromScreen(e.GetPosition(this)));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragged = -1;
        _dragRange = null;
        InvalidateVisual();
    }

    private void EndDrag(IPointer pointer)
    {
        if (_dragged == -1)
        {
            return;
        }

        _dragged = -1;
        _dragRange = null;
        pointer.Capture(null);
        InvalidateVisual();
    }
}
