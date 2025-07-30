using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Loader;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using TerraFX.Interop.Windows;
using TT_Lab.Rendering.Windows;
using GL = Silk.NET.OpenGL.GL;
using Windows = TerraFX.Interop.Windows.Windows;

namespace TT_Lab.Rendering;

public class RenderContext : IDisposable
{
    public GL Gl { get; private set; }
    private readonly Thread _createdThread;

    public RenderContext()
    {
        _createdThread = Thread.CurrentThread;
        InitRenderApi();
    }

    [MemberNotNull(nameof(Gl))]
    private void InitRenderApi()
    {
#if _WINDOWS
        Gl = GL.GetApi(new LabWinNativeContext());
#elif _LINUX
        Gl = GL.GetApi(new LabLinuxNativeContext());
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
        
        Gl.Enable(EnableCap.DebugOutput);
        Gl.DebugMessageCallback(DebugGlCallback, IntPtr.Zero);
        
        Gl.Enable(EnableCap.DepthTest);
        Gl.Enable(EnableCap.StencilTest);
    }

    public void MakeCurrent()
    {
        Debug.Assert(Thread.CurrentThread == _createdThread, "Attempting to access rendering outside of created thread");
        var isCurrent = ((IGlContext)Gl.Context).MakeCurrent();
        Debug.Assert(isCurrent, "Failed to make render context current");
    }

    private static void DebugGlCallback(GLEnum source, GLEnum type, int id, GLEnum severity, int length, IntPtr message, IntPtr userParam)
    {
        var msg = SilkMarshal.PtrToString(message);
        Console.WriteLine($@"GL {source} {type} {severity}: {msg}");
    }

    public void Dispose()
    {
        Gl.Dispose();
        GC.SuppressFinalize(this);
    }
}