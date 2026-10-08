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

    public void ApplyDeform(int mode, float speed, vec3 amplitude)
    {
        Program.SetUniform(KnownUniform.MaterialDeformMode, mode);
        Program.SetUniform(KnownUniform.MaterialDeformSpeed, speed);
        Program.SetUniform(KnownUniform.MaterialDeformAmplitude, amplitude);
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

    public void ApplyUvOffset(vec2 @override)
    {
        Program.SetUniform(KnownUniform.MaterialUvOffset, @override);
    }

    public void ApplyAnimatedColor(vec4 @override)
    {
        Program.SetUniform(KnownUniform.MaterialAnimatedColor, @override);
    }

    // The shader animation's tracks at the render time, where the shader takes them
    private void ApplyAnimation()
    {
        var sample = _materialDesc.Animation != null && (_materialDesc.AnimatesU || _materialDesc.AnimatesV || _materialDesc.AnimatesColor)
            ? ShaderAnimationSampler.At(_materialDesc.Animation, context.Time)
            : ShaderAnimationSampler.Still;
        ApplyUvOffset(new vec2(_materialDesc.AnimatesU ? sample.Uv.x : 0.0f, _materialDesc.AnimatesV ? sample.Uv.y : 0.0f));
        ApplyAnimatedColor(_materialDesc.AnimatesColor ? sample.Color : vec4.Ones);
    }

    public void ApplyAlphaTest(float @override)
    {
        Program.SetUniform(KnownUniform.MaterialAlphaTest, @override);
    }

    public void ApplyEnvMap(float @override)
    {
        Program.SetUniform(KnownUniform.MaterialEnvMap, @override);
    }

    public void ApplyEditorShading(bool @override)
    {
        Program.SetUniform(KnownUniform.MaterialEditorShading, @override ? 1.0f : 0.0f);
    }

    public void ApplyLit(bool @override)
    {
        Program.SetUniform(KnownUniform.MaterialLit, @override ? 1.0f : 0.0f);
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

        // The material shader hands over its color, so Brighten brightens what's behind by the color rather than the alpha. No retail
        // material blends with it
        GsBlending.Apply(context.State, @override);
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
        ApplyDeform(_materialDesc.DeformMode, _materialDesc.DeformSpeed, _materialDesc.DeformAmplitude);
        ApplyBillboardRender(_materialDesc.BillboardRender);
        ApplyAlphaTest(_materialDesc.AlphaTest);
        ApplyEnvMap(_materialDesc.EnvMap);
        ApplyUvScroll(_materialDesc.UvScrollSpeed);
        ApplyAnimation();
        ApplyReflectDistance(_materialDesc.ReflectDist);
        ApplyAlphaBlending(_materialDesc.AlphaBlend.Equals(1.0f));
        ApplyBlending(_materialDesc.BlendFunc);
        ApplyDepthWrite(_materialDesc.DepthWrite);
        ApplyDepthTest(_materialDesc.DepthTest);
        ApplyFog(_materialDesc.PerformFog);
        ApplyEditorShading(_materialDesc.EditorShading);
        ApplyLit(_materialDesc.Lit);
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
