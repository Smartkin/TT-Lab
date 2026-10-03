using System;
using System.Collections.Generic;
using GlmSharp;
using TT_Lab.Rendering.Lighting;

namespace TT_Lab.Rendering.Buffers;

/// <summary>
/// Model matrix and color of everything drawn by the scene passes this frame, shaders find their instance through gl_BaseInstance.
/// Lit meshes also get the light set of the object they're part of, gathered once per object and frame
/// </summary>
public sealed class InstanceBuffer(RenderContext context) : IDisposable
{
    // Has to match InstanceData in ModelLayout.vert: the model matrix, the color and the light set's index (-1 for none) padded to 4
    private const int FloatsPerInstance = 24;
    // Has to match LightSet in ModelLayout.vert: the ambient color, then a direction and a color per slot
    private const int FloatsPerLightSet = 4 * (1 + 2 * LightSet.Slots);
    private const uint Binding = 0;
    private const uint LightSetBinding = 5;
    public const int NoLightSet = -1;

    private readonly StreamStorageBuffer _buffer = new(context, Binding, FloatsPerInstance);
    private readonly StreamStorageBuffer _lightSets = new(context, LightSetBinding, FloatsPerLightSet);
    private readonly Dictionary<Renderable, int> _lightSetOf = new(ReferenceEqualityComparer.Instance);

    public int Count => _buffer.Count;

    public void Clear()
    {
        _buffer.Clear();
        _lightSets.Clear();
        _lightSetOf.Clear();
    }

    public int Add(in mat4 model, in vec4 color, int lightSet = NoLightSet)
    {
        var instance = _buffer.Allocate(out var index);
        StreamStorageBuffer.Write(instance, model);
        StreamStorageBuffer.Write(instance[16..], color);
        instance[20] = BitConverter.Int32BitsToSingle(lightSet);
        instance[21] = 0.0f;
        instance[22] = 0.0f;
        instance[23] = 0.0f;
        return index;
    }

    /// <summary>
    /// The light set the renderable is lit with this frame: its object's, gathered from the scene's lights where the object is
    /// </summary>
    public int LightSetOf(Renderable renderable)
    {
        var anchor = LightAnchor.Of(renderable);
        if (_lightSetOf.TryGetValue(anchor, out var index))
        {
            return index;
        }

        var set = context.Lights.At(anchor.RenderTransform.Column3.xyz);
        var data = _lightSets.Allocate(out index);
        StreamStorageBuffer.Write(data, new vec4(set.Ambient, 0.0f));
        for (var slot = 0; slot < LightSet.Slots; slot++)
        {
            var (direction, color) = set[slot];
            StreamStorageBuffer.Write(data[(4 + slot * 8)..], new vec4(direction, 0.0f));
            StreamStorageBuffer.Write(data[(8 + slot * 8)..], new vec4(color, 0.0f));
        }

        _lightSetOf[anchor] = index;
        return index;
    }

    public void Upload()
    {
        _buffer.Upload();
        _lightSets.Upload();
    }

    public void Dispose()
    {
        _buffer.Dispose();
        _lightSets.Dispose();
    }
}
