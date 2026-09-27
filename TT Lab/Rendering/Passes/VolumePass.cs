using Silk.NET.OpenGL;
using TT_Lab.Rendering.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Passes;

/// <summary>
/// Editor volumes (triggers, cameras) are seen through, so they're drawn after the scene without writing depth: their insides first,
/// then their outsides. Every pixel then blends in the same faces whatever order their triangles are in
/// </summary>
public class VolumePass(RenderContext context, string name, ShaderProgram program, TriangleFace culledFace) : RenderPass(context, name, program, TwinShader.Type.ColorOnly)
{
    public override bool StartPass()
    {
        var state = Context.State;
        state.SetDepthTest(true);
        state.SetDepthMask(false);
        state.SetCullFace(true);
        state.SetCulledFace(culledFace);
        return base.StartPass();
    }

    public override void EndPass()
    {
        var state = Context.State;
        state.SetCullFace(false);
        state.SetCulledFace(TriangleFace.Back);
        state.SetDepthMask(true);
    }
}
