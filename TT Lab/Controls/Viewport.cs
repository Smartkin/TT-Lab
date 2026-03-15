using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using GlmSharp;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using TT_Lab.Rendering;
using TT_Lab.Util;
using Point = Avalonia.Point;
using Window = Silk.NET.Windowing.Window;

namespace TT_Lab.Controls;

public class RenderRoutedEventArgs : RoutedEventArgs
{
    public Viewport RenderArea { get; init; }
    public RenderContext RenderContext { get; init; }
}

public class Viewport : NativeControlHost, ICustomHitTest
{
    private ManualResetEventSlim _windowCreation = new(false);
    private bool _renderStarted = false;
    private Thread? _renderThread;
    private IView? _window;
    private RenderContext _context;
    private readonly Stopwatch _stopwatch;
    
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
        
        _stopwatch = new Stopwatch();
        SizeChanged += OnSizeChanged;
        Focusable = true;
        
        _renderThread = new Thread(RenderThread)
        {
            IsBackground = true
        };
        _renderThread!.Start();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_window == null)
        {
            return;
        }
        
        // _window.Size = new Vector2D<int>((int)e.NewSize.Width, (int)e.NewSize.Height);
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        unsafe
        {
            _window = Silk.NET.Windowing.Sdl.SdlWindowing.CreateFrom((void*)(MiscUtils.GetMainWindow().TryGetPlatformHandle()!.Handle));
        }
        
        var windowHandle = IntPtr.Zero;
        string? descriptorKind = null;
        if (_window is not { IsInitialized: true })
        {
            return new PlatformHandle(windowHandle, descriptorKind);
        }
        
        if (_window.Native!.Kind.HasFlag(NativeWindowFlags.Wayland))
        {
            windowHandle = _window.Native.Wayland!.Value.Surface;
            descriptorKind = "X11 Window";
        }
        
        _windowCreation.Set();
        
        return new PlatformHandle(windowHandle, descriptorKind);
    }

    private void RenderThread()
    {
        _windowCreation.Wait();
        
        // var options = WindowOptions.Default;
        // options.Size = new Vector2D<int>(800, 600);
        // options.IsEventDriven = true;
        // options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(4, 6));
        // options.WindowBorder = WindowBorder.Hidden;
        // options.ShouldSwapAutomatically = true;
        // _window = Window.Create(options);

        _window.Load += OnOpenGlInit;
        _window.Render += OnOpenGlRender;
        _window.Closing += OnOpenGlDeinit;
        
        _window!.Initialize();

        _window.MakeCurrent();
        while (true)
        {
            _window.DoRender();
            _window.DoUpdate();
        }
    }

    private void OnOpenGlInit()
    {
        _context = new RenderContext(_window.CreateOpenGL())
        {
            ViewportSize = new vec2((float)Bounds.Width, (float)Bounds.Height)
        };
        _context.Gl.ClearColor(Color.DimGray);
        _context.Gl.Clear(ClearBufferMask.ColorBufferBit);
        
        Dispatcher.UIThread.Post(() =>
        {
            RaiseEvent(new RenderRoutedEventArgs
            {
                RenderArea = this,
                RenderContext = _context,
                RoutedEvent = RenderInitializedEvent
            });
        });
    }

    private void OnOpenGlDeinit()
    {
        _context.SetGlAccessibility(true);
        RaiseEvent(new RenderRoutedEventArgs
        {
            RenderArea = this,
            RenderContext = _context,
            RoutedEvent = RenderTerminatedEvent
        });
        _context.Dispose();
    }

    private void OnOpenGlRender(double delta)
    {
        _context.SetGlAccessibility(true);
        _context.ViewportSize = new vec2((float)Bounds.Width, (float)Bounds.Height);
        _context.Gl.Viewport(0, 0, (uint)Bounds.Width, (uint)Bounds.Height);
        _context.Gl.Scissor(0, 0, (uint)Bounds.Width, (uint)Bounds.Height);
        _context.PerformRender((float)delta);
        _context.SetGlAccessibility(false);
    }

    public Boolean HitTest(Point point)
    {
        return Bounds.Contains(point);
    }
}