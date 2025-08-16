using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.UniformDescs;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Materials;

public class TwinMaterial(RenderContext context, TwinMaterialDesc materialDesc) : Material
{
    private bool _isBlendingEnabled;
    private bool _isDepthWriteEnabled;
    private bool _isGlBlendingEnabled;
    private bool _isGlDepthWriteEnabled;
    private TwinMaterialDesc _materialDesc = materialDesc;
    
    public TwinMaterialDesc GetCurrentMaterialDesc() => _materialDesc;

    public void ApplyDeformSpeed(vec2 @override)
    {
        var program = context.CurrentPass.Program;
        var deformSpeedLoc = program.GetUniformLocation(TwinMaterialDesc.DeformSpeedPath);
        context.Gl.Uniform2(deformSpeedLoc, @override.Values);
    }

    public void ApplyBillboardRender(bool @override)
    {
        var program = context.CurrentPass.Program;
        var billboardRenderLoc = program.GetUniformLocation(TwinMaterialDesc.BillboardRenderPath);
        context.Gl.Uniform1(billboardRenderLoc, @override ? 1.0f : 0.0f);
    }

    public void ApplyUseTexture(bool @override)
    {
        var program = context.CurrentPass.Program;
        var useTextureLoc = program.GetUniformLocation(TwinMaterialDesc.UseTexturePath);
        context.Gl.Uniform1(useTextureLoc, @override ? 1.0f : 0.0f);
    }

    public void ApplyDoubleColor(float @override)
    {
        var program = context.CurrentPass.Program;
        var doubleColorLoc = program.GetUniformLocation(TwinMaterialDesc.DoubleColorPath);
        context.Gl.Uniform1(doubleColorLoc, @override);
    }

    /// <summary>
    /// </summary>
    /// <param name="override">x component whether it's turned on or off and y is the actual distance</param>
    public void ApplyReflectDistance(vec2 @override)
    {
        var program = context.CurrentPass.Program;
        var reflectDistLoc = program.GetUniformLocation(TwinMaterialDesc.ReflectDistPath);
        context.Gl.Uniform2(reflectDistLoc, @override.Values);
    }

    public void ApplyUvScroll(vec2 @override)
    {
        var program = context.CurrentPass.Program;
        var uvScrollSpeedLoc = program.GetUniformLocation(TwinMaterialDesc.UvScrollSpeedPath);
        context.Gl.Uniform2(uvScrollSpeedLoc, @override.Values);
    }

    public void ApplyAlphaTest(float @override)
    {
        var program = context.CurrentPass.Program;
        var alphaTestLoc = program.GetUniformLocation(TwinMaterialDesc.AlphaTestPath);
        context.Gl.Uniform1(alphaTestLoc, @override);
    }

    public void ApplyMetallicSpecular(float @override)
    {
        var program = context.CurrentPass.Program;
        var metalicSpecularLoc = program.GetUniformLocation(TwinMaterialDesc.MetalicSpecularPath);
        context.Gl.Uniform1(metalicSpecularLoc, @override);
    }

    public void ApplyEnvMap(float @override)
    {
        var program = context.CurrentPass.Program;
        var envMapLoc = program.GetUniformLocation(TwinMaterialDesc.EnvMapPath);
        context.Gl.Uniform1(envMapLoc, @override);
    }

    public void ApplyAlphaBlending(bool @override)
    {
        var program = context.CurrentPass.Program;
        var alphaBlendLoc = program.GetUniformLocation(TwinMaterialDesc.AlphaBlendPath);
        context.Gl.Uniform1(alphaBlendLoc, @override ? 1.0f : 0.0f);
        _isBlendingEnabled = @override;
        
        _isGlBlendingEnabled = context.Gl.IsEnabled(EnableCap.Blend);
        if (!_isGlBlendingEnabled && @override)
        {
            context.Gl.Enable(EnableCap.Blend);
        }
        else if (_isGlBlendingEnabled && !@override)
        {
            context.Gl.Disable(EnableCap.Blend);
        }
    }

    public void ApplyBlending(TwinShader.AlphaBlendPresets @override)
    {
        if (!_isBlendingEnabled)
        {
            return;
        }

        switch (@override)
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

    public void ApplyDepthWrite(bool @override)
    {
        _isGlDepthWriteEnabled = context.Gl.GetBoolean(GetPName.DepthWritemask);
        context.Gl.DepthMask(@override);
        _isDepthWriteEnabled = @override;
    }

    public void ApplyDepthTest(TwinShader.DepthTestMethod @override)
    {
        if (!_isDepthWriteEnabled)
        {
            return;
        }
        
        switch (@override)
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
    
    public override void Bind()
    {
        _materialDesc.Texture?.Bind();
        ApplyUseTexture(_materialDesc.UseTexture.Equals(1.0f));
        ApplyDoubleColor(_materialDesc.DoubleColor);
        ApplyDeformSpeed(_materialDesc.DeformSpeed);
        ApplyBillboardRender(_materialDesc.BillboardRender);
        ApplyAlphaTest(_materialDesc.AlphaTest);
        ApplyEnvMap(_materialDesc.EnvMap);
        ApplyMetallicSpecular(_materialDesc.MetalicSpecular);
        ApplyBillboardRender(_materialDesc.BillboardRender);
        ApplyUvScroll(_materialDesc.UvScrollSpeed);
        ApplyReflectDistance(_materialDesc.ReflectDist);
        ApplyAlphaBlending(_materialDesc.AlphaBlend.Equals(1.0f));
        ApplyBlending(_materialDesc.BlendFunc);
        ApplyDepthWrite(_materialDesc.DepthWrite);
        ApplyDepthTest(_materialDesc.DepthTest);
        
        // var deformSpeedLoc = program.GetUniformLocation(TwinMaterialDesc.DeformSpeedPath);
        // var billboardRenderLoc = program.GetUniformLocation(TwinMaterialDesc.BillboardRenderPath);
        // var doubleColorLoc = program.GetUniformLocation(TwinMaterialDesc.DoubleColorPath);
        // var reflectDistLoc = program.GetUniformLocation(TwinMaterialDesc.ReflectDistPath);
        // var uvScrollSpeedLoc = program.GetUniformLocation(TwinMaterialDesc.UvScrollSpeedPath);
        // var alphaTestLoc = program.GetUniformLocation(TwinMaterialDesc.AlphaTestPath);
        // var alphaBlendLoc = program.GetUniformLocation(TwinMaterialDesc.AlphaBlendPath);
        // var metalicSpecularLoc = program.GetUniformLocation(TwinMaterialDesc.MetalicSpecularPath);
        // var envMapLoc = program.GetUniformLocation(TwinMaterialDesc.EnvMapPath);
        // var useTextureLoc = program.GetUniformLocation(TwinMaterialDesc.UseTexturePath);
        // context.Gl.Uniform1(useTextureLoc, _materialDesc.UseTexture);
        // context.Gl.Uniform1(alphaTestLoc, _materialDesc.AlphaTest);
        // context.Gl.Uniform1(alphaBlendLoc, _materialDesc.AlphaBlend);
        // context.Gl.Uniform1(metalicSpecularLoc, _materialDesc.MetalicSpecular);
        // context.Gl.Uniform1(envMapLoc, _materialDesc.EnvMap);
        // context.Gl.Uniform1(billboardRenderLoc, _materialDesc.BillboardRender ? 1.0f : 0.0f);
        // context.Gl.Uniform1(doubleColorLoc, _materialDesc.DoubleColor);
        // context.Gl.Uniform2(uvScrollSpeedLoc, _materialDesc.UvScrollSpeed.Values);
        // context.Gl.Uniform2(deformSpeedLoc, _materialDesc.DeformSpeed.Values);
        // context.Gl.Uniform2(reflectDistLoc, _materialDesc.ReflectDist.Values);
        //
        // _isBlendingEnabled = context.Gl.IsEnabled(EnableCap.Blend);
        // if (_materialDesc.AlphaBlend.Equals(1.0f))
        // {
        //     if (!_isBlendingEnabled)
        //     {
        //         context.Gl.Enable(EnableCap.Blend);
        //     }
        //
        //     switch (_materialDesc.BlendFunc)
        //     {
        //         case TwinShader.AlphaBlendPresets.Mix:
        //             context.Gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        //             context.Gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        //             break;
        //         case TwinShader.AlphaBlendPresets.Add:
        //             context.Gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        //             context.Gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.One, BlendingFactor.SrcAlpha, BlendingFactor.One);
        //             break;
        //         case TwinShader.AlphaBlendPresets.Sub:
        //             context.Gl.BlendEquation(BlendEquationModeEXT.FuncReverseSubtract);
        //             context.Gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.One, BlendingFactor.SrcAlpha, BlendingFactor.One);
        //             break;
        //         case TwinShader.AlphaBlendPresets.Alpha:
        //             break;
        //         case TwinShader.AlphaBlendPresets.Zero:
        //             break;
        //         case TwinShader.AlphaBlendPresets.Destination:
        //             break;
        //         case TwinShader.AlphaBlendPresets.Source:
        //             break;
        //     }
        // }
        // else
        // {
        //     if (_isBlendingEnabled)
        //     {
        //         context.Gl.Disable(EnableCap.Blend);
        //     }
        // }
        //
        // _isDepthWriteEnabled = context.Gl.GetBoolean(GetPName.DepthWritemask);
        // if (_materialDesc.DepthWrite)
        // {
        //     context.Gl.DepthMask(true);
        //     switch (_materialDesc.DepthTest)
        //     {
        //         case TwinShader.DepthTestMethod.NEVER:
        //             context.Gl.DepthFunc(DepthFunction.Never);
        //             break;
        //         case TwinShader.DepthTestMethod.ALWAYS:
        //             context.Gl.DepthFunc(DepthFunction.Always);
        //             break;
        //         case TwinShader.DepthTestMethod.GEQUAL:
        //             context.Gl.DepthFunc(DepthFunction.Lequal);
        //             break;
        //         case TwinShader.DepthTestMethod.GREATER:
        //             context.Gl.DepthFunc(DepthFunction.Less);
        //             break;
        //     }
        // }
        // else
        // {
        //     context.Gl.DepthMask(false);
        // }
    }

    public override void Unbind()
    {
        context.Gl.DepthMask(_isGlDepthWriteEnabled);
        if (_isGlBlendingEnabled)
        {
            context.Gl.Enable(EnableCap.Blend);
        }
        else
        {
            context.Gl.Disable(EnableCap.Blend);
        }
    }
}