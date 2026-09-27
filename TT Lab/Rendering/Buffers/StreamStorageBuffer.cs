using System;
using GlmSharp;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Buffers;

/// <summary>
/// Shader storage buffer of fixed size float elements that gets refilled every frame
/// </summary>
public sealed class StreamStorageBuffer(RenderContext context, uint binding, int floatsPerElement) : IDisposable
{
    private float[] _data = new float[floatsPerElement * 256];
    private uint _handle;
    private nuint _capacityBytes;

    public int Count { get; private set; }

    public void Clear()
    {
        Count = 0;
    }

    public Span<float> Allocate(out int index)
    {
        return Allocate(1, out index);
    }

    public Span<float> Allocate(int elements, out int firstIndex)
    {
        var required = (Count + elements) * floatsPerElement;
        if (required > _data.Length)
        {
            Array.Resize(ref _data, Math.Max(required, _data.Length * 2));
        }

        firstIndex = Count;
        Count += elements;
        return _data.AsSpan(firstIndex * floatsPerElement, elements * floatsPerElement);
    }

    public unsafe void Upload()
    {
        var gl = context.Gl;
        if (_handle == 0)
        {
            _handle = gl.GenBuffer();
        }

        gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _handle);
        var bytes = (nuint)(Math.Max(Count, 1) * floatsPerElement * sizeof(float));
        // Respecifying the storage lets the driver hand out a new one instead of waiting for last frame's draws to finish reading it
        if (bytes > _capacityBytes)
        {
            _capacityBytes = Math.Max(bytes, _capacityBytes * 2);
        }

        fixed (float* data = _data)
        {
            gl.BufferData(BufferTargetARB.ShaderStorageBuffer, _capacityBytes, null, BufferUsageARB.StreamDraw);
            gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0, bytes, data);
        }

        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, binding, _handle);
    }

    public void Bind()
    {
        if (_handle == 0)
        {
            return;
        }

        context.Gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, binding, _handle);
    }

    public void Dispose()
    {
        if (_handle == 0)
        {
            return;
        }

        context.Gl.DeleteBuffer(_handle);
        _handle = 0;
    }

    public static void Write(Span<float> destination, in mat4 matrix)
    {
        destination[0] = matrix.m00;
        destination[1] = matrix.m01;
        destination[2] = matrix.m02;
        destination[3] = matrix.m03;
        destination[4] = matrix.m10;
        destination[5] = matrix.m11;
        destination[6] = matrix.m12;
        destination[7] = matrix.m13;
        destination[8] = matrix.m20;
        destination[9] = matrix.m21;
        destination[10] = matrix.m22;
        destination[11] = matrix.m23;
        destination[12] = matrix.m30;
        destination[13] = matrix.m31;
        destination[14] = matrix.m32;
        destination[15] = matrix.m33;
    }

    public static void Write(Span<float> destination, in vec4 vector)
    {
        destination[0] = vector.x;
        destination[1] = vector.y;
        destination[2] = vector.z;
        destination[3] = vector.w;
    }
}
