using TT_Lab.Assets.Global;
using TT_Lab.Rendering.Objects;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;

namespace TT_Lab.Tests.Rendering;

// The save icon's viewer draws its triangles the way its model file has them, upright with the texture's UVs and the corners' colors,
// its animation's frames the way PS2IODB's player (timed against the PS2 BIOS) makes them: the shapes times their weights over the
// weights' sum
public class SaveIconPreviewTests
{
    [Fact]
    public void SaveIconsHaveAViewport()
    {
        Assert.True(new SaveIcon().SupportsViewport);
    }

    [Fact]
    public void TheIconStandsUprightAsItsFirstFrameHasIt()
    {
        var icon = TestAssets.MakeSaveIcon();

        var vertexes = SaveIconPreview.GetVertexes(icon);
        var (min, max) = SaveIconPreview.GetBounds(icon);

        Assert.Equal(12, vertexes.Count);
        // The roof's top (Y -12288, going down) is 3 units up, the second shape raises it to 3.5 and the box holds both
        Assert.Equal(3.0f, vertexes.Max(vertex => vertex.Position.Y));
        Assert.Equal([-1.0f, 0.0f, -0.5f], new[] { min.x, min.y, min.z });
        Assert.Equal([1.0f, 3.5f, 0.0f], new[] { max.x, max.y, max.z });
        var first = vertexes[0];
        Assert.Equal((-1.0f, 0.0f, 0.0f), (first.Position.X, first.Position.Y, first.Position.Z));
        // Facing the icon's -Z, the file's +Z
        Assert.Equal((0.0f, 0.0f, 1.0f), (first.Normal.X, first.Normal.Y, first.Normal.Z));
        Assert.Equal((0.0f, 1.0f), (first.UV.X, first.UV.Y));
        Assert.Equal(0xBF / 255.0f, first.Color.X);
        Assert.Equal(1.0f, first.Color.W);
    }

    [Fact]
    public void ShapesTakeTheirWeightsShareOfTheCorners()
    {
        // The first shape's weight goes from 1 to 0 at frame 30 and back at 60, the second's the other way
        var icon = TestAssets.MakeSaveIcon();

        Assert.Equal([1.0f, 0.0f], SaveIconPreview.ShapeWeightsAt(icon, 0));
        Assert.Equal([0.5f, 0.5f], SaveIconPreview.ShapeWeightsAt(icon, 15));
        Assert.Equal([0.0f, 1.0f], SaveIconPreview.ShapeWeightsAt(icon, 30));
        // Past the last key it holds
        Assert.Equal([1.0f, 0.0f], SaveIconPreview.ShapeWeightsAt(icon, 90));
        // At frame 30 the roof's top is the second shape's
        var weights = SaveIconPreview.ShapeWeightsAt(icon, 30);
        Assert.Equal(3.5f, icon.Vertexes.Max(corner => SaveIconPreview.PositionAt(corner, weights).Y));

        // Weights that don't add up to 1 are shares of their sum, weights of nothing leave the first shape
        icon.Frames[0].Keys = [new SaveIconKey(0, 2)];
        icon.Frames[1].Keys = [new SaveIconKey(0, 6)];
        Assert.Equal([0.25f, 0.75f], SaveIconPreview.ShapeWeightsAt(icon, 10));
        icon.Frames[1].Keys = [new SaveIconKey(0, -2)];
        Assert.Equal([1.0f, 0.0f], SaveIconPreview.ShapeWeightsAt(icon, 10));
    }
}
