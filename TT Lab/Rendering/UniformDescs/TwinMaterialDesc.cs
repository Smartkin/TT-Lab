using GlmSharp;
using TT_Lab.Rendering.Buffers;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;

namespace TT_Lab.Rendering.UniformDescs;

public struct TwinMaterialDesc()
{
    public const string DeformModePath = "twin_material.deform_mode";
    public const string DeformSpeedPath = "twin_material.deform_speed";
    public const string DeformAmplitudePath = "twin_material.deform_amplitude";
    public const string BillboardRenderPath = "twin_material.billboard_render";
    public const string DoubleColorPath = "twin_material.double_color";
    public const string UvScrollSpeedPath = "twin_material.uv_scroll_speed";
    public const string UvOffsetPath = "twin_material.uv_offset";
    public const string AnimatedColorPath = "twin_material.animated_color";
    public const string ReflectDistPath = "twin_material.reflect_dist";
    public const string AlphaTestPath = "twin_material.alpha_test";
    public const string AlphaBlendPath = "twin_material.alpha_blend";
    public const string EnvMapPath = "twin_material.env_map";
    public const string BlendFuncPath = "twin_material.blend_func";
    public const string UseTexturePath = "twin_material.use_texture";
    public const string PerformFogPath = "twin_material.perform_fog";
    public const string EditorShadingPath = "twin_material.editor_shading";
    public const string LitPath = "twin_material.lit";
    
    public TextureBuffer? Texture { get; init; }
    public TwinShader.AlphaBlendPresets BlendFunc { get; init; } = TwinShader.AlphaBlendPresets.Mix;
    public int DeformMode { get; init; }
    public float DeformSpeed { get; init; }
    public vec3 DeformAmplitude { get; init; } = vec3.Zero;
    public bool BillboardRender { get; init; } = false;
    public float DoubleColor { get; init; } = 2.0f;
    /// <summary>
    /// Lit by the game's lights: the vertex color times the object's light set (<c>MainPass.vert</c>)
    /// </summary>
    public bool Lit { get; init; }
    public vec2 UvScrollSpeed { get; init; } = vec2.Zero;
    /// <summary>
    /// The shader's animation, played for the U and V tracks the scroll modes take and the color when the shader takes it
    /// </summary>
    public TwinShaderAnimation? Animation { get; init; }
    public bool AnimatesU { get; init; }
    public bool AnimatesV { get; init; }
    public bool AnimatesColor { get; init; }
    public vec2 ReflectDist { get; init; } = vec2.Zero;
    public float AlphaTest { get; init; } = 0.0f;
    public float AlphaBlend { get; init; } = 0.0f;
    /// <summary>
    /// Where the picture is read instead of the UVs: 0 the UVs, 1 like the environment maps, 2 like the metallic shader
    /// (<see cref="EnvironmentMapping"/>)
    /// </summary>
    public float EnvMap { get; init; } = 0.0f;
    public float UseTexture { get; init; } = 0.0f;
    public bool DepthWrite { get; init; } = false;
    public bool PerformFog { get; init; } = false;
    public TwinShader.DepthTestMethod DepthTest { get; init; } = TwinShader.DepthTestMethod.GEQUAL;
    /// <summary>
    /// Shaded flat by its triangles' facing for the editor (collision has nothing of its own to light it by)
    /// </summary>
    public bool EditorShading { get; init; }
}