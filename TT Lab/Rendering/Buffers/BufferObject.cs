using System;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Buffers;

public class BufferObject<TDataType> : IDisposable
    where TDataType : unmanaged
{
    private readonly BufferTargetARB _bufferType;
    private readonly BufferUsageARB _usage;
    private readonly RenderContext _context;

    public BufferObject(RenderContext context, Span<TDataType> data, BufferTargetARB bufferType) : this(context, data, bufferType, BufferUsageARB.StaticDraw)
    {
    }

    public unsafe BufferObject(RenderContext context, Span<TDataType> data, BufferTargetARB bufferType, BufferUsageARB usage)
    {
        _context = context;
        _bufferType = bufferType;
        _usage = usage;

        Handle = _context.Gl.GenBuffer();
        if (usage == BufferUsageARB.DynamicDraw)
        {
            return;
        }
        
        Bind();
        fixed (void* d = data)
        {
            _context.Gl.BufferData(bufferType, (nuint)(data.Length * sizeof(TDataType)), d, usage);
        }
    }
    
    public uint Handle { get; }

    public unsafe void BufferData(Span<TDataType> data)
    {
        Bind();
        fixed (void* d = data)
        {
            _context.Gl.BufferData(_bufferType, (nuint)(data.Length * sizeof(TDataType)), d, _usage);
        }
    }

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