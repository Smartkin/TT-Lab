using System;
using System.Diagnostics.CodeAnalysis;
using Silk.NET.Core.Contexts;

namespace TT_Lab.Rendering.Linux;

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

    public bool MakeCurrent()
    {
        throw new NotImplementedException();
    }
    
}