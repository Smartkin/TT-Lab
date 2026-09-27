using GlmSharp;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Scene;

namespace TT_Lab.Tests.Rendering;

// Bones only keep their render context, the math runs without one
public sealed class TwinBoneTests
{
    private static readonly quat Turn = quat.FromAxisAngle((float)(System.Math.PI / 2), vec3.UnitY);

    private static void AssertVector(vec3 expected, vec3 actual)
    {
        Assert.True((expected - actual).Length < 1e-4f, $"Expected {expected} but got {actual}");
    }

    private static vec3 Sizes(mat4 transform)
    {
        return new vec3(transform.Column0.xyz.Length, transform.Column1.xyz.Length, transform.Column2.xyz.Length);
    }

    private static (TwinBone Parent, TwinBone Child) Chain()
    {
        var root = new TwinBone(null!, new Node(null!));
        var parent = new TwinBone(null!, root);
        var child = new TwinBone(null!, parent);
        return (parent, child);
    }

    // OGI 393's first animation: the joint above everything at 0.3, each part at 0.32 without inheriting it, which ends on the bind
    // pose at that size
    [Fact]
    public void BonesThatDontInheritScaleStillMoveWithTheirParentsScale()
    {
        var (parent, child) = Chain();
        parent.SetPose(new vec3(0, 2, 0), Turn, new vec3(0.3f));
        child.SetInheritScale(false);

        child.SetPose(new vec3(0, 0, 1), quat.Identity, new vec3(0.32f));

        AssertVector(new vec3(0.3f, 2, 0), child.WorldTransform.Column3.xyz);
        AssertVector(new vec3(0.32f), Sizes(child.WorldTransform));
        AssertVector(new vec3(0, 0, -0.32f), child.WorldTransform.Column0.xyz);
    }

    [Fact]
    public void BonesThatInheritScaleGetTheirParentsScale()
    {
        var (parent, child) = Chain();
        parent.SetPose(vec3.Zero, quat.Identity, new vec3(0.5f));

        child.SetPose(new vec3(0, 0, 1), quat.Identity, new vec3(2));

        AssertVector(new vec3(0, 0, 0.5f), child.WorldTransform.Column3.xyz);
        AssertVector(new vec3(1), Sizes(child.WorldTransform));
    }

    // Animations hide joints by scaling them to nothing, the joints under them that don't inherit scale still show turned with them
    [Fact]
    public void BonesUnderAHiddenParentKeepTheirSizeAndTurnWithIt()
    {
        var (parent, child) = Chain();
        parent.SetPose(new vec3(1, 0, 0), Turn, vec3.Zero);
        child.SetInheritScale(false);

        child.SetPose(new vec3(0, 0, 1), quat.Identity, vec3.Ones);

        AssertVector(new vec3(1, 0, 0), child.WorldTransform.Column3.xyz);
        AssertVector(new vec3(0, 0, -1), child.WorldTransform.Column0.xyz);
        AssertVector(new vec3(1, 0, 0), child.WorldTransform.Column2.xyz);
    }

    [Fact]
    public void ResettingThePoseGoesBackToTheRestPose()
    {
        var (parent, child) = Chain();
        parent.SetRest(new vec3(0, 1, 0), Turn);
        child.SetRest(new vec3(0, 0, 2), quat.Identity);
        parent.ResetPose();
        child.ResetPose();
        var rest = child.WorldTransform;
        child.SetInheritScale(false);
        parent.SetPose(vec3.Zero, quat.Identity, new vec3(0.1f));
        child.SetPose(vec3.Zero, quat.Identity, new vec3(0.1f));

        parent.ResetPose();
        child.ResetPose();

        Assert.Equal(rest, child.WorldTransform);
        AssertVector(new vec3(2, 1, 0), child.WorldTransform.Column3.xyz);
    }
}
