using Silk.NET.OpenGL;
using TT_Lab.Rendering.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Passes;

/// <summary>
/// Particles blend over everything else and don't hide each other
/// </summary>
public class ParticlePass(RenderContext context, string name, ShaderProgram program) : RenderPass(context, name, program, TwinShader.Type.Particle)
{
    public override bool StartPass()
    {
        var state = Context.State;
        state.SetBlend(true);
        state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
        state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        state.SetDepthTest(true);
        state.SetDepthFunc(DepthFunction.Lequal);
        state.SetDepthMask(false);
        state.SetCullFace(false);
        return base.StartPass();
    }

    public override void EndPass()
    {
        Context.State.SetDepthMask(true);
    }
}
