using System;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering;

/// <summary>
/// The GS's alpha test (TEST's ATE, ATST, AREF and AFAIL, the shader's settings): the fragment's alpha, 128 being 1, against the reference
/// byte. A fragment that fails keeps nothing (KEEP), only its color (FB_ONLY, RGB_ONLY without its alpha) or only its depth (ZB_ONLY).
/// <c>MainPass.frag</c> does the same: it can't leave the depth alone for a fragment it draws, so the colors of FB_ONLY and RGB_ONLY are
/// drawn with their depth, and ZB_ONLY is left out like KEEP. Retail materials test GEQUAL and keep nothing, a few GEQUAL 125 FB_ONLY
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
    /// Whether a fragment failing the test still draws its color
    /// </summary>
    public static bool FailedDrawsColor(TwinShader.ProcessAfterAlphaTestFailed fail) =>
        fail is TwinShader.ProcessAfterAlphaTestFailed.FB_ONLY or TwinShader.ProcessAfterAlphaTestFailed.RGB_ONLY;
}
