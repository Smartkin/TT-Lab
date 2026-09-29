using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace TT_Lab.Controls;

/// <summary>
/// A glyph's box on a font page: its left column, the row under its bottom one (the game's boxes go up from there) and its size in pixels
/// </summary>
public sealed record FontBox(int Index, int Left, int Bottom, int Width, int Height);

/// <summary>
/// A font's page with its glyph boxes: clicking one selects it, dragging moves it, dragging its bottom right corner resizes it. What
/// happens to the boxes is up to whoever handles the drags. The game keeps its pages upside down (a box goes up from its bottom row),
/// the editor shows them the right way up
/// </summary>
public class FontPageEditor : Control
{
    public static readonly StyledProperty<Bitmap?> PageProperty = AvaloniaProperty.Register<FontPageEditor, Bitmap?>(nameof(Page));
    public static readonly StyledProperty<IReadOnlyList<FontBox>?> BoxesProperty = AvaloniaProperty.Register<FontPageEditor, IReadOnlyList<FontBox>?>(nameof(Boxes));
    public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<FontPageEditor, int>(nameof(SelectedIndex), -1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<FontPageEditor, double>(nameof(Zoom), 3.0);

    private const double HandleSize = 6.0;
    private static readonly IBrush Background = new ImmutableSolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22));
    private static readonly IPen BoxPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0xC0, 0x5B, 0x9B, 0xE0)), 1.0);
    private static readonly IPen SelectedPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x30)), 2.0);
    private static readonly IBrush HandleBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x30));

    private int _dragged = -1;
    private bool _resizing;
    private Point _dragStart;
    private FontBox? _dragOrigin;

    static FontPageEditor()
    {
        AffectsRender<FontPageEditor>(PageProperty, BoxesProperty, SelectedIndexProperty, ZoomProperty);
        AffectsMeasure<FontPageEditor>(PageProperty, ZoomProperty);
        FocusableProperty.OverrideDefaultValue<FontPageEditor>(true);
    }

    public Bitmap? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    public IReadOnlyList<FontBox>? Boxes
    {
        get => GetValue(BoxesProperty);
        set => SetValue(BoxesProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>
    /// Screen pixels per page pixel
    /// </summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public event Action<int>? DragStarted;
    /// <summary>
    /// A box being dragged: its index and where it is now, in page pixels
    /// </summary>
    public event Action<FontBox>? BoxDragged;
    public event Action? DragEnded;

    public Point ToScreen(Point page) => new(page.X * Zoom, page.Y * Zoom);

    public Point FromScreen(Point screen) => new(screen.X / Zoom, screen.Y / Zoom);

    public Rect ToScreen(Rect page) => new(ToScreen(page.TopLeft), ToScreen(page.BottomRight));

    private PixelSize PageSize => Page?.PixelSize ?? new PixelSize(64, 64);

    /// <summary>
    /// Where the box is on the page shown the right way up, in page pixels from its top left corner
    /// </summary>
    public Rect GetShownRect(FontBox box) => new(box.Left, PageSize.Height - box.Bottom, box.Width, box.Height);

    protected override Size MeasureOverride(Size availableSize)
    {
        var page = PageSize;
        return new Size(page.Width * Zoom, page.Height * Zoom);
    }

    /// <summary>
    /// The box under a screen position, the selected one first since it may be under the others, and whether its resize handle is
    /// </summary>
    public (int Index, bool OnHandle) HitTest(Point screen)
    {
        var boxes = Boxes ?? [];
        var selected = SelectedIndex;
        if (selected >= 0 && selected < boxes.Count && boxes[selected].Index == selected)
        {
            var hit = HitTestBox(boxes[selected], screen);
            if (hit != null)
            {
                return (selected, hit.Value);
            }
        }

        for (var i = boxes.Count - 1; i >= 0; i--)
        {
            var hit = HitTestBox(boxes[i], screen);
            if (hit != null)
            {
                return (boxes[i].Index, hit.Value);
            }
        }

        return (-1, false);
    }

    private bool? HitTestBox(FontBox box, Point screen)
    {
        var rect = ToScreen(GetShownRect(box));
        var handle = new Rect(rect.Right - HandleSize / 2, rect.Bottom - HandleSize / 2, HandleSize, HandleSize);
        if (handle.Contains(screen))
        {
            return true;
        }

        return rect.Inflate(1.0).Contains(screen) ? false : null;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background, new Rect(Bounds.Size));
        if (Page != null)
        {
            var size = ToScreen(new Rect(0, 0, Page.PixelSize.Width, Page.PixelSize.Height));
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            // Turned the right way up
            using (context.PushTransform(new Matrix(1.0, 0.0, 0.0, -1.0, 0.0, size.Height)))
            {
                context.DrawImage(Page, new Rect(Page.Size), size);
            }
        }

        var boxes = Boxes ?? [];
        FontBox? selectedBox = null;
        foreach (var box in boxes)
        {
            if (box.Index == SelectedIndex)
            {
                selectedBox = box;
                continue;
            }

            context.DrawRectangle(null, BoxPen, ToScreen(GetShownRect(box)));
        }

        if (selectedBox != null)
        {
            var rect = ToScreen(GetShownRect(selectedBox));
            context.DrawRectangle(null, SelectedPen, rect);
            context.FillRectangle(HandleBrush, new Rect(rect.Right - HandleSize / 2, rect.Bottom - HandleSize / 2, HandleSize, HandleSize));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        var (index, onHandle) = HitTest(position);
        SelectedIndex = index;
        Focus();
        if (index < 0)
        {
            e.Handled = true;
            return;
        }

        _dragged = index;
        _resizing = onHandle;
        _dragStart = position;
        _dragOrigin = FindBox(index);
        DragStarted?.Invoke(index);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragged < 0 || _dragOrigin == null)
        {
            return;
        }

        var delta = e.GetPosition(this) - _dragStart;
        var dx = (int)Math.Round(delta.X / Zoom);
        var dy = (int)Math.Round(delta.Y / Zoom);
        // Down on the screen is down the page, towards its bottom row; the corner keeps the box's top where it is
        var box = _resizing
            ? _dragOrigin with { Width = Math.Max(1, _dragOrigin.Width + dx), Height = Math.Max(1, _dragOrigin.Height + dy) }
            : _dragOrigin with { Left = _dragOrigin.Left + dx, Bottom = _dragOrigin.Bottom - dy };
        BoxDragged?.Invoke(box);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragged < 0)
        {
            return;
        }

        _dragged = -1;
        _dragOrigin = null;
        e.Pointer.Capture(null);
        DragEnded?.Invoke();
        e.Handled = true;
    }

    private FontBox? FindBox(int index)
    {
        foreach (var box in Boxes ?? [])
        {
            if (box.Index == index)
            {
                return box;
            }
        }

        return null;
    }
}
