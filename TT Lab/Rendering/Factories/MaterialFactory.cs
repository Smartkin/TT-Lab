using System;
using GlmSharp;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Rendering.Services;
using TT_Lab.Rendering.UniformDescs;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Factories;

public class MaterialFactory(TextureService textureService)
{
    /// <summary>
    /// Shader types lit by the game's lights: their vertex colors times the light at the object
    /// </summary>
    public static bool IsLit(TwinShader.Type type) => type is TwinShader.Type.StandardLit or TwinShader.Type.LitSkinnedModel or TwinShader.Type.LitEnvironmentMap
        or TwinShader.Type.LitMetallic or TwinShader.Type.LitReflectionSurface;

    /// <param name="bucket">The material's render bucket: the PS2's fog only covers the buckets 0 to 3 (<see cref="GameFog"/>)</param>
    public TwinMaterialDesc GetTwinMaterialFromShader(LabShader shader, uint bucket = 2)
    {
        var texture = textureService.GetTexture(shader.TextureId);
        var unlit = !IsLit(shader.ShaderType);
        var deformMode = 0;
        var deformSpeed = 0.0f;
        var deformAmplitude = vec3.Zero;
        // Where the picture is read instead of the UVs (EnvironmentMapping)
        var envMap = 0.0f;
        var (lodL, lodK) = GsTextureFiltering.Lod(shader.LodParamK, shader.LodParamL);
        // The editor's own visuals (volumes, icons, the built in shapes) aren't the game's to fog
        var fog = shader.ForcedShaderName != null || shader.ShaderType == TwinShader.Type.ColorOnly ? TwinMaterialDesc.NoFog
            : bucket <= GameFog.LastFoggedBucket ? TwinMaterialDesc.BothFogs : TwinMaterialDesc.XboxFog;
        
        switch (shader.ShaderType)
        {
            case TwinShader.Type.StandardUnlit:
                break;
            case TwinShader.Type.StandardLit:
                break;
            case TwinShader.Type.LitSkinnedModel:
                break;
            case TwinShader.Type.UnlitSkydome:
                break;
            case TwinShader.Type.ColorOnly:
                break;
            case TwinShader.Type.LitEnvironmentMap:
                envMap = 1.0f;
                break;
            case TwinShader.Type.UiShader:
                break;
            case TwinShader.Type.LitMetallic:
                envMap = 2.0f;
                break;
            case TwinShader.Type.LitReflectionSurface:
                break;
            case TwinShader.Type.SHADER_17:
                break;
            case TwinShader.Type.Particle:
                break;
            case TwinShader.Type.Decal:
                break;
            case TwinShader.Type.SHADER_20:
                break;
            case TwinShader.Type.UnlitGlossy:
                break;
            case TwinShader.Type.UnlitEnvironmentMap:
                envMap = 1.0f;
                break;
            // The mode, the speed and one amplitude (FUN_001dbdf8 reads them so)
            case TwinShader.Type.UnlitClothDeformation:
                deformMode = (int)shader.IntParam;
                deformSpeed = shader.FloatParam[0];
                deformAmplitude = new vec3(shader.FloatParam[1]);
                break;
            case TwinShader.Type.SHADER_25:
                break;
            // The mode, the speed and an amplitude per axis (FUN_001dbb78)
            case TwinShader.Type.UnlitClothDeformation2:
                deformMode = (int)shader.IntParam;
                deformSpeed = shader.FloatParam[0];
                deformAmplitude = new vec3(shader.FloatParam[1], shader.FloatParam[2], shader.FloatParam[3]);
                break;
            case TwinShader.Type.UnlitBillboard:
                break;
            case TwinShader.Type.SHADER_30:
                break;
            case TwinShader.Type.SHADER_31:
                break;
            case TwinShader.Type.SHADER_32:
                break;
        }
        
        return new TwinMaterialDesc
        {
            Texture = texture,
            UseTexture = shader.TxtMapping == TwinShader.TextureMapping.ON ? 1.0f : 0.0f,
            AlphaBlend = shader.ABlending == TwinShader.AlphaBlending.ON ? 1.0f : 0.0f,
            AlphaTestMethod = GsAlphaTest.MethodOf(shader.ATest, shader.ATestMethod),
            AlphaReference = shader.AlphaValueToBeComparedTo,
            AlphaFail = shader.ProcessMethodWhenAlphaTestFailed,
            Lit = !unlit,
            ReflectDist = shader.ShaderType == TwinShader.Type.LitReflectionSurface ? new vec2(1.0f, shader.FloatParam[0]) : vec2.Zero,
            DeformMode = deformMode,
            DeformSpeed = deformSpeed,
            DeformAmplitude = deformAmplitude,
            EnvMap = envMap,
            ScrollU = shader.XScrollSettings,
            ScrollV = shader.YScrollSettings,
            ScrollPhases = new vec2(shader.UvScrollSpeed.X, shader.UvScrollSpeed.Y),
            ScrollSpeeds = new vec2(shader.UvScrollSpeed.Z, shader.UvScrollSpeed.W),
            Animation = shader.Animation,
            AnimatesU = shader.XScrollSettings == TwinShader.XScrollFormula.FromAnimation,
            AnimatesV = shader.YScrollSettings == TwinShader.YScrollFormula.FromAnimation,
            BlendFunc = shader.AlphaRegSettingsIndex,
            CustomBlend = shader.UseCustomAlphaRegSettings,
            BlendA = shader.SpecOfColA,
            BlendB = shader.SpecOfColB,
            BlendC = shader.SpecOfAlphaC,
            BlendD = shader.SpecOfColD,
            BlendFix = shader.FixedAlphaValue,
            Mipmapped = GsTextureFiltering.MinifiesThroughMips(shader.ShaderType),
            LodL = lodL,
            LodK = lodK,
            MagnifiesLinearly = shader.TextureFilterWhenTextureIsExpanded == TwinShader.TextureFilter.LINEAR || shader.ShaderType == TwinShader.Type.UiShader,
            ClampsCoordinates = GsTextureFiltering.ClampsCoordinates(shader.ShaderType),
            Fog = fog,
            DepthWrite = shader.ZValueDrawingMask == TwinShader.ZValueDrawMask.UPDATE,
            DepthTest = shader.DepthTest,
        };
    }
}