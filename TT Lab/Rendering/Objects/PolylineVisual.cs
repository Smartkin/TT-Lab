using System;
using System.Collections.Generic;
using GlmSharp;

namespace TT_Lab.Rendering.Objects;

/// <param name="Points">Points in world space</param>
/// <param name="Width">Width in pixels</param>
/// <param name="ShowDirection">Puts an arrowhead at the end of the line</param>
public sealed record PolylineStroke(vec3[] Points, vec4 Color, float Width, bool ShowDirection = false);

/// <param name="Radius">Radius in pixels</param>
public sealed record PolylineMarker(vec3 Position, vec4 Color, float Radius);

/// <summary>
/// Lines and markers drawn with the primitive renderer, for editor visuals of things that have no model like paths
/// </summary>
public class PolylineVisual : EditableObject, IPrimitiveRenderable
{
    private const float ArrowLength = 16.0f;
    private const float ArrowRadius = 6.0f;

    // Replaced as a whole so the render thread never sees them half updated
    private IReadOnlyList<PolylineStroke> _strokes = [];
    private IReadOnlyList<PolylineMarker> _markers = [];

    public PolylineVisual(RenderContext context, string name) : base(context, null, name)
    {
    }

    public override bool IsSelectable => false;

    public void SetGeometry(IReadOnlyList<PolylineStroke> strokes, IReadOnlyList<PolylineMarker>? markers = null)
    {
        _strokes = strokes;
        _markers = markers ?? [];
    }

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        foreach (var stroke in _strokes)
        {
            renderer.DrawPolyline(stroke.Points, stroke.Color, stroke.Width, PrimitiveLayer.WorldXRay);
            if (stroke.ShowDirection && stroke.Points.Length > 1)
            {
                DrawArrowhead(renderer, camera, stroke.Points[^2], stroke.Points[^1], stroke.Color);
            }
        }

        foreach (var marker in _markers)
        {
            renderer.DrawSphere(marker.Position, camera.WorldUnitsPerPixel(marker.Position) * marker.Radius, marker.Color, PrimitiveLayer.WorldXRay);
        }
    }

    public static void DrawArrowhead(PrimitiveRenderer renderer, in FrameCamera camera, vec3 from, vec3 tip, vec4 color)
    {
        var direction = tip - from;
        if (direction.LengthSqr < 1e-10f)
        {
            return;
        }

        var pixel = camera.WorldUnitsPerPixel(tip);
        renderer.DrawCone(tip - direction.Normalized * pixel * ArrowLength, tip, pixel * ArrowRadius, color, PrimitiveLayer.WorldXRay);
    }
}
