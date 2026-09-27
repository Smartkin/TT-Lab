using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace TT_Lab.Controls;

// Scroll viewers that also scroll sideways measure what's in them without a limit on the width, text in them never wraps and wide
// rows push out a horizontal scrollbar. This measures its content with the width the outermost of them shows instead
public class FitToScrollViewport : Decorator
{
    private ScrollViewer? _viewer;
    private double _measuredLeft;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewer = this.GetVisualAncestors().OfType<ScrollViewer>().LastOrDefault(viewer => viewer.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled);
        if (_viewer == null)
        {
            return;
        }

        _viewer.PropertyChanged += ViewerPropertyChanged;
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewer != null)
        {
            _viewer.PropertyChanged -= ViewerPropertyChanged;
            LayoutUpdated -= OnLayoutUpdated;
            _viewer = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsInfinity(availableSize.Width) && _viewer is { Viewport.Width: > 0 })
        {
            _measuredLeft = GetLeft(_viewer);
            availableSize = availableSize.WithWidth(Math.Max(_viewer.Viewport.Width - _measuredLeft, 0));
        }

        return base.MeasureOverride(availableSize);
    }

    // Where it sits in the scrolled content is only known once it's arranged, the measure before that had to guess
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_viewer != null && Math.Abs(GetLeft(_viewer) - _measuredLeft) > 0.5)
        {
            InvalidateMeasure();
        }
    }

    private double GetLeft(ScrollViewer viewer)
    {
        return Math.Max((this.TranslatePoint(default, viewer)?.X ?? 0) + viewer.Offset.X, 0);
    }

    private void ViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty)
        {
            InvalidateMeasure();
        }
    }
}
