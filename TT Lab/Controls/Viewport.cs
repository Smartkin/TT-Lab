using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using GlmSharp;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using Splat;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Factories;
using TT_Lab.Rendering.Services;
using TT_Lab.Rendering.Shaders;
using Point = Avalonia.Point;

namespace TT_Lab.Controls;

public class RenderRoutedEventArgs : RoutedEventArgs
{
    public Viewport RenderArea { get; init; }
    public RenderContext RenderContext { get; init; }
}

public class Viewport : OpenGlControlBase, ICustomHitTest
{
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
        _stopwatch = new Stopwatch();
        Focusable = true;
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        _context = new RenderContext(GL.GetApi(gl.GetProcAddress))
        {
            ViewportSize = new vec2((float)Bounds.Width, (float)Bounds.Height)
        };
        RaiseEvent(new RenderRoutedEventArgs
        {
            RenderArea = this,
            RenderContext = _context,
            RoutedEvent = RenderInitializedEvent
        });
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _context.SetGlAccessibility(true);
        RaiseEvent(new RenderRoutedEventArgs
        {
            RenderArea = this,
            RenderContext = _context,
            RoutedEvent = RenderTerminatedEvent
        });
        _context.Dispose();
        
        base.OnOpenGlDeinit(gl);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        _context.SetGlAccessibility(true);
        _context.SetOutputBuffer(fb);
        _context.ViewportSize = new vec2((float)Bounds.Width, (float)Bounds.Height);
        var delta = _stopwatch.ElapsedMilliseconds / 1000.0f;
        _stopwatch.Restart();
        if (Math.Abs(delta) < 0.000001f)
        {
            delta = 0.016f;
        }
        _context.Gl.Viewport(0,0, (uint)Bounds.Width, (uint)Bounds.Height);
        _context.Gl.Scissor(0, 0, (uint)Bounds.Width, (uint)Bounds.Height);
        _context.PerformRender(delta);
        _context.SetGlAccessibility(false);
        RequestNextFrameRendering();
    }

    public Boolean HitTest(Point point)
    {
        return Bounds.Contains(point);
    }
}