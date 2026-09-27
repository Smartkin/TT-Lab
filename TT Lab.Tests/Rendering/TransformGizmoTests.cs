using GlmSharp;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects.Gizmo;

namespace TT_Lab.Tests.Rendering;

public sealed class TransformGizmoTests
{
    private static readonly GizmoTransform Origin = new(vec3.Zero, quat.Identity, vec3.Ones);
    private readonly FrameCamera _camera = ViewportMathTests.CreateCamera(new vec3(6, 5, 8), vec3.Zero);

    private static TransformGizmo CreateGizmo(TransformMode mode, TransformLocality locality = TransformLocality.WORLD)
    {
        return new TransformGizmo { Mode = mode, Locality = locality };
    }

    private float Size(vec3 position) => _camera.WorldUnitsPerPixel(position) * TransformGizmo.SizeInPixels;

    private vec2 Screen(vec3 point)
    {
        Assert.True(_camera.WorldToScreen(point, out var screen));
        return screen;
    }

    private static void AssertClose(vec3 expected, vec3 actual, float tolerance = 0.02f)
    {
        Assert.True((expected - actual).Length < tolerance, $"Expected {expected} but got {actual}");
    }

    [Fact]
    public void ArrowsGetPickedWhereTheyAreDrawn()
    {
        var gizmo = CreateGizmo(TransformMode.TRANSLATE);
        var size = Size(vec3.Zero);

        Assert.Equal(GizmoHandle.AxisX, gizmo.HitTest(_camera, Origin, Screen(vec3.UnitX * size * 0.7f)));
        Assert.Equal(GizmoHandle.AxisY, gizmo.HitTest(_camera, Origin, Screen(vec3.UnitY * size * 0.7f)));
        Assert.Equal(GizmoHandle.AxisZ, gizmo.HitTest(_camera, Origin, Screen(vec3.UnitZ * size * 0.7f)));
        Assert.Equal(GizmoHandle.Center, gizmo.HitTest(_camera, Origin, Screen(vec3.Zero)));
        Assert.Equal(GizmoHandle.None, gizmo.HitTest(_camera, Origin, Screen(new vec3(1, 1, 1) * size * 2.0f)));
    }

    [Fact]
    public void PlanesGetPickedBetweenTheirAxes()
    {
        var gizmo = CreateGizmo(TransformMode.TRANSLATE);
        var size = Size(vec3.Zero);

        // The camera looks from the positive side of every axis, which is where the planes face
        Assert.Equal(GizmoHandle.PlaneY, gizmo.HitTest(_camera, Origin, Screen(new vec3(0.4f, 0, 0.4f) * size)));
        Assert.Equal(GizmoHandle.PlaneX, gizmo.HitTest(_camera, Origin, Screen(new vec3(0, 0.4f, 0.4f) * size)));
        Assert.Equal(GizmoHandle.PlaneZ, gizmo.HitTest(_camera, Origin, Screen(new vec3(0.4f, 0.4f, 0) * size)));
    }

    [Fact]
    public void NothingGetsPickedWhenSelecting()
    {
        var gizmo = CreateGizmo(TransformMode.SELECTION);

        Assert.Equal(GizmoHandle.None, gizmo.HitTest(_camera, Origin, Screen(vec3.Zero)));
        Assert.False(gizmo.BeginDrag(_camera, Origin, Screen(vec3.Zero)));
    }

    [Fact]
    public void DraggingAnArrowMovesAlongItsAxisOnly()
    {
        var gizmo = CreateGizmo(TransformMode.TRANSLATE);
        var grab = vec3.UnitX * Size(vec3.Zero) * 0.7f;
        Assert.True(gizmo.BeginDrag(_camera, Origin, Screen(grab)));

        var result = gizmo.Drag(_camera, Screen(grab + new vec3(2, 0, 0)));

        AssertClose(new vec3(2, 0, 0), result.Position);
        Assert.Equal(Origin, gizmo.EndDrag());
        Assert.False(gizmo.IsDragging);
    }

    [Fact]
    public void DraggingAPlaneMovesAlongIt()
    {
        var gizmo = CreateGizmo(TransformMode.TRANSLATE);
        var grab = new vec3(0.4f, 0, 0.4f) * Size(vec3.Zero);
        Assert.True(gizmo.BeginDrag(_camera, Origin, Screen(grab)));
        Assert.Equal(GizmoHandle.PlaneY, gizmo.Active);

        var result = gizmo.Drag(_camera, Screen(grab + new vec3(1, 0, -1.5f)));

        AssertClose(new vec3(1, 0, -1.5f), result.Position);
    }

    [Fact]
    public void LocalSpaceUsesTheTargetsAxes()
    {
        var gizmo = CreateGizmo(TransformMode.TRANSLATE, TransformLocality.LOCAL);
        // Turned so its X points at world Z, which faces the camera instead of hiding behind the plane handles
        var target = Origin with { Rotation = quat.FromAxisAngle(-MathF.PI / 2, vec3.UnitY) };
        var localX = target.Rotation * vec3.UnitX;
        var grab = localX * Size(vec3.Zero) * 0.7f;
        Assert.Equal(GizmoHandle.AxisX, gizmo.HitTest(_camera, target, Screen(grab)));
        Assert.True(gizmo.BeginDrag(_camera, target, Screen(grab)));

        var result = gizmo.Drag(_camera, Screen(grab + localX * 1.5f));

        AssertClose(localX * 1.5f, result.Position);
    }

    // Whatever part of a ring gets grabbed keeps following the mouse along the ring
    [Fact]
    public void RingsTurnTheWayTheMouseDrags()
    {
        var gizmo = CreateGizmo(TransformMode.ROTATE);
        var size = Size(vec3.Zero);
        var grabPoint = new vec3(1, 0, 1).Normalized * size;
        var grab = Screen(grabPoint);
        Assert.Equal(GizmoHandle.RingY, gizmo.HitTest(_camera, Origin, grab));
        Assert.True(gizmo.BeginDrag(_camera, Origin, grab));
        var along = Screen(quat.FromAxisAngle(0.05f, vec3.UnitY) * grabPoint) - grab;

        var result = gizmo.Drag(_camera, grab + along.Normalized * 40.0f);

        var turned = Screen(result.Rotation * grabPoint);
        Assert.True(vec2.Dot(turned - grab, along) > 0.0f);
        // Only turns around the ring's axis
        AssertClose(vec3.UnitY, result.Rotation * vec3.UnitY, 1e-3f);
        Assert.Equal(Origin.Position, result.Position);
    }

    [Fact]
    public void ScaleArrowScalesItsAxis()
    {
        var gizmo = CreateGizmo(TransformMode.SCALE);
        var size = Size(vec3.Zero);
        var grab = Screen(vec3.UnitX * size * 0.7f);
        Assert.True(gizmo.BeginDrag(_camera, Origin, grab));
        var axisOnScreen = Screen(vec3.UnitX * size) - Screen(vec3.Zero);

        var result = gizmo.Drag(_camera, grab + axisOnScreen);

        AssertClose(new vec3(2, 1, 1), result.Scale);
    }

    [Fact]
    public void SnappedMovesGoByWholeStepsFromWhereTheDragStarted()
    {
        var gizmo = CreateGizmo(TransformMode.TRANSLATE);
        gizmo.IsSnapping = true;
        gizmo.Snapping = new GizmoSnapping(0.5f, 15.0f, 0.1f);
        var start = Origin with { Position = new vec3(0.3f, 0, 0) };
        var grab = start.Position + vec3.UnitX * Size(start.Position) * 0.7f;
        Assert.True(gizmo.BeginDrag(_camera, start, Screen(grab)));

        var result = gizmo.Drag(_camera, Screen(grab + new vec3(1.3f, 0, 0)));

        AssertClose(new vec3(1.8f, 0, 0), result.Position, 1e-3f);
    }

    [Fact]
    public void SnappedPlaneMovesSnapAlongBothAxes()
    {
        var gizmo = CreateGizmo(TransformMode.TRANSLATE);
        gizmo.IsSnapping = true;
        var grab = new vec3(0.4f, 0, 0.4f) * Size(vec3.Zero);
        Assert.True(gizmo.BeginDrag(_camera, Origin, Screen(grab)));

        var result = gizmo.Drag(_camera, Screen(grab + new vec3(1.2f, 0, -1.7f)));

        AssertClose(new vec3(1, 0, -2), result.Position, 1e-3f);
    }

    // Like holding Ctrl during a drag
    [Fact]
    public void InvertedSnappingSnapsWhenItsOffAndTheOtherWayAround()
    {
        var gizmo = CreateGizmo(TransformMode.TRANSLATE);
        var grab = vec3.UnitX * Size(vec3.Zero) * 0.7f;
        Assert.True(gizmo.BeginDrag(_camera, Origin, Screen(grab)));
        var mouse = Screen(grab + new vec3(1.3f, 0, 0));

        AssertClose(new vec3(1.3f, 0, 0), gizmo.Drag(_camera, mouse).Position);
        AssertClose(new vec3(1, 0, 0), gizmo.Drag(_camera, mouse, true).Position, 1e-3f);
        gizmo.IsSnapping = true;
        AssertClose(new vec3(1, 0, 0), gizmo.Drag(_camera, mouse).Position, 1e-3f);
        AssertClose(new vec3(1.3f, 0, 0), gizmo.Drag(_camera, mouse, true).Position);
    }

    [Fact]
    public void SnappedRotationsTurnByWholeSteps()
    {
        var gizmo = CreateGizmo(TransformMode.ROTATE);
        gizmo.IsSnapping = true;
        var grabPoint = new vec3(1, 0, 1).Normalized * Size(vec3.Zero);
        var grab = Screen(grabPoint);
        Assert.True(gizmo.BeginDrag(_camera, Origin, grab));
        var along = Screen(quat.FromAxisAngle(0.05f, vec3.UnitY) * grabPoint) - grab;

        // About 21 degrees of dragging
        var result = gizmo.Drag(_camera, grab + along.Normalized * 40.0f);

        var degrees = glm.Degrees(2.0f * MathF.Acos(Math.Clamp(MathF.Abs(result.Rotation.w), 0.0f, 1.0f)));
        Assert.Equal(15.0f, degrees, 0.01f);
    }

    [Fact]
    public void SnappedScalingScalesByWholeSteps()
    {
        var gizmo = CreateGizmo(TransformMode.SCALE);
        gizmo.IsSnapping = true;
        gizmo.Snapping = new GizmoSnapping(1.0f, 15.0f, 0.25f);
        var start = Origin with { Scale = new vec3(2, 1, 1) };
        var size = Size(vec3.Zero);
        var grab = Screen(vec3.UnitX * size * 0.7f);
        Assert.True(gizmo.BeginDrag(_camera, start, grab));
        var axisOnScreen = Screen(vec3.UnitX * size) - Screen(vec3.Zero);

        var result = gizmo.Drag(_camera, grab + axisOnScreen * 0.6f);

        AssertClose(new vec3(3, 1, 1), result.Scale, 1e-3f);
    }

    [Fact]
    public void ScaleCenterScalesEverything()
    {
        var gizmo = CreateGizmo(TransformMode.SCALE);
        var center = Screen(vec3.Zero);
        Assert.True(gizmo.BeginDrag(_camera, Origin, center));
        Assert.Equal(GizmoHandle.Center, gizmo.Active);

        var result = gizmo.Drag(_camera, center + new vec2(TransformGizmo.SizeInPixels * 0.5f, -TransformGizmo.SizeInPixels * 0.5f));

        AssertClose(new vec3(2, 2, 2), result.Scale);
    }
}
