using TT_Lab.Rendering;

namespace TT_Lab.Tests.Rendering;

// The PS2 version draws the frame over itself through the chunk's table by the depth (FilterColours), the Xbox version blends a ramp of the
// same tables by the clip depth in a texture stage (DrawToTexture)
public class GameFogTests
{
    // The amounts go to the shaders as bytes
    private const float OneTexelStep = 1.0f / 255.0f;

    [Fact]
    public void TheTablesAreTheExecutables()
    {
        Assert.Equal(((byte)52, (byte)0, (byte)247, (byte)90), GameFog.Entry(0, 0));
        Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)128), GameFog.Entry(0, 255));
        Assert.Equal(((byte)131, (byte)147, (byte)202, (byte)77), GameFog.Entry(7, 1));
        Assert.Equal(((byte)237, (byte)41, (byte)3, (byte)128), GameFog.Entry(7, 255));
        Assert.Equal(((byte)165, (byte)235, (byte)255, (byte)112), GameFog.Entry(2, 128));
        // The sixth leaves the picture as it is, and so does a table past the eighth
        Assert.Equal(128, GameFog.Entry(6, 0).A);
        Assert.Equal(128, GameFog.Entry(9, 0).A);
    }

    [Fact]
    public void ThePs2FilterLeavesWhatsNearAlone()
    {
        Assert.Equal(-1, GameFog.Ps2Index(1.0f));
        Assert.Equal(-1, GameFog.Ps2Index(25.0f));
        Assert.InRange(GameFog.Ps2Index(26.0f), 200, 255);
        Assert.Equal(GameFog.Ps2Depth(GameFog.GameNear), 8388607.0f * 2.0f, 1.0f);
        Assert.Equal(0.0f, GameFog.Ps2Depth(GameFog.GameFar), 1e-3f);
    }

    // The depth halves as the distance doubles: its bits 8 to 15 are the entry, past the far plane the first
    [Fact]
    public void ThePs2FilterPicksTheEntryByTheDepthsSecondByte()
    {
        Assert.Equal(126, GameFog.Ps2Index(50.0f));
        Assert.Equal(61, GameFog.Ps2Index(100.0f));
        Assert.Equal(28, GameFog.Ps2Index(200.0f));
        Assert.Equal(2, GameFog.Ps2Index(1000.0f));
        Assert.Equal(0, GameFog.Ps2Index(GameFog.GameFar + 100.0f));

        var fog = new GameFog(1, false);
        Assert.Equal(0.0f, fog.At(10.0f).w);
        // 23% at a hundred units, 40% at a thousand
        Assert.Equal(1.0f - 98.0f / 128.0f, fog.At(100.0f).w, OneTexelStep);
        Assert.Equal(1.0f - 77.0f / 128.0f, fog.At(1000.0f).w, OneTexelStep);
        // The sky and the cleared background have the depth 0
        Assert.Equal(1.0f - 90.0f / 128.0f, fog.Sky.w, OneTexelStep);
    }

    [Fact]
    public void TheTableThatLeavesThePictureAloneHasNoFog()
    {
        var fog = GameFog.Of(6, false);
        Assert.Equal(0.0f, fog.At(1000.0f).w);
        Assert.Equal(0.0f, fog.Sky.w);
        Assert.Equal(0.0f, GameFog.Of(12, true).Sky.w);
    }

    // The Xbox's tables are the PS2's as the GS keeps a palette (the second and third 8 of every 32 swapped), entry 0's alpha 77
    [Fact]
    public void TheXboxsTablesAreInThePs2sPaletteOrder()
    {
        Assert.Equal(GameFog.Entry(7, 16), GameFog.XboxEntry(7, 8));
        Assert.Equal(GameFog.Entry(7, 8), GameFog.XboxEntry(7, 16));
        Assert.Equal(GameFog.Entry(7, 100), GameFog.XboxEntry(7, 100));
        Assert.Equal(77, GameFog.XboxEntry(0, 0).A);
        Assert.Equal(128, GameFog.XboxEntry(6, 0).A);
    }

    // The ramp's first texel is the table's last entry (alpha 128, nothing), its last the first 16 entries' (0.5 - a/256) on average,
    // reached 50 units away
    [Fact]
    public void TheXboxsFogIsHalfAsStrongAndWholeFrom50Units()
    {
        var fog = new GameFog(4, true);
        Assert.Equal(0.0f, fog.At(GameFog.GameNear).w);
        Assert.Equal(255, GameFog.XboxTexel(50.5f));
        Assert.Equal(255, GameFog.XboxTexel(500.0f));
        Assert.InRange(GameFog.XboxTexel(25.0f), 126, 128);
        var mean = Enumerable.Range(0, 16).Average(index => 0.5 - GameFog.XboxEntry(4, index).A / 256.0);
        Assert.Equal((float)mean, fog.Sky.w, OneTexelStep);
        Assert.Equal(fog.Sky, fog.At(1000.0f));
        Assert.InRange(fog.Sky.w, 0.18f, 0.2f);
    }
}
