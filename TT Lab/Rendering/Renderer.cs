using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Windows;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using GlmSharp;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Passes;
using TT_Lab.Rendering.Services;
using TT_Lab.Rendering.Shaders;
using Action = System.Action;
using Buffer = System.Buffer;

namespace TT_Lab.Rendering;

public class Renderer : IView
{
    private readonly RenderContext _renderContext;
    private readonly PrimitiveRenderer _primitiveRenderer;
    private readonly PassService _passService;
    private readonly BatchStorage _batchStorage;
    private readonly List<Renderable> _updaters = [];
    private readonly List<IInstancedRenderable> _instancedRenderables = [];
    private readonly List<IPrimitiveRenderable> _primitiveRenderables = [];
    private readonly object _renderablesLock = new();
    private VertexArrayObject<float, float> _emptyVao;
    private IInputContext? _inputContext;
    private FrameBuffer[] _pongBuffers;
    private FrameBuffer _screenBuffer;
    private ivec2 _frameBufferSize => new((int)_renderContext.ViewportSize.x, (int)_renderContext.ViewportSize.y);
    private readonly Stopwatch _renderTime = new();
    private readonly Stopwatch _updateWatch = new();
    private bool _isInitialized;
    private byte[] _framebufferData = [];
    private object _updatersLock = new();
    private object _framebufferWriteLock = new();
    private int _readBuffer = 0;
    private int _writeBuffer = 1;

    public Renderer(RenderContext renderContext)
    {
        _renderContext = renderContext;
        _primitiveRenderer = _renderContext.PrimitiveRenderer;
        _renderContext.QueueRenderAction(SetupRenderBuffer);
        _batchStorage = renderContext.BatchService.GenerateBatchStorage();
        _batchStorage.NewBatchCreated += BatchStorageOnNewBatchCreated;
        _passService = renderContext.PassService;
        renderContext.Render += DoRender;
        renderContext.ResizeFramebuffer += RenderContextOnResizeFramebuffer;
        renderContext.Destroy += Dispose;
        _updateWatch.Start();
    }

    private void RenderContextOnResizeFramebuffer()
    {
        RecreateRenderBuffers();
    }

    private void RecreateRenderBuffers()
    {
        DeleteRenderBuffer();
        SetupRenderBuffer();

        Resize?.Invoke(new Vector2D<Int32>(_frameBufferSize.x, _frameBufferSize.y));
        FramebufferResize?.Invoke(new Vector2D<Int32>(_frameBufferSize.x, _frameBufferSize.y));
    }

    private void BatchStorageOnNewBatchCreated(RenderBatch renderBatch)
    {
        lock (_renderablesLock)
        {
            _instancedRenderables.Add(renderBatch);
        }

        _passService.RegisterRenderableInPasses(renderBatch, renderBatch.GetPriorityPasses());
        renderBatch.RequestPassSwitch += () =>
        {
            _passService.UnregisterRenderableInPasses(renderBatch);
            _passService.RegisterRenderableInPasses(renderBatch, renderBatch.GetPriorityPasses());
        };
    }

    public void InitInput(IInputContext inputContext)
    {
        _inputContext = inputContext;
    }
    
    public ivec2 GetFrameBufferSize() => _frameBufferSize;

    public void SetFrameBufferSize(ivec2 frameBufferSize)
    {
        _renderContext.QueueRenderAction(RecreateRenderBuffers);
    }

    private void SubscribeToRenderableEvents(Renderable renderable)
    {
        renderable.ChildAdded += RenderableOnChildAdded;
        renderable.ChildRemoved += RenderableOnChildRemoved;
    }

    private void UnsubscribeFromRenderableEvents(Renderable renderable)
    {
        renderable.ChildAdded -= RenderableOnChildAdded;
        renderable.ChildRemoved -= RenderableOnChildRemoved;
    }

    // Registering subscribes to the renderable's events itself, subscribing here as well used to register the grandchildren twice
    private void RenderableOnChildAdded(Renderable child)
    {
        RegisterForRendering(child);
        RegisterForUpdating(child);
    }
    
    private void RenderableOnChildRemoved(Renderable child)
    {
        UnregisterFromRendering(child);
        UnregisterForUpdating(child);
    }

    public void RegisterForRendering(Renderable renderable, bool initBatchStorage = false)
    {
        SubscribeToRenderableEvents(renderable);
        if (renderable is Mesh mesh)
        {
            _batchStorage.AddMeshToBatch(mesh);
        }
        else
        {
            _passService.RegisterRenderableInPasses(renderable, renderable.GetPriorityPasses());
        }

        lock (_renderablesLock)
        {
            if (renderable is IInstancedRenderable instanced && renderable is not RenderBatch)
            {
                _instancedRenderables.Add(instanced);
            }

            if (renderable is IPrimitiveRenderable primitives)
            {
                _primitiveRenderables.Add(primitives);
            }
        }

        foreach (var renderChild in renderable.Children)
        {
            RegisterForRendering(renderChild);
        }
    }

    private void UnregisterFromRendering(Renderable renderable)
    {
        UnsubscribeFromRenderableEvents(renderable);
        if (renderable is Mesh mesh)
        {
            _batchStorage.RemoveMeshFromBatch(mesh);
        }
        else
        {
            _passService.UnregisterRenderableInPasses(renderable);
        }

        lock (_renderablesLock)
        {
            if (renderable is IInstancedRenderable instanced && renderable is not RenderBatch)
            {
                _instancedRenderables.Remove(instanced);
            }

            if (renderable is IPrimitiveRenderable primitives)
            {
                _primitiveRenderables.Remove(primitives);
            }
        }

        foreach (var renderChild in renderable.Children)
        {
            UnregisterFromRendering(renderChild);
        }
    }

    public void RegisterForUpdating(Renderable renderable)
    {
        if (renderable.DoesUpdates)
        {
            lock (_updatersLock)
            {
                _updaters.Add(renderable);
            }
        }

        foreach (var child in renderable.Children)
        {
            RegisterForUpdating(child);
        }
    }

    private void UnregisterForUpdating(Renderable renderable)
    {
        if (renderable.DoesUpdates)
        {
            lock (_updatersLock)
            {
                if (_updaters.Contains(renderable))
                {
                    _updaters.Remove(renderable);
                }
            }
        }

        foreach (var child in renderable.Children)
        {
            UnregisterForUpdating(child);
        }
    }

    public void Initialize()
    {
    }

    public void DoRender()
    {
    }

    private void DoRender(double delta)
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            _renderTime.Start();
        }
        
        if (_frameBufferSize == ivec2.Ones)
        {
            return;
        }

        var gl = _renderContext.Gl;
        _renderContext.State.Reset();
        gl.ClearColor(Color.DimGray);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        _renderContext.FrameCamera = Camera?.GetFrameCamera(true) ?? default;
        _pongBuffers[_readBuffer].TextureAttachment!.Bind(TextureUnit.Texture5);

        PrepareInstances();
        
        // Opaque skydome pass
        PerformPassChain((float)delta, _passService.GetSkydomeOpaquePasses);

        // Transparent skydome pass
        PerformPassChain((float)delta, _passService.GetSkydomeTransparentPasses);

        // Opaque objects pass
        PerformPassChain((float)delta, _passService.GetPasses);
        
        // Alpha blending pass
        PerformPassChain((float)delta, _passService.GetTransparentPasses);
        
        // Billboards pass
        PerformPassChain((float)delta, _passService.GetBillboardPasses);
        
        // Primitives pass
        RenderPrimitives();
        
        _renderContext.State.SetDepthTest(false);
        _renderContext.State.SetBlend(false);
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _screenBuffer.Handler);
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _renderContext.GetOutputBuffer());
        gl.BlitFramebuffer(0, 0, _frameBufferSize.x, _frameBufferSize.y, 0, 0, _frameBufferSize.x, _frameBufferSize.y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _renderContext.GetOutputBuffer());
        
        var screenFlipProgram = _renderContext.GetProgram("ScreenFlipX");
        screenFlipProgram.Use();
        _screenBuffer.TextureAttachment!.Bind(TextureUnit.Texture5);
        _emptyVao.Bind();
        gl.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
        _renderContext.State.SetBlend(true);
        _renderContext.State.SetDepthTest(true);
        
        Render?.Invoke(delta);
        _renderContext.Invalidate();
        FinishRender?.Invoke();
    }

    private void PrepareInstances()
    {
        var instances = _renderContext.Instances;
        instances.Clear();
        lock (_renderablesLock)
        {
            foreach (var instancedRenderable in _instancedRenderables)
            {
                instancedRenderable.PrepareInstances(instances);
            }
        }

        instances.Upload();
    }

    private void RenderPrimitives()
    {
        var camera = _renderContext.FrameCamera;
        if (!camera.IsValid)
        {
            return;
        }

        lock (_renderablesLock)
        {
            foreach (var primitiveRenderable in _primitiveRenderables)
            {
                if (((Renderable)primitiveRenderable).IsVisible)
                {
                    primitiveRenderable.DrawPrimitives(_primitiveRenderer, camera);
                }
            }
        }

        DrawPrimitives?.Invoke(_primitiveRenderer, camera);
        _primitiveRenderer.Render(camera);
    }

    private void PerformPassChain(float delta, Func<IList<RenderPass>> passGetter)
    {
        foreach (var pass in passGetter.Invoke())
        {
            PerformPass(delta, pass);
        }
    }

    private void PerformPass(float delta, RenderPass pass)
    {
        var renderables = _passService.GetRenderablesInPass(pass.Name);
        // Only 1 renderable indicates we only have the camera
        if (renderables.Count <= 1)
        {
            return;
        }

        if (pass.StartPass())
        {
            var program = _renderContext.CurrentPass.Program;
            program.SetUniform(KnownUniform.Time, (float)Time);
            program.SetUniform(KnownUniform.Resolution, new vec2(_frameBufferSize.x, _frameBufferSize.y));
        }

        foreach (var renderable in renderables)
        {
            renderable.Render(delta);
        }

        pass.EndPass();
    }

    public void DoUpdate()
    {
        var delta = _updateWatch.ElapsedMilliseconds / 1000.0;
        _updateWatch.Restart();
        lock (_updatersLock)
        {
            foreach (var updater in _updaters)
            {
                updater.Update((float)delta);
            }
        }

        Update?.Invoke(delta);
    }

    public void DoEvents()
    {
    }

    public void ContinueEvents()
    {
    }

    public RenderContext GetRenderContext()
    {
        return _renderContext;
    }

    public IInputContext? GetInputContext()
    {
        return _inputContext;
    }

    public void Reset()
    {
        _renderTime.Restart();
        _updateWatch.Restart();
        
        DeleteRenderBuffer();
        SetupRenderBuffer();
    }

    public void Focus()
    {
    }

    public void Close()
    {
        Dispose();
    }

    public Vector2D<Int32> PointToClient(Vector2D<Int32> point)
    {
        throw new NotImplementedException();
    }

    public Vector2D<Int32> PointToScreen(Vector2D<Int32> point)
    {
        throw new NotImplementedException();
    }

    public Vector2D<Int32> PointToFramebuffer(Vector2D<Int32> point)
    {
        throw new NotImplementedException();
    }

    public Object Invoke(Delegate d, params object[] args)
    {
        return d.DynamicInvoke(args);
    }

    public void Run(Action onFrame)
    {
    }

    public void FireSceneInitialized()
    {
        Dispatcher.UIThread.Post(() =>
        {
            SceneInitialized?.Invoke();
        });
    }

    public IntPtr Handle => ((IGLContext)_renderContext.Gl.Context).Handle;
    public bool IsClosing => false;
    public double Time => _renderTime.ElapsedMilliseconds / 1000.0;
    public Vector2D<Int32> FramebufferSize => new(_frameBufferSize.x, _frameBufferSize.y);
    public bool IsInitialized => true;

    [MemberNotNull(nameof(_screenBuffer))]
    [MemberNotNull(nameof(_screenBuffer))]
    [MemberNotNull(nameof(_emptyVao))]
    private void SetupRenderBuffer()
    {
        _pongBuffers = [new FrameBuffer(_renderContext, _frameBufferSize, true), new FrameBuffer(_renderContext, _frameBufferSize, true)];
        _screenBuffer = new FrameBuffer(_renderContext, _frameBufferSize);
        _emptyVao = new VertexArrayObject<float, float>(_renderContext, null, null);
    }

    private void DeleteRenderBuffer()
    {
        _emptyVao.Dispose();
        foreach (var pongBuffer in _pongBuffers)
        {
            pongBuffer.Dispose();
        }
        _screenBuffer.Dispose();
    }
    
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }
        IsDisposed = true;
        
        Closing?.Invoke();
        _renderContext.Render -= DoRender;
        _renderContext.ResizeFramebuffer -= RenderContextOnResizeFramebuffer;
        _renderContext.Destroy -= Dispose;
        DeleteRenderBuffer();
        
        GC.SuppressFinalize(this);
    }

    public event Action<Vector2D<Int32>>? Resize;
    public event Action<Vector2D<Int32>>? FramebufferResize;
    public event Action? Closing;
    public event Action<Boolean>? FocusChanged;
    public event Action? Load;
    public event Action<Double>? Update;
    public event Action<Double>? Render;
    public event Action? FinishRender;
    public event Action? SceneInitialized;
    /// <summary>
    /// Raised on the render thread every frame to collect primitives from things that aren't part of the scene
    /// </summary>
    public event Action<PrimitiveRenderer, FrameCamera>? DrawPrimitives;
    public Scene.Camera? Camera { get; set; }
    public bool ShouldSwapAutomatically { get; set; }
    public bool IsEventDriven { get; set; }
    public bool IsContextControlDisabled { get; set; }
    public Vector2D<Int32> Size => new(_frameBufferSize.x, _frameBufferSize.y);
    public double FramesPerSecond { get; set; }
    public double UpdatesPerSecond { get; set; }
    public GraphicsAPI API => GraphicsAPI.None;
    public bool VSync { get; set; }
    public VideoMode VideoMode => VideoMode.Default;
    public int? PreferredDepthBufferBits => 32;
    public int? PreferredStencilBufferBits => 8;
    public Vector4D<Int32>? PreferredBitDepth => new(8, 8, 8, 8);
    public int? Samples => 1;
    public IGLContext? GLContext => _renderContext.Gl.Context as IGLContext;
    public IVkSurface? VkSurface => null;
    public INativeWindow? Native => null;
}