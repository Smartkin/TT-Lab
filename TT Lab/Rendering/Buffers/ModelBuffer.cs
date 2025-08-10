using System;
using System.Collections.Generic;
using System.Linq;
using Silk.NET.OpenGL;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Rendering.Factories;
using TT_Lab.Rendering.Passes;
using TT_Lab.Rendering.UniformDescs;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Buffers;

public class ModelBuffer(RenderContext context, ModelBufferBuild build, MaterialFactory materialFactory, MaterialData material)
{
    private bool _isDepthWriteEnabled;
    private bool _isBlendingEnabled;
    
    public uint IndexCount => build.IndicesAmount;
    public bool Wireframe { get; set; }

    public (string, int)[] GetPriorityPass()
    {
        var result = new List<(string, int)>();
        var shaderIndex = 0;
        foreach (var shader in material.Shaders)
        {
            var passName = shader.ShaderType.ToString();
            if (shader.ABlending == TwinShader.AlphaBlending.ON)
            {
                passName += "Transparent";
            }
            var priority = (int)(shader.UnkVector2.W - 128 + material.DmaChainIndex + shaderIndex);
            result.Add((passName, priority));
            shaderIndex++;
        }
        return result.ToArray();
    }
    
    public virtual bool Bind()
    {
        var shader = GetShaderFromPass(context.CurrentPass);
        if (shader == null)
        {
            return false;
        }
        
        build.Vao.Bind();
        var materialDesc = materialFactory.GetTwinMaterialFromShader(shader);
        materialDesc.Texture?.Bind();
        var program = context.CurrentPass.Program;
        var deformSpeedLoc = program.GetUniformLocation(TwinMaterialDesc.DeformSpeedPath);
        var billboardRenderLoc = program.GetUniformLocation(TwinMaterialDesc.BillboardRenderPath);
        var doubleColorLoc = program.GetUniformLocation(TwinMaterialDesc.DoubleColorPath);
        var reflectDistLoc = program.GetUniformLocation(TwinMaterialDesc.ReflectDistPath);
        var uvScrollSpeedLoc = program.GetUniformLocation(TwinMaterialDesc.UvScrollSpeedPath);
        var alphaTestLoc = program.GetUniformLocation(TwinMaterialDesc.AlphaTestPath);
        var alphaBlendLoc = program.GetUniformLocation(TwinMaterialDesc.AlphaBlendPath);
        var metalicSpecularLoc = program.GetUniformLocation(TwinMaterialDesc.MetalicSpecularPath);
        var envMapLoc = program.GetUniformLocation(TwinMaterialDesc.EnvMapPath);
        // var blendFuncLoc = program.GetUniformLocation(TwinMaterialDesc.BlendFuncPath);
        var useTextureLoc = program.GetUniformLocation(TwinMaterialDesc.UseTexturePath);
        context.Gl.Uniform1(useTextureLoc, materialDesc.UseTexture);
        // context.Gl.Uniform1(blendFuncLoc, (int)materialDesc.BlendFunc);
        context.Gl.Uniform1(alphaTestLoc, materialDesc.AlphaTest);
        context.Gl.Uniform1(alphaBlendLoc, materialDesc.AlphaBlend);
        context.Gl.Uniform1(metalicSpecularLoc, materialDesc.MetalicSpecular);
        context.Gl.Uniform1(envMapLoc, materialDesc.EnvMap);
        context.Gl.Uniform1(billboardRenderLoc, materialDesc.BillboardRender ? 1.0f : 0.0f);
        context.Gl.Uniform1(doubleColorLoc, materialDesc.DoubleColor);
        context.Gl.Uniform2(uvScrollSpeedLoc, materialDesc.UvScrollSpeed.Values);
        context.Gl.Uniform2(deformSpeedLoc, materialDesc.DeformSpeed.Values);
        context.Gl.Uniform2(reflectDistLoc, materialDesc.ReflectDist.Values);

        _isBlendingEnabled = context.Gl.IsEnabled(EnableCap.Blend);
        if (shader.ABlending == TwinShader.AlphaBlending.ON)
        {
            if (!_isBlendingEnabled)
            {
                context.Gl.Enable(EnableCap.Blend);
            }

            switch (shader.AlphaRegSettingsIndex)
            {
                case TwinShader.AlphaBlendPresets.Mix:
                    context.Gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
                    context.Gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
                    break;
                case TwinShader.AlphaBlendPresets.Add:
                    context.Gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
                    context.Gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.One, BlendingFactor.SrcAlpha, BlendingFactor.One);
                    break;
                case TwinShader.AlphaBlendPresets.Sub:
                    context.Gl.BlendEquation(BlendEquationModeEXT.FuncReverseSubtract);
                    context.Gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.One, BlendingFactor.SrcAlpha, BlendingFactor.One);
                    break;
                case TwinShader.AlphaBlendPresets.Alpha:
                    break;
                case TwinShader.AlphaBlendPresets.Zero:
                    break;
                case TwinShader.AlphaBlendPresets.Destination:
                    break;
                case TwinShader.AlphaBlendPresets.Source:
                    break;
            }
        }
        else
        {
            if (_isBlendingEnabled)
            {
                context.Gl.Disable(EnableCap.Blend);
            }
        }

        _isDepthWriteEnabled = context.Gl.GetBoolean(GetPName.DepthWritemask);
        if (shader.ZValueDrawingMask == TwinShader.ZValueDrawMask.UPDATE)
        {
            context.Gl.DepthMask(true);
            switch (shader.DepthTest)
            {
                case TwinShader.DepthTestMethod.NEVER:
                    context.Gl.DepthFunc(DepthFunction.Never);
                    break;
                case TwinShader.DepthTestMethod.ALWAYS:
                    context.Gl.DepthFunc(DepthFunction.Always);
                    break;
                case TwinShader.DepthTestMethod.GEQUAL:
                    context.Gl.DepthFunc(DepthFunction.Lequal);
                    break;
                case TwinShader.DepthTestMethod.GREATER:
                    context.Gl.DepthFunc(DepthFunction.Less);
                    break;
            }
        }
        else
        {
            context.Gl.DepthMask(false);
        }

        if (Wireframe)
        {
            context.Gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
            context.Gl.LineWidth(3);
        }

        return true;
    }

    public void Unbind()
    {
        context.Gl.DepthMask(_isDepthWriteEnabled);
        if (_isBlendingEnabled)
        {
            context.Gl.Enable(EnableCap.Blend);
        }
        else
        {
            context.Gl.Disable(EnableCap.Blend);
        }

        if (Wireframe)
        {
            context.Gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
            context.Gl.LineWidth(1);
        }
    }

    private LabShader? GetShaderFromPass(RenderPass renderPass)
    {
        var passName = renderPass.Name;
        return material.Shaders.FirstOrDefault(s =>
        {
            var shaderName = s.ShaderType.ToString();
            if (s.ABlending == TwinShader.AlphaBlending.ON)
            {
                shaderName += "Transparent";
            }
            return shaderName == passName;
        });
    }
}