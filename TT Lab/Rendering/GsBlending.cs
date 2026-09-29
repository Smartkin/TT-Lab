using Silk.NET.OpenGL;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering;

/// <summary>
/// The GS's alpha blending as GL's. The GS blends ((A - B) * C >> 7) + D, the game's presets (<c>G_AlphaRegPresets</c>, filled by
/// <c>FUN_001daa68</c>) are 0 (Cs - Cd) * As + Cd, 1 Cs * As + Cd, 2 Cd - Cs * As, 3 Cd * As + Cd, 4 Cd - Cd * As and 5 Cd * As.
/// The PS2 version's table has zeros past them, (Cs - Cs) * As + Cs: the source as it is. The Xbox version's presets past 6 aren't
/// known and blend like 0
/// </summary>
public static class GsBlending
{
    /// <summary>
    /// Sets the blend equation and factors of a preset, true when the fragments have to hand their alpha over as the color for it (3
    /// multiplies what's behind by one plus the alpha, which GL only has as the destination color times the source)
    /// </summary>
    public static bool Apply(GlStateCache state, TwinShader.AlphaBlendPresets preset)
    {
        switch (preset)
        {
            case TwinShader.AlphaBlendPresets.Add:
                state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
                state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One, BlendingFactor.SrcAlpha, BlendingFactor.One);
                return false;
            case TwinShader.AlphaBlendPresets.Sub:
                state.SetBlendEquation(BlendEquationModeEXT.FuncReverseSubtract);
                state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One, BlendingFactor.SrcAlpha, BlendingFactor.One);
                return false;
            case TwinShader.AlphaBlendPresets.Brighten:
                state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
                state.SetBlendFunc(BlendingFactor.DstColor, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);
                return true;
            case TwinShader.AlphaBlendPresets.Darken:
                state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
                state.SetBlendFunc(BlendingFactor.Zero, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.Zero, BlendingFactor.One);
                return false;
            case TwinShader.AlphaBlendPresets.Scale:
                state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
                state.SetBlendFunc(BlendingFactor.Zero, BlendingFactor.SrcAlpha, BlendingFactor.Zero, BlendingFactor.One);
                return false;
            case TwinShader.AlphaBlendPresets.Replace:
                state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
                state.SetBlendFunc(BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One, BlendingFactor.Zero);
                return false;
            default:
                state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
                state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
                return false;
        }
    }
}
