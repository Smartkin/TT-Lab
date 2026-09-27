using System;
using GlmSharp;

namespace TT_Lab.Rendering.Objects.Gizmo;

public enum GizmoHandle
{
    None,
    AxisX,
    AxisY,
    AxisZ,
    // Planes are named by the axis they are perpendicular to, so moving on PlaneX moves along Y and Z
    PlaneX,
    PlaneY,
    PlaneZ,
    // Moving along the view plane or scaling uniformly
    Center,
    RingX,
    RingY,
    RingZ,
    // Rotating around the direction the camera looks in
    RingView,
}

public readonly record struct GizmoTransform(vec3 Position, quat Rotation, vec3 Scale);

/// <summary>
/// Steps drags snap to, counted from where the drag started. Moving snaps along the gizmo's axes and scaling snaps the factor the drag
/// scales by
/// </summary>
public readonly record struct GizmoSnapping(float Translation, float RotationDegrees, float Scale);

/// <summary>
/// Translation, rotation and scale handles that get picked and dragged with the mouse. Picking and dragging happen on the UI thread
/// while drawing happens on the render thread, the only state they share are a few plain values
/// </summary>
public sealed class TransformGizmo
{
    // How long the axes are on screen
    public const float SizeInPixels = 110.0f;
    private const float PickDistance = 8.0f;
    private const float AxisPickStart = 0.2f;
    private const float PlaneStart = 0.28f;
    private const float PlaneEnd = 0.5f;
    private const float CenterPickRadius = 10.0f;
    private const float ViewRingScale = 1.18f;
    private const int RingSegments = 64;
    private const float MinScale = 0.001f;

    private static readonly vec4 HighlightColor = new(1.0f, 0.85f, 0.1f, 1.0f);
    private static readonly vec4 CenterColor = new(0.92f, 0.92f, 0.92f, 1.0f);
    private static readonly vec4[] AxisColors =
    [
        new(0.93f, 0.23f, 0.23f, 1.0f),
        new(0.38f, 0.85f, 0.2f, 1.0f),
        new(0.22f, 0.5f, 1.0f, 1.0f),
    ];

    private readonly vec3[] _ring = new vec3[RingSegments + 1];
    private DragState? _drag;

    public TransformMode Mode { get; set; } = TransformMode.SELECTION;
    public TransformLocality Locality { get; set; } = TransformLocality.LOCAL;
    public bool IsSnapping { get; set; }
    public GizmoSnapping Snapping { get; set; } = new(1.0f, 15.0f, 0.1f);
    public GizmoHandle Hovered { get; set; }
    public GizmoHandle Active => _drag?.Handle ?? GizmoHandle.None;
    public bool IsDragging => _drag != null;
    public GizmoTransform? DragStart => _drag?.Start;
    public bool IsVisible => Mode != TransformMode.SELECTION;

    public GizmoHandle HitTest(in FrameCamera camera, GizmoTransform target, vec2 mouse)
    {
        if (!IsVisible || !camera.IsValid || !camera.WorldToScreen(target.Position, out var center))
        {
            return GizmoHandle.None;
        }

        var size = GetWorldSize(camera, target.Position);
        var orientation = GetOrientation(target);
        var best = GizmoHandle.None;
        var bestDistance = PickDistance;
        switch (Mode)
        {
            case TransformMode.TRANSLATE:
            case TransformMode.SCALE:
            {
                if ((mouse - center).Length < CenterPickRadius)
                {
                    return GizmoHandle.Center;
                }

                Span<vec2> corners = stackalloc vec2[4];
                for (var axis = 0; axis < 3; axis++)
                {
                    if (GetPlaneCorners(camera, target.Position, orientation, size, axis, corners) && GizmoMath.IsInsideConvex(mouse, corners))
                    {
                        return GizmoHandle.PlaneX + axis;
                    }
                }

                for (var axis = 0; axis < 3; axis++)
                {
                    var direction = GetAxis(orientation, axis);
                    if (!camera.WorldToScreen(target.Position + direction * size * AxisPickStart, out var start) ||
                        !camera.WorldToScreen(target.Position + direction * size, out var end))
                    {
                        continue;
                    }

                    var distance = GizmoMath.DistanceToSegment(mouse, start, end);
                    if (distance < bestDistance)
                    {
                        best = GizmoHandle.AxisX + axis;
                        bestDistance = distance;
                    }
                }

                break;
            }
            case TransformMode.ROTATE:
            {
                for (var axis = 0; axis < 3; axis++)
                {
                    var distance = GetRingDistance(camera, target.Position, GetAxis(orientation, axis), size, mouse, out _);
                    if (distance < bestDistance)
                    {
                        best = GizmoHandle.RingX + axis;
                        bestDistance = distance;
                    }
                }

                var viewDistance = GetRingDistance(camera, target.Position, camera.Forward, size * ViewRingScale, mouse, out _);
                if (viewDistance < bestDistance)
                {
                    best = GizmoHandle.RingView;
                }

                break;
            }
        }

        return best;
    }

    public bool BeginDrag(in FrameCamera camera, GizmoTransform target, vec2 mouse)
    {
        var handle = HitTest(camera, target, mouse);
        if (handle == GizmoHandle.None)
        {
            return false;
        }

        var ray = camera.ScreenRay(mouse);
        var size = GetWorldSize(camera, target.Position);
        var orientation = GetOrientation(target);
        var drag = new DragState(handle, target, mouse) { Orientation = orientation };
        switch (handle)
        {
            case GizmoHandle.AxisX or GizmoHandle.AxisY or GizmoHandle.AxisZ when Mode == TransformMode.TRANSLATE:
                drag.Direction = GetAxis(orientation, handle - GizmoHandle.AxisX);
                drag.StartParameter = GizmoMath.ClosestLineParameter(ray, target.Position, drag.Direction) ?? 0.0f;
                break;
            case GizmoHandle.PlaneX or GizmoHandle.PlaneY or GizmoHandle.PlaneZ or GizmoHandle.Center when Mode == TransformMode.TRANSLATE:
                drag.Direction = handle == GizmoHandle.Center ? camera.Forward : GetAxis(orientation, handle - GizmoHandle.PlaneX);
                drag.StartPoint = GizmoMath.IntersectPlane(ray, target.Position, drag.Direction) ?? target.Position;
                break;
            case GizmoHandle.RingX or GizmoHandle.RingY or GizmoHandle.RingZ or GizmoHandle.RingView:
            {
                var isView = handle == GizmoHandle.RingView;
                drag.Direction = isView ? camera.Forward : GetAxis(orientation, handle - GizmoHandle.RingX);
                GetRingDistance(camera, target.Position, drag.Direction, size * (isView ? ViewRingScale : 1.0f), mouse, out var grabPoint);
                drag.StartPoint = grabPoint;
                // Dragging along the ring's tangent where it got grabbed turns it, a full radius on screen is a radian
                var tangent = vec3.Cross(drag.Direction, grabPoint - target.Position).NormalizedSafe;
                if (camera.WorldToScreen(grabPoint, out var grabScreen) &&
                    camera.WorldToScreen(grabPoint + tangent * size * 0.1f, out var tangentScreen))
                {
                    drag.ScreenDirection = (tangentScreen - grabScreen).NormalizedSafe;
                }

                break;
            }
            case GizmoHandle.AxisX or GizmoHandle.AxisY or GizmoHandle.AxisZ:
            {
                var axis = handle - GizmoHandle.AxisX;
                SetScreenDirection(camera, drag, target.Position, GetAxis(orientation, axis) * size);
                break;
            }
            case GizmoHandle.PlaneX or GizmoHandle.PlaneY or GizmoHandle.PlaneZ:
            {
                // Pulling the handle away from the center grows the target, the handle may be on the negative side of its axes
                Span<vec3> corners = stackalloc vec3[4];
                GetPlaneQuad(camera, target.Position, orientation, size, handle - GizmoHandle.PlaneX, corners);
                SetScreenDirection(camera, drag, target.Position, corners[2] - target.Position);
                break;
            }
        }

        _drag = drag;
        Hovered = handle;
        return true;
    }

    /// <summary>
    /// Where the target ends up with the mouse at the given position, always worked out from where the drag started. Inverting the
    /// snapping snaps when it's off and the other way around, the way holding a key during a drag does
    /// </summary>
    public GizmoTransform Drag(in FrameCamera camera, vec2 mouse, bool invertSnapping = false)
    {
        if (_drag == null)
        {
            throw new InvalidOperationException("Gizmo isn't being dragged");
        }

        var drag = _drag;
        var start = drag.Start;
        var ray = camera.ScreenRay(mouse);
        var snapping = IsSnapping != invertSnapping ? Snapping : default;
        switch (Mode)
        {
            case TransformMode.TRANSLATE when drag.Handle is GizmoHandle.AxisX or GizmoHandle.AxisY or GizmoHandle.AxisZ:
            {
                var parameter = GizmoMath.ClosestLineParameter(ray, start.Position, drag.Direction);
                if (parameter == null)
                {
                    return drag.Last;
                }

                var distance = Snap(parameter.Value - drag.StartParameter, snapping.Translation);
                drag.Last = start with { Position = start.Position + drag.Direction * distance };
                break;
            }
            case TransformMode.TRANSLATE:
            {
                var point = GizmoMath.IntersectPlane(ray, start.Position, drag.Direction);
                // A plane seen from its edge would send the target flying off
                if (point == null || MathF.Abs(vec3.Dot(ray.Direction, drag.Direction)) < 0.02f)
                {
                    return drag.Last;
                }

                drag.Last = start with { Position = start.Position + SnapAlongAxes(point.Value - drag.StartPoint, drag.Orientation, snapping.Translation) };
                break;
            }
            case TransformMode.ROTATE:
            {
                var angle = Snap(vec2.Dot(mouse - drag.StartMouse, drag.ScreenDirection) / SizeInPixels, glm.Radians(snapping.RotationDegrees));
                drag.Angle = angle;
                drag.Last = start with { Rotation = (quat.FromAxisAngle(angle, drag.Direction) * start.Rotation).Normalized };
                break;
            }
            case TransformMode.SCALE:
            {
                var delta = mouse - drag.StartMouse;
                if (drag.Handle == GizmoHandle.Center)
                {
                    var factor = SnapFactor(MathF.Max(1.0f + (delta.x - delta.y) / SizeInPixels, MinScale), snapping.Scale);
                    drag.Last = start with { Scale = start.Scale * factor };
                    break;
                }

                var scaleFactor = SnapFactor(MathF.Max(1.0f + vec2.Dot(delta, drag.ScreenDirection) / drag.ScreenLength, MinScale), snapping.Scale);
                var factors = vec3.Ones;
                if (drag.Handle is GizmoHandle.AxisX or GizmoHandle.AxisY or GizmoHandle.AxisZ)
                {
                    factors[drag.Handle - GizmoHandle.AxisX] = scaleFactor;
                }
                else
                {
                    var axis = drag.Handle - GizmoHandle.PlaneX;
                    factors[(axis + 1) % 3] = scaleFactor;
                    factors[(axis + 2) % 3] = scaleFactor;
                }

                drag.Last = start with { Scale = start.Scale * factors };
                break;
            }
        }

        return drag.Last;
    }

    /// <summary>
    /// Ends the drag and gives where the target was before it
    /// </summary>
    public GizmoTransform? EndDrag()
    {
        var start = _drag?.Start;
        _drag = null;
        return start;
    }

    public void Draw(PrimitiveRenderer renderer, in FrameCamera camera, GizmoTransform target)
    {
        if (!IsVisible || !camera.IsValid)
        {
            return;
        }

        var drag = _drag;
        var position = target.Position;
        var size = GetWorldSize(camera, position);
        // Rings keep the orientation the drag started with, otherwise the grabbed ring would turn away from under the mouse
        var orientation = GetOrientation(Mode == TransformMode.ROTATE && drag != null ? drag.Start : target);
        var hovered = drag?.Handle ?? Hovered;
        switch (Mode)
        {
            case TransformMode.TRANSLATE:
                DrawTranslation(renderer, camera, position, orientation, size, hovered, drag);
                break;
            case TransformMode.ROTATE:
                DrawRotation(renderer, camera, position, orientation, size, hovered, drag);
                break;
            case TransformMode.SCALE:
                DrawScale(renderer, camera, position, orientation, size, hovered, drag);
                break;
        }
    }

    private void DrawTranslation(PrimitiveRenderer renderer, in FrameCamera camera, vec3 position, quat orientation, float size, GizmoHandle hovered, DragState? drag)
    {
        if (drag != null && drag.Handle is GizmoHandle.AxisX or GizmoHandle.AxisY or GizmoHandle.AxisZ)
        {
            var guide = drag.Direction * camera.WorldUnitsPerPixel(position) * 4000.0f;
            renderer.DrawLine(drag.Start.Position - guide, drag.Start.Position + guide, AxisColors[drag.Handle - GizmoHandle.AxisX] with { w = 0.6f }, 1.5f, PrimitiveLayer.Overlay);
        }

        Span<vec3> plane = stackalloc vec3[4];
        for (var axis = 0; axis < 3; axis++)
        {
            GetPlaneQuad(camera, position, orientation, size, axis, plane);
            var planeColor = GetColor(GizmoHandle.PlaneX + axis, hovered, drag, AxisColors[axis]);
            renderer.DrawQuad((plane[0] + plane[2]) * 0.5f, (plane[1] - plane[0]) * 0.5f, (plane[3] - plane[0]) * 0.5f, planeColor with { w = planeColor.w * 0.4f }, PrimitiveLayer.Overlay);
            renderer.DrawPolyline(plane, planeColor, 1.5f, PrimitiveLayer.Overlay, true);
        }

        for (var axis = 0; axis < 3; axis++)
        {
            var direction = GetAxis(orientation, axis);
            var color = GetColor(GizmoHandle.AxisX + axis, hovered, drag, AxisColors[axis]);
            renderer.DrawArrow(position + direction * size * 0.08f, position + direction * size, color, 3.0f, size * 0.22f, size * 0.065f, PrimitiveLayer.Overlay);
        }

        renderer.DrawSphere(position, size * 0.055f, GetColor(GizmoHandle.Center, hovered, drag, CenterColor), PrimitiveLayer.Overlay);
    }

    private void DrawRotation(PrimitiveRenderer renderer, in FrameCamera camera, vec3 position, quat orientation, float size, GizmoHandle hovered, DragState? drag)
    {
        var toCamera = (camera.Position - position).NormalizedSafe;
        for (var axis = 0; axis < 3; axis++)
        {
            var handle = GizmoHandle.RingX + axis;
            var color = GetColor(handle, hovered, drag, AxisColors[axis]);
            FillRing(position, GetAxis(orientation, axis), size);
            for (var i = 0; i < RingSegments; i++)
            {
                // The half facing away from the camera is faded so the rings don't read as a tangle
                var facing = vec3.Dot(((_ring[i] + _ring[i + 1]) * 0.5f - position).NormalizedSafe, toCamera);
                var alpha = facing >= -0.05f ? color.w : color.w * 0.25f;
                renderer.DrawLine(_ring[i], _ring[i + 1], color with { w = alpha }, hovered == handle ? 4.0f : 3.0f, PrimitiveLayer.Overlay);
            }
        }

        var viewColor = GetColor(GizmoHandle.RingView, hovered, drag, CenterColor with { w = 0.8f });
        renderer.DrawCircle(position, camera.Forward, size * ViewRingScale, viewColor, hovered == GizmoHandle.RingView ? 3.0f : 2.0f, PrimitiveLayer.Overlay);

        if (drag == null)
        {
            return;
        }

        // Where the ring got grabbed and where that point got turned to
        var grabbed = drag.StartPoint - drag.Start.Position;
        var turned = quat.FromAxisAngle(drag.Angle, drag.Direction) * grabbed;
        renderer.DrawLine(position, position + grabbed, CenterColor with { w = 0.5f }, 1.5f, PrimitiveLayer.Overlay);
        renderer.DrawLine(position, position + turned, HighlightColor, 2.0f, PrimitiveLayer.Overlay);
    }

    private void DrawScale(PrimitiveRenderer renderer, in FrameCamera camera, vec3 position, quat orientation, float size, GizmoHandle hovered, DragState? drag)
    {
        Span<vec3> plane = stackalloc vec3[4];
        for (var axis = 0; axis < 3; axis++)
        {
            GetPlaneQuad(camera, position, orientation, size, axis, plane);
            var planeColor = GetColor(GizmoHandle.PlaneX + axis, hovered, drag, AxisColors[axis]);
            renderer.DrawQuad((plane[0] + plane[2]) * 0.5f, (plane[1] - plane[0]) * 0.5f, (plane[3] - plane[0]) * 0.5f, planeColor with { w = planeColor.w * 0.4f }, PrimitiveLayer.Overlay);
            renderer.DrawPolyline(plane, planeColor, 1.5f, PrimitiveLayer.Overlay, true);
        }

        // Stretches along with the drag so the handle stays under the mouse
        var stretch = drag != null ? drag.Last.Scale / Nonzero(drag.Start.Scale) : vec3.Ones;
        for (var axis = 0; axis < 3; axis++)
        {
            var direction = GetAxis(orientation, axis);
            var color = GetColor(GizmoHandle.AxisX + axis, hovered, drag, AxisColors[axis]);
            var tip = position + direction * size * Math.Clamp(stretch[axis], 0.05f, 20.0f);
            renderer.DrawLine(position + direction * size * 0.08f, tip, color, 3.0f, PrimitiveLayer.Overlay);
            renderer.DrawBox(tip, new vec3(size * 0.06f), orientation, color, PrimitiveLayer.Overlay);
        }

        renderer.DrawBox(position, new vec3(size * 0.075f), orientation, GetColor(GizmoHandle.Center, hovered, drag, CenterColor), PrimitiveLayer.Overlay);
    }

    private static vec4 GetColor(GizmoHandle handle, GizmoHandle hovered, DragState? drag, vec4 color)
    {
        if (handle == hovered)
        {
            return HighlightColor;
        }

        // Handles that aren't being dragged fade out of the way
        return drag != null ? color with { w = color.w * 0.3f } : color;
    }

    /// <summary>
    /// How the gizmo's axes are turned for the target
    /// </summary>
    public quat GetOrientation(GizmoTransform target)
    {
        // Scale always happens along the target's own axes
        return Locality == TransformLocality.LOCAL || Mode == TransformMode.SCALE ? target.Rotation.NormalizedSafe : quat.Identity;
    }

    private static vec3 GetAxis(quat orientation, int axis)
    {
        return orientation * (axis switch
        {
            0 => vec3.UnitX,
            1 => vec3.UnitY,
            _ => vec3.UnitZ
        });
    }

    private static float GetWorldSize(in FrameCamera camera, vec3 position)
    {
        return camera.WorldUnitsPerPixel(position) * SizeInPixels;
    }

    public static vec4 GetAxisColor(int axis)
    {
        return AxisColors[axis];
    }

    // No step means no snapping
    private static float Snap(float value, float step)
    {
        return step > 0.0f ? MathF.Round(value / step) * step : value;
    }

    private static vec3 SnapAlongAxes(vec3 offset, quat orientation, float step)
    {
        if (step <= 0.0f)
        {
            return offset;
        }

        var local = orientation.Inverse * offset;
        return orientation * new vec3(Snap(local.x, step), Snap(local.y, step), Snap(local.z, step));
    }

    // Shrinking all the way to nothing isn't a scale that can be undone, the smallest step is as far as it goes
    private static float SnapFactor(float factor, float step)
    {
        return step > 0.0f ? MathF.Max(Snap(factor, step), step) : factor;
    }

    private static vec3 Nonzero(vec3 value)
    {
        return new vec3(MathF.Abs(value.x) < 1e-6f ? 1.0f : value.x, MathF.Abs(value.y) < 1e-6f ? 1.0f : value.y, MathF.Abs(value.z) < 1e-6f ? 1.0f : value.z);
    }

    // Plane handles sit in the corner between their two axes facing the camera, so they never end up hidden behind the others
    private static void GetPlaneQuad(in FrameCamera camera, vec3 position, quat orientation, float size, int normalAxis, Span<vec3> corners)
    {
        var first = GetAxis(orientation, (normalAxis + 1) % 3);
        var second = GetAxis(orientation, (normalAxis + 2) % 3);
        var toCamera = camera.Position - position;
        first *= vec3.Dot(first, toCamera) < 0.0f ? -1.0f : 1.0f;
        second *= vec3.Dot(second, toCamera) < 0.0f ? -1.0f : 1.0f;
        corners[0] = position + (first * PlaneStart + second * PlaneStart) * size;
        corners[1] = position + (first * PlaneEnd + second * PlaneStart) * size;
        corners[2] = position + (first * PlaneEnd + second * PlaneEnd) * size;
        corners[3] = position + (first * PlaneStart + second * PlaneEnd) * size;
    }

    private static bool GetPlaneCorners(in FrameCamera camera, vec3 position, quat orientation, float size, int normalAxis, Span<vec2> screenCorners)
    {
        Span<vec3> corners = stackalloc vec3[4];
        GetPlaneQuad(camera, position, orientation, size, normalAxis, corners);
        for (var i = 0; i < 4; i++)
        {
            if (!camera.WorldToScreen(corners[i], out screenCorners[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void FillRing(vec3 center, vec3 normal, float radius)
    {
        var (u, v) = PrimitiveRenderer.GetPerpendiculars(normal);
        for (var i = 0; i <= RingSegments; i++)
        {
            var angle = MathF.Tau * i / RingSegments;
            _ring[i] = center + (u * MathF.Cos(angle) + v * MathF.Sin(angle)) * radius;
        }
    }

    private float GetRingDistance(in FrameCamera camera, vec3 center, vec3 normal, float radius, vec2 mouse, out vec3 closestPoint)
    {
        FillRing(center, normal, radius);
        var best = float.MaxValue;
        closestPoint = _ring[0];
        var toCamera = (camera.Position - center).NormalizedSafe;
        for (var i = 0; i < RingSegments; i++)
        {
            if (!camera.WorldToScreen(_ring[i], out var start) || !camera.WorldToScreen(_ring[i + 1], out var end))
            {
                continue;
            }

            var distance = GizmoMath.DistanceToSegment(mouse, start, end);
            // Prefer the half facing the camera when both halves overlap on screen
            if (vec3.Dot(((_ring[i] + _ring[i + 1]) * 0.5f - center).NormalizedSafe, toCamera) < -0.05f)
            {
                distance += 3.0f;
            }

            if (distance < best)
            {
                best = distance;
                closestPoint = (_ring[i] + _ring[i + 1]) * 0.5f;
            }
        }

        return best;
    }

    private static void SetScreenDirection(in FrameCamera camera, DragState drag, vec3 position, vec3 offset)
    {
        if (!camera.WorldToScreen(position, out var start) || !camera.WorldToScreen(position + offset, out var end))
        {
            return;
        }

        var screen = end - start;
        drag.ScreenLength = Math.Max(screen.Length, 10.0f);
        drag.ScreenDirection = screen.NormalizedSafe;
    }

    private sealed class DragState(GizmoHandle handle, GizmoTransform start, vec2 startMouse)
    {
        public GizmoHandle Handle { get; } = handle;
        public GizmoTransform Start { get; } = start;
        public GizmoTransform Last { get; set; } = start;
        public vec2 StartMouse { get; } = startMouse;
        public vec3 Direction { get; set; }
        public quat Orientation { get; set; } = quat.Identity;
        public vec3 StartPoint { get; set; }
        public float StartParameter { get; set; }
        public vec2 ScreenDirection { get; set; } = new(1.0f, 0.0f);
        public float ScreenLength { get; set; } = SizeInPixels;
        public float Angle { get; set; }
    }
}
