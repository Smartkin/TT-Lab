using GlmSharp;
using TT_Lab.Rendering;

namespace TT_Lab.Tests.Rendering;

// The game's environment and metallic shaders read their pictures by directions taken through the object's clip matrix (VU1 programs
// 0x1D, 0x14 and 0x17): on the camera's axes scaled by the game's lens, not by the scene's lights
public class EnvironmentMappingTests
{
    private static readonly vec3 Eye = new(0, 0, 10);
    // Looking at the origin along -Z: GL's view x is the world's X, which TT Lab shows on the left (its frame is shown mirrored)
    private static readonly mat4 View = mat4.LookAt(Eye, vec3.Zero, vec3.UnitY);

    private static void AssertClose(vec2 expected, vec2 actual)
    {
        Assert.Equal(expected.x, actual.x, 4);
        Assert.Equal(expected.y, actual.y, 4);
    }

    [Fact]
    public void TheLensIsTheFollowCamerasOnAFourByThreeScreen()
    {
        Assert.Equal(1.0f + MathF.Sqrt(2.0f), EnvironmentMapping.ClipScale.y, 5);
        Assert.Equal(EnvironmentMapping.ClipScale.y * 0.75f, EnvironmentMapping.ClipScale.x, 5);
    }

    [Fact]
    public void ASurfaceFacingTheCameraReadsThePicturesMiddle()
    {
        AssertClose(new vec2(0.5f, 0.5f), EnvironmentMapping.EnvironmentUv(View, Eye, vec3.Zero, vec3.UnitZ));
        AssertClose(new vec2(0.5f, 0.5f), EnvironmentMapping.MetallicUv(View, Eye, vec3.Zero, vec3.UnitZ));
    }

    // The half vector of the way to the eye and a normal turned 45 degrees, on the screen's axes scaled by the lens
    [Fact]
    public void TheEnvironmentMapReadsWhereTheNormalTurnsOnTheScreen()
    {
        var shownRight = EnvironmentMapping.EnvironmentUv(View, Eye, vec3.Zero, new vec3(-1, 0, 1).Normalized);
        var half = new vec3(MathF.Sqrt(0.5f) * EnvironmentMapping.ClipScale.x, 0, -(1 + MathF.Sqrt(0.5f))).Normalized;
        AssertClose(new vec2(0.5f + 0.5f * half.x, 0.5f), shownRight);

        // Up is towards V's start
        var up = EnvironmentMapping.EnvironmentUv(View, Eye, vec3.Zero, new vec3(0, 1, 1).Normalized);
        Assert.Equal(0.5f, up.x, 4);
        Assert.True(up.y < 0.2f);
    }

    // The way to the eye reflected off a normal turned up is up: not normalized after the lens, it's clamped to the picture's edge, and up
    // is towards V's end, the other way from the environment maps
    [Fact]
    public void TheMetallicShaderReadsWhereTheReflectionGoesClamped()
    {
        AssertClose(new vec2(0.5f, 1.0f), EnvironmentMapping.MetallicUv(View, Eye, vec3.Zero, new vec3(0, 1, 1).Normalized));
        Assert.True(EnvironmentMapping.MetallicUv(View, Eye, vec3.Zero, new vec3(-0.1f, 0, 1).Normalized).x > 0.5f);
    }

    // The same surface seen from the side reads another part of the picture
    [Fact]
    public void TheLookupTurnsWithTheView()
    {
        var sideEye = new vec3(10, 0, 0);
        var sideView = mat4.LookAt(sideEye, vec3.Zero, vec3.UnitY);

        AssertClose(new vec2(0.5f, 0.5f), EnvironmentMapping.EnvironmentUv(sideView, sideEye, vec3.Zero, vec3.UnitX));
        var seen = EnvironmentMapping.EnvironmentUv(sideView, sideEye, vec3.Zero, vec3.UnitZ);
        Assert.True(seen.x > 0.5f, $"{seen}");
        Assert.Equal(0.5f, seen.y, 4);
    }
}
