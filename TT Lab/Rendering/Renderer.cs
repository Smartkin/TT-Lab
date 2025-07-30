using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using GlmSharp;
using ImGuiNET;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using TT_Lab.Rendering.Buffers;
using Action = System.Action;
using Buffer = System.Buffer;

namespace TT_Lab.Rendering;

public class Renderer : IDisposable
{
    private readonly RenderContext _renderContext;
    private FrameBuffer _renderBuffer;
    private RenderBuffer _depthStencilBuffer;
    private TextureBuffer _colorBuffer;
    private ivec2 _frameBufferSize = ivec2.Ones;
    private readonly ImGuiController _imgui;
    private float _delta = 0.01f;
    private bool _isDisposed;
    private Stopwatch _renderWatch = new();

    public Renderer(RenderContext renderContext)
    {
        _renderContext = renderContext;
        SetupRenderBuffer();
        _imgui = new ImGuiController(renderContext, this);
        _renderContext.Gl.Enable(EnableCap.ScissorTest);
        _renderWatch.Start();
    }
    
    public ivec2 GetFrameBufferSize() => _frameBufferSize;

    public void SetFrameBufferSize(ivec2 frameBufferSize)
    {
        _renderWatch.Restart();
        _frameBufferSize = frameBufferSize;
        DeleteRenderBuffer();
        SetupRenderBuffer();
        Resize?.Invoke(new Vector2D<Int32>(_frameBufferSize.x, _frameBufferSize.y));
        FramebufferResize?.Invoke(new Vector2D<Int32>(_frameBufferSize.x, _frameBufferSize.y));
    }

    public void DoRender()
    {
        _delta = Math.Clamp(_renderWatch.ElapsedMilliseconds / 1000.0f, 0.01f, 1.0f);
        _renderWatch.Restart();
        if (_frameBufferSize == ivec2.Ones)
        {
            return;
        }
        
        _renderContext.MakeCurrent();
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, _renderBuffer.Handler);
        _renderContext.Gl.Viewport(0, 0, (uint)_frameBufferSize.x, (uint)_frameBufferSize.y);
        _renderContext.Gl.Scissor(0, 0, (uint)_frameBufferSize.x, (uint)_frameBufferSize.y);
        
        _imgui.Update(_delta);
        
        _renderContext.Gl.ClearColor(Color.Gray);
        _renderContext.Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        
        ImGui.ShowDemoWindow();
        ImGui.ShowMetricsWindow();

        _imgui.Render();
        
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _renderContext.Gl.Finish();
    }

    public unsafe void GetRenderImage(WriteableBitmap bitmap)
    {
        _renderContext.MakeCurrent();
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, _renderBuffer.Handler);
        var memSize = _frameBufferSize.x * _frameBufferSize.y * 4;
        var pixels = new byte[memSize];
        fixed (byte* p = pixels)
        {
            _renderContext.Gl.PixelStore(GLEnum.PackAlignment, 1);
            _renderContext.Gl.Finish();
            _renderContext.Gl.ReadPixels(0, 0, (uint)_frameBufferSize.x, (uint)_frameBufferSize.y, GLEnum.Bgra,
                GLEnum.UnsignedByte, p);
        }
        
        FlipY(pixels, _frameBufferSize.x, _frameBufferSize.y);

        bitmap.WritePixels(new Int32Rect(0, 0, _frameBufferSize.x, _frameBufferSize.y), pixels, bitmap.BackBufferStride, 0);
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }
    
    private static void FlipY(byte[] pixels, int width, int height)
    {
        var rowSize = width * 4; // 4 bytes per pixel (RGBA)
        var tempRow = new byte[rowSize];

        for (var y = 0; y < height / 2; y++)
        {
            var topOffset = y * rowSize;
            var bottomOffset = (height - 1 - y) * rowSize;

            // Swap rows
            Buffer.BlockCopy(pixels, topOffset, tempRow, 0, rowSize);
            Buffer.BlockCopy(pixels, bottomOffset, pixels, topOffset, rowSize);
            Buffer.BlockCopy(tempRow, 0, pixels, bottomOffset, rowSize);
        }
    }

    [MemberNotNull(nameof(_renderBuffer))]
    [MemberNotNull(nameof(_depthStencilBuffer))]
    [MemberNotNull(nameof(_colorBuffer))]
    private void SetupRenderBuffer()
    {
        _renderContext.MakeCurrent();
        _renderBuffer = new FrameBuffer(_renderContext);
        _depthStencilBuffer = new RenderBuffer(_renderContext);
        _colorBuffer = new TextureBuffer(_renderContext, _frameBufferSize);
        
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, _renderBuffer.Handler);
        _renderContext.Gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthStencilBuffer.Handler);
        _renderContext.Gl.RenderbufferStorage(GLEnum.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)_frameBufferSize.x, (uint)_frameBufferSize.y);
        _renderContext.Gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        
        _renderContext.Gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _colorBuffer.Handler, 0);
        _renderContext.Gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depthStencilBuffer.Handler);
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void DeleteRenderBuffer()
    {
        _renderContext.MakeCurrent();
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, _renderBuffer.Handler);
        _renderContext.Gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, 0, 0);
        _renderContext.Gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, 0);
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        _renderBuffer.Dispose();
        _depthStencilBuffer.Dispose();
        _colorBuffer.Dispose();
    }
    
    public bool IsDisposed => _isDisposed;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _imgui.Dispose();
        DeleteRenderBuffer();
        
        _isDisposed = true;
    }

    public event Action<Vector2D<Int32>>? Resize;
    public event Action<Vector2D<Int32>>? FramebufferResize;
}