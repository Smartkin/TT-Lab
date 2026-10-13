using System;
using System.Collections.Generic;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.UniformDescs;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.ShaderAnimation;

namespace TT_Lab.Rendering.Materials;

/// <summary>
/// The materials the Generic program draws with (<c>MainPass.vert</c>/<c>.frag</c>'s TwinMaterial) in a storage buffer the shaders index:
/// set as uniforms they took 17 calls for every batch drawn, which made the draws cost three times as much. An entry is found by what it
/// holds, so the batches of a material share one, and the entries of scrolling and animated shaders get their UV offset of the frame's
/// time before the frame is drawn. On the render thread
/// </summary>
public sealed class MaterialTable(RenderContext context) : IDisposable
{
    // Has to match TwinMaterialEntry in TwinMaterial.glsl: six vec4s
    public const int FloatsPerEntry = 24;
    private const uint Binding = 6;
    // Editing a material makes an entry of every value it goes through: the table starts over past this many, before the next frame
    internal const int MostEntries = 16384;

    private readonly Dictionary<Key, int> _entries = [];
    private readonly List<(int Index, Key Key)> _moving = [];
    private float[] _data = new float[FloatsPerEntry * 64];
    // Entry 0 is all zeros, what the uniforms held before any material set them
    private int _count = 1;
    private bool _changed = true;
    private double _time;
    private uint _handle;
    private nuint _capacityBytes;

    /// <summary>
    /// Changes when the table starts over: the materials holding an entry of an older one look theirs up again
    /// </summary>
    public int Generation { get; private set; }

    public int Count => _count;

    /// <summary>
    /// The entry of a material, made the first time a material of its values is drawn
    /// </summary>
    public int IndexOf(in TwinMaterialDesc material)
    {
        var key = Key.Of(material);
        if (_entries.TryGetValue(key, out var index))
        {
            return index;
        }

        index = _count++;
        if (_count * FloatsPerEntry > _data.Length)
        {
            Array.Resize(ref _data, _data.Length * 2);
        }

        key.Write(_data.AsSpan(index * FloatsPerEntry, FloatsPerEntry), key.Moves ? key.UvOffsetAt(_time) : vec2.Zero);
        _entries[key] = index;
        if (key.Moves)
        {
            _moving.Add((index, key));
        }

        // Materials get made while the frame is drawn, their first draw already reads the entry
        if (_handle != 0)
        {
            Upload(index);
        }
        else
        {
            _changed = true;
        }

        return index;
    }

    /// <summary>
    /// Before the frame is drawn: the scrolls and animations at its time, and the table where the shaders read it
    /// </summary>
    public void Prepare(double time)
    {
        StartOverWhenFull();
        _time = time;
        foreach (var (index, key) in _moving)
        {
            var offset = key.UvOffsetAt(time);
            var entry = _data.AsSpan(index * FloatsPerEntry, FloatsPerEntry);
            if (entry[4].Equals(offset.x) && entry[5].Equals(offset.y))
            {
                continue;
            }

            entry[4] = offset.x;
            entry[5] = offset.y;
            _changed = true;
        }

        var gl = context.Gl;
        if (_handle == 0)
        {
            _handle = gl.CreateBuffer();
        }

        if (_changed)
        {
            // A storage of its own every time it changes: the draws of the frame before may still read the old one
            Upload(-1);
            _changed = false;
        }

        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, Binding, _handle);
    }

    // The whole table into a new storage, or only an entry made since while there's room for it
    private unsafe void Upload(int index)
    {
        var gl = context.Gl;
        var bytes = (nuint)(Math.Max(_count, 1) * FloatsPerEntry * sizeof(float));
        var grows = bytes > _capacityBytes;
        if (grows)
        {
            _capacityBytes = Math.Max(bytes, _capacityBytes * 2);
        }

        fixed (float* data = _data)
        {
            if (index < 0 || grows)
            {
                gl.NamedBufferData(_handle, _capacityBytes, null, VertexBufferObjectUsage.DynamicDraw);
                gl.NamedBufferSubData(_handle, 0, bytes, data);
                return;
            }

            var entryBytes = (nuint)(FloatsPerEntry * sizeof(float));
            gl.NamedBufferSubData(_handle, index * FloatsPerEntry * sizeof(float), entryBytes, data + index * FloatsPerEntry);
        }
    }

    /// <summary>
    /// The floats of a material's entry with its UV offset at the time, what the table holds for it
    /// </summary>
    internal static float[] EntryOf(in TwinMaterialDesc material, double time)
    {
        var key = Key.Of(material);
        var entry = new float[FloatsPerEntry];
        key.Write(entry, key.Moves ? key.UvOffsetAt(time) : vec2.Zero);
        return entry;
    }

    /// <summary>
    /// Whether two materials are drawn with the same entry
    /// </summary>
    internal static bool SharesEntry(in TwinMaterialDesc material, in TwinMaterialDesc other)
    {
        return Key.Of(material) == Key.Of(other);
    }

    /// <summary>
    /// Starts the table over once it's full, only between frames: the indirect draws keep the entries they were made with for the whole frame
    /// </summary>
    internal void StartOverWhenFull()
    {
        if (_count < MostEntries)
        {
            return;
        }

        _entries.Clear();
        _moving.Clear();
        _count = 1;
        _changed = true;
        Generation++;
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

    // What the shaders read of a material, and what moves its UV offset. Textures and the GL state (blending, depth) stay the draw's
    private readonly record struct Key(
        int DeformMode, float DeformSpeed, float DeformX, float DeformY, float DeformZ,
        float ReflectX, float ReflectY,
        int AlphaMethod, int AlphaReference, bool AlphaFailsWithPassing, float AlphaBlend, float EnvMap,
        float UseTexture, bool Mipmapped, int LodL, float LodK,
        int Fog, bool EditorShading, bool Lit,
        TwinShader.XScrollFormula ScrollU, TwinShader.YScrollFormula ScrollV, float PhaseU, float PhaseV, float SpeedU, float SpeedV,
        TwinShaderAnimation? Animation)
    {
        public bool Moves => ScrollU != TwinShader.XScrollFormula.Disabled || ScrollV != TwinShader.YScrollFormula.Disabled;

        public static Key Of(in TwinMaterialDesc material)
        {
            var moves = material.ScrollU != TwinShader.XScrollFormula.Disabled || material.ScrollV != TwinShader.YScrollFormula.Disabled;
            // Only what moves the UVs tells the entries of materials that don't move apart
            var animated = moves && material.Animation != null && (material.AnimatesU || material.AnimatesV);
            return new Key(material.DeformMode, material.DeformSpeed, material.DeformAmplitude.x, material.DeformAmplitude.y, material.DeformAmplitude.z,
                material.ReflectDist.x, material.ReflectDist.y,
                material.AlphaTestMethod, material.AlphaReference, material.FailDraw == AlphaFailDraw.WithPassing, material.AlphaBlend.Equals(1.0f) ? 1.0f : 0.0f,
                material.EnvMap,
                material.UseTexture.Equals(1.0f) ? 1.0f : 0.0f, material.Mipmapped, material.LodL, material.LodK,
                material.Fog, material.EditorShading, material.Lit,
                material.ScrollU, material.ScrollV,
                moves ? material.ScrollPhases.x : 0.0f, moves ? material.ScrollPhases.y : 0.0f, moves ? material.ScrollSpeeds.x : 0.0f, moves ? material.ScrollSpeeds.y : 0.0f,
                animated ? material.Animation : null);
        }

        // The scrolls, and the shader animation's U and V tracks the scrolls take (ShaderScroll)
        public vec2 UvOffsetAt(double time)
        {
            var animation = Animation != null ? ShaderAnimationSampler.At(Animation, time).Uv : vec2.Zero;
            return ShaderScroll.At(ScrollU, ScrollV, new vec2(PhaseU, PhaseV), new vec2(SpeedU, SpeedV), time, animation);
        }

        public void Write(Span<float> entry, vec2 uvOffset)
        {
            // The deformation's amplitude per axis and speed
            entry[0] = DeformX;
            entry[1] = DeformY;
            entry[2] = DeformZ;
            entry[3] = DeformSpeed;
            entry[4] = uvOffset.x;
            entry[5] = uvOffset.y;
            entry[6] = ReflectX;
            entry[7] = ReflectY;
            // The ints, as their bits
            entry[8] = BitConverter.Int32BitsToSingle(DeformMode);
            entry[9] = BitConverter.Int32BitsToSingle(AlphaMethod);
            entry[10] = BitConverter.Int32BitsToSingle(Fog);
            entry[11] = 0.0f;
            entry[12] = AlphaReference;
            // Whether a draw of every fragment keeps the failing ones (drawn apart, they're left out of it: RenderBatch draws them on their own)
            entry[13] = AlphaFailsWithPassing ? 1.0f : 0.0f;
            entry[14] = AlphaBlend;
            entry[15] = EnvMap;
            entry[16] = UseTexture;
            entry[17] = Mipmapped ? 1.0f : 0.0f;
            entry[18] = 1 << LodL;
            entry[19] = LodK;
            // How a model turns to the camera is its instances' (InstanceBuffer), not its material's
            entry[20] = 0.0f;
            entry[21] = EditorShading ? 1.0f : 0.0f;
            entry[22] = Lit ? 1.0f : 0.0f;
            entry[23] = 0.0f;
        }
    }
}
