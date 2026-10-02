using GlmSharp;
using TT_Lab.Rendering.Objects;

namespace TT_Lab.Tests.Rendering;

// A dynamic scenery model's box is kept in the model's space, the game's models move far from it with their animation
public class DynamicSceneryBoundsTests
{
    [Fact]
    public void TheCubeBecomesTheBox()
    {
        var box = DynamicSceneryBounds.BoxTransform(new vec3(-1.4f, -17.4f, -0.9f), new vec3(0.6f, 0.9f, 0.9f));

        AssertClose(new vec3(-1.4f, -17.4f, -0.9f), (box * new vec4(-1, -1, -1, 1)).xyz);
        AssertClose(new vec3(0.6f, 0.9f, 0.9f), (box * new vec4(1, 1, 1, 1)).xyz);
        AssertClose(new vec3(-0.4f, -8.25f, 0.0f), (box * new vec4(0, 0, 0, 1)).xyz);
    }

    [Fact]
    public void TheBoxFollowsTheModel()
    {
        var model = mat4.Translate(new vec3(-11.7f, -15.3f, 0.7f)) * quat.FromAxisAngle(MathF.PI / 2, vec3.UnitY).ToMat4;
        var box = model * DynamicSceneryBounds.BoxTransform(new vec3(0, 0, 0), new vec3(2, 1, 1));

        // The box's corner at the model's +X turned a quarter about Y goes to the model's -Z
        AssertClose(new vec3(-11.7f, -14.3f, 0.7f - 2.0f), (box * new vec4(1, 1, -1, 1)).xyz);
    }

    private static void AssertClose(vec3 expected, vec3 actual)
    {
        Assert.True((expected - actual).Length < 1e-4f, $"{actual} isn't {expected}");
    }
}
