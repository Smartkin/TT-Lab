using System;
using GlmSharp;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Buffers;

public class RenderBuffer : IDisposable
{
    private readonly RenderContext _renderContext;
    private uint _rbo;
    
    public RenderBuffer(RenderContext renderContext)
    {
        _renderContext = renderContext;

        _rbo = _renderContext.Gl.GenRenderbuffer();
    }
    
    public uint Handler => _rbo;
    
    public void Dispose()
    {
        if (_rbo == 0)
        {
            return;
        }
        
        _renderContext.Gl.BindRenderbuffer(GLEnum.Renderbuffer, 0);
        _renderContext.Gl.Finish();
        _renderContext.Gl.DeleteRenderbuffer(_rbo);
        _rbo = 0;
    }
}