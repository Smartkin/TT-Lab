using GlmSharp;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// What a scenery's bounds are picked and dragged by: a handle on the middle of the box's top, the box itself would take every click inside
/// the level. Its transform is the handle's place scaled by the box's half size, so the scale tool sizes the box
/// </summary>
public sealed class SceneryBoundsHandle(RenderContext context, string name) : EditableObject(context, null, name)
{
    // Half the size of the cube it's picked by
    public const float HandleHalfSize = 2.0f;

    public override mat4 GetBoundsTransform()
    {
        return mat4.Translate(WorldTransform.Column3.xyz) * mat4.Scale(new vec3(HandleHalfSize));
    }
}

/// <summary>
/// The box the game keeps the chunk's objects in, drawn down from its handle
/// </summary>
public sealed class SceneryBoundsVisual : Renderable, IPrimitiveRenderable
{
    // The handle stays this many pixels wide wherever it is
    private const float HandlePixels = 10.0f;

    private static readonly vec4 BoxColor = new(0.3f, 0.85f, 1.0f, 0.45f);
    private static readonly vec4 SelectedColor = new(0.55f, 0.95f, 1.0f, 0.95f);

    private readonly SceneryBoundsHandle _handle;

    public SceneryBoundsVisual(RenderContext context, SceneryBoundsHandle handle) : base(context, "SCENERY_BOUNDS")
    {
        _handle = handle;
        handle.AddChild(this);
    }

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        if (!_handle.IsVisible)
        {
            return;
        }

        var world = _handle.WorldTransform;
        var selected = _handle.IsSelected;
        var color = selected ? SelectedColor : BoxColor;
        // The handle is on the top, a half size above the middle
        renderer.DrawWireBox(world * mat4.Translate(new vec3(0.0f, -1.0f, 0.0f)), color, selected ? 2.5f : 1.5f, PrimitiveLayer.WorldXRay);
        var top = world.Column3.xyz;
        renderer.DrawSphere(top, camera.WorldUnitsPerPixel(top) * HandlePixels * 0.5f, color with { w = 1.0f }, PrimitiveLayer.Overlay);
    }
}

/// <summary>
/// The bounds' handle stands on the middle of the box's top, the Center node is the box's middle
/// </summary>
public sealed class SceneryBoundsTop(PropertyNode halfSize) : IPositionConverter
{
    public vec3 ToPosition(object? data)
    {
        return data is Vector3 center ? new vec3(center.X, center.Y + HalfHeight(), center.Z) : vec3.Zero;
    }

    public object ToData(vec3 position)
    {
        return new Vector3(position.x, position.y - HalfHeight(), position.z);
    }

    private float HalfHeight() => halfSize.GetValue() is Vector3 half ? half.Y : 0.0f;
}
