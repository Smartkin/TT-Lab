using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering;
using Veldrid.Sdl2;

namespace TT_Lab.Controls;

/// <summary>
/// A viewport's GL context, render thread and last frame, kept for as long as the editor showing it is open
/// </summary>
/// <remarks>
/// Controls showing the viewport come and go while the editor stays open (switching tabs, floating them into another window), so
/// the context doesn't belong to them: a control only shows the frames and passes on the input while it's attached. The context
/// renders into a framebuffer of its own, its window is a hidden 1x1 one only there because SDL makes contexts for windows
/// </remarks>
public sealed class ViewportHost : IDisposable
{
    // SDL isn't thread safe and every viewport creates its window and GL context on its own render thread
    private static readonly object SdlLock = new();
    private static bool _isGlLibraryPinned;
    private const int GlContextCreationAttempts = 5;
    private const double FrameTime = 1.0 / 60.0;
    // Viewports nobody sees only keep handling queued work, like building their scene
    private const double HiddenFrameTime = 1.0 / 10.0;
    private static readonly long DisplayTimeout = Stopwatch.Frequency / 2;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SdlGlLoadLibrary(IntPtr path);

    private readonly object _frameLock = new();
    private readonly AutoResetEvent _wakeRenderThread = new(false);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly HashSet<string> _reportedErrors = [];
    private readonly uint[] _readbackBuffers = new uint[2];
    private Thread? _renderThread;
    private Viewport? _presenter;
    private WriteableBitmap? _frame;
    private PixelSize _size;
    private long _lastDisplayed;
    private int _invalidatePending;
    private Sdl2Window? _sdlWindow;
    private IntPtr _glContext;
    private RenderContext? _context;
    private uint _outputFramebuffer;
    private uint _outputColor;
    private uint _outputDepthStencil;
    private PixelSize _outputSize;
    private PixelSize _readbackSize;
    private int _readbackIndex;
    private bool _hasPendingReadback;
    private bool _isDisposed;
    private volatile bool _hasFailed;

    /// <summary>
    /// Raised on the UI thread once the context is made, it lives until the host is disposed
    /// </summary>
    public event Action<RenderContext>? Initialized;

    /// <summary>
    /// Raised on the UI thread when another control starts showing the viewport or the last one stops
    /// </summary>
    public event Action<Viewport?>? PresenterChanged;

    public Viewport? Presenter => _presenter;

    public void Attach(Viewport presenter)
    {
        if (_isDisposed || _presenter == presenter)
        {
            return;
        }

        _presenter = presenter;
        Resize(presenter.Bounds.Size);
        PresenterChanged?.Invoke(presenter);
        // A context that couldn't be made gets another try when the viewport is shown again
        if (_renderThread == null || (_hasFailed && !_renderThread.IsAlive))
        {
            _hasFailed = false;
            _renderThread = new Thread(() => RenderThread(_cancellation.Token))
            {
                Name = $"Viewport Render Thread {GetHashCode()}",
                IsBackground = true
            };
            _renderThread.Start();
        }

        _wakeRenderThread.Set();
        InvalidatePresenter();
    }

    public void Detach(Viewport presenter)
    {
        if (_presenter != presenter)
        {
            return;
        }

        _presenter = null;
        Interlocked.Exchange(ref _lastDisplayed, 0);
        PresenterChanged?.Invoke(null);
    }

    public void Resize(Size size)
    {
        var pixelSize = new PixelSize(Math.Max((int)size.Width, 1), Math.Max((int)size.Height, 1));
        lock (_frameLock)
        {
            if (_frame != null && _size == pixelSize)
            {
                return;
            }

            // Drawing operations keep their own reference to the bitmap, disposing it doesn't pull it from under a frame being composed
            _frame?.Dispose();
            _frame = new WriteableBitmap(pixelSize, new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Unpremul);
            _size = pixelSize;
        }

        _wakeRenderThread.Set();
    }

    /// <summary>
    /// Draws the last frame and tells the render thread someone sees its frames
    /// </summary>
    public void Draw(DrawingContext context, Viewport presenter)
    {
        lock (_frameLock)
        {
            if (_frame != null)
            {
                context.DrawImage(_frame, new Rect(0, 0, presenter.Bounds.Width, presenter.Bounds.Height));
            }
        }

        // Invisible controls can still get rendered when they keep invalidating themselves
        if (presenter != _presenter || !presenter.IsEffectivelyVisible)
        {
            return;
        }

        var previous = Interlocked.Exchange(ref _lastDisplayed, Stopwatch.GetTimestamp());
        if (Stopwatch.GetTimestamp() - previous >= DisplayTimeout)
        {
            _wakeRenderThread.Set();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_presenter != null)
        {
            Detach(_presenter);
        }

        // The render thread cleans up after itself, waiting for it here could wait on work it queued for the UI thread
        _cancellation.Cancel();
        _wakeRenderThread.Set();
    }

    private PixelSize GetSize()
    {
        lock (_frameLock)
        {
            return _size;
        }
    }

    private void RenderThread(CancellationToken token)
    {
        try
        {
            RenderLoop(token);
        }
        catch (Exception ex)
        {
            // An exception leaving a thread of its own ends the whole application
            Log.WriteLine($"Viewport stopped working: {ex}", Log.LogType.Error);
        }
        finally
        {
            Cleanup();
        }
    }

    private void RenderLoop(CancellationToken token)
    {
        if (!TryCreateGlContext(token))
        {
            _hasFailed = true;
            return;
        }

        try
        {
            _context = new RenderContext(GL.GetApi(Sdl2Native.SDL_GL_GetProcAddress));
            SyncOutputSize();
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Failed to initialize viewport: {ex.Message}\n Try to reopen the tab or reopen the application.", Log.LogType.Error);
            _hasFailed = true;
            return;
        }

        var context = _context;
        context.SetGlAccessibility(false);
        Dispatcher.UIThread.Post(() =>
        {
            if (!_isDisposed)
            {
                Initialized?.Invoke(context);
            }
        });

        var stopwatch = Stopwatch.StartNew();
        var lastFrame = stopwatch.Elapsed.TotalSeconds;
        while (!token.IsCancellationRequested)
        {
            var isDisplayed = Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastDisplayed) < DisplayTimeout;
            var nextFrame = lastFrame + (isDisplayed ? FrameTime : HiddenFrameTime);
            var wait = nextFrame - stopwatch.Elapsed.TotalSeconds;
            if (wait > 0.0)
            {
                // Rendering as fast as possible only to show every few frames kept the GPU and a core busy for nothing
                WaitHandle.WaitAny([token.WaitHandle, _wakeRenderThread], TimeSpan.FromSeconds(wait));
                continue;
            }

            var now = stopwatch.Elapsed.TotalSeconds;
            var delta = (float)(now - lastFrame);
            lastFrame = now;
            context.SetGlAccessibility(true);
            try
            {
                SyncOutputSize();
                if (isDisplayed)
                {
                    // ImGui leaves its scissor behind
                    context.Gl.Viewport(0, 0, (uint)_outputSize.Width, (uint)_outputSize.Height);
                    context.Gl.Scissor(0, 0, (uint)_outputSize.Width, (uint)_outputSize.Height);
                    context.PerformRender(delta);
                    ReadBackFrame();
                }
                else
                {
                    context.ProcessRenderQueue();
                }

                // Frames only get rendered once the control drew one. Dock keeps the content of the tabs that aren't shown, only hiding it,
                // and a control shown again keeps what it showed without drawing until something invalidates it, like resizing it
                if (!isDisplayed && Volatile.Read(ref _presenter) != null)
                {
                    InvalidatePresenter();
                }
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
            finally
            {
                context.SetGlAccessibility(false);
            }

            if (isDisplayed)
            {
                InvalidatePresenter();
            }
        }
    }

    // A frame going wrong the same way every time would fill the log
    private void ReportError(Exception ex)
    {
        if (_reportedErrors.Add($"{ex.GetType()}{ex.Message}{ex.StackTrace}"))
        {
            Log.WriteLine($"Viewport error: {ex}", Log.LogType.Error);
        }
    }

    private void InvalidatePresenter()
    {
        if (Interlocked.Exchange(ref _invalidatePending, 1) == 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _invalidatePending, 0);
            _presenter?.InvalidateVisual();
        });
    }

    private void Cleanup()
    {
        if (_context != null)
        {
            _context.SetGlAccessibility(true);
            try
            {
                DeleteReadbackBuffers();
                DeleteOutputFramebuffer();
                _context.Dispose();
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Failed to release viewport: {ex}", Log.LogType.Error);
            }

            _context = null;
        }

        if (_sdlWindow != null)
        {
            DestroyGlContext();
        }

        lock (_frameLock)
        {
            _frame?.Dispose();
            _frame = null;
        }
    }

    private bool TryCreateGlContext(CancellationToken token)
    {
        string? lastError = null;
        for (var attempt = 1; attempt <= GlContextCreationAttempts && !token.IsCancellationRequested; attempt++)
        {
            string? error;
            lock (SdlLock)
            {
                PinGlLibrary();
                _sdlWindow = new Sdl2Window($"VIEWPORT_{GetHashCode()}", 0, 0, 1, 1,
                    SDL_WindowFlags.Borderless | SDL_WindowFlags.Hidden | SDL_WindowFlags.SkipTaskbar | SDL_WindowFlags.OpenGL,
                    false);

                _glContext = Sdl2Native.SDL_GL_CreateContext(_sdlWindow.SdlWindowHandle);
                if (_glContext != IntPtr.Zero && Sdl2Native.SDL_GL_MakeCurrent(_sdlWindow.SdlWindowHandle, _glContext) == 0)
                {
                    return true;
                }

                error = GetSdlError();
                DestroyGlContextUnsafe();
            }

            // Creating a context right as a new top level window appears (e.g. floating a tab) can fail transiently, mostly on NVIDIA EGL
            lastError = error;
            Log.WriteLine($"Failed to create viewport OpenGL context (attempt {attempt}/{GlContextCreationAttempts}): {error}", Log.LogType.Debug);
            Thread.Sleep(100 * attempt);
        }

        if (!token.IsCancellationRequested)
        {
            Log.WriteLine($"Failed to initialize viewport: {lastError}\n Try to reopen the tab or reopen the application.", Log.LogType.Error);
        }

        return false;
    }

    // SDL unloads the GL driver once its last GL window is gone. Closing the last viewport then ran the driver's teardown while Avalonia
    // and viewports on other threads still used it, which crashed the application, so the driver stays loaded
    private static void PinGlLibrary()
    {
        if (_isGlLibraryPinned)
        {
            return;
        }

        _isGlLibraryPinned = true;
        Sdl2Native.SDL_Init(SDLInitFlags.Video);
        if (Sdl2Native.LoadFunction<SdlGlLoadLibrary>("SDL_GL_LoadLibrary")(IntPtr.Zero) != 0)
        {
            Log.WriteLine($"Failed to load the OpenGL library: {GetSdlError()}", Log.LogType.Warning);
        }
    }

    private void DestroyGlContext()
    {
        lock (SdlLock)
        {
            DestroyGlContextUnsafe();
        }
    }

    private void DestroyGlContextUnsafe()
    {
        if (_sdlWindow == null)
        {
            return;
        }

        Sdl2Native.SDL_GL_MakeCurrent(_sdlWindow.SdlWindowHandle, IntPtr.Zero);
        if (_glContext != IntPtr.Zero)
        {
            Sdl2Native.SDL_GL_DeleteContext(_glContext);
            _glContext = IntPtr.Zero;
        }

        _sdlWindow.Close();
        _sdlWindow = null;
    }

    private static unsafe string? GetSdlError()
    {
        return Marshal.PtrToStringAnsi((IntPtr)Sdl2Native.SDL_GetError());
    }

    private void SyncOutputSize()
    {
        var size = GetSize();
        if (size == _outputSize)
        {
            return;
        }

        var gl = _context!.Gl;
        DeleteOutputFramebuffer();
        _outputColor = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _outputColor);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Rgba8, (uint)size.Width, (uint)size.Height);
        _outputDepthStencil = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _outputDepthStencil);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)size.Width, (uint)size.Height);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        _outputFramebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _outputFramebuffer);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _outputColor);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _outputDepthStencil);
        _outputSize = size;
        _context.OutputBuffer = _outputFramebuffer;
        _context.ViewportSize = new vec2(size.Width, size.Height);
        _context.FireResize();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _outputFramebuffer);
    }

    private void DeleteOutputFramebuffer()
    {
        if (_outputFramebuffer == 0)
        {
            return;
        }

        var gl = _context!.Gl;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.DeleteFramebuffer(_outputFramebuffer);
        gl.DeleteRenderbuffer(_outputColor);
        gl.DeleteRenderbuffer(_outputDepthStencil);
        _outputFramebuffer = 0;
        _outputColor = 0;
        _outputDepthStencil = 0;
        _outputSize = default;
    }

    // Pixels get read into a buffer and copied out a frame later, so reading them back doesn't wait for the GPU to finish the frame
    private unsafe void ReadBackFrame()
    {
        var gl = _context!.Gl;
        var size = _outputSize;
        var bytes = (nuint)(size.Width * size.Height * 4);
        if (size != _readbackSize)
        {
            DeleteReadbackBuffers();
            gl.GenBuffers(2, _readbackBuffers);
            foreach (var buffer in _readbackBuffers)
            {
                gl.BindBuffer(BufferTargetARB.PixelPackBuffer, buffer);
                gl.BufferData(BufferTargetARB.PixelPackBuffer, bytes, null, BufferUsageARB.StreamRead);
            }

            _readbackSize = size;
            _hasPendingReadback = false;
        }

        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _outputFramebuffer);
        gl.BindBuffer(BufferTargetARB.PixelPackBuffer, _readbackBuffers[_readbackIndex]);
        gl.ReadPixels(0, 0, (uint)size.Width, (uint)size.Height, GLEnum.Bgra, GLEnum.UnsignedByte, null);

        var previous = _readbackBuffers[_readbackIndex ^ 1];
        _readbackIndex ^= 1;
        if (_hasPendingReadback)
        {
            gl.BindBuffer(BufferTargetARB.PixelPackBuffer, previous);
            var pixels = (byte*)gl.MapBufferRange(BufferTargetARB.PixelPackBuffer, 0, bytes, MapBufferAccessMask.ReadBit);
            if (pixels != null)
            {
                CopyToFrame(pixels, size);
                gl.UnmapBuffer(BufferTargetARB.PixelPackBuffer);
            }
        }

        _hasPendingReadback = true;
        gl.BindBuffer(BufferTargetARB.PixelPackBuffer, 0);
    }

    private unsafe void CopyToFrame(byte* pixels, PixelSize size)
    {
        lock (_frameLock)
        {
            // The frame got resized since this one was rendered
            if (_frame == null || _frame.PixelSize != size)
            {
                return;
            }

            using var framebuffer = _frame.Lock();
            var rowSize = size.Width * 4;
            var destination = (byte*)framebuffer.Address;
            // GL's rows go from the bottom up
            for (var y = size.Height - 1; y >= 0; --y)
            {
                Unsafe.CopyBlock(destination, pixels + y * rowSize, (uint)rowSize);
                destination += framebuffer.RowBytes;
            }
        }
    }

    private void DeleteReadbackBuffers()
    {
        if (_readbackBuffers[0] == 0)
        {
            return;
        }

        _context?.Gl.DeleteBuffers(2, _readbackBuffers);
        _readbackBuffers[0] = 0;
        _readbackBuffers[1] = 0;
        _readbackSize = default;
    }
}
