using Silk.NET.OpenGL;
using TT_Lab.Rendering.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Passes;

/// <summary>
/// What's about to be placed (a prefab dragged over the scene) is drawn after the scene, once the depth is all there: first as it looks,
/// marking the pixels it shows on in the stencil, then as a see-through silhouette of its color wherever something's in front of it,
/// every pixel blended once however many of its triangles are there
/// </summary>
public class PreviewPass(RenderContext context, string name, ShaderProgram program, bool silhouette) : RenderPass(context, name, program, TwinShader.Type.StandardUnlit)
{
    public bool IsSilhouette => silhouette;

    public override bool StartPass()
    {
        if (silhouette)
        {
            Context.State.SetStencil(StencilFunction.Equal, 0, StencilOp.Keep, StencilOp.Keep, StencilOp.Incr);
        }
        else
        {
            Context.State.SetStencil(StencilFunction.Always, 1, StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
        }

        return base.StartPass();
    }

    public override void EndPass()
    {
        Context.State.SetStencil(StencilFunction.Always, 0, StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
    }

    /// <summary>
    /// Over the material's own state: blended over everything without writing depth, in the instances' color
    /// </summary>
    public void BeginSilhouette()
    {
        var state = Context.State;
        state.SetBlend(true);
        state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
        state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        state.SetDepthTest(false);
        state.SetDepthMask(false);
        Program.SetUniform(KnownUniform.DiffuseOnly, 1.0f);
    }

    public void EndSilhouette()
    {
        Program.SetUniform(KnownUniform.DiffuseOnly, 0.0f);
        Context.State.SetDepthTest(true);
    }
}
