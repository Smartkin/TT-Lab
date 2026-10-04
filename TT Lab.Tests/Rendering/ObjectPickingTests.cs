using GlmSharp;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Objects.Gizmo;

namespace TT_Lab.Tests.Rendering;

// Objects are clicked by their model's box. The beach's butterflies have a box of no height, which no ray hit: only the rubber band,
// going by the box's middle, selected them
public sealed class ObjectPickingTests
{
    private static EditableObject Butterfly(vec3 position)
    {
        var butterfly = new EditableObject(null!, null, "butterfly", new vec3(-0.352243f, -1.5174533e-16f, -0.2530629f), new vec3(0.704486f, 3.0349065e-16f, 0.5061259f));
        butterfly.Init();
        butterfly.SetPosition(position);
        return butterfly;
    }

    [Fact]
    public void FlatBoxesArePickedByAClick()
    {
        var bounds = Butterfly(new vec3(10, 2, 10)).GetBoundsTransform();

        Assert.InRange(GizmoMath.IntersectBox(new Ray(new vec3(10, 5, 10), -vec3.UnitY), bounds)!.Value, 2.99f, 3.0f);
        Assert.NotNull(GizmoMath.IntersectBox(new Ray(new vec3(10.3f, 0, 10.2f), vec3.UnitY), bounds));
        var slanted = (new vec3(10.1f, 2, 9.9f) - new vec3(15, 4, 5)).Normalized;
        Assert.NotNull(GizmoMath.IntersectBox(new Ray(new vec3(15, 4, 5), slanted), bounds));
        Assert.Null(GizmoMath.IntersectBox(new Ray(new vec3(10.5f, 5, 10), -vec3.UnitY), bounds));
    }

    // The thickness goes around the flat box, its middle stays where the rubber band and the camera look for it
    [Fact]
    public void AFlatBoxKeepsItsMiddle()
    {
        var bounds = Butterfly(new vec3(10, 2, 10)).GetBoundsTransform();

        Assert.True((bounds.Column3.xyz - new vec3(10, 2, 10)).Length < 1e-5f);
        Assert.InRange(bounds.Column1.xyz.Length, 0.004f, 0.006f);
        Assert.Equal(0.352243f, bounds.Column0.xyz.Length, 5);
    }
}
