using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using TT_Lab.AssetData.Instance.Particle;

namespace TT_Lab.Controls;

// A particle texture page with the picture a particle system takes from it: dragging its corners resizes the rectangle, dragging it
// moves it, dragging anywhere else draws a new one and clicking one of the game's pictures, outlined, takes that one. What happens to
// the rectangle is up to whoever handles the events
public class ParticleTexturePage : Control
{
    public static readonly StyledProperty<Bitmap?> PageProperty = AvaloniaProperty.Register<ParticleTexturePage, Bitmap?>(nameof(Page));
    public static readonly StyledProperty<ParticleTextureRect> RectProperty = AvaloniaProperty.Register<ParticleTexturePage, ParticleTextureRect>(nameof(Rect));
    public static readonly StyledProperty<IReadOnlyList<ParticleSprite>?> SpritesProperty = AvaloniaProperty.Register<ParticleTexturePage, IReadOnlyList<ParticleSprite>?>(nameof(Sprites));
    public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<ParticleTexturePage, double>(nameof(Zoom), 2.0);

    private const double HandleSize = 7.0;
    private const double CornerReach = 5.0;
    private static readonly IBrush Background = new ImmutableSolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22));
    private static readonly IBrush CheckerBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x30));
    private static readonly IPen SpritePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0x70, 0xE0, 0xE0, 0xE0)), 1.0, new ImmutableDashStyle([3, 3], 0));
    private static readonly IPen HoveredPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0xE0, 0x5B, 0x9B, 0xE0)), 1.5);
    private static readonly IPen RectPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x30)), 2.0);
    private static readonly IBrush HandleBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x30));

    private enum DragKind
    {
        None,
        Move,
        Corner,
        Draw
    }

    private DragKind _drag;
    private Point _dragStart;
    private ParticleTextureRect _dragOrigin;
    // The corner staying where it is while another one is dragged, in page pixels
    private (int X, int Y) _anchor;
    private ParticleSprite? _hovered;

    static ParticleTexturePage()
    {
        AffectsRender<ParticleTexturePage>(PageProperty, RectProperty, SpritesProperty, ZoomProperty);
        AffectsMeasure<ParticleTexturePage>(PageProperty, ZoomProperty);
        FocusableProperty.OverrideDefaultValue<ParticleTexturePage>(true);
    }

    public Bitmap? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    public ParticleTextureRect Rect
    {
        get => GetValue(RectProperty);
        set => SetValue(RectProperty, value);
    }

    // The game's pictures on this page
    public IReadOnlyList<ParticleSprite>? Sprites
    {
        get => GetValue(SpritesProperty);
        set => SetValue(SpritesProperty, value);
    }

    // Screen pixels per page pixel
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public event Action? DragStarted;
    public event Action<ParticleTextureRect>? RectDragged;
    public event Action? DragEnded;
    public event Action<ParticleSprite>? SpritePicked;
    public event Action<ParticleSprite?>? HoveredSpriteChanged;

    private PixelSize PageSize => Page?.PixelSize ?? new PixelSize(128, 128);

    public Rect ToScreen(ParticleTextureRect rect) => new(rect.X * Zoom, rect.Y * Zoom, rect.Width * Zoom, rect.Height * Zoom);

    public Point FromScreen(Point screen) => new(screen.X / Zoom, screen.Y / Zoom);

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(PageSize.Width * Zoom, PageSize.Height * Zoom);
    }

    public override void Render(DrawingContext context)
    {
        var size = new Rect(0, 0, PageSize.Width * Zoom, PageSize.Height * Zoom);
        context.FillRectangle(Background, size);
        // Transparent parts of the page show a checkerboard
        var cell = 8.0 * Zoom;
        for (var y = 0.0; y < size.Height; y += cell)
        {
            for (var x = ((int)(y / cell) % 2) * cell; x < size.Width; x += cell * 2)
            {
                context.FillRectangle(CheckerBrush, new Rect(x, y, Math.Min(cell, size.Width - x), Math.Min(cell, size.Height - y)));
            }
        }

        if (Page != null)
        {
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            {
                context.DrawImage(Page, new Rect(Page.Size), size);
            }
        }

        foreach (var sprite in Sprites ?? [])
        {
            context.DrawRectangle(null, sprite == _hovered ? HoveredPen : SpritePen, ToScreen(sprite.Rect));
        }

        var rect = ToScreen(Rect);
        context.DrawRectangle(null, RectPen, rect);
        foreach (var corner in Corners(rect))
        {
            context.FillRectangle(HandleBrush, HandleAt(corner));
        }
    }

    private static Point[] Corners(Rect rect) => [rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft];

    private static Rect HandleAt(Point corner) => new(corner.X - HandleSize / 2, corner.Y - HandleSize / 2, HandleSize, HandleSize);

    private ParticleSprite? SpriteAt(Point screen)
    {
        var page = FromScreen(screen);
        ParticleSprite? found = null;
        foreach (var sprite in Sprites ?? [])
        {
            // The smallest one under the pointer, pictures sit inside the boxes of bigger ones
            if (sprite.Rect.Contains(page.X, page.Y) && (found == null || sprite.Rect.Width * sprite.Rect.Height < found.Rect.Width * found.Rect.Height))
            {
                found = sprite;
            }
        }

        return found;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        var position = e.GetPosition(this);
        var rect = ToScreen(Rect);
        var corners = Corners(rect);
        // Only right at a corner, the game's pictures are small and the rectangle has to stay easy to grab in the middle
        var corner = Array.FindIndex(corners, point => Point.Distance(point, position) <= CornerReach);
        _dragStart = position;
        _dragOrigin = Rect;
        if (corner >= 0)
        {
            // The opposite corner stays
            var opposite = corners[(corner + 2) % 4];
            _anchor = ((int)Math.Round(opposite.X / Zoom), (int)Math.Round(opposite.Y / Zoom));
            _drag = DragKind.Corner;
        }
        else if (rect.Contains(position))
        {
            _drag = DragKind.Move;
        }
        else if (SpriteAt(position) is { } sprite)
        {
            SpritePicked?.Invoke(sprite);
            e.Handled = true;
            return;
        }
        else
        {
            var page = FromScreen(position);
            _anchor = ((int)Math.Floor(page.X), (int)Math.Floor(page.Y));
            _drag = DragKind.Draw;
        }

        DragStarted?.Invoke();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (_drag == DragKind.None)
        {
            var hovered = ToScreen(Rect).Contains(position) ? null : SpriteAt(position);
            if (hovered != _hovered)
            {
                _hovered = hovered;
                HoveredSpriteChanged?.Invoke(hovered);
                InvalidateVisual();
            }

            return;
        }

        var page = FromScreen(position);
        var pageSize = PageSize;
        ParticleTextureRect rect;
        if (_drag == DragKind.Move)
        {
            var delta = position - _dragStart;
            rect = (_dragOrigin with { X = _dragOrigin.X + (int)Math.Round(delta.X / Zoom), Y = _dragOrigin.Y + (int)Math.Round(delta.Y / Zoom) })
                .Within(pageSize.Width, pageSize.Height);
        }
        else
        {
            // From the corner that stays to the one under the pointer, whichever way round they are
            var x = Math.Clamp((int)Math.Round(page.X), 0, pageSize.Width);
            var y = Math.Clamp((int)Math.Round(page.Y), 0, pageSize.Height);
            rect = _dragOrigin with
            {
                X = Math.Min(_anchor.X, x),
                Y = Math.Min(_anchor.Y, y),
                Width = Math.Max(1, Math.Abs(x - _anchor.X)),
                Height = Math.Max(1, Math.Abs(y - _anchor.Y)),
            };
            rect = rect.Within(pageSize.Width, pageSize.Height);
        }

        RectDragged?.Invoke(rect);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_drag != DragKind.None)
        {
            _drag = DragKind.None;
            DragEnded?.Invoke();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hovered != null && _drag == DragKind.None)
        {
            _hovered = null;
            HoveredSpriteChanged?.Invoke(null);
            InvalidateVisual();
        }
    }

    private void EndDrag(IPointer pointer)
    {
        if (_drag == DragKind.None)
        {
            return;
        }

        _drag = DragKind.None;
        pointer.Capture(null);
        DragEnded?.Invoke();
    }
}
