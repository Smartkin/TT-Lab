using System;
using GlmSharp;

namespace TT_Lab.Rendering.Buffers;

/// <summary>
/// Model matrix and color of everything drawn by the scene passes this frame, shaders find their instance through gl_BaseInstance
/// </summary>
public sealed class InstanceBuffer(RenderContext context) : IDisposable
{
    // Has to match InstanceData in ModelLayout.vert
    private const int FloatsPerInstance = 20;
    private const uint Binding = 0;

    private readonly StreamStorageBuffer _buffer = new(context, Binding, FloatsPerInstance);

    public int Count => _buffer.Count;

    public void Clear()
    {
        _buffer.Clear();
    }

    public int Add(in mat4 model, in vec4 color)
    {
        var instance = _buffer.Allocate(out var index);
        StreamStorageBuffer.Write(instance, model);
        StreamStorageBuffer.Write(instance[16..], color);
        return index;
    }

    public void Upload()
    {
        _buffer.Upload();
    }

    public void Dispose()
    {
        _buffer.Dispose();
    }
}
