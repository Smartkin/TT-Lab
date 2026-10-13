using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using GlmSharp;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Buffers;

public unsafe class TextureBuffer : IDisposable
{
    private readonly RenderContext _renderContext;
    private uint _textureBuffer;
    private byte[] _data;
    private readonly InternalFormat _internalFormat = InternalFormat.Rgba8;
    private readonly PixelFormat _pixelFormat = PixelFormat.Bgra;
    private readonly PixelType _pixelType = PixelType.UnsignedByte;
    // Bindless handles of the texture with the samplers it's drawn with, after which the texture can't change
    private Dictionary<uint, ulong>? _handles;

    /// <summary>
    /// Changes when the texture's handles go (the texture made again or deleted), the ones taken before are no use then
    /// </summary>
    public int Generation { get; private set; }

    private static int _handlesReleased;

    /// <summary>
    /// Changes whenever any texture's handles go
    /// </summary>
    public static int HandlesReleased => Volatile.Read(ref _handlesReleased);

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
    public TextureBuffer(RenderContext renderContext, Span<byte> data, uint width, uint height, InternalFormat internalFormat = InternalFormat.Rgba8, PixelFormat pixelFormat = PixelFormat.Bgra, PixelType pixelType = PixelType.UnsignedByte) : this(renderContext)
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
        _data = new byte[(int)bitmap.Size.Width * (int)bitmap.Size.Height * 4];
        CopyImageData(bitmap);
        
        InitDefaultParams((uint)bitmap.Size.Width, (uint)bitmap.Size.Height);
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

    /// <summary>
    /// The texture with the sampler as a bindless handle (<see cref="BindlessTextures"/>), made the first time it's asked for
    /// </summary>
    public ulong GetHandle(BindlessTextures bindless, uint sampler)
    {
        _handles ??= [];
        if (!_handles.TryGetValue(sampler, out var handle))
        {
            handle = bindless.MakeHandle(_textureBuffer, sampler);
            _handles[sampler] = handle;
        }

        return handle;
    }

    // A texture with handles can't change: it's made again
    private void ReleaseHandles()
    {
        if (_handles == null)
        {
            return;
        }

        foreach (var handle in _handles.Values)
        {
            _renderContext.Bindless?.Release(handle);
        }

        _handles = null;
        Generation++;
        Interlocked.Increment(ref _handlesReleased);
        _renderContext.State.ForgetTexture(_textureBuffer);
        _renderContext.Gl.DeleteTexture(_textureBuffer);
        _textureBuffer = _renderContext.Gl.GenTexture();
        Bind();
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
        Unbind();
    }

    public void InvalidateWithData(Bitmap bitmap)
    {
        ReleaseHandles();
        _data = new byte[(int)bitmap.Size.Width * (int)bitmap.Size.Height * 4];
        CopyImageData(bitmap);
        Bind();
        UploadTextureData((uint)bitmap.Size.Width, (uint)bitmap.Size.Height);
        Unbind();
    }

    public void Resize(ivec2 newSize)
    {
        ReleaseHandles();
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

    /// <summary>
    /// Mips averaging the picture, as many levels as asked (the largest included): the samplers' mip filter reads no further
    /// </summary>
    public void GenerateMipmaps(int levels)
    {
        Bind();
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, Math.Max(levels - 1, 0));
        _renderContext.Gl.GenerateMipmap(TextureTarget.Texture2D);
        Unbind();
    }

    // Through the state cache, so a material binding the texture its unit already holds makes no GL call
    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        _renderContext.State.BindTexture(unit - TextureUnit.Texture0, _textureBuffer);
    }

    public void Unbind()
    {
        _renderContext.State.UnbindActiveTexture();
    }

    private void InitDefaultParams(uint width, uint height)
    {
        Bind();
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        // Complete without mips under the materials' samplers, which filter through mips
        _renderContext.Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
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
        var dataHandle = GCHandle.Alloc(_data, GCHandleType.Pinned);
        bitmap.CopyPixels(new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), dataHandle.AddrOfPinnedObject(), _data.Length, bitmap.PixelSize.Width * 4);
        dataHandle.Free();
    }
    
    public uint Handler => _textureBuffer;
    
    public void Dispose()
    {
        if (_textureBuffer == 0)
        {
            return;
        }
        
        _renderContext.State.UnbindActiveTexture();
        _renderContext.Gl.Finish();
        if (_handles != null)
        {
            foreach (var handle in _handles.Values)
            {
                _renderContext.Bindless?.Release(handle);
            }

            Interlocked.Increment(ref _handlesReleased);
        }

        _handles = null;
        Generation++;
        _renderContext.State.ForgetTexture(_textureBuffer);
        _renderContext.Gl.DeleteTexture(_textureBuffer);
        _textureBuffer = 0;
    }
}