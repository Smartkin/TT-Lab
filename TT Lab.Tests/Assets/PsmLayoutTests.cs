using TT_Lab.AssetData.Global;

namespace TT_Lab.Tests.Assets;

// The game's pictures are 8 tiles 4 across (the level titles one tile), Icons.psm's parts are pictures of their own. Every part is
// kept upside down
public class PsmLayoutTests
{
    private static (int, int)[] Sizes(int count, int width, int height) => Enumerable.Repeat((width, height), count).ToArray();

    [Fact]
    public void EightTilesMakeAPictureFourAcross()
    {
        var layout = PsmLayout.Of("Loading1", Sizes(8, 128, 256));

        Assert.True(layout.IsPicture);
        Assert.Equal((512, 512), (layout.Width, layout.Height));
        Assert.Equal(new PsmPartPlace(5, 128, 256, 128, 256), layout.Parts[5]);
        Assert.Equal(new PsmPartPlace(3, 384, 0, 128, 256), layout.Parts[3]);

        var title = PsmLayout.Of("Level01", Sizes(1, 128, 128));
        Assert.True(title.IsPicture);
        Assert.Equal((128, 128), (title.Width, title.Height));
    }

    [Fact]
    public void TheIconsArePicturesOfTheirOwn()
    {
        var sizes = Sizes(39, 64, 64).Append((32, 32)).ToArray();

        var icons = PsmLayout.Of("Icons", sizes);

        Assert.False(icons.IsPicture);
        // A sheet of 8 across, every icon in the middle of a cell the size of the biggest
        Assert.Equal((8 * 64 + 7 * PsmLayout.SheetGap, 5 * 64 + 4 * PsmLayout.SheetGap), (icons.Width, icons.Height));
        Assert.Equal(new PsmPartPlace(39, 7 * (64 + PsmLayout.SheetGap) + 16, 4 * (64 + PsmLayout.SheetGap) + 16, 32, 32), icons.Parts[39]);
        // Even all of one size, the game draws each on its own
        Assert.False(PsmLayout.Of("ICONS", Sizes(40, 64, 64)).IsPicture);
        Assert.False(PsmLayout.Of("Odd", Sizes(3, 64, 64)).IsPicture);
    }

    [Fact]
    public void PartsAreShownTheRightWayUpAndCutBackTheGamesWay()
    {
        var layout = PsmLayout.Of("Credit01", Sizes(8, 2, 3));
        var parts = Enumerable.Range(0, 8).Select(part => Enumerable.Range(0, 6).Select(pixel => (uint)(part * 100 + pixel)).ToArray()).ToList();

        var picture = layout.Compose(parts.Cast<uint[]?>().ToList());

        // The part's first row is the bottom of its place
        Assert.Equal(400u + 0, picture[(3 + 2) * layout.Width + 0]);
        Assert.Equal(400u + 4, picture[(3 + 0) * layout.Width + 0]);
        Assert.All(Enumerable.Range(0, 8), part => Assert.Equal(parts[part], layout.Cut(picture, part)));
        Assert.Equal(new uint[] { 4, 5, 2, 3, 0, 1 }, PsmLayout.Flip([0, 1, 2, 3, 4, 5], 2, 3));
    }
}
