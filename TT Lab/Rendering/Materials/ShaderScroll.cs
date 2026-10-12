using System;
using GlmSharp;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Materials;

/// <summary>
/// How far a shader moves its texture coordinates after some seconds (the decomp's <c>UpdateShader</c>, <c>UpdateShaderScrollAndColor</c>):
/// each scroll's phase starts at the shader's <c>UvScrollSpeed</c> X (U) or Y (V) and moves on by its Z or W turns a second, giving the
/// phase wrapped into 0 to 1 (Linear), its turn's sine (LinearPlus_1) or cosine (LinearPlus_2). U's cosine goes into V's offset, which V's
/// own scroll takes over when it has one. The animated scrolls take the shader animation's U and V instead. The environment maps add it to
/// where they read their picture like to the UVs
/// </summary>
public static class ShaderScroll
{
    public static vec2 At(TwinShader.XScrollFormula u, TwinShader.YScrollFormula v, vec2 phases, vec2 speeds, double seconds, vec2 animation)
    {
        var offset = vec2.Zero;
        switch (u)
        {
            case TwinShader.XScrollFormula.Linear:
                offset.x = Wrapped(phases.x, speeds.x, seconds);
                break;
            case TwinShader.XScrollFormula.LinearPlus_1:
                offset.x = Waved(phases.x, speeds.x, seconds, false);
                break;
            case TwinShader.XScrollFormula.LinearPlus_2:
                offset.y = Waved(phases.x, speeds.x, seconds, true);
                break;
            case TwinShader.XScrollFormula.FromAnimation:
                offset.x = animation.x;
                break;
        }

        switch (v)
        {
            case TwinShader.YScrollFormula.Linear:
                offset.y = Wrapped(phases.y, speeds.y, seconds);
                break;
            case TwinShader.YScrollFormula.LinearPlus_1:
                offset.y = Waved(phases.y, speeds.y, seconds, false);
                break;
            case TwinShader.YScrollFormula.LinearPlus_2:
                offset.y = Waved(phases.y, speeds.y, seconds, true);
                break;
            case TwinShader.YScrollFormula.FromAnimation:
                offset.y = animation.y;
                break;
        }

        return offset;
    }

    // Leftover bytes in the vector read as NaN or infinities, a shader of them moves nowhere
    private static double Phase(float phase, float speed, double seconds)
    {
        var value = (double)phase + speed * seconds;
        return double.IsFinite(value) ? value : 0.0;
    }

    private static float Wrapped(float phase, float speed, double seconds)
    {
        var value = Phase(phase, speed, seconds);
        return (float)(value - Math.Floor(value));
    }

    private static float Waved(float phase, float speed, double seconds, bool cosine)
    {
        var turn = Phase(phase, speed, seconds) * Math.Tau;
        return (float)(cosine ? Math.Cos(turn) : Math.Sin(turn));
    }
}
