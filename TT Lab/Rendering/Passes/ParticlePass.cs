using Silk.NET.OpenGL;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Passes;

/// <summary>
/// Particles draw over everything else. The emitters queue their particles while the pass goes through them and the pass draws them
/// all at its end, each blend mode with its own blending in the order the game draws them
/// </summary>
public class ParticlePass(RenderContext context, string name, ShaderProgram program) : RenderPass(context, name, program, TwinShader.Type.Particle)
{
    public override bool StartPass()
    {
        var state = Context.State;
        state.SetBlend(true);
        state.SetDepthTest(true);
        state.SetDepthFunc(DepthFunction.Lequal);
        state.SetDepthMask(false);
        state.SetCullFace(false);
        return base.StartPass();
    }

    public override void EndPass()
    {
        ParticleEmitter.DrawQueued(Context);
        var state = Context.State;
        state.SetBlend(true);
        state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
        state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        state.SetDepthFunc(DepthFunction.Lequal);
        state.SetDepthMask(true);
    }
}
