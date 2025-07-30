using System;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.ImGuiUtil;

/*
MIT License

- Copyright (c) 2019-2020 Ultz Limited
- Copyright (c) 2021- .NET Foundation and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

public enum TextureCoordinate
{
    S = TextureParameterName.TextureWrapS,
    T = TextureParameterName.TextureWrapT,
    R = TextureParameterName.TextureWrapR
}

public class Texture : IDisposable
{
    public const SizedInternalFormat Srgb8Alpha8 = (SizedInternalFormat)GLEnum.Srgb8Alpha8;
    public const SizedInternalFormat Rgb32F = (SizedInternalFormat)GLEnum.Rgb32f;
    public const GLEnum MaxTextureMaxAnisotropy = (GLEnum)0x84FF;
    public static float? MaxAniso;
    private readonly GL _gl;
    public readonly string Name;
    public readonly uint GlTexture;
    public readonly uint Width, Height;
    public readonly uint MipmapLevels;
    public readonly SizedInternalFormat InternalFormat;

    public unsafe Texture(GL gl, int width, int height, IntPtr data, bool generateMipmaps = false, bool srgb = false)
    {
        _gl = gl;
        MaxAniso ??= gl.GetFloat(MaxTextureMaxAnisotropy);
        Width = (uint)width;
        Height = (uint)height;
        InternalFormat = srgb ? Srgb8Alpha8 : SizedInternalFormat.Rgba8;
        MipmapLevels = (uint)(generateMipmaps == false ? 1 : (int)Math.Floor(Math.Log(Math.Max(Width, Height), 2)));
        GlTexture = _gl.GenTexture();
        Bind();
        PixelFormat pxFormat = PixelFormat.Bgra;
        _gl.TexStorage2D(GLEnum.Texture2D, MipmapLevels, InternalFormat, Width, Height);
        _gl.TexSubImage2D(GLEnum.Texture2D, 0, 0, 0, Width, Height, pxFormat, PixelType.UnsignedByte, (void*)data);
        if (generateMipmaps) _gl.GenerateTextureMipmap(GlTexture);
        SetWrap(TextureCoordinate.S, TextureWrapMode.Repeat);
        SetWrap(TextureCoordinate.T, TextureWrapMode.Repeat);
        _gl.TexParameterI(GLEnum.Texture2D, TextureParameterName.TextureMaxLevel, MipmapLevels - 1);
    }

    public void Bind()
    {
        _gl.BindTexture(GLEnum.Texture2D, GlTexture);
    }

    public void SetMinFilter(TextureMinFilter filter)
    {
        _gl.TexParameterI(GLEnum.Texture2D, TextureParameterName.TextureMinFilter, (int)filter);
    }

    public void SetMagFilter(TextureMagFilter filter)
    {
        _gl.TexParameterI(GLEnum.Texture2D, TextureParameterName.TextureMagFilter, (int)filter);
    }

    public void SetAnisotropy(float level)
    {
        const TextureParameterName textureMaxAnisotropy = (TextureParameterName)0x84FE;
        _gl.TexParameter(GLEnum.Texture2D, (GLEnum)textureMaxAnisotropy,
            Util.Clamp(level, 1, MaxAniso.GetValueOrDefault()));
    }

    public void SetLod(int @base, int min, int max)
    {
        _gl.TexParameterI(GLEnum.Texture2D, TextureParameterName.TextureLodBias, @base);
        _gl.TexParameterI(GLEnum.Texture2D, TextureParameterName.TextureMinLod, min);
        _gl.TexParameterI(GLEnum.Texture2D, TextureParameterName.TextureMaxLod, max);
    }

    public void SetWrap(TextureCoordinate coord, TextureWrapMode mode)
    {
        _gl.TexParameterI(GLEnum.Texture2D, (TextureParameterName)coord, (int)mode);
    }

    public void Dispose()
    {
        _gl.DeleteTexture(GlTexture);
    }
}