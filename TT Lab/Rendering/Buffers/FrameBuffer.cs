using System;
using GlmSharp;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Buffers;

public class FrameBuffer : IDisposable
{
    private readonly RenderContext _renderContext;
    private readonly TextureBuffer? _textureBuffer;
    private readonly RenderBuffer? _depthStencilBuffer;
    private uint _fbo;
    
    public FrameBuffer(RenderContext renderContext)
    {
        _renderContext = renderContext;
        _fbo = renderContext.Gl.GenFramebuffer();
    }

    public FrameBuffer(RenderContext renderContext, ivec2 textureAttachmentSize, bool createDepthStencil = false) : this(renderContext)
    {
        _textureBuffer = new TextureBuffer(renderContext, textureAttachmentSize);
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        if (createDepthStencil)
        {
            _depthStencilBuffer = new RenderBuffer(_renderContext);
            _renderContext.Gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthStencilBuffer.Handler);
            _renderContext.Gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)textureAttachmentSize.x, (uint)textureAttachmentSize.y);
            _renderContext.Gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        }
        
        _renderContext.Gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _textureBuffer.Handler, 0);
        if (createDepthStencil)
        {
            _renderContext.Gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depthStencilBuffer!.Handler);
        }
        
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }
    
    public uint Handler => _fbo;
    public TextureBuffer? TextureAttachment => _textureBuffer;
    
    public void Dispose()
    {
        if (_fbo == 0)
        {
            return;
        }
        
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        if (_textureBuffer != null)
        {
            _renderContext.Gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, 0, 0);
        }
        if (_depthStencilBuffer != null)
        {
            _renderContext.Gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, 0);
        }
        
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _renderContext.Gl.DeleteFramebuffer(_fbo);
        _fbo = 0;
        
        _textureBuffer?.Dispose();
        _depthStencilBuffer?.Dispose();
        GC.SuppressFinalize(this);
    }
}