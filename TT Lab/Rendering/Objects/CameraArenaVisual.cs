using System;
using System.Linq;
using GlmSharp;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// The cylinder a boss camera stands on around its arena's axis: a circle at the arena's floor and at the height it follows the
/// target up to, drawn in the arena's space
/// </summary>
public sealed class CameraArenaVisual : Renderable, IPrimitiveRenderable
{
    private const int Segments = 48;

    private readonly EditableObject _owner;
    private readonly vec4 _color;
    private volatile vec3[] _floor = [];
    private volatile vec3[] _top = [];

    public CameraArenaVisual(RenderContext context, EditableObject owner, vec4 color, float radius, float height) : base(context, "CAMERA_ARENA")
    {
        _owner = owner;
        _color = color;
        SetShape(radius, height);
        owner.AddChild(this);
    }

    public void SetShape(float radius, float height)
    {
        var floor = new vec3[Segments + 1];
        var top = new vec3[Segments + 1];
        for (var i = 0; i <= Segments; i++)
        {
            var angle = i * 2.0f * MathF.PI / Segments;
            floor[i] = new vec3(MathF.Cos(angle) * radius, 0, MathF.Sin(angle) * radius);
            top[i] = new vec3(floor[i].x, height, floor[i].z);
        }

        _floor = floor;
        _top = top;
    }

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        var world = WorldTransform;
        var floor = _floor.Select(point => (world * new vec4(point, 1.0f)).xyz).ToArray();
        var top = _top.Select(point => (world * new vec4(point, 1.0f)).xyz).ToArray();
        var width = _owner.IsSelected ? 3.0f : 1.5f;
        renderer.DrawPolyline(floor, _color, width, PrimitiveLayer.WorldXRay);
        renderer.DrawPolyline(top, _color with { w = 0.6f }, width, PrimitiveLayer.WorldXRay);
        for (var i = 0; i < floor.Length - 1; i += Segments / 8)
        {
            renderer.DrawLine(floor[i], top[i], _color with { w = 0.4f }, 1.0f, PrimitiveLayer.WorldXRay);
        }

        // The axis the camera stays away from
        renderer.DrawLine((world * new vec4(0, 0, 0, 1)).xyz, (world * new vec4(0, top.Length > 0 ? _top[0].y : 0, 0, 1)).xyz, _color, width, PrimitiveLayer.WorldXRay);
    }
}
