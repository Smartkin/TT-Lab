using GlmSharp;
using TT_Lab.AssetData.Code;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Scene;
using Twinsanity.TwinsanityInterchange.Common;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.Tests.Rendering;

// The skeleton the viewer poses and skins with, against what the game does: its joints rest at their bind poses, the root's
// included, and a vertex follows its joint from where the bind pose has it
public sealed class TwinSkeletonTests
{
    private static readonly quat Turn = quat.FromAxisAngle((float)(System.Math.PI / 2), vec3.UnitY);

    private static void AssertVector(vec3 expected, vec3 actual)
    {
        Assert.True((expected - actual).Length < 1e-4f, $"Expected {expected} but got {actual}");
    }

    private static TwinJoint Joint(int index, int parent, vec3 translation) => new()
    {
        Index = index,
        ParentIndex = parent,
        Id = 0xFF,
        LocalTranslation = new Vector4(translation.x, translation.y, translation.z, 1),
        LocalRotation = new Vector4(0, 0, 0, 1),
        AdditionalAnimationRotation = new Vector4(0, 0, 0, 1)
    };

    private static TwinSkeleton SkeletonOf(params TwinJoint[] joints)
    {
        var ogi = new OGIData(null!) { Joints = [.. joints] };
        return new TwinSkeletonManager(null!).CreateSceneNodeSkeleton(new Node(null!), ogi);
    }

    private static vec3 Skinned(TwinBone bone, vec3 vertex) => (bone.GetBoneMatrix() * new vec4(vertex, 1)).xyz;

    // A character's hips above its feet: Reimu's animations tore her apart in the viewer, every limb off by her hips' height turned
    // with the limb
    [Fact]
    public void AnAnimatedModelsRootRestsAtItsBindPoseAndItsPartsFollowTheirJoints()
    {
        var skeleton = SkeletonOf(Joint(0, -1, new vec3(0, 1, 0)), Joint(1, 0, new vec3(0.5f, 0, 0)));
        var root = skeleton.Bones[0];
        var arm = skeleton.Bones[1];

        AssertVector(new vec3(0, 1, 0), root.WorldTransform.Column3.xyz);
        AssertVector(new vec3(0.5f, 1, 0), Skinned(arm, new vec3(0.5f, 1, 0)));

        root.SetPose(new vec3(0, 1, 0), Turn, vec3.Ones);
        arm.SetPose(new vec3(0.5f, 0, 0), quat.Identity, vec3.Ones);

        AssertVector(new vec3(0, 1, -0.5f), Skinned(arm, new vec3(0.5f, 1, 0)));
        AssertVector(new vec3(0, 1, 0), Skinned(root, new vec3(0, 1, 0)));
    }

    // ModelNode::SetOgi gives a model of one joint and no exit points no animator, and the game draws its rigid models at the instance
    [Fact]
    public void AModelWithoutAnAnimatorStaysAtItsInstance()
    {
        var root = SkeletonOf(Joint(0, -1, new vec3(0, 2, 0))).Bones[0];

        AssertVector(vec3.Zero, root.WorldTransform.Column3.xyz);
    }

    [Fact]
    public void AnExitPointGivesAModelOfOneJointAnAnimator()
    {
        var ogi = new OGIData(null!) { Joints = [Joint(0, -1, new vec3(0, 2, 0))], ExitPoints = [new TwinExitPoint()] };

        var root = new TwinSkeletonManager(null!).CreateSceneNodeSkeleton(new Node(null!), ogi).Bones[0];

        AssertVector(new vec3(0, 2, 0), root.WorldTransform.Column3.xyz);
    }
}
