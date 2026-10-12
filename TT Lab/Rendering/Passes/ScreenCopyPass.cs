using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Passes;

/// <summary>
/// The reflection surface's passes: the game copies the frame drawn so far into a 256 by 256 buffer before the draw (<c>WriteScreenCopy</c>)
/// and draws that instead of the material's texture (<c>MakeScreenCopySettings</c>: clamped, filtered), which <c>MainPass.frag</c> reads
/// as <c>Screen</c>. The opaque pass goes after every other opaque pass, so the copy has the rest of the scenery
/// </summary>
public class ScreenCopyPass : RenderPass
{
    private const int CopySize = 256;
    private FrameBuffer? _copy;

    public ScreenCopyPass(RenderContext context, string name, ShaderProgram program, TwinShader.Type type) : base(context, name, program, type)
    {
        // The copy is made once, when the first reflection draws, and let go of with the context, on the render thread like the renderer's
        // own buffers
        context.Destroy += ReleaseCopy;
    }

    private void ReleaseCopy()
    {
        Context.Destroy -= ReleaseCopy;
        _copy?.Dispose();
        _copy = null;
    }

    public override bool StartPass()
    {
        var started = base.StartPass();
        CopyFrame();
        return started;
    }

    public override void EndPass()
    {
    }

    private void CopyFrame()
    {
        var gl = Context.Gl;
        var width = (int)Context.ViewportSize.x;
        var height = (int)Context.ViewportSize.y;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (_copy == null)
        {
            _copy = new FrameBuffer(Context, new GlmSharp.ivec2(CopySize, CopySize));
            _copy.TextureAttachment!.Bind(TextureUnit.Texture5);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }

        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Context.GetOutputBuffer());
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _copy.Handler);
        gl.BlitFramebuffer(0, 0, width, height, 0, 0, CopySize, CopySize, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, Context.GetOutputBuffer());
        _copy.TextureAttachment!.Bind(TextureUnit.Texture5);
        gl.ActiveTexture(TextureUnit.Texture0);
    }
}
