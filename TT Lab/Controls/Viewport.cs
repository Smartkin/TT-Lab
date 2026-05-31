using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.OpenGL.Controls;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using GlmSharp;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using TT_Lab.Rendering;
using TT_Lab.Util;
using Veldrid.Sdl2;
using Color = System.Drawing.Color;
using PixelFormat = Avalonia.Platform.PixelFormat;
using Point = Avalonia.Point;
using Window = Silk.NET.Windowing.Window;

namespace TT_Lab.Controls;

public class RenderRoutedEventArgs : RoutedEventArgs
{
    public Viewport RenderArea { get; init; }
    public RenderContext RenderContext { get; init; }
}

public class Viewport : Control, ICustomHitTest
{
    private ManualResetEventSlim _imageReadySlim = new(false);
    private Thread? _renderThread;
    private Sdl2Window _sdlWindow;
    private RenderContext? _context;
    private uint[] _framebufferData = new uint[2];
    private object _imageLock = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private WriteableBitmap _bitmap;
    private readonly IBrush _noRenderColor = new ImmutableSolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 255));
    
    public event EventHandler<RenderRoutedEventArgs> RenderInitialized
    {
        add => AddHandler(RenderInitializedEvent, value);
        remove => RemoveHandler(RenderInitializedEvent, value);
    }
    
    public event EventHandler<RenderRoutedEventArgs> RenderTerminated
    {
        add => AddHandler(RenderTerminatedEvent, value);
        remove => RemoveHandler(RenderTerminatedEvent, value);
    }
    
    public static readonly RoutedEvent RenderInitializedEvent =
        RoutedEvent.Register<Viewport, RenderRoutedEventArgs>(nameof(RenderInitialized), RoutingStrategies.Bubble);
    
    public static readonly RoutedEvent RenderTerminatedEvent =
        RoutedEvent.Register<Viewport, RenderRoutedEventArgs>(nameof(RenderTerminated), RoutingStrategies.Bubble);

    public Viewport()
    {
        if (Design.IsDesignMode)
        {
            return;
        }
        
        SizeChanged += OnSizeChanged;
        
        Focusable = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        
        _bitmap = new WriteableBitmap(new PixelSize(1, 1), new Vector(96, 96), PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);
        
        _renderThread = new Thread(() => RenderThread(_cancellationTokenSource.Token))
        {
            Name = $"Viewport Render Thread {GetHashCode()}",
            IsBackground = true
        };
        _renderThread!.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _renderThread?.Join();
        
        _bitmap.Dispose();
        
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        lock (_imageLock)
        {
            _bitmap.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize((int)e.NewSize.Width, (int)e.NewSize.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            _framebufferData = new uint[_bitmap.PixelSize.Width * _bitmap.PixelSize.Height];
            _imageReadySlim.Set();
        }
        
        _context?.QueueRenderAction(() =>
        {
            lock (_imageLock)
            {
                _sdlWindow.Width = _bitmap.PixelSize.Width;
                _sdlWindow.Height = _bitmap.PixelSize.Height;
            }

            _sdlWindow.X = 0;
            _sdlWindow.Y = 0;
            _context!.ViewportSize = new vec2(_sdlWindow.Width, _sdlWindow.Height);
            Sdl2Native.SDL_PumpEvents();
        });
    }

    private unsafe void RenderThread(CancellationToken token)
    {
        _imageReadySlim.Wait(token);
        var stopwatch = Stopwatch.StartNew();
        
        _sdlWindow = new Sdl2Window($"VIEWPORT_{GetHashCode()}", 0, 0, (int)Bounds.Width, (int)Bounds.Height,
            SDL_WindowFlags.Borderless | SDL_WindowFlags.Hidden | SDL_WindowFlags.SkipTaskbar | SDL_WindowFlags.OpenGL,
            false);

        var glCtxPtr = Sdl2Native.SDL_GL_CreateContext(_sdlWindow.SdlWindowHandle);
        if (glCtxPtr == IntPtr.Zero)
        {
            var sdlError = Sdl2Native.SDL_GetError();
            var sdlErrorString = Marshal.PtrToStringAnsi((IntPtr)sdlError);
            Log.WriteLine($"Failed to initialize viewport: {sdlErrorString}\n Try to reopen the tab or reopen to application.", Log.LogType.Error);
            _sdlWindow.Close();
            return;
        }
        Sdl2Native.SDL_GL_MakeCurrent(_sdlWindow.SdlWindowHandle, glCtxPtr);
        
        _context = new RenderContext(GL.GetApi(Sdl2Native.SDL_GL_GetProcAddress))
        {
            ViewportSize = new vec2((float)Bounds.Width, (float)Bounds.Height)
        };
        
        Dispatcher.UIThread.Post(() =>
        {
            RaiseEvent(new RenderRoutedEventArgs
            {
                RenderArea = this,
                RenderContext = _context,
                RoutedEvent = RenderInitializedEvent
            });
        });

        _sdlWindow.Closing += OnOpenGlDeinit;

        var resizeHack = false;
        var presentElapsedTime = 0.0f;
        while (!token.IsCancellationRequested)
        {
            Thread.Sleep(1);
            _context.SetGlAccessibility(true);
            var delta = stopwatch.ElapsedMilliseconds / 1000.0f;
            stopwatch.Restart();
            var renderDelta = delta;
            OnOpenGlRender(renderDelta);
            presentElapsedTime += delta;
            if (presentElapsedTime < 0.016f)
            {
                _context.SetGlAccessibility(false);
                continue;
            }
            presentElapsedTime = 0.0f;
            
            _context.Gl.Flush();
            _context.Gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0U);
            lock (_imageLock)
            {
                fixed (uint* ptr = _framebufferData)
                {
                    _context.Gl.ReadPixels(0, 0, (uint)_bitmap.PixelSize.Width, (uint)_bitmap.PixelSize.Height, GLEnum.Bgra, GLEnum.UnsignedByte, ptr);
                }
                var bitsHandle = GCHandle.Alloc(_framebufferData, GCHandleType.Pinned);
                using var fb = _bitmap.Lock();
                var startAddr = fb.Address;
                var rowSize = _bitmap.PixelSize.Width * 4;
                for (var y = _bitmap.PixelSize.Height - 1; y >= 0; --y)
                {
                    var pixel = bitsHandle.AddrOfPinnedObject() + y * rowSize;
                    Unsafe.CopyBlock(startAddr.ToPointer(), pixel.ToPointer(), (uint)rowSize);
                    startAddr += rowSize;
                }
                bitsHandle.Free();
            }
            
            if (!resizeHack)
            {
                _sdlWindow.Width += 1;
                _context.FireResize();
                resizeHack = true;
            }
            
            _context.SetGlAccessibility(false);
            
            Dispatcher.UIThread.Post(InvalidateVisual);
        }
        
        Sdl2Native.SDL_GL_MakeCurrent(_sdlWindow.SdlWindowHandle, IntPtr.Zero);
        _sdlWindow.Close();
    }

    private void OnOpenGlDeinit()
    {
        _context?.SetGlAccessibility(true);
        Dispatcher.UIThread.Post(() =>
        {
            RaiseEvent(new RenderRoutedEventArgs
            {
                RenderArea = this,
                RenderContext = null!,
                RoutedEvent = RenderTerminatedEvent
            });
        });
        _context?.Dispose();
    }

    private void OnOpenGlRender(double delta)
    {
        var sdlWindowWidth = _sdlWindow.Width;
        var sdlWindowHeight = _sdlWindow.Height;
        _context!.Gl.Viewport(0, 0, (uint)sdlWindowWidth, (uint)sdlWindowHeight);
        _context.Gl.Scissor(0, 0, (uint)sdlWindowWidth, (uint)sdlWindowHeight);
        _context.PerformRender((float)delta);
    }

    public override void Render(DrawingContext context)
    {
        if (Design.IsDesignMode)
        {
            context.DrawText(new FormattedText("Rendering is not supported in design mode", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, _noRenderColor),
                new Point(5, 5));
            return;
        }

        lock (_imageLock)
        {
            context.DrawImage(_bitmap, new Rect(0, 0, Bounds.Width, Bounds.Height));
        }
    }

    public Boolean HitTest(Point point)
    {
        return Bounds.Contains(point);
    }
}