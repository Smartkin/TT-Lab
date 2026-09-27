using GlmSharp;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Rendering;

public sealed class LoadWallTests
{
    // Corners go around the wall from the bottom left like the game's walls
    private static Matrix4 Wall(vec3 bottomLeft, vec3 bottomRight, vec3 topRight, vec3 topLeft)
    {
        return new mat4(new vec4(bottomLeft, 1), new vec4(bottomRight, 1), new vec4(topRight, 1), new vec4(topLeft, 1)).ToTwin();
    }

    private static void AssertCorner(vec3 expected, Vector4 actual)
    {
        Assert.True((expected - new vec3(actual.X, actual.Y, actual.Z)).Length < 1e-4f, $"Expected {expected} but got ({actual.X}, {actual.Y}, {actual.Z})");
        Assert.Equal(1.0f, actual.W);
    }

    [Fact]
    public void WallsGetTheRectangleBetweenTheirCornersAsTransform()
    {
        var corners = new LoadWallCorners();

        var transform = corners.ToTransform(Wall(new vec3(-26, -14, -120), new vec3(2, -14, -120), new vec3(2, 14, -120), new vec3(-26, 14, -120)));

        Assert.Equal(new vec3(-12, 0, -120), transform.Column3.xyz);
        Assert.Equal(new vec3(14, 0, 0), transform.Column0.xyz);
        Assert.Equal(new vec3(0, 14, 0), transform.Column1.xyz);
        Assert.Equal(new vec3(0, 0, 1), transform.Column2.xyz);
    }

    [Fact]
    public void MovingTheTransformMovesTheCorners()
    {
        var corners = new LoadWallCorners();
        var transform = corners.ToTransform(Wall(new vec3(-1, 0, 0), new vec3(1, 0, 0), new vec3(1, 2, 0), new vec3(-1, 2, 0)));

        var moved = corners.ToData(mat4.Translate(5, 0, 0) * transform * mat4.Scale(2, 1, 1));

        AssertCorner(new vec3(3, 0, 0), moved.Column1);
        AssertCorner(new vec3(7, 0, 0), moved.Column2);
        AssertCorner(new vec3(7, 2, 0), moved.Column3);
        AssertCorner(new vec3(3, 2, 0), moved.Column4);
    }

    [Fact]
    public void WallsThatArentRectanglesKeepTheirShape()
    {
        var corners = new LoadWallCorners();
        var wall = Wall(new vec3(0, 0, 0), new vec3(4, 0, 0), new vec3(3, 3, 0), new vec3(0.5f, 2, 0));
        var transform = corners.ToTransform(wall);

        var turnAndMove = mat4.Translate(0, 0, 10) * quat.FromAxisAngle(MathF.PI / 2, vec3.UnitY).ToMat4 * mat4.Translate(-transform.Column3.xyz);

        var turned = corners.ToData(turnAndMove * transform);

        for (var i = 0; i < 4; i++)
        {
            AssertCorner((turnAndMove * new vec4(wall[i].ToGlm().xyz, 1)).xyz, turned[i]);
        }
    }

    [Fact]
    public void WallsWithoutAnAreaArentUsable()
    {
        Assert.False(LoadWallCorners.IsUsable(null));
        Assert.False(LoadWallCorners.IsUsable(mat4.Zero.ToTwin()));
        Assert.True(LoadWallCorners.IsUsable(Wall(new vec3(0, 0, 0), new vec3(1, 0, 0), new vec3(1, 1, 0), new vec3(0, 1, 0))));
    }
}
