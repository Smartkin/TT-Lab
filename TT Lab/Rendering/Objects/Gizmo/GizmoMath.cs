using System;
using GlmSharp;

namespace TT_Lab.Rendering.Objects.Gizmo;

public static class GizmoMath
{
    /// <summary>
    /// Parameter along the line of its point closest to the ray, null when the two are close to parallel
    /// </summary>
    public static float? ClosestLineParameter(Ray ray, vec3 linePoint, vec3 lineDirection)
    {
        var b = vec3.Dot(lineDirection, ray.Direction);
        var denominator = 1.0f - b * b;
        if (denominator < 1e-4f)
        {
            return null;
        }

        var offset = linePoint - ray.Origin;
        var d = vec3.Dot(lineDirection, offset);
        var e = vec3.Dot(ray.Direction, offset);
        return (b * e - d) / denominator;
    }

    /// <summary>
    /// Point where the ray crosses the plane, null when it runs along the plane
    /// </summary>
    public static vec3? IntersectPlane(Ray ray, vec3 planePoint, vec3 planeNormal)
    {
        var facing = vec3.Dot(ray.Direction, planeNormal);
        if (MathF.Abs(facing) < 1e-4f)
        {
            return null;
        }

        var distance = vec3.Dot(planePoint - ray.Origin, planeNormal) / facing;
        return ray.GetPoint(distance);
    }

    /// <summary>
    /// Distance along the ray to the cube from -1 to 1 transformed by the given matrix. When the ray starts inside the box the distance
    /// to where it leaves it is used, so a box around the camera doesn't hide everything else in it
    /// </summary>
    public static float? IntersectBox(Ray ray, in mat4 boxTransform)
    {
        if (MathF.Abs(boxTransform.Determinant) < 1e-12f)
        {
            return null;
        }

        // The transform is affine, so the distance along the ray stays the same in the box's space
        var inverse = boxTransform.Inverse;
        var origin = (inverse * new vec4(ray.Origin, 1.0f)).xyz;
        var direction = (inverse * new vec4(ray.Direction, 0.0f)).xyz;
        var near = float.NegativeInfinity;
        var far = float.PositiveInfinity;
        for (var axis = 0; axis < 3; axis++)
        {
            if (MathF.Abs(direction[axis]) < 1e-9f)
            {
                if (origin[axis] < -1.0f || origin[axis] > 1.0f)
                {
                    return null;
                }

                continue;
            }

            var first = (-1.0f - origin[axis]) / direction[axis];
            var second = (1.0f - origin[axis]) / direction[axis];
            near = MathF.Max(near, MathF.Min(first, second));
            far = MathF.Min(far, MathF.Max(first, second));
        }

        if (far < MathF.Max(near, 0.0f))
        {
            return null;
        }

        return near >= 0.0f ? near : far;
    }

    public static float DistanceToSegment(vec2 point, vec2 start, vec2 end)
    {
        var segment = end - start;
        var lengthSquared = vec2.Dot(segment, segment);
        var along = lengthSquared < 1e-6f ? 0.0f : Math.Clamp(vec2.Dot(point - start, segment) / lengthSquared, 0.0f, 1.0f);
        return (point - (start + segment * along)).Length;
    }

    /// <summary>
    /// Whether the point is inside the convex polygon, whichever way it's wound
    /// </summary>
    public static bool IsInsideConvex(vec2 point, ReadOnlySpan<vec2> polygon)
    {
        var sign = 0;
        for (var i = 0; i < polygon.Length; i++)
        {
            var edge = polygon[(i + 1) % polygon.Length] - polygon[i];
            var cross = edge.x * (point.y - polygon[i].y) - edge.y * (point.x - polygon[i].x);
            var side = MathF.Sign(cross);
            if (side == 0)
            {
                continue;
            }

            if (sign != 0 && side != sign)
            {
                return false;
            }

            sign = side;
        }

        return sign != 0;
    }
}
