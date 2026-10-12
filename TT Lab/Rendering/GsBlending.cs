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

    /// <summary>
    /// A shader's own formula (A - B) * C + D in place of the preset: the colors A, B and D the source's, what's behind or none, C the
    /// source's alpha, what's behind's or the fixed one (FIX, 128 is 1), the reserved ones taken for none and FIX like the Blender add-on
    /// does. GL's factors take every formula whose source and destination factors each come out as 0, 1, C, 1 - C or one of them -C; the
    /// others (Cs·(C + 1)) are drawn with 1 in place of C + 1
    /// </summary>
    public static void ApplyFormula(RenderContext context, TwinShader.ColorSpecMethod a, TwinShader.ColorSpecMethod b, TwinShader.AlphaSpecMethod c,
        TwinShader.ColorSpecMethod d, byte fix)
    {
        if (c is not (TwinShader.AlphaSpecMethod.SOURCE or TwinShader.AlphaSpecMethod.FB))
        {
            context.Gl.BlendColor(0.0f, 0.0f, 0.0f, fix / 128.0f);
        }

        var (equation, source, frame) = Formula(a, b, c, d);
        context.State.SetBlendEquation(equation);
        context.State.SetBlendFunc(source, frame, source, frame);
    }

    /// <summary>
    /// GL's equation and factors of the source and of what's behind for a formula (<see cref="ApplyFormula"/>)
    /// </summary>
    public static (BlendEquationModeEXT Equation, BlendingFactor Source, BlendingFactor Frame) Formula(TwinShader.ColorSpecMethod a, TwinShader.ColorSpecMethod b,
        TwinShader.AlphaSpecMethod c, TwinShader.ColorSpecMethod d)
    {
        var (alpha, oneMinusAlpha) = c switch
        {
            TwinShader.AlphaSpecMethod.SOURCE => (BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha),
            TwinShader.AlphaSpecMethod.FB => (BlendingFactor.DstAlpha, BlendingFactor.OneMinusDstAlpha),
            _ => (BlendingFactor.ConstantAlpha, BlendingFactor.OneMinusConstantAlpha),
        };

        int Is(TwinShader.ColorSpecMethod method, TwinShader.ColorSpecMethod of) => method == of ? 1 : 0;
        // Each side's factor: k times C plus m
        var source = (K: Is(a, TwinShader.ColorSpecMethod.SOURCE) - Is(b, TwinShader.ColorSpecMethod.SOURCE), M: Is(d, TwinShader.ColorSpecMethod.SOURCE));
        var frame = (K: Is(a, TwinShader.ColorSpecMethod.FB) - Is(b, TwinShader.ColorSpecMethod.FB), M: Is(d, TwinShader.ColorSpecMethod.FB));
        BlendingFactor Factor((int K, int M) side) => side switch
        {
            (0, 0) => BlendingFactor.Zero,
            (1, 0) or (-1, 0) => alpha,
            (-1, 1) => oneMinusAlpha,
            _ => BlendingFactor.One,
        };

        var sourceTakenAway = source is (K: -1, M: 0);
        var frameTakenAway = frame is (K: -1, M: 0);
        if (sourceTakenAway && frameTakenAway)
        {
            // What's behind and the source both taken away: nothing's left
            return (BlendEquationModeEXT.FuncAdd, BlendingFactor.Zero, BlendingFactor.Zero);
        }

        var equation = sourceTakenAway ? BlendEquationModeEXT.FuncReverseSubtract : frameTakenAway ? BlendEquationModeEXT.FuncSubtract : BlendEquationModeEXT.FuncAdd;
        return (equation, Factor(source), Factor(frame));
    }
}
