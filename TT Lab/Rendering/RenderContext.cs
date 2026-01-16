using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using GlmSharp;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
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
        if (majorVersion < 3)
        {
            throw new Exception("OpenGL version 4 or above is required for TT Lab");
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
        Gl.DebugMessageCallback(DebugGlCallback, IntPtr.Zero);
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
        
        var instancedVertShader = new Shader(this, ShaderType.VertexShader, "MainPassInstanced.vert");
        var instancedFragShader = new Shader(this, ShaderType.FragmentShader, "MainPass.frag");
        var instancedProgram = new ShaderProgram(this, instancedVertShader, instancedFragShader);
        _programs.Add("GenericInstanced", instancedProgram);
        WriteProgramUniforms(instancedProgram);

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
    private void InitServices()
    {
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
    }
    
    public PassService PassService { get; private set; }
    public TwinSkeletonManager SkeletonManager { get; private set; }
    public MeshBuilder MeshBuilder { get; private set; }
    public BatchService BatchService { get; private set; }
    public TextureService TextureService { get; private set; }
    public MaterialFactory MaterialFactory { get; private set; }
    public MeshFactory MeshFactory { get; private set; }
    public MeshService MeshService { get; private set; }
    public SceneInstanceFactory SceneInstanceFactory { get; private set; }
    public PrimitiveRenderer PrimitiveRenderer { get; private set; }
    public vec2 ViewportSize { get; set; }

    public void SetGlAccessibility(bool isAccessible)
    {
        _isGlAccessible = isAccessible;
    }

    public void SetOutputBuffer(int fb) => _outputBuffer = fb;
    public uint GetOutputBuffer() => (uint)_outputBuffer;

    public void QueueRenderAction(Action action)
    {
        _renderQueue.Enqueue(action);
    }

    public void PerformRender(float delta)
    {
        Render?.Invoke(delta);

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

    private static void DebugGlCallback(GLEnum source, GLEnum type, int id, GLEnum severity, int length, IntPtr message, IntPtr userParam)
    {
        // TODO: We don't care about performance right now, maybe we will later... Also we don't care about notifications
        if (type == GLEnum.DebugTypePerformance || severity == GLEnum.DebugSeverityNotification)
        {
            return;
        }

        var msg = SilkMarshal.PtrToString(message);
        if (type == GLEnum.DebugTypeError)
        {
            throw new Exception($@"GLEnum.DebugTypeError: {msg}");
        }
        
        Console.WriteLine($@"GL {source} {type} {severity}: {msg}");
    }

    public void Dispose()
    {
        Gl.Dispose();
        SetGlAccessibility(false);
        GC.SuppressFinalize(this);
    }
}