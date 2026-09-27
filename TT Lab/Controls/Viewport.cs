using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering;
using Point = Avalonia.Point;

namespace TT_Lab.Controls;

/// <summary>
/// Shows the frames of a <see cref="ViewportHost"/> and passes it the input while it's attached
/// </summary>
public class Viewport : Control, ICustomHitTest
{
    public static readonly StyledProperty<ViewportHost?> HostProperty =
        AvaloniaProperty.Register<Viewport, ViewportHost?>(nameof(Host));

    private readonly IBrush _noRenderColor = new ImmutableSolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 255));
    private bool _isAttached;

    public Viewport()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        SizeChanged += OnSizeChanged;
        Focusable = true;
    }

    public ViewportHost? Host
    {
        get => GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        Host?.Attach(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        Host?.Detach(this);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != HostProperty)
        {
            return;
        }

        // Controls get reused for other editors' viewports
        change.GetOldValue<ViewportHost?>()?.Detach(this);
        if (_isAttached)
        {
            change.GetNewValue<ViewportHost?>()?.Attach(this);
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (Host?.Presenter == this)
        {
            Host.Resize(e.NewSize);
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Design.IsDesignMode)
        {
            context.DrawText(new FormattedText("Rendering is not supported in design mode", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, _noRenderColor),
                new Point(5, 5));
            return;
        }

        Host?.Draw(context, this);
    }

    public Boolean HitTest(Point point)
    {
        return Bounds.Contains(point);
    }
}
