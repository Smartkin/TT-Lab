using System;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Buffers;

public class VertexArrayObject<TVertexType, TIndexType> : IDisposable
    where TVertexType : unmanaged
    where TIndexType : unmanaged
{
    private uint _handle;
    private RenderContext _context;

    public VertexArrayObject(RenderContext context, BufferObject<TVertexType>? vbo, BufferObject<TIndexType>? ebo)
    {
        _context = context;

        _handle = _context.Gl.GenVertexArray();
        Bind();
        vbo?.Bind();
        ebo?.Bind();
    }

    public unsafe void VertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint vertexSize, int offSet)
    {
        _context.Gl.VertexAttribPointer(index, count, type, false, vertexSize * (uint) sizeof(TVertexType), (void*) (offSet * sizeof(TVertexType)));
        _context.Gl.EnableVertexAttribArray(index);
    }

    public void Bind()
    {
        _context.Gl.BindVertexArray(_handle);
    }

    public void Dispose()
    {
        _context.Gl.DeleteVertexArray(_handle);
        GC.SuppressFinalize(this);
    }
}