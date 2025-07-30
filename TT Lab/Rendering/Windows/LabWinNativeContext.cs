using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Loader;
using TerraFX.Interop.Windows;
using WinApi = TerraFX.Interop.Windows.Windows;

namespace TT_Lab.Rendering.Windows;

public unsafe class LabWinNativeContext : IGlContext
{
    private readonly HWND _hwnd;
    private readonly HDC _hdc;
    private readonly HGLRC _realContext;
    private readonly UnmanagedLibrary _openGlLib;

    public LabWinNativeContext()
    {
        _openGlLib = new UnmanagedLibrary("opengl32.dll");

        // Step 1: Create dummy window and context (just like your current code)
        var dummyWindow = CreateWindow("dummy");
        var dummyHdc = WinApi.GetDC(dummyWindow);

        var dummyPfd = new PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)sizeof(PIXELFORMATDESCRIPTOR),
            nVersion = 1,
            dwFlags = PFD.PFD_DRAW_TO_WINDOW | PFD.PFD_SUPPORT_OPENGL | PFD.PFD_DOUBLEBUFFER,
            iPixelType = PFD.PFD_TYPE_RGBA,
            cColorBits = 32,
            cDepthBits = 24,
            cStencilBits = 8,
            iLayerType = PFD.PFD_MAIN_PLANE
        };

        var pixelFormat = WinApi.ChoosePixelFormat(dummyHdc, &dummyPfd);
        WinApi.SetPixelFormat(dummyHdc, pixelFormat, &dummyPfd);
        var dummyContext = WinApi.wglCreateContext(dummyHdc);
        WinApi.wglMakeCurrent(dummyHdc, dummyContext);
        
        // Step 2: Load wglCreateContextAttribsARB
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int*, IntPtr> attribsFuncPtr;
        var functionName = "wglCreateContextAttribsARB";
        var asciiName = Encoding.ASCII.GetBytes(functionName + '\0');
        fixed (byte* namePtr = asciiName)
        {
            attribsFuncPtr = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int*, IntPtr>)
                WinApi.wglGetProcAddress((sbyte*)namePtr);
        }

        if (attribsFuncPtr == null)
            throw new InvalidOperationException("wglCreateContextAttribsARB not available");

        // Step 3: Create real window and set same pixel format
        WinApi.wglMakeCurrent(dummyHdc, HGLRC.NULL);
        WinApi.wglDeleteContext(dummyContext);
        WinApi.ReleaseDC(dummyWindow, dummyHdc);
        WinApi.DestroyWindow(dummyWindow);

        var realHwnd = CreateWindow("modern");
        _hwnd = realHwnd;
        _hdc = WinApi.GetDC(_hwnd);

        WinApi.SetPixelFormat(_hdc, pixelFormat, &dummyPfd); // reuse the same pixel format

        // Step 4: Create real context
        int[] attribs =
        [
            WGL_CONTEXT_MAJOR_VERSION_ARB, 4,
            WGL_CONTEXT_MINOR_VERSION_ARB, 6,
            WGL_CONTEXT_PROFILE_MASK_ARB, WGL_CONTEXT_CORE_PROFILE_BIT_ARB,
            WGL_CONTEXT_FLAGS_ARB, WGL_CONTEXT_DEBUG_BIT_ARB,
            0
        ];

        fixed (int* attribsPtr = attribs)
        {
            _realContext = (HGLRC)attribsFuncPtr(_hdc, IntPtr.Zero, attribsPtr);
        }

        if (_realContext == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create modern OpenGL context");

        WinApi.wglMakeCurrent(_hdc, _realContext);
    }

    public void Dispose()
    {
        WinApi.wglMakeCurrent(_hdc, HGLRC.NULL);
        WinApi.wglDeleteContext(_realContext);
        WinApi.ReleaseDC(_hwnd, _hdc);
        WinApi.DestroyWindow(_hwnd);
        _openGlLib.Dispose();
        GC.SuppressFinalize(this);
    }

    private static HWND CreateWindow(string title)
    {
        const string className = "ModernGLWindowClass";

        fixed (char* classNamePtr = className)
        {
            var wndClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                hInstance = HINSTANCE.NULL,
                lpszClassName = classNamePtr,
                lpfnWndProc = &DefaultWndProc
            };
            WinApi.RegisterClassExW(&wndClass);
        }

        fixed (char* titlePtr = title)
        fixed (char* classNamePtr = className)
        {
            return WinApi.CreateWindowExW(0, classNamePtr, titlePtr, WS.WS_OVERLAPPEDWINDOW,
                0, 0, 128, 128, HWND.NULL, HMENU.NULL, HINSTANCE.NULL, null);
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT DefaultWndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        return WinApi.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public Boolean TryGetProcAddress(string proc, [UnscopedRef] out nint addr, int? slot = null)
    {
        if (_openGlLib.TryLoadFunction(proc, out addr))
        {
            return true;
        }
        
        var asciiName = new byte[proc.Length + 1];
        Encoding.ASCII.GetBytes(proc, asciiName);

        fixed (byte* name = asciiName)
        {
            addr = (nint)WinApi.wglGetProcAddress((sbyte*)name);
            
            if (addr != IntPtr.Zero)
            {
                return true;
            }
        }

        return false;
    }

    public bool MakeCurrent()
    {
        return WinApi.wglMakeCurrent(_hdc, _realContext);
    }

    public nint Handle => _hwnd;
    public IGLContextSource? Source => null;
    public bool IsCurrent => WinApi.wglGetCurrentContext() == _realContext;

    public IntPtr GetProcAddress(string proc, int? slot = null)
    {
        if (TryGetProcAddress(proc, out var addr, slot))
        {
            return addr;
        }

        throw new InvalidOperationException($"No function was found with the name {proc}.");
    }
    
    private const int WGL_CONTEXT_MAJOR_VERSION_ARB = 0x2091;
    private const int WGL_CONTEXT_MINOR_VERSION_ARB = 0x2092;
    private const int WGL_CONTEXT_PROFILE_MASK_ARB = 0x9126;
    private const int WGL_CONTEXT_CORE_PROFILE_BIT_ARB = 0x00000001;
    private const int WGL_CONTEXT_DEBUG_BIT_ARB = 0x00000001;
    private const int WGL_CONTEXT_FLAGS_ARB = 0x2094;
}

