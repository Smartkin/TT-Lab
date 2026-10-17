using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
    // The batches writing their instances every frame, after every batch that changed wrote its kept ones
    private readonly List<RenderBatch> _frameBatches = [];
    // Every batch whose instances were written this frame, the indirect draws of the others stay as they were
    private readonly List<RenderBatch> _writtenBatches = [];
    // Picking their levels before the batches gather their instances: a batch registered before the LOD gathered last frame's levels
    private readonly List<LodVisual> _lods = [];
    private readonly List<IPrimitiveRenderable> _primitiveRenderables = [];
    private readonly object _renderablesLock = new();
    private VertexArrayObject<float, float> _emptyVao;
    private IInputContext? _inputContext;
    private FrameBuffer[] _pongBuffers;
    private FrameBuffer _screenBuffer;
    private FrameBuffer? _skyBuffer;
    private ivec2 _skyBufferSize;
    private TextureBuffer? _fogTable;
    private GameFog? _fogTableOf;
    private vec4 _skyFog;
    private ivec2 _frameBufferSize => new((int)_renderContext.ViewportSize.x, (int)_renderContext.ViewportSize.y);
    private readonly Stopwatch _renderTime = new();
    private readonly Stopwatch _updateWatch = new();
    private bool _isInitialized;
    // Whether the frame being drawn draws the passes' batches in indirect draws
    private bool _drawsIndirect;
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
        renderContext.RenderQueueProcessed += ReleaseForgottenMeshes;
        renderContext.Destroy += Dispose;
        _updateWatch.Start();
    }

    // What the last look at the forgotten meshes saw: one only gets undrawn when a mesh leaves the scene, or when more gets forgotten
    private (int Removals, int Forgotten) _releaseLookedAt = (-1, -1);

    // The meshes the context forgot (a model made again of new data, that undone or redone) give back what they were drawn from once no
    // batch draws them, and their batches go: every copy stayed on the GPU and in the passes until the tab closed
    private void ReleaseForgottenMeshes()
    {
        var lookingAt = (_batchStorage.Removals, _renderContext.MeshService.ForgottenVersion);
        if (lookingAt == _releaseLookedAt)
        {
            return;
        }

        _releaseLookedAt = lookingAt;
        var released = _renderContext.MeshService.ReleaseUndrawn(_batchStorage.Draws);
        if (released.Count == 0)
        {
            return;
        }

        var dropped = new List<RenderBatch>();
        foreach (var model in released)
        {
            _batchStorage.Drop(model, dropped);
        }

        if (dropped.Count == 0)
        {
            return;
        }

        _passService.UnregisterBatches(dropped);
        var gone = new HashSet<RenderBatch>(dropped, ReferenceEqualityComparer.Instance);
        lock (_renderablesLock)
        {
            _frameBatches.RemoveAll(gone.Contains);
        }

        foreach (var batch in dropped)
        {
            batch.IsFrameBatch = false;
        }
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
        _passService.RegisterBatch(renderBatch);
        renderBatch.RequestPassSwitch += () =>
        {
            _passService.UnregisterRenderableInPasses(renderBatch);
            _passService.RegisterBatch(renderBatch);
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

            if (renderable is LodVisual lod)
            {
                _lods.Add(lod);
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

            if (renderable is LodVisual lod)
            {
                _lods.Remove(lod);
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

    /// <summary>
    /// The editor's own visuals (cursor, selection, grids), left out of pictures of the scene
    /// </summary>
    public bool DrawEditorPrimitives { get; set; } = true;

    /// <summary>
    /// Renders a frame right away, on the render thread between the frames the context renders
    /// </summary>
    internal void RenderNow(float delta)
    {
        DoRender(delta);
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
        // One time for the whole frame: the material table's scrolls and every pass's Time uniform
        _renderContext.Time = Time;
        var fog = _renderContext.Fog;
        // The game clears a chunk without a sky to black (SetUpFrame), the editor's other views are grey
        gl.ClearColor(fog != null ? Color.Black : Color.DimGray);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        _renderContext.FrameCamera = Camera?.GetFrameCamera(true) ?? default;
        _pongBuffers[_readBuffer].TextureAttachment!.Bind(TextureUnit.Texture5);
        BindFogTable(fog);

        PrepareInstances();
        _renderContext.Materials.Prepare(_renderContext.Time);
        _drawsIndirect = _renderContext.DrawsIndirect;
        if (_drawsIndirect)
        {
            _renderContext.IndirectDraws.Build(_passService, _writtenBatches);
        }
        
        RenderSkydome((float)delta);
        RenderBackgroundFog(fog);

        // The scene in the game's order (SceneOrder): its buckets up to the particles', the particles, the buckets after them
        PerformPass((float)delta, _passService.ScenePass);
        PerformPass((float)delta, _passService.ParticlePass);
        PerformPass((float)delta, _passService.LateScenePass);

        // TT Lab's own: volumes, what's about to be placed, the icons
        PerformPassChain((float)delta, _passService.OverlayPasses);
        PerformPassChain((float)delta, _passService.BillboardPasses);
        
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

    // The game draws the sky first, at half the screen's size into a buffer of its own, and copies it over the whole screen doubled pixel
    // for pixel before the level draws (FUN_001ba488, FUN_001ba6c0). The level's depth stays clear, so it always draws over the sky
    private void RenderSkydome(float delta)
    {
        // The scene's camera is in every pass
        if (!_passService.SkydomePasses.Any(pass => _passService.GetEntries(pass).Any(entry => entry.Renderable is not Scene.Camera)))
        {
            return;
        }

        var gl = _renderContext.Gl;
        var size = new ivec2(Math.Max(_frameBufferSize.x / 2, 1), Math.Max(_frameBufferSize.y / 2, 1));
        if (_skyBuffer == null || _skyBufferSize != size)
        {
            _skyBuffer?.Dispose();
            _skyBuffer = new FrameBuffer(_renderContext, size);
            _skyBufferSize = size;
        }

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _skyBuffer.Handler);
        gl.Viewport(0, 0, (uint)size.x, (uint)size.y);
        gl.Scissor(0, 0, (uint)size.x, (uint)size.y);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        PerformPassChain(delta, _passService.SkydomePasses);
        gl.Viewport(0, 0, (uint)_frameBufferSize.x, (uint)_frameBufferSize.y);
        gl.Scissor(0, 0, (uint)_frameBufferSize.x, (uint)_frameBufferSize.y);
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _skyBuffer.Handler);
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _renderContext.GetOutputBuffer());
        gl.BlitFramebuffer(0, 0, size.x, size.y, 0, 0, _frameBufferSize.x, _frameBufferSize.y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _renderContext.GetOutputBuffer());
    }

    // The texels of the chunk's fog the shaders read (FogTable, unit 7), made again when the fog changes
    private void BindFogTable(GameFog? fog)
    {
        if (fog == null)
        {
            return;
        }

        if (_fogTable == null || _fogTableOf != fog)
        {
            _fogTable?.Dispose();
            _fogTable = new TextureBuffer(_renderContext, fog.Texels(), GameFog.Entries, 1, InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte);
            _fogTableOf = fog;
            _skyFog = fog.Sky;
        }

        _fogTable.Bind(TextureUnit.Texture7);
        _renderContext.State.SetActiveTexture(0);
    }

    // The PS2's fog over everything the opaque passes don't draw over: the sky and the cleared background have the depth 0, which picks the
    // table's first entry (GameFog). The Xbox's fog stage only goes over the sky's fragments
    private void RenderBackgroundFog(GameFog? fog)
    {
        if (fog == null || fog.Xbox)
        {
            return;
        }

        var color = _skyFog;
        if (color.w <= 0.0f)
        {
            return;
        }

        var state = _renderContext.State;
        var program = _renderContext.GetProgram("ScreenFog");
        program.Use();
        program.SetUniform(KnownUniform.FogColor, color);
        state.SetDepthTest(false);
        state.SetDepthMask(false);
        state.SetBlend(true);
        state.SetBlendEquation(BlendEquationModeEXT.FuncAdd);
        state.SetBlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.Zero, BlendingFactor.One);
        _emptyVao.Bind();
        _renderContext.Gl.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
        state.SetDepthMask(true);
        state.SetDepthTest(true);
        // The passes after it use their programs from scratch
        _renderContext.Invalidate();
    }

    private void PrepareInstances()
    {
        var instances = _renderContext.Instances;
        instances.Clear();
        var camera = _renderContext.FrameCamera;
        var cullsLods = _renderContext.CullsLods;
        lock (_renderablesLock)
        {
            foreach (var lod in _lods)
            {
                lod.ChooseLevel(camera.IsValid, camera.Position, cullsLods);
            }

            // The batches that changed keep their instances again first, the frame's are written after the kept ones
            _writtenBatches.Clear();
            while (_renderContext.TakeChangedBatch(out var batch))
            {
                var kept = batch.PrepareKeptInstances(instances);
                _writtenBatches.Add(batch);
                if (kept != batch.IsFrameBatch)
                {
                    continue;
                }

                batch.IsFrameBatch = !kept;
                if (kept)
                {
                    _frameBatches.Remove(batch);
                }
                else
                {
                    _frameBatches.Add(batch);
                }
            }

            foreach (var batch in _frameBatches)
            {
                batch.PrepareInstances(instances);
                _writtenBatches.Add(batch);
            }

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

        if (DrawEditorPrimitives)
        {
            DrawPrimitives?.Invoke(_primitiveRenderer, camera);
        }

        _primitiveRenderer.Render(camera);
    }

    private void PerformPassChain(float delta, IList<RenderPass> passes)
    {
        foreach (var pass in passes)
        {
            PerformPass(delta, pass);
        }
    }

    private void PerformPass(float delta, RenderPass pass)
    {
        var entries = _passService.GetEntries(pass);
        // Only the camera, which is in every pass
        if (entries.Count <= 1)
        {
            return;
        }

        if (pass.StartPass())
        {
            var program = _renderContext.CurrentPass.Program;
            program.SetUniform(KnownUniform.Time, (float)_renderContext.Time);
            program.SetUniform(KnownUniform.Resolution, new vec2(_frameBufferSize.x, _frameBufferSize.y));
        }

        // The materials' state stays set from one draw to the next and what the pass started with comes back at its end: it used to come
        // back after every draw, which turned blending off and on again around every opaque draw
        var state = _renderContext.State;
        state.ForgetBindings();
        var blend = state.Blend;
        var depthMask = state.DepthMask;
        var binds = state.MaterialBinds;
        if (_drawsIndirect)
        {
            _renderContext.IndirectDraws.Draw(pass, delta);
        }
        else
        {
            foreach (var entry in entries)
            {
                if (entry.CopiesScreen)
                {
                    _renderContext.ScreenCopy.Take();
                }

                entry.Draw(delta);
            }
        }

        if (state.MaterialBinds != binds)
        {
            state.SetBlend(blend);
            state.SetDepthMask(depthMask);
            state.SetColorMask(true, true, true, true);
            state.SetDepthFunc(DepthFunction.Lequal);
            // The particles sample their pages without a sampler
            state.BindSampler(0, 0);
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
        _skyBuffer?.Dispose();
        _skyBuffer = null;
        _fogTable?.Dispose();
        _fogTable = null;
        _fogTableOf = null;
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
        _renderContext.RenderQueueProcessed -= ReleaseForgottenMeshes;
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