using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Shaders;
using TT_Lab.Rendering.UniformDescs;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Materials;

public class TwinMaterial(RenderContext context, TwinMaterialDesc materialDesc) : Material
{
    private bool _isBlendingEnabled;
    private bool _isDepthWriteEnabled;
    private bool _previousBlend;
    private bool _previousDepthWrite;
    private TwinMaterialDesc _materialDesc = materialDesc;

    public TwinMaterialDesc GetCurrentMaterialDesc() => _materialDesc;

    private ShaderProgram Program => context.CurrentPass.Program;

    public void ApplyFog(bool @override)
    {
        Program.SetUniform(KnownUniform.MaterialPerformFog, @override ? 1.0f : 0.0f);
    }

    public void ApplyDeformSpeed(vec2 @override)
    {
        Program.SetUniform(KnownUniform.MaterialDeformSpeed, @override);
    }

    public void ApplyBillboardRender(bool @override)
    {
        Program.SetUniform(KnownUniform.MaterialBillboardRender, @override ? 1.0f : 0.0f);
    }

    public void ApplyUseTexture(bool @override)
    {
        Program.SetUniform(KnownUniform.MaterialUseTexture, @override ? 1.0f : 0.0f);
    }

    public void ApplyDoubleColor(float @override)
    {
        Program.SetUniform(KnownUniform.MaterialDoubleColor, @override);
    }

    /// <summary>
    /// </summary>
    /// <param name="override">x component whether it's turned on or off and y is the actual distance</param>
    public void ApplyReflectDistance(vec2 @override)
    {
        Program.SetUniform(KnownUniform.MaterialReflectDist, @override);
    }

    public void ApplyUvScroll(vec2 @override)
    {
        Program.SetUniform(KnownUniform.MaterialUvScrollSpeed, @override);
    }

    public void ApplyAlphaTest(float @override)
    {
        Program.SetUniform(KnownUniform.MaterialAlphaTest, @override);
    }

    public void ApplyMetallicSpecular(float @override)
    {
        Program.SetUniform(KnownUniform.MaterialMetalicSpecular, @override);
    }

    public void ApplyEnvMap(float @override)
    {
        Program.SetUniform(KnownUniform.MaterialEnvMap, @override);
    }

    public void ApplyAlphaBlending(bool @override)
    {
        Program.SetUniform(KnownUniform.MaterialAlphaBlend, @override ? 1.0f : 0.0f);
        _isBlendingEnabled = @override;
        context.State.SetBlend(@override);
    }

    public void ApplyBlending(TwinShader.AlphaBlendPresets @override)
    {
        if (!_isBlendingEnabled)
        {
            return;
        }

        var state = context.State;
        switch (@override)
        {
            case TwinShader.AlphaBlendPresets.Mix:
                state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
                state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
                break;
            case TwinShader.AlphaBlendPresets.Add:
                state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
                state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One, BlendingFactor.SrcAlpha, BlendingFactor.One);
                break;
            case TwinShader.AlphaBlendPresets.Sub:
                state.SetBlendEquation(BlendEquationModeEXT.FuncReverseSubtract);
                state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One, BlendingFactor.SrcAlpha, BlendingFactor.One);
                break;
            case TwinShader.AlphaBlendPresets.Alpha:
                state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
                state.SetBlendFunc(BlendingFactor.Zero, BlendingFactor.SrcAlpha, BlendingFactor.Zero, BlendingFactor.One);
                break;
            case TwinShader.AlphaBlendPresets.Zero:
                state.SetBlendEquation(BlendEquationModeEXT.FuncReverseSubtract);
                state.SetBlendFunc(BlendingFactor.Zero, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);
                break;
            case TwinShader.AlphaBlendPresets.Destination:
                state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
                state.SetBlendFunc(BlendingFactor.Zero, BlendingFactor.DstAlpha, BlendingFactor.Zero, BlendingFactor.One);
                break;
            case TwinShader.AlphaBlendPresets.Source:
                state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
                state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.Zero, BlendingFactor.One, BlendingFactor.Zero);
                break;
        }
    }

    public void ApplyDepthWrite(bool @override)
    {
        context.State.SetDepthMask(@override);
        _isDepthWriteEnabled = @override;
    }

    public void ApplyDepthTest(TwinShader.DepthTestMethod @override)
    {
        if (!_isDepthWriteEnabled)
        {
            return;
        }

        var state = context.State;
        switch (@override)
        {
            case TwinShader.DepthTestMethod.NEVER:
                state.SetDepthFunc(DepthFunction.Never);
                break;
            case TwinShader.DepthTestMethod.ALWAYS:
                state.SetDepthFunc(DepthFunction.Always);
                break;
            case TwinShader.DepthTestMethod.GEQUAL:
                state.SetDepthFunc(DepthFunction.Lequal);
                break;
            case TwinShader.DepthTestMethod.GREATER:
                state.SetDepthFunc(DepthFunction.Less);
                break;
        }
    }

    public override void Bind()
    {
        _previousBlend = context.State.Blend;
        _previousDepthWrite = context.State.DepthMask;
        _materialDesc.Texture?.Bind();
        ApplyUseTexture(_materialDesc.UseTexture.Equals(1.0f));
        ApplyDoubleColor(_materialDesc.DoubleColor);
        ApplyDeformSpeed(_materialDesc.DeformSpeed);
        ApplyBillboardRender(_materialDesc.BillboardRender);
        ApplyAlphaTest(_materialDesc.AlphaTest);
        ApplyEnvMap(_materialDesc.EnvMap);
        ApplyMetallicSpecular(_materialDesc.MetalicSpecular);
        ApplyUvScroll(_materialDesc.UvScrollSpeed);
        ApplyReflectDistance(_materialDesc.ReflectDist);
        ApplyAlphaBlending(_materialDesc.AlphaBlend.Equals(1.0f));
        ApplyBlending(_materialDesc.BlendFunc);
        ApplyDepthWrite(_materialDesc.DepthWrite);
        ApplyDepthTest(_materialDesc.DepthTest);
        ApplyFog(_materialDesc.PerformFog);
    }

    public override void Unbind()
    {
        var state = context.State;
        state.SetDepthMask(_previousDepthWrite);
        state.SetBlend(_previousBlend);
        // Materials without depth writes keep whatever depth test is set, which shouldn't depend on what got drawn before them
        state.SetDepthFunc(DepthFunction.Lequal);
    }
}
