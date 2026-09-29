using TT_Lab.Rendering.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Passes;

/// <summary>
/// The skydome's parts, blended or not, painted over each other in their order around the camera (FUN_001ba350): every sky material of
/// the game tests no depth, and the game resets the depth once the sky is drawn so the level always draws over it
/// </summary>
public class SkydomePass(RenderContext context, string name, ShaderProgram program) : RenderPass(context, name, program, TwinShader.Type.UnlitSkydome)
{
    public override bool StartPass()
    {
        var started = base.StartPass();
        Context.State.SetDepthTest(false);
        Program.SetUniform(KnownUniform.FollowsCamera, true);
        return started;
    }

    public override void EndPass()
    {
        Program.SetUniform(KnownUniform.FollowsCamera, false);
        Context.State.SetDepthTest(true);
    }
}
