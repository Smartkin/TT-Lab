using GlmSharp;
using TT_Lab.Rendering;

namespace TT_Lab.Tests.Rendering;

public sealed class FlyCameraTests
{
    private static readonly mat4 Level = mat4.Translate(new vec3(3, 4, 5));

    private static vec3 Forward(mat4 camera) => -camera.Column2.xyz.Normalized;

    private static void AssertClose(vec3 expected, vec3 actual)
    {
        Assert.True((expected - actual).Length < 1e-4f, $"Expected {expected}, got {actual}");
    }

    [Fact]
    public void TurnsAroundTheWorldsUpAndItsOwnSide()
    {
        var camera = FlyCamera.Look(Level, 0.3f, 0.2f);

        var expected = (quat.FromAxisAngle(0.3f, vec3.UnitY) * quat.FromAxisAngle(0.2f, vec3.UnitX)).ToMat4;
        AssertClose(expected.Column0.xyz, camera.Column0.xyz);
        AssertClose(expected.Column1.xyz, camera.Column1.xyz);
        AssertClose(expected.Column2.xyz, camera.Column2.xyz);
        Assert.Equal(Level.Column3, camera.Column3);
    }

    [Theory]
    [InlineData(0.4f)]
    [InlineData(-0.4f)]
    public void StopsLookingStraightUpOrDown(float pitch)
    {
        var camera = FlyCamera.Look(Level, 0.7f, 0);

        for (var step = 0; step < 20; step++)
        {
            camera = FlyCamera.Look(camera, 0, pitch);
        }

        AssertClose(vec3.UnitY * MathF.Sign(pitch), Forward(camera));
        Assert.Equal(0.0f, camera.Column0.y, 5);
        Assert.Equal(1.0f, camera.Column0.xyz.Length, 5);

        camera = FlyCamera.Look(camera, 0, -pitch);

        Assert.Equal(MathF.Sign(pitch) * MathF.Cos(0.4f), Forward(camera).y, 4);
        Assert.True(camera.Column1.y > 0, "The camera turned upside down");
    }

    [Fact]
    public void TurnsAroundWhileLookingStraightDown()
    {
        var camera = FlyCamera.Look(Level, 0, -2.0f);

        camera = FlyCamera.Look(camera, 0.5f, 0);
        camera = FlyCamera.Look(camera, 0, 0.5f);

        var expected = FlyCamera.Look(FlyCamera.Look(Level, 0.5f, 0), 0, -MathF.PI * 0.5f + 0.5f);
        AssertClose(Forward(expected), Forward(camera));
        AssertClose(expected.Column0.xyz, camera.Column0.xyz);
    }

    [Fact]
    public void ACameraTurnedOverGetsTurnedBackUpLookingTheSameWay()
    {
        var overTheTop = quat.FromAxisAngle(0.4f, vec3.UnitY) * quat.FromAxisAngle(MathF.PI - 0.3f, vec3.UnitX);
        var turnedOver = overTheTop.ToMat4 with { Column3 = Level.Column3 };

        var camera = FlyCamera.Look(turnedOver, 0, 0);

        AssertClose(Forward(turnedOver), Forward(camera));
        Assert.True(camera.Column1.y > 0, "The camera is still upside down");
        Assert.Equal(0.0f, camera.Column0.y, 5);
    }
}
