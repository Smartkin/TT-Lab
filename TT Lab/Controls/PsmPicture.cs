using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using TT_Lab.AssetData.Global;

namespace TT_Lab.Controls;

/// <summary>
/// A PSM's picture with its parts outlined, clicking one picks it
/// </summary>
public class PsmPicture : Control
{
    public static readonly StyledProperty<Bitmap?> PictureProperty = AvaloniaProperty.Register<PsmPicture, Bitmap?>(nameof(Picture));
    public static readonly StyledProperty<IReadOnlyList<PsmPartPlace>?> PartsProperty = AvaloniaProperty.Register<PsmPicture, IReadOnlyList<PsmPartPlace>?>(nameof(Parts));
    public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<PsmPicture, int>(nameof(SelectedIndex), -1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<PsmPicture, double>(nameof(Zoom), 1.0);

    private static readonly IBrush Background = new ImmutableSolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22));
    private static readonly IPen PartPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0x70, 0x5B, 0x9B, 0xE0)), 1.0);
    private static readonly IPen SelectedPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x30)), 2.0);

    static PsmPicture()
    {
        AffectsRender<PsmPicture>(PictureProperty, PartsProperty, SelectedIndexProperty, ZoomProperty);
        AffectsMeasure<PsmPicture>(PictureProperty, ZoomProperty);
    }

    public Bitmap? Picture
    {
        get => GetValue(PictureProperty);
        set => SetValue(PictureProperty, value);
    }

    public IReadOnlyList<PsmPartPlace>? Parts
    {
        get => GetValue(PartsProperty);
        set => SetValue(PartsProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>
    /// Screen pixels per picture pixel
    /// </summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    private Rect ToScreen(PsmPartPlace place) => new(place.X * Zoom, place.Y * Zoom, place.Width * Zoom, place.Height * Zoom);

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Picture?.PixelSize ?? new PixelSize(64, 64);
        return new Size(size.Width * Zoom, size.Height * Zoom);
    }

    /// <summary>
    /// The part under a point of the control, -1 for none
    /// </summary>
    public int PartAt(Point point)
    {
        var parts = Parts ?? [];
        for (var i = parts.Count - 1; i >= 0; i--)
        {
            if (ToScreen(parts[i]).Contains(point))
            {
                return parts[i].Index;
            }
        }

        return -1;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background, new Rect(Bounds.Size));
        if (Picture != null)
        {
            var size = new Rect(0, 0, Picture.PixelSize.Width * Zoom, Picture.PixelSize.Height * Zoom);
            // Pixels stay sharp zoomed in
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = Zoom >= 1.0 ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality }))
            {
                context.DrawImage(Picture, new Rect(Picture.Size), size);
            }
        }

        PsmPartPlace? selected = null;
        foreach (var place in Parts ?? [])
        {
            if (place.Index == SelectedIndex)
            {
                selected = place;
                continue;
            }

            context.DrawRectangle(null, PartPen, ToScreen(place).Deflate(0.5));
        }

        if (selected != null)
        {
            context.DrawRectangle(null, SelectedPen, ToScreen(selected).Deflate(1.0));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        SelectedIndex = PartAt(e.GetPosition(this));
        e.Handled = true;
    }
}
