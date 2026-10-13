using System;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering;

/// <summary>
/// What a draw does with the fragments failing a material's alpha test, for what they write next to what passing fragments write
/// (<see cref="GsAlphaTest.FailDrawOf"/>)
/// </summary>
public enum AlphaFailDraw
{
    /// <summary>
    /// They write nothing (KEEP, ZB_ONLY without depth writes): left out
    /// </summary>
    LeftOut,
    /// <summary>
    /// They write what passing fragments write (FB_ONLY without depth writes) or nothing fails: drawn with them
    /// </summary>
    WithPassing,
    /// <summary>
    /// They write something else (FB_ONLY and RGB_ONLY without the depth the passing fragments write, RGB_ONLY without alpha, ZB_ONLY only
    /// the depth): GL can't leave a write alone for some fragments of a draw, so they're drawn on their own after the passing ones, a
    /// triangle at a time like the GS writes them or, for a part whose triangles never lie over each other, an instance at a time
    /// (RenderBatch.DrawApart)
    /// </summary>
    Apart,
}

/// <summary>
/// The GS's alpha test (TEST's ATE, ATST, AREF and AFAIL, the shader's settings): the fragment's alpha, 128 being 1, against the reference
/// byte. A fragment that fails keeps nothing (KEEP), only its color (FB_ONLY, RGB_ONLY without its alpha) or only its depth (ZB_ONLY), its
/// depth only ever written while the shader writes depth (ZMSK). Retail materials test GEQUAL and keep nothing but 11 that keep the color
/// (FB_ONLY): the school's landscape billboards (roof01, roof03, roof05, buschase, crgpa04) write depth where they pass, never where they're
/// see-through, and a billboard drawn before another one in front of it doesn't hide it
/// </summary>
public static class GsAlphaTest
{
    /// <summary>
    /// What <c>MainPass.frag</c> gets for the test: the method, or -1 without one
    /// </summary>
    public static int MethodOf(TwinShader.AlphaTest test, TwinShader.AlphaTestMethod method) => test == TwinShader.AlphaTest.ON ? (int)method : -1;

    /// <summary>
    /// The GS's alpha of a fragment from TT Lab's: textures are their PNG's values (twice what the GS gets) and vertex alphas 0x7F the
    /// most, the GS's modulation (At · Av >> 7) of them gives the alpha times 127 rounded down
    /// </summary>
    public static int GsAlpha(float alpha) => (int)MathF.Floor(alpha * 127.0f + 0.001f);

    public static bool Passes(int method, float alpha, int reference)
    {
        var value = GsAlpha(alpha);
        return (TwinShader.AlphaTestMethod)method switch
        {
            _ when method < 0 => true,
            TwinShader.AlphaTestMethod.NEVER => false,
            TwinShader.AlphaTestMethod.ALWAYS => true,
            TwinShader.AlphaTestMethod.LESS => value < reference,
            TwinShader.AlphaTestMethod.LEQUAL => value <= reference,
            TwinShader.AlphaTestMethod.EQUAL => value == reference,
            TwinShader.AlphaTestMethod.GEQUAL => value >= reference,
            TwinShader.AlphaTestMethod.GREATER => value > reference,
            _ => value != reference,
        };
    }

    /// <summary>
    /// What a fragment failing the test writes: the color but its alpha, the alpha and the depth, the depth only while the shader writes it
    /// </summary>
    public static (bool Color, bool Alpha, bool Depth) FailedWrites(TwinShader.ProcessAfterAlphaTestFailed fail, bool depthWrite) => fail switch
    {
        TwinShader.ProcessAfterAlphaTestFailed.FB_ONLY => (true, true, false),
        TwinShader.ProcessAfterAlphaTestFailed.RGB_ONLY => (true, false, false),
        TwinShader.ProcessAfterAlphaTestFailed.ZB_ONLY => (false, false, depthWrite),
        _ => (false, false, false),
    };

    /// <summary>
    /// How the fragments failing the test are drawn, by what they write next to the passing ones (the color with its alpha and the depth
    /// the shader writes)
    /// </summary>
    public static AlphaFailDraw FailDrawOf(int method, TwinShader.ProcessAfterAlphaTestFailed fail, bool depthWrite)
    {
        if (method is < 0 or (int)TwinShader.AlphaTestMethod.ALWAYS)
        {
            return AlphaFailDraw.WithPassing;
        }

        var (color, alpha, depth) = FailedWrites(fail, depthWrite);
        if (!color && !depth)
        {
            return AlphaFailDraw.LeftOut;
        }

        return color && alpha && depth == depthWrite ? AlphaFailDraw.WithPassing : AlphaFailDraw.Apart;
    }
}
