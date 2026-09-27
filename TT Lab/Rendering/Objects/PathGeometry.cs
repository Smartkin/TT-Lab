using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.Extensions;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;

namespace TT_Lab.Rendering.Objects;

public static class PathGeometry
{
    private const int SamplesPerSegment = 8;
    private const float PointMarkerRadius = 4.0f;

    public static readonly vec4 PathColor = new(0.35f, 0.85f, 1.0f, 1.0f);
    public static readonly vec4 MainCamera1Color = new(0.35f, 0.55f, 1.0f, 1.0f);
    public static readonly vec4 MainCamera2Color = new(0.78f, 0.45f, 1.0f, 1.0f);

    /// <summary>
    /// The curve along with the points shaping it, which it doesn't go through
    /// </summary>
    public static void AddSpline(List<PolylineStroke> strokes, vec3[] controlPoints, vec4 color)
    {
        if (controlPoints.Length < 2)
        {
            return;
        }

        strokes.Add(new PolylineStroke(controlPoints, color with { w = color.w * 0.35f }, 1.0f));
        var curve = SplineMath.SampleUniformCubicBSpline(controlPoints, SamplesPerSegment);
        if (curve.Length > 1)
        {
            strokes.Add(new PolylineStroke(curve, color, 3.0f, true));
        }
    }

    public static IReadOnlyList<PolylineStroke> CreatePath(IEnumerable<Vector3> points)
    {
        var strokes = new List<PolylineStroke>();
        AddSpline(strokes, points.Select(point => point.ToGlm()).ToArray(), PathColor);
        return strokes;
    }

    /// <summary>
    /// What the camera follows depends on its type, only the ones made of positions get drawn
    /// </summary>
    public static void AddCamera(List<PolylineStroke> strokes, List<PolylineMarker> markers, CameraSubBase? camera, vec3 trigger, vec4 color)
    {
        vec3[] points;
        switch (camera)
        {
            case CameraPoint point:
                points = [point.Point.ToGlm().xyz];
                break;
            case CameraPoint2 point:
                points = [point.Point.ToGlm().xyz];
                break;
            case CameraLine line:
                points = [line.LineStart.ToGlm().xyz, line.LineEnd.ToGlm().xyz];
                strokes.Add(new PolylineStroke(points, color, 3.0f, true));
                break;
            case CameraLine2 line:
                points = [line.LineStart.ToGlm().xyz, line.LineEnd.ToGlm().xyz];
                strokes.Add(new PolylineStroke(points, color, 3.0f, true));
                break;
            case CameraPath path:
                points = path.PathPoints.Select(point => point.ToGlm().xyz).ToArray();
                AddSpline(strokes, points, color);
                break;
            // Already sampled densely enough to be drawn as is
            case CameraSpline spline:
                points = spline.PathPoints.Select(point => point.ToGlm().xyz).ToArray();
                if (points.Length > 1)
                {
                    strokes.Add(new PolylineStroke(points, color, 3.0f, true));
                }

                break;
            default:
                return;
        }

        if (points.Length == 0)
        {
            return;
        }

        if (points.Length <= 2)
        {
            markers.AddRange(points.Select(point => new PolylineMarker(point, color, PointMarkerRadius)));
        }

        // Ties what the camera follows to the trigger it belongs to
        strokes.Add(new PolylineStroke([trigger, points[0]], color with { w = color.w * 0.4f }, 1.0f));
    }
}
