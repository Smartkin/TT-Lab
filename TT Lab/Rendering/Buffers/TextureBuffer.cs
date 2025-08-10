using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using GlmSharp;
using Silk.NET.OpenGL;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace TT_Lab.Rendering.Buffers;

public unsafe class TextureBuffer : IDisposable
{
    private readonly RenderContext _renderContext;
    private uint _textureBuffer;
    private byte[] _data;
    private readonly InternalFormat _internalFormat = InternalFormat.Rgba8;
    private readonly Silk.NET.OpenGL.PixelFormat _pixelFormat = Silk.NET.OpenGL.PixelFormat.Bgra;
    private readonly PixelType _pixelType = PixelType.UnsignedByte;

    private TextureBuffer(RenderContext renderContext)
    {
        _renderContext = renderContext;
        _textureBuffer = _renderContext.Gl.GenTexture();
        _data = [];
    }

    /// <summary>
    /// Allocate texture with custom data and custom format
    /// </summary>
    /// <param name="renderContext"></param>
    /// <param name="data"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="internalFormat"></param>
    /// <param name="pixelFormat"></param>
    /// <param name="pixelType"></param>
    public TextureBuffer(RenderContext renderContext, Span<byte> data, uint width, uint height, InternalFormat internalFormat = InternalFormat.Rgba8, Silk.NET.OpenGL.PixelFormat pixelFormat = Silk.NET.OpenGL.PixelFormat.Bgra, PixelType pixelType = PixelType.UnsignedByte) : this(renderContext)
    {
        _data = data.ToArray();

        _internalFormat = internalFormat;
        _pixelFormat = pixelFormat;
        _pixelType = pixelType;
        InitDefaultParams(width, height);
    }

    /// <summary>
    /// Allocate texture with bitmap (Bitmap must be in 32bpp format)
    /// </summary>
    /// <param name="renderContext"></param>
    /// <param name="bitmap"></param>
    public TextureBuffer(RenderContext renderContext, Bitmap bitmap) : this(renderContext)
    {
        _data = new byte[bitmap.Width * bitmap.Height * 4];
        CopyImageData(bitmap);
        
        InitDefaultParams((uint)bitmap.Width, (uint)bitmap.Height);
    }
    
    /// <summary>
    /// Allocate texture of arbitrary size that's tightly packed and can be freely read from
    /// </summary>
    /// <param name="renderContext"></param>
    /// <param name="size"></param>
    public TextureBuffer(RenderContext renderContext, ivec2 size) : this(renderContext)
    {
        _data = new byte[size.x * size.y * 4];
        _textureBuffer = _renderContext.Gl.GenTexture();
        Bind();
        fixed (byte* p = _data)
        {
            _renderContext.Gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgba8, (uint)size.x, (uint)size.y, 0,
                GLEnum.Bgra, GLEnum.UnsignedByte, p);
        }

        _renderContext.Gl.TexParameter(GLEnum.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _renderContext.Gl.TexParameter(GLEnum.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        Unbind();
    }

    public void InvalidateWithData(Bitmap bitmap)
    {
        _data = new byte[bitmap.Width * bitmap.Height * 4];
        CopyImageData(bitmap);
        UploadTextureData((uint)bitmap.Width, (uint)bitmap.Height);
    }

    public void Resize(ivec2 newSize)
    {
        Bind();
        _renderContext.Gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        _renderContext.Gl.Finish();
        _data = new byte[newSize.x * newSize.y * 4];
        fixed (byte* p = _data)
        {
            _renderContext.Gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgba8, (uint)newSize.x, (uint)newSize.y, 0,
                GLEnum.Bgra, GLEnum.UnsignedByte, p);
        }
        Unbind();
    }

    public void GenerateMipmaps()
    {
        Bind();
        _renderContext.Gl.GenerateMipmap(TextureTarget.Texture2D);
        Unbind();
    }

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        _renderContext.Gl.ActiveTexture(unit);
        _renderContext.Gl.BindTexture(TextureTarget.Texture2D, _textureBuffer);
    }

    public void Unbind()
    {
        _renderContext.Gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private void InitDefaultParams(uint width, uint height)
    {
        Bind();
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        UploadTextureData(width, height);
        Unbind();
    }

    private void UploadTextureData(uint width, uint height)
    {
        fixed (byte* p = _data)
        {
            _renderContext.Gl.TexImage2D(GLEnum.Texture2D, 0, _internalFormat, width, height, 0, _pixelFormat, _pixelType, p);
        }
    }

    private void CopyImageData(Bitmap bitmap)
    {
        var imageData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        Marshal.Copy(imageData.Scan0, _data, 0, _data.Length);
        bitmap.UnlockBits(imageData);
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