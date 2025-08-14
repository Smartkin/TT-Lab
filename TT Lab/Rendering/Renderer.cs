using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using GlmSharp;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.ImGuiUtil;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Passes;
using TT_Lab.Rendering.Services;
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
    private VertexArrayObject<float, float> _emptyVao;
    private IInputContext? _inputContext;
    private FrameBuffer[] _pongBuffers;
    private FrameBuffer _screenBuffer;
    private ivec2 _frameBufferSize = ivec2.Ones;
    private ImGuiController? _imgui;
    private readonly Stopwatch _renderTime = new();
    private readonly Stopwatch _updateWatch = new();
    private bool _isInitialized;
    private byte[] _framebufferData = [];
    private object _updatersLock = new();
    private object _framebufferWriteLock = new();
    private static object _imguiLock = new();
    private int _readBuffer = 0;
    private int _writeBuffer = 1;

    public Renderer(RenderContext renderContext, PassService passService, BatchService batchService)
    {
        _renderContext = renderContext;
        _primitiveRenderer = _renderContext.GetPrimitiveRenderer();
        _renderContext.QueueRenderAction(SetupRenderBuffer);
        _batchStorage = batchService.GenerateBatchStorage();
        _passService = passService;
        renderContext.Render += DoRender;
        _updateWatch.Start();
    }

    public void InitInput(IInputContext inputContext)
    {
        _inputContext = inputContext;
        _renderContext.QueueRenderAction(() =>
        {
            lock (_imguiLock)
            {
                _imgui = new ImGuiController(_renderContext, this, _inputContext);
            }
        });
    }
    
    public ivec2 GetFrameBufferSize() => _frameBufferSize;

    public void SetFrameBufferSize(ivec2 frameBufferSize)
    {
        lock (_framebufferWriteLock)
        {
            _framebufferData = new byte[frameBufferSize.x * frameBufferSize.y * 4];
            _frameBufferSize = frameBufferSize;
        }

        _renderContext.QueueRenderAction(() =>
        {
            DeleteRenderBuffer();
            SetupRenderBuffer();
            _renderContext.Gl.Viewport(0, 0, (uint)_frameBufferSize.x, (uint)_frameBufferSize.y);
            _renderContext.Gl.Scissor(0, 0, (uint)_frameBufferSize.x, (uint)_frameBufferSize.y);
            
            Resize?.Invoke(new Vector2D<Int32>(_frameBufferSize.x, _frameBufferSize.y));
            FramebufferResize?.Invoke(new Vector2D<Int32>(_frameBufferSize.x, _frameBufferSize.y));
        });
    }

    public void RegisterForRendering(Renderable renderable, bool initBatchStorage = false)
    {
        if (renderable is Mesh mesh)
        {
            _batchStorage.AddMeshToBatch(mesh);
        }
        else
        {
            _passService.RegisterRenderableInPasses(renderable, renderable.GetPriorityPasses());
        }

        foreach (var renderChild in renderable.Children)
        {
            RegisterForRendering(renderChild);
        }

        if (!initBatchStorage)
        {
            return;
        }

        var idx = 0;
        foreach (var renderBatch in _batchStorage.GetRenderBatches())
        {
            idx++;
            _passService.RegisterRenderableInPasses(renderBatch, renderBatch.GetPriorityPasses());
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
            SetupRenderBuffer();
        }
        
        if (_frameBufferSize == ivec2.Ones)
        {
            return;
        }
        
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, _pongBuffers[_writeBuffer].Handler);
        
        _renderContext.Gl.ClearColor(Color.Gray);
        _renderContext.Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        
        // Opaque skydome pass
        PerformPassChain((float)delta, _passService.GetSkydomeOpaquePasses);

        // Transparent skydome pass
        PerformPassChain((float)delta, _passService.GetSkydomeTransparentPasses);

        // Opaque objects pass
        PerformPassChain((float)delta, _passService.GetPasses);
        
        // Alpha blending pass
        PerformPassChain((float)delta, _passService.GetTransparentPasses);
        
        // Primitives pass
        foreach (var primitivePass in _passService.GetPrimitivePasses())
        {
            if (primitivePass.StartPass())
            {
                var program = _renderContext.CurrentPass.Program;
                var timeLoc = program.GetUniformLocation("Time");
                var resolutionLoc = program.GetUniformLocation("Resolution");
                _renderContext.Gl.Uniform1(timeLoc, (float)Time);
                _renderContext.Gl.Uniform2(resolutionLoc, new Vector2(_frameBufferSize.x, _frameBufferSize.y));
            }
            var renderables = _passService.GetRenderablesInPass(primitivePass.Name);
            renderables[0].Render((float)delta);
            _primitiveRenderer.Render();
            //_renderContext.GetPrimitiveRenderer().DrawSphere(new vec3(10, 10.0f, 0), 5.0f, new vec4(1.0f, 0.0f, 0.0f, 0.5f));
            primitivePass.EndPass();
        }
        
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _screenBuffer.Handler);
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _pongBuffers[_writeBuffer].Handler);
        _renderContext.Gl.BlitFramebuffer(0, 0, _frameBufferSize.x, _frameBufferSize.y, 0, 0, _frameBufferSize.x, _frameBufferSize.y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _pongBuffers[_writeBuffer].Handler);
        
        var screenFlipProgram = _renderContext.GetProgram("ScreenFlipX");
        screenFlipProgram.Use();
        _screenBuffer.TextureAttachment!.Bind(TextureUnit.Texture5);
        _emptyVao.Bind();
        _renderContext.Gl.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
        
        Render?.Invoke(delta);
        if (_imgui != null)
        {
            lock (_imguiLock)
            {
                _imgui.StartFrame((float)delta);
                
                RenderImgui?.Invoke();

                _imgui.Render();
            }
        }
        
        _renderContext.Invalidate();
        // Swap buffers
        SaveFramebuffer();
        (_readBuffer, _writeBuffer) = (_writeBuffer, _readBuffer);
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        FinishRender?.Invoke();
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
            _pongBuffers[_readBuffer].TextureAttachment!.Bind(TextureUnit.Texture5);
            var program = _renderContext.CurrentPass.Program;
            var timeLoc = program.GetUniformLocation("Time");
            var resolutionLoc = program.GetUniformLocation("Resolution");
            _renderContext.Gl.Uniform1(timeLoc, (float)Time);
            _renderContext.Gl.Uniform2(resolutionLoc, new Vector2(_frameBufferSize.x, _frameBufferSize.y));
        }

        foreach (var renderable in renderables)
        {
            renderable.Render(delta);
        }

        pass.EndPass();
    }

    private unsafe void SaveFramebuffer()
    {
        _renderContext.Gl.Flush();
        _renderContext.Gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _pongBuffers[_readBuffer].Handler);
        lock (_framebufferWriteLock)
        {
            fixed (byte* p = _framebufferData)
            {
                _renderContext.Gl.PixelStore(GLEnum.PackAlignment, 1);
                _renderContext.Gl.ReadPixels(0, 0, (uint)_frameBufferSize.x, (uint)_frameBufferSize.y, GLEnum.Bgra,
                    GLEnum.UnsignedByte, p);
            }

            FlipY(_framebufferData, _frameBufferSize.x, _frameBufferSize.y);
        }
    }

    public void DoUpdate()
    {
        var delta = _updateWatch.ElapsedMilliseconds / 1000.0;
        _updateWatch.Restart();
        if (_imgui != null)
        {
            lock (_imguiLock)
            {
                _imgui.Update((float)delta);
            }
        }

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
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            SceneInitialized?.Invoke();
        });
    }

    public IntPtr Handle => ((IGLContext)_renderContext.Gl.Context).Handle;
    public bool IsClosing => false;
    public double Time => _renderTime.ElapsedMilliseconds / 1000.0;
    public Vector2D<Int32> FramebufferSize => new(_frameBufferSize.x, _frameBufferSize.y);
    public bool IsInitialized => true;

    public void GetRenderImage(WriteableBitmap bitmap)
    {
        lock (_framebufferWriteLock)
        {
            bitmap.Lock();
            bitmap.WritePixels(new Int32Rect(0, 0, _frameBufferSize.x, _frameBufferSize.y), _framebufferData,
                bitmap.BackBufferStride, 0);
            bitmap.Unlock();
        }
    }
    
    private static void FlipY(byte[] pixels, int width, int height)
    {
        var rowSize = width * 4;
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

    [MemberNotNull(nameof(_screenBuffer))]
    private void SetupRenderBuffer()
    {
        _renderContext.MakeCurrent();
        _pongBuffers = [new FrameBuffer(_renderContext, _frameBufferSize, true), new FrameBuffer(_renderContext, _frameBufferSize, true)];
        _screenBuffer = new FrameBuffer(_renderContext, _frameBufferSize);
        _emptyVao = new VertexArrayObject<float, float>(_renderContext, null, null);
    }

    private void DeleteRenderBuffer()
    {
        _renderContext.MakeCurrent();
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
        
        Closing?.Invoke();
        var manualResetEventSlim = new System.Threading.ManualResetEventSlim(false);
        _renderContext.Render -= DoRender;
        _renderContext.QueueRenderAction(() =>
        {
            _imgui?.Dispose();
            _emptyVao.Dispose();
            DeleteRenderBuffer();
            // ReSharper disable once AccessToDisposedClosure
            manualResetEventSlim.Set();
        });
        
        IsDisposed = true;
        manualResetEventSlim.Wait();
        manualResetEventSlim.Dispose();
        GC.SuppressFinalize(this);
    }

    public event Action<Vector2D<Int32>>? Resize;
    public event Action<Vector2D<Int32>>? FramebufferResize;
    public event Action? Closing;
    public event Action<Boolean>? FocusChanged;
    public event Action? Load;
    public event Action<Double>? Update;
    public event Action? RenderImgui;
    public event Action<Double>? Render;
    public event Action? FinishRender;
    public event Action? SceneInitialized;
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