using System;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Buffers;

/// <summary>
/// One buffer of the vertexes of a context's models, drawn through one vertex array from each model's first vertex: a vertex array per
/// model made every draw switch arrays, which cost the driver more than the draw. Every model's vertexes have the same layout
/// (<see cref="MeshBuilder.RigidVertexFloats"/> floats), the attributes the vertex arrays of <see cref="MeshBuilder"/> had. On the render thread
/// </summary>
public sealed class VertexPool(RenderContext context) : IDisposable
{
    private const int VertexBytes = MeshBuilder.RigidVertexFloats * sizeof(float);
    private const int FirstCapacity = 1 << 14;

    // Location, floats and offset in floats of every attribute (MainPass.vert's in_Position to in_BoneWeights)
    private static readonly (uint Location, int Size, int Offset)[] Attributes = [(0, 3, 0), (1, 4, 3), (2, 2, 7), (3, 3, 9), (4, 4, 12), (5, 3, 16), (6, 3, 19)];

    private readonly RangeAllocator _ranges = new();
    private uint _buffer;
    private uint _vertexArray;
    private int _capacity;

    /// <summary>
    /// The vertex array drawing the pool's vertexes
    /// </summary>
    public uint VertexArray
    {
        get
        {
            EnsureVertexArray();
            return _vertexArray;
        }
    }

    public int Used => _ranges.End;

    /// <summary>
    /// Copies the vertexes of a model into the pool, they're drawn from the first vertex this gives
    /// </summary>
    public unsafe int Add(ReadOnlySpan<float> vertexes)
    {
        var count = vertexes.Length / MeshBuilder.RigidVertexFloats;
        var first = _ranges.Take(count);
        EnsureCapacity(_ranges.End);
        if (count > 0)
        {
            fixed (float* data = vertexes)
            {
                context.Gl.NamedBufferSubData(_buffer, (nint)first * VertexBytes, (nuint)(count * VertexBytes), data);
            }
        }

        return first;
    }

    /// <summary>
    /// Lets the vertexes of a model go, another model's can take their place
    /// </summary>
    public void Remove(int first, int count)
    {
        _ranges.Give(first, count);
    }

    private unsafe void EnsureCapacity(int vertexes)
    {
        EnsureVertexArray();
        if (vertexes <= _capacity)
        {
            return;
        }

        var gl = context.Gl;
        var capacity = Math.Max(_capacity, FirstCapacity);
        while (capacity < vertexes)
        {
            capacity *= 2;
        }

        // A bigger buffer with what the old one held: the models keep their first vertexes
        var buffer = gl.CreateBuffer();
        gl.NamedBufferStorage(buffer, (nuint)((long)capacity * VertexBytes), null, BufferStorageMask.DynamicStorageBit);
        if (_buffer != 0)
        {
            gl.CopyNamedBufferSubData(_buffer, buffer, 0, 0, (nuint)((long)_capacity * VertexBytes));
            gl.DeleteBuffer(_buffer);
        }

        _buffer = buffer;
        _capacity = capacity;
        gl.VertexArrayVertexBuffer(_vertexArray, 0, _buffer, 0, VertexBytes);
    }

    private void EnsureVertexArray()
    {
        if (_vertexArray != 0)
        {
            return;
        }

        var gl = context.Gl;
        _vertexArray = gl.CreateVertexArray();
        foreach (var (location, size, offset) in Attributes)
        {
            gl.EnableVertexArrayAttrib(_vertexArray, location);
            gl.VertexArrayAttribFormat(_vertexArray, location, size, VertexAttribType.Float, false, (uint)(offset * sizeof(float)));
            gl.VertexArrayAttribBinding(_vertexArray, location, 0);
        }
    }

    public void Dispose()
    {
        var gl = context.Gl;
        if (_vertexArray != 0)
        {
            context.State.ForgetVertexArray(_vertexArray);
            gl.DeleteVertexArray(_vertexArray);
            _vertexArray = 0;
        }

        if (_buffer != 0)
        {
            gl.DeleteBuffer(_buffer);
            _buffer = 0;
        }
    }
}
