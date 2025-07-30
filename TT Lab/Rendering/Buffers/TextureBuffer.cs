using System;
using System.Threading;
using GlmSharp;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Buffers;

public unsafe class TextureBuffer : IDisposable
{
    private readonly RenderContext _renderContext;
    private uint _textureBuffer;
    private byte[] _data;
    
    public TextureBuffer(RenderContext renderContext, ivec2 size)
    {
        _renderContext = renderContext;
        _data = new byte[size.x * size.y * 4];
        _textureBuffer = _renderContext.Gl.GenTexture();
        _renderContext.Gl.BindTexture(TextureTarget.Texture2D, _textureBuffer);
        _renderContext.Gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        fixed (byte* p = _data)
        {
            _renderContext.Gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgba8, (uint)size.x, (uint)size.y, 0,
                GLEnum.Rgba, GLEnum.UnsignedByte, p);
        }

        _renderContext.Gl.TexParameter(GLEnum.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _renderContext.Gl.TexParameter(GLEnum.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _renderContext.Gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Resize(ivec2 newSize)
    {
        _renderContext.Gl.BindTexture(TextureTarget.Texture2D, _textureBuffer);
        _renderContext.Gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        _renderContext.Gl.Finish();
        _data = new byte[newSize.x * newSize.y * 4];
        fixed (byte* p = _data)
        {
            _renderContext.Gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgba8, (uint)newSize.x, (uint)newSize.y, 0,
                GLEnum.Rgba, GLEnum.UnsignedByte, p);
        }

        _renderContext.Gl.BindTexture(TextureTarget.Texture2D, 0);
    }
    
    public uint Handler => _textureBuffer;
    
    public void Dispose()
    {
        if (_textureBuffer == 0)
        {
            return;
        }
        
        _renderContext.Gl.BindTexture(TextureTarget.Texture2D, 0);
        _renderContext.Gl.Finish();
        _renderContext.Gl.DeleteTexture(_textureBuffer);
        _textureBuffer = 0;
    }
}