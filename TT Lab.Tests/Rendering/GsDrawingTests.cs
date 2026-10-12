using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Materials;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;

namespace TT_Lab.Tests.Rendering;

// What the viewport draws the GS's way (the decomp's shadersettings.cpp and UpdateShader): the mip level by the distance, the alpha test
// against its reference byte, the scrolls
public class GsDrawingTests
{
    // Retail shaders' K is -69 sixteenths: the largest level up to 28 units, the next past them, then one more every time the distance doubles
    [Fact]
    public void TheMipLevelGoesByTheDistance()
    {
        var (l, k) = GsTextureFiltering.Lod(unchecked((ushort)-69), 0);
        Assert.Equal(0, l);
        Assert.Equal(-69 / 16.0f, k);
        Assert.Equal(0, GsTextureFiltering.Level(GsTextureFiltering.LevelAt(1.0f, l, k), 5));
        Assert.Equal(0, GsTextureFiltering.Level(GsTextureFiltering.LevelAt(28.0f, l, k), 5));
        Assert.Equal(1, GsTextureFiltering.Level(GsTextureFiltering.LevelAt(29.0f, l, k), 5));
        Assert.Equal(2, GsTextureFiltering.Level(GsTextureFiltering.LevelAt(60.0f, l, k), 5));
        // No further than the texture's last level
        Assert.Equal(4, GsTextureFiltering.Level(GsTextureFiltering.LevelAt(5000.0f, l, k), 5));
        Assert.Equal(0, GsTextureFiltering.Level(GsTextureFiltering.LevelAt(5000.0f, l, k), 1));
    }

    // The game ORs the halfwords into TEX1 sign extended: the NTSC version's 0xCDCD leftovers in L set K's every bit
    [Fact]
    public void ANegativeLRunsIntoK()
    {
        Assert.Equal((1, -1 / 16.0f), GsTextureFiltering.Lod(0xCDCD, 0xCDCD));
        Assert.Equal((2, 0.0f), GsTextureFiltering.Lod(0, 2));
    }

    [Fact]
    public void TheEnvironmentAndParticleTypesDrawTheLargestLevel()
    {
        Assert.True(GsTextureFiltering.MinifiesThroughMips(TwinShader.Type.StandardUnlit));
        Assert.True(GsTextureFiltering.MinifiesThroughMips(TwinShader.Type.LitSkinnedModel));
        Assert.True(GsTextureFiltering.MinifiesThroughMips(TwinShader.Type.UnlitEnvironmentMap));
        Assert.False(GsTextureFiltering.MinifiesThroughMips(TwinShader.Type.LitEnvironmentMap));
        Assert.False(GsTextureFiltering.MinifiesThroughMips(TwinShader.Type.UnlitSkydome));
        Assert.False(GsTextureFiltering.MinifiesThroughMips(TwinShader.Type.Particle));
        Assert.True(GsTextureFiltering.ClampsCoordinates(TwinShader.Type.LitMetallic));
        Assert.False(GsTextureFiltering.ClampsCoordinates(TwinShader.Type.StandardUnlit));
    }

    // The levels the PS2 build gives a texture: down to 8 pixels on the shorter side, none 256 pixels wide or of a size without a layout
    [Fact]
    public void TexturesHaveTheBuildsLevels()
    {
        Assert.Equal(5, PS2AnyTexture.LevelsOf(128, 128, true));
        Assert.Equal(4, PS2AnyTexture.LevelsOf(64, 64, true));
        Assert.Equal(4, PS2AnyTexture.LevelsOf(128, 64, true));
        Assert.Equal(1, PS2AnyTexture.LevelsOf(256, 256, true));
        Assert.Equal(1, PS2AnyTexture.LevelsOf(64, 64, false));
        Assert.Equal(1, PS2AnyTexture.LevelsOf(48, 48, true));
    }

    // The GS's alpha is TT Lab's times 127 rounded down (textures are twice what the GS gets, vertex alphas 0x7F the most), 128 being 1
    [Fact]
    public void TheAlphaTestComparesTheGsAlphaWithItsReference()
    {
        var geq = (int)TwinShader.AlphaTestMethod.GEQUAL;
        Assert.False(GsAlphaTest.Passes(geq, 0.39f, 50));
        Assert.True(GsAlphaTest.Passes(geq, 0.4f, 50));
        Assert.Equal(126, GsAlphaTest.GsAlpha(254 / 255.0f));
        Assert.False(GsAlphaTest.Passes((int)TwinShader.AlphaTestMethod.NEVER, 1.0f, 0));
        Assert.True(GsAlphaTest.Passes((int)TwinShader.AlphaTestMethod.LESS, 0.1f, 50));
        Assert.True(GsAlphaTest.Passes(GsAlphaTest.MethodOf(TwinShader.AlphaTest.OFF, TwinShader.AlphaTestMethod.NEVER), 0.0f, 255));
        Assert.True(GsAlphaTest.FailedDrawsColor(TwinShader.ProcessAfterAlphaTestFailed.FB_ONLY));
        Assert.False(GsAlphaTest.FailedDrawsColor(TwinShader.ProcessAfterAlphaTestFailed.ZB_ONLY));
    }

    [Fact]
    public void ScrollsWrapAndSway()
    {
        var phases = new vec2(0.25f, 0.5f);
        var speeds = new vec2(0.5f, 1.0f);
        var wrapped = ShaderScroll.At(TwinShader.XScrollFormula.Linear, TwinShader.YScrollFormula.Linear, phases, speeds, 2.0, vec2.Zero);
        Assert.Equal(0.25f, wrapped.x, 5);
        Assert.Equal(0.5f, wrapped.y, 5);

        var swayed = ShaderScroll.At(TwinShader.XScrollFormula.LinearPlus_1, TwinShader.YScrollFormula.Disabled, phases, speeds, 0.0, vec2.Zero);
        Assert.Equal(1.0f, swayed.x, 5);
        Assert.Equal(0.0f, swayed.y);
    }

    // U's cosine goes into V's offset, which V's own scroll takes over
    [Fact]
    public void TheCosineOfUMovesV()
    {
        var phases = new vec2(0.0f, 0.25f);
        var speeds = new vec2(0.0f, 0.0f);
        var cosine = ShaderScroll.At(TwinShader.XScrollFormula.LinearPlus_2, TwinShader.YScrollFormula.Disabled, phases, speeds, 0.0, vec2.Zero);
        Assert.Equal(0.0f, cosine.x);
        Assert.Equal(1.0f, cosine.y, 5);

        var overridden = ShaderScroll.At(TwinShader.XScrollFormula.LinearPlus_2, TwinShader.YScrollFormula.Linear, phases, speeds, 0.0, vec2.Zero);
        Assert.Equal(0.25f, overridden.y, 5);

        var animated = ShaderScroll.At(TwinShader.XScrollFormula.FromAnimation, TwinShader.YScrollFormula.FromAnimation, phases, speeds, 0.0, new vec2(0.1f, 0.2f));
        Assert.Equal(new vec2(0.1f, 0.2f), animated);
    }

    // A shader's own (A - B) * C + D as GL's factors: the presets' formulas come out as the presets do
    [Fact]
    public void CustomFormulasBlendLikeTheGs()
    {
        const TwinShader.ColorSpecMethod source = TwinShader.ColorSpecMethod.SOURCE;
        const TwinShader.ColorSpecMethod frame = TwinShader.ColorSpecMethod.FB;
        const TwinShader.ColorSpecMethod zero = TwinShader.ColorSpecMethod.ZERO;
        // (Cs - Cd) * As + Cd
        Assert.Equal((BlendEquationModeEXT.FuncAdd, BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha),
            GsBlending.Formula(source, frame, TwinShader.AlphaSpecMethod.SOURCE, frame));
        // Cd - Cs * As
        Assert.Equal((BlendEquationModeEXT.FuncReverseSubtract, BlendingFactor.SrcAlpha, BlendingFactor.One),
            GsBlending.Formula(zero, source, TwinShader.AlphaSpecMethod.SOURCE, frame));
        // Cs * FIX + Cd
        Assert.Equal((BlendEquationModeEXT.FuncAdd, BlendingFactor.ConstantAlpha, BlendingFactor.One),
            GsBlending.Formula(source, zero, TwinShader.AlphaSpecMethod.FIX, frame));
        // Cs - Cd * Ad
        Assert.Equal((BlendEquationModeEXT.FuncSubtract, BlendingFactor.One, BlendingFactor.DstAlpha),
            GsBlending.Formula(zero, frame, TwinShader.AlphaSpecMethod.FB, source));
        // (Cs - Cs) * As + Cs: the source as it is
        Assert.Equal((BlendEquationModeEXT.FuncAdd, BlendingFactor.One, BlendingFactor.Zero),
            GsBlending.Formula(source, source, TwinShader.AlphaSpecMethod.SOURCE, source));
    }

    // Leftover bytes that read as NaN move nothing
    [Fact]
    public void NonFiniteScrollsStayPut()
    {
        var offset = ShaderScroll.At(TwinShader.XScrollFormula.Linear, TwinShader.YScrollFormula.LinearPlus_1, new vec2(float.NaN, 0.0f), new vec2(1.0f, float.PositiveInfinity), 1.0, vec2.Zero);
        Assert.Equal(0.0f, offset.x);
        Assert.Equal(0.0f, offset.y);
    }
}
