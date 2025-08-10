using System;
using System.Diagnostics.CodeAnalysis;
using Silk.NET.Core.Contexts;

namespace TT_Lab.Rendering.Native.Linux;

public class LabLinuxNativeContext : IGlContext
{
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public IntPtr GetProcAddress(string proc, int? slot = null)
    {
        throw new NotImplementedException();
    }

    public Boolean TryGetProcAddress(string proc, [UnscopedRef] out IntPtr addr, int? slot = null)
    {
        throw new NotImplementedException();
    }

    public void SwapInterval(int interval)
    {
        throw new NotImplementedException();
    }

    public void SwapBuffers()
    {
        throw new NotImplementedException();
    }

    void IGLContext.MakeCurrent()
    {
        MakeCurrent();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    public IntPtr Handle { get; }
    public IGLContextSource? Source { get; }
    public bool IsCurrent { get; }

    public bool MakeCurrent()
    {
        throw new NotImplementedException();
    }
    
}