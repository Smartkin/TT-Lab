using System;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering;

/// <summary>
/// How the GS samples a shader's texture (the decomp's shadersettings.cpp, <c>MakeSettings</c>): TEX1's magnifying filter is the shader's
/// filter bit, and most types minify a texture with mips through them, picking the nearest level filtered bilinearly. The level comes from
/// the distance alone: LOD = log2(1/Q) · 2^L + K, Q being one over the clip w, drawn magnified up to 0 and past it from level round(LOD), at
/// most the texture's last. Retail shaders have K = -69/16 and L = 0: the largest level up to 28 units, the next from there, then every
/// time the distance doubles. The environment, metallic and particle types (and the sky's, the UI's) always draw the largest level, the
/// metallic type, the UI's and the screen copies clamp the coordinates (repeated otherwise)
/// </summary>
public static class GsTextureFiltering
{
    // Where the game shifts L and K into TEX1, and the fields the GS reads them from: L's 2 bits, K's 12 (7.4 fixed point)
    private const int LShift = 19;
    private const int KShift = 32;

    /// <summary>
    /// L and K as the GS reads them from TEX1: the game ORs the shader's halfwords in sign-extended, so a negative L (the NTSC version's
    /// 0xCDCD leftovers) sets every bit of K's field too
    /// </summary>
    public static (int L, float K) Lod(UInt16 lodK, UInt16 lodL)
    {
        var tex1 = (UInt64)(Int64)(Int16)lodL << LShift | (UInt64)(Int64)(Int16)lodK << KShift;
        var l = (int)(tex1 >> LShift) & 0x3;
        var k = (int)(tex1 >> KShift) & 0xFFF;
        if ((k & 0x800) != 0)
        {
            k -= 0x1000;
        }

        return (l, k / 16.0f);
    }

    /// <summary>
    /// The GS's LOD for a fragment that far along the view (the clip w): its level is the nearest one, the largest up to 0
    /// </summary>
    public static float LevelAt(float viewDepth, int l, float k)
    {
        return MathF.Log2(Math.Max(viewDepth, 1e-6f)) * (1 << l) + k;
    }

    /// <summary>
    /// The level the GS draws at that LOD from a texture of that many levels: the nearest, rounding halves up
    /// </summary>
    public static int Level(float lod, int levels)
    {
        return Math.Clamp((int)MathF.Floor(lod + 0.5f), 0, Math.Max(levels - 1, 0));
    }

    /// <summary>
    /// Whether the type minifies a texture with mips through them (LINEAR_MIPMAP_NEAREST), the others always draw its largest level
    /// </summary>
    public static bool MinifiesThroughMips(TwinShader.Type type) => type switch
    {
        TwinShader.Type.StandardUnlit or TwinShader.Type.StandardLit or TwinShader.Type.LitSkinnedModel or TwinShader.Type.UiShader
            or TwinShader.Type.SHADER_20 or TwinShader.Type.UnlitGlossy or TwinShader.Type.UnlitEnvironmentMap or TwinShader.Type.UnlitClothDeformation
            or TwinShader.Type.SHADER_25 or TwinShader.Type.UnlitClothDeformation2 or TwinShader.Type.UnlitBillboard or TwinShader.Type.SHADER_30
            or TwinShader.Type.SHADER_31 or TwinShader.Type.SHADER_32 => true,
        _ => false,
    };

    /// <summary>
    /// Whether the type clamps the texture's coordinates (CLAMP's REGION modes aren't used): the metallic type, the UI's, type 20 and the
    /// screen copies
    /// </summary>
    public static bool ClampsCoordinates(TwinShader.Type type) => type is TwinShader.Type.LitMetallic or TwinShader.Type.UiShader
        or TwinShader.Type.SHADER_20 or TwinShader.Type.LitReflectionSurface or TwinShader.Type.SHADER_17 or TwinShader.Type.ScreenCopy;
}
