using GlmSharp;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects.Gizmo;

namespace TT_Lab.Tests.Rendering;

// The objects selected along with the gizmo's follow its drag: the same offset, and turns and scales about the first object's place
public sealed class GroupTransformTests
{
    private static readonly GizmoTransform Start = new(new vec3(10, 0, 0), quat.Identity, vec3.Ones);
    private static readonly GroupTransform.Placement Other = new(new vec3(12, 0, 0), quat.Identity, new vec3(2, 2, 2));

    private static void AssertClose(vec3 expected, vec3 actual)
    {
        Assert.True((expected - actual).Length < 1e-4f, $"expected {expected}, got {actual}");
    }

    [Fact]
    public void MovingMovesTheOthersByTheSameOffset()
    {
        var moved = GroupTransform.Apply(TransformMode.TRANSLATE, Start, Start with { Position = new vec3(10, 5, -3) }, Other);

        AssertClose(new vec3(12, 5, -3), moved.Position);
        Assert.Equal(Other.Rotation, moved.Rotation);
        Assert.Equal(Other.Scale, moved.Scale);
    }

    [Fact]
    public void TurningOrbitsTheOthersAroundTheFirst()
    {
        var quarter = quat.FromAxisAngle(glm.Radians(90.0f), vec3.UnitY);
        var moved = GroupTransform.Apply(TransformMode.ROTATE, Start, Start with { Rotation = quarter }, Other);

        // Two units along X from the pivot end up two units along -Z after a quarter turn about Y
        AssertClose(new vec3(10, 0, -2), moved.Position);
        AssertClose(quarter * vec3.UnitX, moved.Rotation * vec3.UnitX);
        Assert.Equal(Other.Scale, moved.Scale);
    }

    [Fact]
    public void ScalingScalesTheOthersAndTheirDistanceToTheFirst()
    {
        var moved = GroupTransform.Apply(TransformMode.SCALE, Start, Start with { Scale = new vec3(3, 3, 3) }, Other);

        AssertClose(new vec3(16, 0, 0), moved.Position);
        AssertClose(new vec3(6, 6, 6), moved.Scale);
    }

    [Fact]
    public void MatricesGetTheSameChange()
    {
        var matrix = mat4.Translate(12, 0, 0);
        var moved = GroupTransform.ApplyToMatrix(TransformMode.TRANSLATE, Start, Start with { Position = new vec3(10, 5, 0) }, matrix);
        AssertClose(new vec3(12, 5, 0), moved.Column3.xyz);

        var turned = GroupTransform.ApplyToMatrix(TransformMode.ROTATE, Start, Start with { Rotation = quat.FromAxisAngle(glm.Radians(90.0f), vec3.UnitY) }, matrix);
        AssertClose(new vec3(10, 0, -2), turned.Column3.xyz);

        var scaled = GroupTransform.ApplyToMatrix(TransformMode.SCALE, Start, Start with { Scale = new vec3(2, 2, 2) }, matrix);
        AssertClose(new vec3(14, 0, 0), scaled.Column3.xyz);
        AssertClose(new vec3(2, 0, 0), scaled.Column0.xyz);
    }
}
