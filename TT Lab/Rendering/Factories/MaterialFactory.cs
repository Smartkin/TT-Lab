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
    /// Shader types lit by the game's lights, whose vertex colors count double (128 is full)
    /// </summary>
    public static bool IsLit(TwinShader.Type type) => type is TwinShader.Type.StandardLit or TwinShader.Type.LitSkinnedModel or TwinShader.Type.LitEnvironmentMap
        or TwinShader.Type.LitMetallic or TwinShader.Type.LitReflectionSurface;

    public TwinMaterialDesc GetTwinMaterialFromShader(LabShader shader)
    {
        var texture = textureService.GetTexture(shader.TextureId);
        var unlit = !IsLit(shader.ShaderType);
        var uvScrollSpeed = vec2.Zero;
        var deformMode = 0;
        var deformSpeed = 0.0f;
        var deformAmplitude = vec3.Zero;
        // Where the picture is read instead of the UVs (EnvironmentMapping)
        var envMap = 0.0f;
        if (shader.XScrollSettings is not (TwinShader.XScrollFormula.Disabled or TwinShader.XScrollFormula.FromAnimation))
        {
            uvScrollSpeed.x = shader.UvScrollSpeed.Z;
        }

        if (shader.YScrollSettings is not (TwinShader.YScrollFormula.Disabled or TwinShader.YScrollFormula.FromAnimation))
        {
            uvScrollSpeed.y = shader.UvScrollSpeed.W;
        }
        
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
            AlphaTest = shader.ATest == TwinShader.AlphaTest.ON ? shader.AlphaValueToBeComparedTo / 255.0f : 0.0f,
            BillboardRender = shader.ShaderType == TwinShader.Type.UnlitBillboard,
            DoubleColor = unlit ? 1.0f : 2.0f,
            Lit = !unlit,
            ReflectDist = shader.ShaderType == TwinShader.Type.LitReflectionSurface ? new vec2(1.0f, shader.FloatParam[0]) : vec2.Zero,
            DeformMode = deformMode,
            DeformSpeed = deformSpeed,
            DeformAmplitude = deformAmplitude,
            EnvMap = envMap,
            UvScrollSpeed = uvScrollSpeed,
            Animation = shader.Animation,
            AnimatesU = shader.XScrollSettings == TwinShader.XScrollFormula.FromAnimation,
            AnimatesV = shader.YScrollSettings == TwinShader.YScrollFormula.FromAnimation,
            AnimatesColor = shader.AnimationDrivesColor,
            BlendFunc = shader.AlphaRegSettingsIndex,
            DepthWrite = shader.ZValueDrawingMask == TwinShader.ZValueDrawMask.UPDATE,
            DepthTest = shader.DepthTest,
            PerformFog = shader.Fog == TwinShader.Fogging.ON
        };
    }
}