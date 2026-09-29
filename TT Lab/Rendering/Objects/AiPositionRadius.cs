using GlmSharp;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// An AI position's radius: a ring on the ground around the position, the whole sphere while the position or its handle is selected,
/// and the handle on the ring's +X side it's dragged by
/// </summary>
public sealed class AiPositionRadiusVisual : Renderable, IPrimitiveRenderable
{
    // The handle stays this many pixels wide wherever it is
    private const float HandlePixels = 7.0f;

    private static readonly vec4 RingColor = new(0.35f, 0.6f, 1.0f, 0.6f);
    private static readonly vec4 SelectedColor = new(0.55f, 0.8f, 1.0f, 0.95f);
    private static readonly vec4 HandleSelectedColor = new(1.0f, 0.85f, 0.3f, 1.0f);

    private readonly EditableObject _position;
    private readonly EditableObject _handle;
    private volatile float _radius;

    public AiPositionRadiusVisual(RenderContext context, EditableObject position, EditableObject handle, float radius) : base(context, "AI_POSITION_RADIUS")
    {
        _position = position;
        _handle = handle;
        _radius = radius;
        position.AddChild(this);
    }

    /// <summary>
    /// Set from the UI thread, the render thread draws what it last got
    /// </summary>
    public float Radius
    {
        get => _radius;
        set => _radius = value;
    }

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        var center = WorldTransform.Column3.xyz;
        var radius = _radius;
        var selected = _position.IsSelected || _handle.IsSelected;
        var color = selected ? SelectedColor : RingColor;
        if (selected)
        {
            renderer.DrawWireSphere(center, radius, color, 2.0f, PrimitiveLayer.WorldXRay);
        }
        else
        {
            renderer.DrawCircle(center, vec3.UnitY, radius, color, 1.5f, PrimitiveLayer.WorldXRay);
        }

        var handle = HandlePosition(center, radius);
        renderer.DrawLine(center, handle, color with { w = color.w * 0.6f }, 1.0f, PrimitiveLayer.WorldXRay);
        renderer.DrawSphere(handle, camera.WorldUnitsPerPixel(handle) * HandlePixels * 0.5f, _handle.IsSelected ? HandleSelectedColor : color, PrimitiveLayer.Overlay);
    }

    /// <summary>
    /// Where the handle of a radius around a point is
    /// </summary>
    public static vec3 HandlePosition(vec3 center, float radius) => center + vec3.UnitX * radius;
}
