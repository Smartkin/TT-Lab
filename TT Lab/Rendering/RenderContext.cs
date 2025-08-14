using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using GlmSharp;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Native;
using TT_Lab.Rendering.Native.Windows;
using TT_Lab.Rendering.Passes;
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
            Debug.Assert(Thread.CurrentThread == _createdThread, "Attempting to access rendering context outside of created thread");
            return _gl;
        }
    }

    public event Action<double>? Render;
    
    private const int FPS = 60;
    private const float RenderDelta = 1.0f / FPS;
    private readonly Stopwatch _renderWatch = new();
    private RenderPass? _currentPass;
    private readonly Dictionary<string, ShaderProgram> _programs = [];
    private Thread _createdThread;
    private GL _gl;
    private CancellationToken _renderCancelToken;
    private CancellationTokenSource _tokenSource;
    private readonly ConcurrentQueue<Action> _renderQueue = new();
    private PrimitiveRenderer _primitiveRenderer;
    private bool _invalidated = true;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public RenderContext()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
    }

    public void InitRenderApi()
    {
        _tokenSource = new CancellationTokenSource();
        _renderCancelToken = _tokenSource.Token;
        
        _createdThread = new Thread(() =>
        {
#if _WINDOWS
            _gl = GL.GetApi(new LabWinNativeContext());
#elif _LINUX
            _gl = GL.GetApi(new LabLinuxNativeContext());
#else
            throw new Exception("Unsupported platform.");
#endif

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
            Gl.CullFace(TriangleFace.FrontAndBack);
            Gl.DepthMask(true);
            Gl.DepthFunc(DepthFunction.Lequal);

            var colorVertShader = new Shader(this, ShaderType.VertexShader, "MainPass.vert");
            var colorFragShader = new Shader(this, ShaderType.FragmentShader, "MainPass.frag");
            var program = new ShaderProgram(this, colorVertShader, colorFragShader);
            _programs.Add("Generic", program);
            WriteProgramUniforms(program);

            var screenVertShader = new Shader(this, ShaderType.VertexShader, "ScreenRender.vert");
            var screenFlipFragShader = new Shader(this, ShaderType.FragmentShader, "ScreenHorizontalFlip.frag");
            var screenFlipProgram = new ShaderProgram(this, screenVertShader, screenFlipFragShader);
            _programs.Add("ScreenFlipX", screenFlipProgram);
            WriteProgramUniforms(screenFlipProgram);

            _primitiveRenderer = new PrimitiveRenderer(this);
            
            _renderWatch.Start();
            
            while (!_renderCancelToken.IsCancellationRequested)
            {
                var delta = _renderWatch.Elapsed.TotalSeconds;
                if (delta < RenderDelta)
                {
                    continue;
                }
                
                _renderWatch.Restart();
                MakeCurrent();
                Render?.Invoke(delta);

                while (_renderQueue.TryDequeue(out var renderAction))
                {
                    renderAction.Invoke();
                }
            }
        })
        {
            IsBackground = true,
            Name = "Render Thread"
        };

        _createdThread.Start();
    }
    
    public PrimitiveRenderer GetPrimitiveRenderer() => _primitiveRenderer;

    public void QueueRenderAction(Action action)
    {
        _renderQueue.Enqueue(action);
    }

    public void ShutdownRender()
    {
        _tokenSource.Cancel();
        _createdThread.Join();
        _tokenSource.Dispose();
    }

    public ShaderProgram GetProgram(string shaderName)
    {
        return _programs[shaderName];
    }

    public void SwapBuffers()
    {
        ((IGlContext)Gl.Context).SwapBuffers();
    }

    public void ChangePass(RenderPass? pass)
    {
        _currentPass = pass;
    }

    public void MakeCurrent()
    {
        Debug.Assert(Thread.CurrentThread == _createdThread, "Attempting to access rendering outside of created thread");
        var isCurrent = ((IGlContext)Gl.Context).MakeCurrent();
        Debug.Assert(isCurrent, "Failed to make render context current");
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
        // TODO: We don't care about performance right now, maybe we will later...
        if (type == GLEnum.DebugTypePerformance)
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
        GC.SuppressFinalize(this);
    }
}