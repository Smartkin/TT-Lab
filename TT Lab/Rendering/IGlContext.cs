using Silk.NET.Core.Contexts;

namespace TT_Lab.Rendering;

public interface IGlContext : INativeContext
{
    bool MakeCurrent();
}