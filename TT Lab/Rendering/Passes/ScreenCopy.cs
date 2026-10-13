using System;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;

namespace TT_Lab.Rendering.Passes;

/// <summary>
/// The reflection surface's copy of the frame: the game copies the frame drawn so far into a 256 by 256 buffer as a material reading it
/// starts drawing (<c>WriteScreenCopy</c>, once per material at its place in its bucket) and draws that instead of the material's texture
/// (<c>MakeScreenCopySettings</c>: clamped, filtered), which <c>MainPass.frag</c> reads as <c>Screen</c>. On the render thread
/// </summary>
public sealed class ScreenCopy(RenderContext context) : IDisposable
{
    private const int CopySize = 256;
    private FrameBuffer? _copy;

    /// <summary>
    /// Copies the frame drawn so far, where the shaders read it (unit 5)
    /// </summary>
    public void Take()
    {
        var gl = context.Gl;
        var width = (int)context.ViewportSize.x;
        var height = (int)context.ViewportSize.y;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (_copy == null)
        {
            _copy = new FrameBuffer(context, new GlmSharp.ivec2(CopySize, CopySize));
            _copy.TextureAttachment!.Bind(TextureUnit.Texture5);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }

        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, context.GetOutputBuffer());
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _copy.Handler);
        gl.BlitFramebuffer(0, 0, width, height, 0, 0, CopySize, CopySize, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, context.GetOutputBuffer());
        _copy.TextureAttachment!.Bind(TextureUnit.Texture5);
        context.State.SetActiveTexture(0);
    }

    public void Dispose()
    {
        _copy?.Dispose();
        _copy = null;
    }
}
