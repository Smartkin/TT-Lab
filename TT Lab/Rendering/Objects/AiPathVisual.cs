using System;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Extensions;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// Connection between the two AI positions of an AI path. Their positions are read every frame, so moving either of them moves the line
/// and the box it's picked by
/// </summary>
public class AiPathVisual(RenderContext context, string name) : EditableObject(context, null, name), IPrimitiveRenderable
{
    private static readonly vec4 Color = new(1.0f, 0.86f, 0.25f, 0.9f);
    private static readonly vec4 SelectedLineColor = new(1.0f, 1.0f, 0.75f, 1.0f);
    // How far from the line a click still picks the path
    private const float PickRadius = 0.35f;

    private AiPositionData? _begin;
    private AiPositionData? _end;

    public void SetEnds(AiPositionData? begin, AiPositionData? end)
    {
        _begin = begin;
        _end = end;
    }

    private bool TryGetEnds(out vec3 start, out vec3 finish)
    {
        var begin = _begin?.Coords;
        var end = _end?.Coords;
        start = begin?.ToGlm() ?? vec3.Zero;
        finish = end?.ToGlm() ?? vec3.Zero;
        return begin != null && end != null;
    }

    // A thin box along the line, nothing to pick without both ends
    public override mat4 GetBoundsTransform()
    {
        if (!TryGetEnds(out var start, out var finish))
        {
            return mat4.Scale(0.0f);
        }

        var center = (start + finish) * 0.5f;
        var along = finish - start;
        var length = along.Length;
        if (length < 1e-4f)
        {
            return mat4.Translate(center) * mat4.Scale(PickRadius);
        }

        var axis = along / length;
        var up = MathF.Abs(axis.y) < 0.99f ? vec3.UnitY : vec3.UnitX;
        var side = vec3.Cross(axis, up).Normalized;
        var normal = vec3.Cross(side, axis);
        return new mat4(new vec4(axis * (length * 0.5f), 0.0f), new vec4(normal * PickRadius, 0.0f), new vec4(side * PickRadius, 0.0f), new vec4(center, 1.0f));
    }

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        if (!TryGetEnds(out var start, out var finish))
        {
            return;
        }

        var color = IsSelected ? SelectedLineColor : Color;
        renderer.DrawLine(start, finish, color, IsSelected ? 4.0f : 2.5f, PrimitiveLayer.WorldXRay);
        // Shows which way the path goes without covering the positions at its ends
        PolylineVisual.DrawArrowhead(renderer, camera, start, vec3.Lerp(start, finish, 0.6f), color);
    }
}
