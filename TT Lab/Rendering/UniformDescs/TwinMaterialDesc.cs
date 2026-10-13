using GlmSharp;
using TT_Lab.Rendering.Buffers;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;

namespace TT_Lab.Rendering.UniformDescs;

public struct TwinMaterialDesc()
{
    /// <summary>
    /// Fogged by neither version of the game's fog: what the editor draws
    /// </summary>
    public const int NoFog = 0;
    /// <summary>
    /// Fogged by the Xbox version's fog stage, which every material goes through, not by the PS2's colour filter (drawn after its buckets)
    /// </summary>
    public const int XboxFog = 1;
    /// <summary>
    /// Fogged by both versions': the PS2's colour filter covers the buckets 0 to 3
    /// </summary>
    public const int BothFogs = 2;

    public TextureBuffer? Texture { get; init; }
    public TwinShader.AlphaBlendPresets BlendFunc { get; init; } = TwinShader.AlphaBlendPresets.Mix;
    /// <summary>
    /// The shader's own blending formula in place of the preset (<see cref="GsBlending.ApplyFormula"/>): (A - B) * C + D and the fixed alpha
    /// </summary>
    public bool CustomBlend { get; init; }
    public TwinShader.ColorSpecMethod BlendA { get; init; }
    public TwinShader.ColorSpecMethod BlendB { get; init; }
    public TwinShader.AlphaSpecMethod BlendC { get; init; }
    public TwinShader.ColorSpecMethod BlendD { get; init; }
    public byte BlendFix { get; init; }
    public int DeformMode { get; init; }
    public float DeformSpeed { get; init; }
    public vec3 DeformAmplitude { get; init; } = vec3.Zero;
    /// <summary>
    /// Lit by the game's lights: the vertex color times the object's light set (<c>MainPass.vert</c>)
    /// </summary>
    public bool Lit { get; init; }
    /// <summary>
    /// How the U and V scrolls move the texture (<see cref="Materials.ShaderScroll"/>), from the phases (the shader's UvScrollSpeed's X and
    /// Y) at the speeds (its Z and W)
    /// </summary>
    public TwinShader.XScrollFormula ScrollU { get; init; }
    public TwinShader.YScrollFormula ScrollV { get; init; }
    public vec2 ScrollPhases { get; init; } = vec2.Zero;
    public vec2 ScrollSpeeds { get; init; } = vec2.Zero;
    /// <summary>
    /// The shader's animation, played for the U and V tracks the scroll modes take
    /// </summary>
    public TwinShaderAnimation? Animation { get; init; }
    public bool AnimatesU { get; init; }
    public bool AnimatesV { get; init; }
    public vec2 ReflectDist { get; init; } = vec2.Zero;
    /// <summary>
    /// The GS's alpha test (<see cref="GsAlphaTest"/>): its method, -1 without one, the reference byte and what failing fragments write
    /// (AFAIL)
    /// </summary>
    public int AlphaTestMethod { get; init; } = -1;
    public int AlphaReference { get; init; }
    public TwinShader.ProcessAfterAlphaTestFailed AlphaFail { get; init; }
    /// <summary>
    /// How the fragments failing the test are drawn, by what they write next to the passing ones
    /// </summary>
    public AlphaFailDraw FailDraw => GsAlphaTest.FailDrawOf(AlphaTestMethod, AlphaFail, DepthWrite);
    public float AlphaBlend { get; init; } = 0.0f;
    /// <summary>
    /// Where the picture is read instead of the UVs: 0 the UVs, 1 like the environment maps, 2 like the metallic shader
    /// (<see cref="EnvironmentMapping"/>)
    /// </summary>
    public float EnvMap { get; init; } = 0.0f;
    public float UseTexture { get; init; } = 0.0f;
    /// <summary>
    /// How the GS samples the texture (<see cref="GsTextureFiltering"/>): through its mips by the distance (L and K) or only its largest
    /// level, the magnifying filter and whether the coordinates are clamped
    /// </summary>
    public bool Mipmapped { get; init; }
    public int LodL { get; init; }
    public float LodK { get; init; }
    public bool MagnifiesLinearly { get; init; } = true;
    public bool ClampsCoordinates { get; init; }
    /// <summary>
    /// Which of the game's fogs draw over the material (<see cref="NoFog"/>, <see cref="XboxFog"/>, <see cref="BothFogs"/>, <see cref="GameFog"/>)
    /// </summary>
    public int Fog { get; init; } = BothFogs;
    public bool DepthWrite { get; init; } = false;
    public TwinShader.DepthTestMethod DepthTest { get; init; } = TwinShader.DepthTestMethod.GEQUAL;
    /// <summary>
    /// Shaded flat by its triangles' facing for the editor (collision has nothing of its own to light it by)
    /// </summary>
    public bool EditorShading { get; init; }
}
