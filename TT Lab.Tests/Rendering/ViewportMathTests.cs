using GlmSharp;
using TT_Lab.Extensions;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Objects.Gizmo;

namespace TT_Lab.Tests.Rendering;

public sealed class ViewportMathTests
{
    public static FrameCamera CreateCamera(vec3 eye, vec3 target)
    {
        var view = mat4.LookAt(eye, target, vec3.UnitY);
        return new FrameCamera(view.Inverse, mat4.Perspective(glm.Radians(60.0f), 800.0f / 600.0f, 0.05f, 1000.0f), new vec2(800, 600), glm.Radians(60.0f));
    }

    private static void AssertClose(vec3 expected, vec3 actual, float tolerance = 1e-3f)
    {
        Assert.True((expected - actual).Length < tolerance, $"Expected {expected} but got {actual}");
    }

    [Fact]
    public void ScreenRayGoesThroughWhatsShownThere()
    {
        var camera = CreateCamera(new vec3(3, 4, 10), vec3.Zero);
        foreach (var point in new[] { vec3.Zero, new vec3(2, 1, -3), new vec3(-4, 0.5f, 2) })
        {
            Assert.True(camera.WorldToScreen(point, out var screen));
            var ray = camera.ScreenRay(screen);
            var closest = ray.GetPoint(vec3.Dot(point - ray.Origin, ray.Direction));
            AssertClose(point, closest);
        }
    }

    // The rendered image gets mirrored before it's shown, things to the camera's right end up on the left of the viewport
    [Fact]
    public void ViewportIsMirroredHorizontally()
    {
        var camera = CreateCamera(new vec3(0, 0, 10), vec3.Zero);

        Assert.True(camera.WorldToScreen(new vec3(2, 0, 0), out var right));
        Assert.True(camera.WorldToScreen(new vec3(0, 2, 0), out var up));

        Assert.True(right.x < 400.0f);
        Assert.True(up.y < 300.0f);
    }

    [Fact]
    public void PointsBehindTheCameraAreNotOnScreen()
    {
        var camera = CreateCamera(new vec3(0, 0, 10), vec3.Zero);

        Assert.False(camera.WorldToScreen(new vec3(0, 0, 20), out _));
    }

    [Fact]
    public void PixelsGetBiggerFurtherAway()
    {
        var camera = CreateCamera(new vec3(0, 0, 10), vec3.Zero);

        Assert.Equal(2.0f * 10.0f * MathF.Tan(glm.Radians(30.0f)) / 600.0f, camera.WorldUnitsPerPixel(vec3.Zero), 4);
        Assert.Equal(camera.WorldUnitsPerPixel(vec3.Zero) * 2.0f, camera.WorldUnitsPerPixel(new vec3(0, 0, -10)), 4);
    }

    // Volumes draw their insides and then their outsides by culling front faces and then back faces, the faces a camera sees from
    // outside have to be wound counter-clockwise on screen
    [Fact]
    public void BoxFacesSeenFromOutsideAreFrontFaces()
    {
        var vertices = new List<float>();
        PrimitiveMeshes.AddBox(vertices);
        var positions = vertices.Chunk(PrimitiveMeshes.VertexFloats).Select(vertex => new vec3(vertex[0], vertex[1], vertex[2])).ToArray();
        var eye = new vec3(5, 3, 4);
        var camera = CreateCamera(eye, vec3.Zero);

        Assert.Equal(36, positions.Length);
        for (var i = 0; i < positions.Length; i += 3)
        {
            var (a, b, c) = (positions[i], positions[i + 1], positions[i + 2]);
            var normal = vec3.Cross(b - a, c - a);
            Assert.True(vec3.Dot(normal, a + b + c) > 0.0f, "Faces point outwards");

            var screen = new[] { a, b, c }.Select(point => { var clip = camera.ViewProjection * new vec4(point, 1.0f); return clip.xy / clip.w; }).ToArray();
            var area = (screen[1].x - screen[0].x) * (screen[2].y - screen[0].y) - (screen[1].y - screen[0].y) * (screen[2].x - screen[0].x);
            Assert.Equal(vec3.Dot(normal, eye - a) > 0.0f, area > 0.0f);
        }
    }

    [Fact]
    public void BSplineNeedsFourPoints()
    {
        Assert.Empty(SplineMath.SampleUniformCubicBSpline([vec3.Zero, vec3.UnitX, vec3.UnitY], 8));
    }

    // Paths are uniform B-splines, the curve approaches the first and last points without reaching them
    [Fact]
    public void BSplineStartsAndEndsWhereItsSegmentsDo()
    {
        vec3[] points = [new(0, 0, 0), new(1, 2, 0), new(2, 0, 1), new(3, 2, 1), new(4, 0, 0), new(5, 1, 2)];

        var samples = SplineMath.SampleUniformCubicBSpline(points, 8);

        Assert.Equal(3 * 8 + 1, samples.Length);
        AssertClose((points[0] + points[1] * 4.0f + points[2]) / 6.0f, samples[0]);
        AssertClose((points[3] + points[4] * 4.0f + points[5]) / 6.0f, samples[^1]);
        for (var segment = 0; segment < 2; segment++)
        {
            AssertClose(SplineMath.EvaluateSegment(points.AsSpan(segment, 4), 1.0f), SplineMath.EvaluateSegment(points.AsSpan(segment + 1, 4), 0.0f));
        }
    }

    [Fact]
    public void BSplineOfPointsOnALineStaysOnIt()
    {
        vec3[] points = [new(0, 0, 0), new(1, 1, 1), new(2, 2, 2), new(3, 3, 3), new(4, 4, 4)];

        foreach (var sample in SplineMath.SampleUniformCubicBSpline(points, 5))
        {
            AssertClose(new vec3(sample.x), sample);
        }
    }

    [Fact]
    public void EulerAnglesTurnBackIntoTheSameRotation()
    {
        var random = new Random(3);
        for (var i = 0; i < 1000; i++)
        {
            var axis = new vec3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f).Normalized;
            var rotation = quat.FromAxisAngle((float)(random.NextDouble() * 6.0 - 3.0), axis);

            var restored = new quat(rotation.ToEulerAngles());

            Assert.True(MathF.Min((restored - rotation).Length, (restored + rotation).Length) < 1e-3f);
        }
    }

    [Fact]
    public void RayHitsBoxWhereItEntersIt()
    {
        var box = mat4.Translate(0, 0, -5) * mat4.Scale(2, 1, 1);
        var ray = new Ray(new vec3(0, 0, 5), -vec3.UnitZ);

        Assert.Equal(9.0f, GizmoMath.IntersectBox(ray, box)!.Value, 4);
        Assert.Null(GizmoMath.IntersectBox(new Ray(new vec3(0, 3, 5), -vec3.UnitZ), box));
        Assert.Null(GizmoMath.IntersectBox(new Ray(new vec3(0, 0, 5), vec3.UnitZ), box));
    }

    // Big boxes around the camera, like triggers, mustn't hide everything else inside them
    [Fact]
    public void RayFromInsideABoxHitsWhereItLeavesIt()
    {
        var box = mat4.Scale(10.0f);

        Assert.Equal(10.0f, GizmoMath.IntersectBox(new Ray(vec3.Zero, vec3.UnitX), box)!.Value, 4);
    }

    [Fact]
    public void ClosestPointOnLineToRay()
    {
        var ray = new Ray(new vec3(3, 0, 10), -vec3.UnitZ);

        Assert.Equal(3.0f, GizmoMath.ClosestLineParameter(ray, vec3.Zero, vec3.UnitX)!.Value, 4);
        Assert.Null(GizmoMath.ClosestLineParameter(ray, vec3.Zero, vec3.UnitZ));
    }
}
