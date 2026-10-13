using System;
using System.Runtime.InteropServices;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Lighting;

namespace TT_Lab.Rendering.Buffers;

/// <summary>
/// Model matrix and color of everything drawn by the scene passes this frame, shaders find their instance through gl_BaseInstance.
/// Batches whose meshes didn't change keep their instances where they wrote them (<see cref="Keep"/>): gathering every mesh of the scene
/// every frame took more time than drawing them. Everything else is written every frame after them. Lit meshes also get the light set of
/// the object they're part of, gathered once per object and frame
/// </summary>
public sealed class InstanceBuffer(RenderContext context) : IDisposable
{
    // Has to match LightSet in ModelLayout.vert: the ambient color, then a direction and a color per slot
    private const int FloatsPerLightSet = 4 * (1 + 2 * LightSet.Slots);
    private const uint Binding = 0;
    private const uint LightSetBinding = 5;
    public const int NoLightSet = -1;

    /// <summary>
    /// How an instance turns to the camera (<c>MainPass.vert</c>): not, like the game turns a placed model with a billboard's shader, or
    /// facing the view (the editor's icons)
    /// </summary>
    public const int NoTurn = 0;
    public const int BillboardTurn = 1;
    public const int FacesView = 2;

    private readonly RangeAllocator _kept = new();
    private readonly StreamStorageBuffer _lightSets = new(context, LightSetBinding, FloatsPerLightSet);
    private InstanceRecord[] _records = new InstanceRecord[1024];
    private int _frameCount;
    private uint _handle;
    private nuint _capacityBytes;
    // Tells this frame's light sets of the anchors from last frame's
    private int _frame;

    public int Count => _kept.End + _frameCount;

    // InstanceData of ModelLayout.vert, written whole: a float at a time took most of the time gathering the instances
    [StructLayout(LayoutKind.Sequential)]
    private struct InstanceRecord
    {
        public mat4 Model;
        public vec4 Color;
        public int LightSet;
        public int Turn;
        public int Unused1;
        public int Unused2;
    }

    // LightSet of ModelLayout.vert
    [StructLayout(LayoutKind.Sequential)]
    private struct LightSetRecord
    {
        public vec4 Ambient;
        public vec4 Direction0;
        public vec4 Color0;
        public vec4 Direction1;
        public vec4 Color1;
        public vec4 Direction2;
        public vec4 Color2;
    }

    /// <summary>
    /// Starts the frame's instances, the kept ones stay
    /// </summary>
    public void Clear()
    {
        _frameCount = 0;
        _lightSets.Clear();
        _frame++;
    }

    /// <summary>
    /// Instances kept from frame to frame, written again with <see cref="Write"/> when they change. Taken before the frame's are added
    /// </summary>
    public int Keep(int count)
    {
        var first = _kept.Take(count);
        EnsureCapacity(_kept.End);
        return first;
    }

    public void Release(int first, int count)
    {
        _kept.Give(first, count);
    }

    public void Write(int index, in mat4 model, in vec4 color, int lightSet = NoLightSet, int turn = NoTurn)
    {
        _records[index] = new InstanceRecord { Model = model, Color = color, LightSet = lightSet, Turn = turn };
    }

    /// <summary>
    /// An instance of this frame only, after the kept ones
    /// </summary>
    public int Add(in mat4 model, in vec4 color, int lightSet = NoLightSet, int turn = NoTurn)
    {
        var index = _kept.End + _frameCount;
        EnsureCapacity(index + 1);
        _records[index] = new InstanceRecord { Model = model, Color = color, LightSet = lightSet, Turn = turn };
        _frameCount++;
        return index;
    }

    private void EnsureCapacity(int count)
    {
        if (count > _records.Length)
        {
            Array.Resize(ref _records, Math.Max(count, _records.Length * 2));
        }
    }

    /// <summary>
    /// The light set the renderable is lit with this frame: its object's, gathered from the scene's lights where the object is
    /// </summary>
    public int LightSetOf(Renderable renderable)
    {
        var anchor = LightAnchor.Of(renderable);
        ref var lighting = ref anchor.AnchorLight;
        if (lighting.Frame == _frame && lighting.Buffer == this)
        {
            return lighting.Index;
        }

        // The lights at the anchor only change when the anchor moves or the lights change
        var position = anchor.RenderTransform.Column3.xyz;
        var lights = context.Lights;
        if (lighting.Lights != lights || lighting.Position != position)
        {
            lighting.Set = lights.At(position);
            lighting.Lights = lights;
            lighting.Position = position;
        }

        var set = lighting.Set;
        var data = _lightSets.Allocate(out var index);
        MemoryMarshal.Write(MemoryMarshal.AsBytes(data), new LightSetRecord
        {
            Ambient = new vec4(set.Ambient, 0.0f),
            Direction0 = new vec4(set.Direction0, 0.0f),
            Color0 = new vec4(set.Color0, 0.0f),
            Direction1 = new vec4(set.Direction1, 0.0f),
            Color1 = new vec4(set.Color1, 0.0f),
            Direction2 = new vec4(set.Direction2, 0.0f),
            Color2 = new vec4(set.Color2, 0.0f),
        });
        lighting.Frame = _frame;
        lighting.Buffer = this;
        lighting.Index = index;
        return index;
    }

    public unsafe void Upload()
    {
        var gl = context.Gl;
        if (_handle == 0)
        {
            _handle = gl.CreateBuffer();
        }

        var bytes = (nuint)(Math.Max(Count, 1) * sizeof(InstanceRecord));
        if (bytes > _capacityBytes)
        {
            _capacityBytes = Math.Max(bytes, _capacityBytes * 2);
        }

        // Respecifying the storage lets the driver hand out a new one instead of waiting for last frame's draws to finish reading it
        fixed (InstanceRecord* data = _records)
        {
            gl.NamedBufferData(_handle, _capacityBytes, null, VertexBufferObjectUsage.StreamDraw);
            gl.NamedBufferSubData(_handle, 0, bytes, data);
        }

        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, Binding, _handle);
        _lightSets.Upload();
    }

    public void Dispose()
    {
        if (_handle != 0)
        {
            context.Gl.DeleteBuffer(_handle);
            _handle = 0;
        }

        _lightSets.Dispose();
    }
}
