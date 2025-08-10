using System;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Buffers;

public class BufferObject<TDataType> : IDisposable
    where TDataType : unmanaged
{
    private readonly BufferTargetARB _bufferType;
    private readonly RenderContext _context;

    public unsafe BufferObject(RenderContext context, Span<TDataType> data, BufferTargetARB bufferType)
    {
        _context = context;
        _bufferType = bufferType;

        Handle = _context.Gl.GenBuffer();
        Bind();
        fixed (void* d = data)
        {
            _context.Gl.BufferData(bufferType, (nuint) (data.Length * sizeof(TDataType)), d, BufferUsageARB.StaticDraw);
        }
    }
    
    public uint Handle { get; }

    public void Bind()
    {
        _context.Gl.BindBuffer(_bufferType, Handle);
    }

    public void Unbind()
    {
        _context.Gl.BindBuffer(_bufferType, 0);
    }

    public void Dispose()
    {
        _context.Gl.DeleteBuffer(Handle);
        GC.SuppressFinalize(this);
    }
}