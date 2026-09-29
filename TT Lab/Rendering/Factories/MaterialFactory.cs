using System;
using GlmSharp;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Rendering.Services;
using TT_Lab.Rendering.UniformDescs;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Factories;

public class MaterialFactory(TextureService textureService)
{
    public TwinMaterialDesc GetTwinMaterialFromShader(LabShader shader)
    {
        var texture = textureService.GetTexture(shader.TextureId);
        var unlit = true;
        var uvScrollSpeed = vec2.Zero;
        var deformMode = 0;
        var deformSpeed = 0.0f;
        var deformAmplitude = vec3.Zero;
        var envMap = false;
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
                unlit = false;
                break;
            case TwinShader.Type.LitSkinnedModel:
                unlit = false;
                break;
            case TwinShader.Type.UnlitSkydome:
                break;
            case TwinShader.Type.ColorOnly:
                break;
            case TwinShader.Type.LitEnvironmentMap:
                unlit = false;
                envMap = true;
                break;
            case TwinShader.Type.UiShader:
                break;
            case TwinShader.Type.LitMetallic:
                unlit = false;
                break;
            case TwinShader.Type.LitReflectionSurface:
                unlit = false;
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
                envMap = true;
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
            ReflectDist = shader.ShaderType == TwinShader.Type.LitReflectionSurface ? new vec2(1.0f, shader.FloatParam[0]) : vec2.Zero,
            MetalicSpecular = shader.ShaderType is TwinShader.Type.LitMetallic or TwinShader.Type.UnlitGlossy ? 1.0f : 0.0f,
            DeformMode = deformMode,
            DeformSpeed = deformSpeed,
            DeformAmplitude = deformAmplitude,
            EnvMap = envMap ? 1.0f : 0.0f,
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