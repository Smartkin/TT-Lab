using System;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Buffers;

public class FrameBuffer : IDisposable
{
    private readonly RenderContext _renderContext;
    private uint _fbo;
    
    public FrameBuffer(RenderContext renderContext)
    {
        _renderContext = renderContext;
        _fbo = renderContext.Gl.GenFramebuffer();
    }
    
    public uint Handler => _fbo;
    
    public void Dispose()
    {
        if (_fbo == 0)
        {
            return;
        }
        
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _renderContext.Gl.Finish();
        _renderContext.Gl.DeleteFramebuffer(_fbo);
        _fbo = 0;
    }
}