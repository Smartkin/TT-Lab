using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using GlmSharp;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Factories;
using TT_Lab.Rendering.Native;
using TT_Lab.Rendering.Passes;
using TT_Lab.Rendering.Services;
using TT_Lab.Rendering.Shaders;
using GL = Silk.NET.OpenGL.GL;
using Shader = TT_Lab.Rendering.Shaders.Shader;

namespace TT_Lab.Rendering;

public class RenderContext : IDisposable
{
    public GL Gl
    {
        get
        {
            Debug.Assert(_isGlAccessible, "Attempting to access rendering context outside of rendering loop");
            return _gl;
        }
    }

    public event Action<double>? Render;
    public event Action? ResizeFramebuffer;
    public event Action? Destroy;

    private bool _isInit = false;
    
    private RenderPass? _currentPass;
    private readonly Dictionary<string, ShaderProgram> _programs = [];
    private GL _gl;
    private readonly ConcurrentQueue<Action> _renderQueue = new();
    private bool _invalidated = true;
    private bool _isGlAccessible = true;
    private int _outputBuffer = -1;

    public RenderContext(GL gl)
    {
        _gl = gl;
        State = new GlStateCache(gl);
        InitRenderApi();
        InitServices();
    }

    private void InitRenderApi()
    {
        var version = Gl.GetStringS(GLEnum.Version);
        if (version == null)
        {
            throw new Exception("Failed to initialize OpenGL context");
        }
        
        var majorVersion = Gl.GetInteger(GLEnum.MajorVersion);
        if (majorVersion < 4)
        {
            throw new Exception("OpenGL version 4.6 or above is required for TT Lab");
        }

        var minorVersion = Gl.GetInteger(GLEnum.MinorVersion);
        if (minorVersion < 6)
        {
            throw new Exception("OpenGL version 4.6 or above is required for TT Lab");
        }
        
        Console.WriteLine($@"OpenGL version loaded: {version}");
        var renderer = Gl.GetStringS(StringName.Renderer);
        var vendor = Gl.GetStringS(StringName.Vendor);
        Console.WriteLine($@"Renderer: {renderer}");
        Console.WriteLine($@"Vendor: {vendor}");

        var maxTextureSize = Gl.GetInteger(GLEnum.MaxTextureSize);
        Console.WriteLine($@"Max texture size: {maxTextureSize}");
        
#if DEBUG
        Gl.Enable(EnableCap.DebugOutputSynchronous);
        Gl.DebugMessageCallback(DebugCallback, IntPtr.Zero);
#endif
        
        Gl.Enable(EnableCap.DepthTest);
        Gl.Enable(EnableCap.StencilTest);
        Gl.Enable(EnableCap.ScissorTest);
        Gl.Enable(EnableCap.Blend);
        Gl.CullFace(TriangleFace.FrontAndBack);
        Gl.DepthMask(true);
        Gl.DepthFunc(DepthFunction.Lequal);

        var colorVertShader = new Shader(this, ShaderType.VertexShader, "MainPass.vert");
        var colorFragShader = new Shader(this, ShaderType.FragmentShader, "MainPass.frag");
        var program = new ShaderProgram(this, colorVertShader, colorFragShader);
        _programs.Add("Generic", program);
        WriteProgramUniforms(program);
        
        var lineVertShader = new Shader(this, ShaderType.VertexShader, "PrimitiveLine.vert");
        var lineFragShader = new Shader(this, ShaderType.FragmentShader, "PrimitiveLine.frag");
        _programs.Add("PrimitiveLine", new ShaderProgram(this, lineVertShader, lineFragShader));

        var shapeVertShader = new Shader(this, ShaderType.VertexShader, "PrimitiveShape.vert");
        var shapeFragShader = new Shader(this, ShaderType.FragmentShader, "PrimitiveShape.frag");
        _programs.Add("PrimitiveShape", new ShaderProgram(this, shapeVertShader, shapeFragShader));

        var gridVertShader = new Shader(this, ShaderType.VertexShader, "PrimitiveGrid.vert");
        var gridFragShader = new Shader(this, ShaderType.FragmentShader, "PrimitiveGrid.frag");
        _programs.Add("PrimitiveGrid", new ShaderProgram(this, gridVertShader, gridFragShader));

        var particleVertShader = new Shader(this, ShaderType.VertexShader, "Particle.vert");
        var particleFragShader = new Shader(this, ShaderType.FragmentShader, "Particle.frag");
        _programs.Add("Particle", new ShaderProgram(this, particleVertShader, particleFragShader));

        var distortionVertShader = new Shader(this, ShaderType.VertexShader, "ParticleDistortion.vert");
        var distortionFragShader = new Shader(this, ShaderType.FragmentShader, "ParticleDistortion.frag");
        _programs.Add("ParticleDistortion", new ShaderProgram(this, distortionVertShader, distortionFragShader));

        var screenVertShader = new Shader(this, ShaderType.VertexShader, "ScreenRender.vert");
        var screenFlipFragShader = new Shader(this, ShaderType.FragmentShader, "ScreenHorizontalFlip.frag");
        var screenFlipProgram = new ShaderProgram(this, screenVertShader, screenFlipFragShader);
        _programs.Add("ScreenFlipX", screenFlipProgram);
        WriteProgramUniforms(screenFlipProgram);
    }

    [MemberNotNull(nameof(SkeletonManager))]
    [MemberNotNull(nameof(MeshBuilder))]
    [MemberNotNull(nameof(BatchService))]
    [MemberNotNull(nameof(TextureService))]
    [MemberNotNull(nameof(MaterialFactory))]
    [MemberNotNull(nameof(MeshFactory))]
    [MemberNotNull(nameof(MeshService))]
    [MemberNotNull(nameof(SceneInstanceFactory))]
    [MemberNotNull(nameof(PassService))]
    [MemberNotNull(nameof(PrimitiveRenderer))]
    [MemberNotNull(nameof(MaterialService))]
    [MemberNotNull(nameof(Instances))]
    private void InitServices()
    {
        Instances = new InstanceBuffer(this);
        PrimitiveRenderer = new PrimitiveRenderer(this);
        SkeletonManager = new TwinSkeletonManager(this);
        MeshBuilder = new MeshBuilder(this);
        BatchService = new BatchService(this);
        TextureService = new TextureService(this);
        MaterialFactory = new MaterialFactory(TextureService);
        MeshFactory = new MeshFactory(this, MeshBuilder, MaterialFactory);
        MeshService = new MeshService(MeshFactory);
        SceneInstanceFactory = new SceneInstanceFactory(MeshService, SkeletonManager);
        PassService = new PassService(this);
        MaterialService = new MaterialService(this, MaterialFactory);
    }
    
    public GlStateCache State { get; }
    public InstanceBuffer Instances { get; private set; }
    // Camera the current frame gets rendered from, only valid on the render thread while rendering
    public FrameCamera FrameCamera { get; set; }
    public PassService PassService { get; private set; }
    public TwinSkeletonManager SkeletonManager { get; private set; }
    public MeshBuilder MeshBuilder { get; private set; }
    public BatchService BatchService { get; private set; }
    public TextureService TextureService { get; private set; }
    public MaterialFactory MaterialFactory { get; private set; }
    public MaterialService MaterialService { get; private set; }
    public MeshFactory MeshFactory { get; private set; }
    public MeshService MeshService { get; private set; }
    public SceneInstanceFactory SceneInstanceFactory { get; private set; }
    public PrimitiveRenderer PrimitiveRenderer { get; private set; }

    /// <summary>
    /// Seconds since the renderer started, what the shaders' Time uniform holds, set by the renderer before every frame
    /// </summary>
    public double Time { get; set; }

    /// <summary>
    /// The scene's three strongest lights, unit vectors towards where the light comes from, what the game's environment map looks up
    /// by. A scene without lights gets ones from above and the sides
    /// </summary>
    public vec3[] EnvLights { get; set; } = [new vec3(0.0f, 1.0f, 0.0f), new vec3(1.0f, 0.0f, 0.0f), new vec3(0.0f, 0.0f, 1.0f)];
    public vec2 ViewportSize { get; set; }

    public void SetGlAccessibility(bool isAccessible)
    {
        _isGlAccessible = isAccessible;
    }

    public void FireResize()
    {
        ResizeFramebuffer?.Invoke();
    }

    // Framebuffer the viewport shows, the context's window is only there to make the context current
    public uint OutputBuffer { get; set; }

    public uint GetOutputBuffer() => OutputBuffer;

    public void QueueRenderAction(Action action)
    {
        _renderQueue.Enqueue(action);
    }

    public void PerformRender(float delta)
    {
        ProcessRenderQueue();
        Render?.Invoke(delta);
    }

    public void ProcessRenderQueue()
    {
        while (_renderQueue.TryDequeue(out var renderAction))
        {
            renderAction.Invoke();
        }
    }

    public ShaderProgram GetProgram(string shaderName)
    {
        return _programs[shaderName];
    }

    public void ChangePass(RenderPass? pass)
    {
        _currentPass = pass;
    }

    public void Invalidate()
    {
        _invalidated = true;
    }

    public void Validate()
    {
        _invalidated = false;
    }
    
    public bool Invalidated => _invalidated;
    public RenderPass CurrentPass => _currentPass;

    private void WriteProgramUniforms(ShaderProgram program)
    {
        Gl.GetProgram(program.Handle, GLEnum.ActiveUniforms, out var uniformCount);
        for (var i = 0; i < uniformCount; i++)
        {
            var name = Gl.GetActiveUniform(program.Handle, (uint)i, out var size, out var type);
            Console.WriteLine($@"Uniform {i}: {name} {type} {size}");
        }
    }

    // Kept alive for as long as the driver can call it
    private static readonly DebugProc DebugCallback = DebugGlCallback;

    private static void DebugGlCallback(GLEnum source, GLEnum type, int id, GLEnum severity, int length, IntPtr message, IntPtr userParam)
    {
        // TODO: We don't care about performance right now, maybe we will later... Also we don't care about notifications
        if (type == GLEnum.DebugTypePerformance || severity == GLEnum.DebugSeverityNotification)
        {
            return;
        }

        var msg = SilkMarshal.PtrToString(message);
        // Exceptions can't go through the driver's frames back to managed code, throwing here ended the application
        if (type == GLEnum.DebugTypeError)
        {
            Log.WriteLine($"GL error: {msg}\n{Environment.StackTrace}", Log.LogType.Error);
            return;
        }
        
        Console.WriteLine($@"GL {source} {type} {severity}: {msg}");
    }

    public void Dispose()
    {
        Destroy?.Invoke();
        Instances.Dispose();
        PrimitiveRenderer.Dispose();
        Gl.Dispose();
        SetGlAccessibility(false);
        GC.SuppressFinalize(this);
    }
}