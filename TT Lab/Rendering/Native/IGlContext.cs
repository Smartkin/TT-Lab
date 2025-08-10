using Silk.NET.Core.Contexts;

namespace TT_Lab.Rendering.Native;

public interface IGlContext : IGLContext
{
    new bool MakeCurrent();
}