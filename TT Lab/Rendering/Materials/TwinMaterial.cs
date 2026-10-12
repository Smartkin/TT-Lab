using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Shaders;
using TT_Lab.Rendering.UniformDescs;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Materials;

public class TwinMaterial(RenderContext context, TwinMaterialDesc materialDesc) : Material
{
    private bool _isBlendingEnabled;
    private bool _previousBlend;
    private bool _previousDepthWrite;
    private TwinMaterialDesc _materialDesc = materialDesc;

    public TwinMaterialDesc GetCurrentMaterialDesc() => _materialDesc;

    private ShaderProgram Program => context.CurrentPass.Program;

    public void ApplyFog(int @override)
    {
        Program.SetUniform(KnownUniform.MaterialFog, @override);
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

    /// <summary>
    /// </summary>
    /// <param name="override">x component whether it's turned on or off and y is the actual distance</param>
    public void ApplyReflectDistance(vec2 @override)
    {
        Program.SetUniform(KnownUniform.MaterialReflectDist, @override);
    }

    public void ApplyUvOffset(vec2 @override)
    {
        Program.SetUniform(KnownUniform.MaterialUvOffset, @override);
    }

    // The scrolls and the shader animation's U and V tracks at the render time, where the shader takes them. The color the animation drives
    // goes into the shader's color, which only the shadows' volumes' program reads (the shader record's second quadword): the game draws
    // every other shader the same whatever it is
    private void ApplyAnimation()
    {
        var animation = _materialDesc.Animation != null && (_materialDesc.AnimatesU || _materialDesc.AnimatesV)
            ? ShaderAnimationSampler.At(_materialDesc.Animation, context.Time).Uv
            : vec2.Zero;
        ApplyUvOffset(ShaderScroll.At(_materialDesc.ScrollU, _materialDesc.ScrollV, _materialDesc.ScrollPhases, _materialDesc.ScrollSpeeds, context.Time, animation));
    }

    public void ApplyAlphaTest(int method, int reference, bool failDrawsColor)
    {
        Program.SetUniform(KnownUniform.MaterialAlphaMethod, method);
        Program.SetUniform(KnownUniform.MaterialAlphaReference, (float)reference);
        Program.SetUniform(KnownUniform.MaterialAlphaFailDraws, failDrawsColor ? 1.0f : 0.0f);
    }

    // The GS's mip choice (TEX1) and the sampler of its filter and coordinates, over the texture's own settings
    private void ApplyTextureSampling()
    {
        Program.SetUniform(KnownUniform.MaterialMipmapped, _materialDesc.Mipmapped ? 1.0f : 0.0f);
        Program.SetUniform(KnownUniform.MaterialLodScale, (float)(1 << _materialDesc.LodL));
        Program.SetUniform(KnownUniform.MaterialLodK, _materialDesc.LodK);
        context.Gl.BindSampler(0, context.Samplers.Get(_materialDesc.MagnifiesLinearly, _materialDesc.ClampsCoordinates));
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

        if (_materialDesc.CustomBlend)
        {
            GsBlending.ApplyFormula(context, _materialDesc.BlendA, _materialDesc.BlendB, _materialDesc.BlendC, _materialDesc.BlendD, _materialDesc.BlendFix);
            return;
        }

        // The material shader hands over its color, so Brighten brightens what's behind by the color rather than the alpha. No retail
        // material blends with it
        GsBlending.Apply(context.State, @override);
    }

    public void ApplyDepthWrite(bool @override)
    {
        context.State.SetDepthMask(@override);
    }

    // The GS tests the depth whether it writes it or not
    public void ApplyDepthTest(TwinShader.DepthTestMethod @override)
    {
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
        ApplyTextureSampling();
        ApplyDeform(_materialDesc.DeformMode, _materialDesc.DeformSpeed, _materialDesc.DeformAmplitude);
        ApplyBillboardRender(_materialDesc.BillboardRender);
        ApplyAlphaTest(_materialDesc.AlphaTestMethod, _materialDesc.AlphaReference, _materialDesc.AlphaFailDrawsColor);
        ApplyEnvMap(_materialDesc.EnvMap);
        ApplyAnimation();
        ApplyReflectDistance(_materialDesc.ReflectDist);
        ApplyAlphaBlending(_materialDesc.AlphaBlend.Equals(1.0f));
        ApplyBlending(_materialDesc.BlendFunc);
        ApplyDepthWrite(_materialDesc.DepthWrite);
        ApplyDepthTest(_materialDesc.DepthTest);
        ApplyFog(_materialDesc.Fog);
        ApplyEditorShading(_materialDesc.EditorShading);
        ApplyLit(_materialDesc.Lit);
    }

    public override void Unbind()
    {
        var state = context.State;
        state.SetDepthMask(_previousDepthWrite);
        state.SetBlend(_previousBlend);
        context.Gl.BindSampler(0, 0);
        // Materials without depth writes keep whatever depth test is set, which shouldn't depend on what got drawn before them
        state.SetDepthFunc(DepthFunction.Lequal);
    }
}
