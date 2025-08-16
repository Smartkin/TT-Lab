using Silk.NET.OpenGL;
using TT_Lab.Rendering.UniformDescs;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Materials;

public class TwinMaterial(RenderContext context, TwinMaterialDesc materialDesc)
{
    private bool _isBlendingEnabled;
    private bool _isDepthWriteEnabled;
    
    public void Bind()
    {
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
        var useTextureLoc = program.GetUniformLocation(TwinMaterialDesc.UseTexturePath);
        context.Gl.Uniform1(useTextureLoc, materialDesc.UseTexture);
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
        if (materialDesc.AlphaBlend.Equals(1.0f))
        {
            if (!_isBlendingEnabled)
            {
                context.Gl.Enable(EnableCap.Blend);
            }

            switch (materialDesc.BlendFunc)
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
        if (materialDesc.DepthWrite)
        {
            context.Gl.DepthMask(true);
            switch (materialDesc.DepthTest)
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
    }
}