using GlmSharp;
using TT_Lab.Rendering;

namespace TT_Lab.Tests.Rendering;

public sealed class OrbitCameraTests
{
    private static readonly float FovY = glm.Radians(60.0f);
    private static readonly vec3 Min = new(-1, 0, -1);
    private static readonly vec3 Max = new(1, 4, 1);

    private static void AssertLooksAt(vec3 target, mat4 camera)
    {
        var toTarget = (target - camera.Column3.xyz).Normalized;
        var forward = -camera.Column2.xyz.Normalized;
        Assert.True((toTarget - forward).Length < 1e-4f, $"Looks along {forward} instead of at {target}, which is along {toTarget}");
    }

    [Fact]
    public void CamerasStartALittleAboveTheModelLookingAtItsMiddle()
    {
        var orbit = new OrbitCamera();

        var camera = orbit.Frame(Min, Max, FovY);

        Assert.Equal(new vec3(0, 2, 0), orbit.Pivot);
        Assert.True(camera.Column3.y > orbit.Pivot.y);
        Assert.True(camera.Column3.z > orbit.Pivot.z);
        AssertLooksAt(orbit.Pivot, camera);
        Assert.True(orbit.Distance * MathF.Tan(FovY * 0.5f) >= orbit.Radius, "The model doesn't fit in the view");
        Assert.True(camera.Column1.y > 0);
    }

    [Fact]
    public void OrbitingKeepsLookingAtTheModelFromTheSameDistance()
    {
        var orbit = new OrbitCamera();
        var camera = orbit.Frame(Min, Max, FovY);

        camera = orbit.Orbit(camera, 1.2f, 0.3f);

        AssertLooksAt(orbit.Pivot, camera);
        Assert.Equal(orbit.Distance, (camera.Column3.xyz - orbit.Pivot).Length, 3);
        Assert.Equal(1.0f, camera.Column0.xyz.Length, 4);
    }

    [Theory]
    [InlineData(0.4f)]
    [InlineData(-0.4f)]
    public void OrbitingNeverGoesOverTheTopOrUnderTheBottom(float pitch)
    {
        var orbit = new OrbitCamera();
        var camera = orbit.Frame(Min, Max, FovY);

        for (var step = 0; step < 20; step++)
        {
            camera = orbit.Orbit(camera, 0, pitch);
        }

        Assert.True(MathF.Abs(camera.Column2.xyz.Normalized.y) <= 0.98f);
        Assert.True(camera.Column1.y > 0, "The camera turned upside down");
        AssertLooksAt(orbit.Pivot, camera);
    }

    [Fact]
    public void ZoomingStopsShortOfTheMiddle()
    {
        var orbit = new OrbitCamera();
        var camera = orbit.Frame(Min, Max, FovY);

        camera = orbit.Zoom(camera, 0);

        Assert.Equal(orbit.Radius * 0.05f, orbit.Distance, 5);
        AssertLooksAt(orbit.Pivot, camera);
    }

    [Fact]
    public void PanningMovesWhatTheCameraTurnsAround()
    {
        var orbit = new OrbitCamera();
        var camera = orbit.Frame(Min, Max, FovY);
        var before = camera.Column3.xyz;

        camera = orbit.Pan(camera, new vec3(1, 0, 0));

        Assert.Equal(new vec3(1, 2, 0), orbit.Pivot);
        Assert.True((camera.Column3.xyz - before - new vec3(1, 0, 0)).Length < 1e-4f);
    }
}
