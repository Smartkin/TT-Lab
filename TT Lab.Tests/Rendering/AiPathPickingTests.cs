using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Objects.Gizmo;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Rendering;

// An AI path is picked by a thin box along the line between its positions, wherever they've been moved to
public sealed class AiPathPickingTests
{
    private static AiPositionData Position(float x, float y, float z) => new(null!) { Coords = new Vector3(x, y, z) };

    private static float? Click(AiPathVisual visual, vec3 from) => GizmoMath.IntersectBox(new Ray(from, -vec3.UnitY), visual.GetBoundsTransform());

    [Fact]
    public void ClicksOnTheLinePickThePath()
    {
        var visual = new AiPathVisual(null!, "path");
        var begin = Position(0, 0, 0);
        var end = Position(10, 0, 10);
        visual.SetEnds(begin, end);

        Assert.NotNull(Click(visual, new vec3(5, 5, 5)));
        Assert.NotNull(Click(visual, new vec3(1, 5, 1)));
        Assert.Null(Click(visual, new vec3(5, 5, 6)));
        Assert.Null(Click(visual, new vec3(12, 5, 12)));

        // The box follows the positions
        end.Coords = new Vector3(-10, 0, 0);
        Assert.NotNull(Click(visual, new vec3(-5, 5, 0)));
        Assert.Null(Click(visual, new vec3(5, 5, 5)));
    }

    [Fact]
    public void APathMissingAnEndIsNeverPicked()
    {
        var visual = new AiPathVisual(null!, "path");
        visual.SetEnds(Position(0, 0, 0), null);

        Assert.Null(Click(visual, new vec3(0, 5, 0)));
    }
}
