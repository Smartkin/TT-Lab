using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Rendering.Buffers;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering;

public class MeshBuilder(RenderContext renderContext)
{
    // A rigid vertex's floats: its position, color, UV, normal, emit color, joint indexes and joint weights
    public const int RigidVertexFloats = 22;

    // A dynamic build keeps its vertexes' floats, a corner after another, to change and upload again. The others are kept in the context's
    // VertexPool, but for the ones of their own buffer: what gets replaced as a whole (collision)
    public ModelBufferBuild BuildRigidVaoFromVertexes(List<Vertex> vertexes, List<IndexedFace> faces, Func<int, Vector4>? colorSelector = null, bool dynamic = false,
        bool ownBuffer = false)
    {
        var indices = new uint[faces.Count * 3];
        var rawData = new List<float>();
        
        var idx = 0;
        foreach (var _ in from face in faces from faceIndex in face.Indexes! select vertexes[faceIndex])
        {
            indices[idx] = (uint)idx;
            idx++;
        }
        
        var hasNormals = vertexes.Any(v => v.HasNormals);
        var hasEmits = vertexes.Any(v => v.HasEmitColor);

        var normals = hasNormals ? null : GenerateNormals(vertexes, faces, indices);
        idx = 0;
        foreach (var vertex in from face in faces from faceIndex in face.Indexes! select vertexes[faceIndex])
        {
            rawData.Add(vertex.Position.X);
            rawData.Add(vertex.Position.Y);
            rawData.Add(vertex.Position.Z);
            var color = colorSelector != null ? colorSelector(idx / 3) : vertex.Color;
            if (hasNormals && colorSelector != null)
            {
                color.W = 1.0f;
                idx++;
            }
            rawData.Add(color.X);
            rawData.Add(color.Y);
            rawData.Add(color.Z);
            rawData.Add(color.W);
            rawData.Add(vertex.UV.X);
            rawData.Add(vertex.UV.Y);
            
            if (hasNormals)
            {
                rawData.Add(vertex.Normal.X);
                rawData.Add(vertex.Normal.Y);
                rawData.Add(vertex.Normal.Z);
            }
            else
            {
                rawData.AddRange(normals![idx++]);
            }

            if (hasEmits)
            {
                rawData.Add(vertex.EmitColor.X);
                rawData.Add(vertex.EmitColor.Y);
                rawData.Add(vertex.EmitColor.Z);
                rawData.Add(vertex.EmitColor.W);
            }
            else
            {
                rawData.AddRange([0, 0, 0, 0]);
            }
            
            rawData.AddRange([0, 0, 0]);
            rawData.AddRange([0, 0, 0]);
        }

        var data = rawData.ToArray();
        // A dynamic build's corners move
        var flat = !dynamic && IsFlat(data);
        var hash = DataHashOf(data);
        if (!dynamic && !ownBuffer)
        {
            return Pooled(data, (uint)indices.Length) with { Flat = flat, DataHash = hash };
        }

        var vbo = new BufferObject<float>(renderContext, data, BufferTargetARB.ArrayBuffer, dynamic ? BufferUsageARB.DynamicDraw : BufferUsageARB.StaticDraw);
        // A dynamic buffer object leaves its data to be given
        if (dynamic)
        {
            vbo.BufferData(data);
        }

        return new ModelBufferBuild(CreateVertexArray(vbo), (uint)indices.Length)
        {
            VertexData = dynamic ? data : null, VertexBuffer = dynamic ? vbo : null, OwnedBuffers = [vbo], Flat = flat, DataHash = hash,
        };
    }

    // Whether every corner of a rigid build's vertexes lies in the plane of its first triangle that has one, within a ten thousandth of its
    // size: no two triangles of such a part lie over each other on screen
    internal static bool IsFlat(float[] data)
    {
        var corners = data.Length / RigidVertexFloats;
        if (corners < 6)
        {
            return true;
        }

        dvec3 Corner(int index) => new(data[index * RigidVertexFloats], data[index * RigidVertexFloats + 1], data[index * RigidVertexFloats + 2]);
        var min = Corner(0);
        var max = min;
        for (var i = 1; i < corners; i++)
        {
            min = dvec3.Min(min, Corner(i));
            max = dvec3.Max(max, Corner(i));
        }

        var size = (max - min).Length;
        for (var i = 0; i + 2 < corners; i += 3)
        {
            var origin = Corner(i);
            var normal = dvec3.Cross(Corner(i + 1) - origin, Corner(i + 2) - origin);
            if (normal.Length <= size * size * 1e-8)
            {
                continue;
            }

            normal = normal.Normalized;
            for (var corner = 0; corner < corners; corner++)
            {
                if (Math.Abs(dvec3.Dot(Corner(corner) - origin, normal)) > size * 1e-4)
                {
                    return false;
                }
            }

            return true;
        }

        // Every triangle is a line or a point
        return true;
    }

    private static int DataHashOf(float[] data)
    {
        var hash = new HashCode();
        hash.AddBytes(MemoryMarshal.AsBytes(data.AsSpan()));
        return hash.ToHashCode();
    }

    // Blend skins keep a buffer of their own: their shapes' offsets are found by the vertex's index in it (gl_VertexID)
    public ModelBufferBuild BuildSkinnedVaoFromVertexes(List<Vertex> vertexes, List<IndexedFace> faces, bool ownBuffer = false)
    {

        var indices = new uint[faces.Count * 3];
        var rawData = new List<float>();
        var idx = 0;
        foreach (var _ in from face in faces from faceIndex in face.Indexes! select vertexes[faceIndex])
        {
            indices[idx] = (uint)idx;
            idx++;
        }

        var normals = GenerateNormals(vertexes, faces, indices);

        idx = 0;
        foreach (var vertex in from face in faces from faceIndex in face.Indexes! select vertexes[faceIndex])
        {
            rawData.Add(vertex.Position.X);
            rawData.Add(vertex.Position.Y);
            rawData.Add(vertex.Position.Z);
            rawData.Add(vertex.Color.X);
            rawData.Add(vertex.Color.Y);
            rawData.Add(vertex.Color.Z);
            rawData.Add(vertex.Color.W);
            rawData.Add(vertex.UV.X);
            rawData.Add(vertex.UV.Y);
            rawData.AddRange(normals[idx++]);
            rawData.AddRange([0, 0, 0, 0]);
            rawData.Add(vertex.JointInfo.JointIndex1);
            rawData.Add(vertex.JointInfo.JointIndex2);
            rawData.Add(vertex.JointInfo.JointIndex3);
            rawData.Add(vertex.JointInfo.Weight1);
            rawData.Add(vertex.JointInfo.Weight2);
            rawData.Add(vertex.JointInfo.Weight3);
        }

        var data = rawData.ToArray();
        if (!ownBuffer)
        {
            return Pooled(data, (uint)indices.Length);
        }

        var vbo = new BufferObject<float>(renderContext, data, BufferTargetARB.ArrayBuffer);
        return new ModelBufferBuild(CreateVertexArray(vbo), (uint)indices.Length) { OwnedBuffers = [vbo] };
    }

    private ModelBufferBuild Pooled(float[] data, uint corners)
    {
        var pool = renderContext.VertexPool;
        return new ModelBufferBuild(null, corners) { Pool = pool, FirstVertex = pool.Add(data) };
    }

    // The attributes the pool's vertex array has too (VertexPool)
    private VertexArrayObject<float, uint> CreateVertexArray(BufferObject<float> vbo)
    {
        var vao = new VertexArrayObject<float, uint>(renderContext, vbo, null);
        const uint vertexSize = RigidVertexFloats;
        // Position
        vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, vertexSize, 0);
        // Color
        vao.VertexAttributePointer(1, 4, VertexAttribPointerType.Float, vertexSize, 3);
        // UV
        vao.VertexAttributePointer(2, 2, VertexAttribPointerType.Float, vertexSize, 7);
        // Normals
        vao.VertexAttributePointer(3, 3, VertexAttribPointerType.Float, vertexSize, 9);
        // Emits
        vao.VertexAttributePointer(4, 4, VertexAttribPointerType.Float, vertexSize, 12);
        // Joint matrix index
        vao.VertexAttributePointer(5,  3, VertexAttribPointerType.Float, vertexSize, 16);
        // Joint weight
        vao.VertexAttributePointer(6, 3, VertexAttribPointerType.Float, vertexSize, 19);
        return vao;
    }

    private static vec3[] GenerateNormals(List<Vertex> vertexes, List<IndexedFace> faces, uint[] indices)
    {
        var vertices = new List<vec3>(vertexes.Count);
        vertices.AddRange(from vertex in from face in faces from faceIndex in face.Indexes! select vertexes[faceIndex] select new vec3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z));

        var normals = new List<vec3>();
        for (var i = 0; i < indices.Length; ++i)
        {
            normals.Add(vec3.Zero);
        }
        
        for (var i = 0; i < indices.Length; i += 3)
        {
            var vec1 = vertices[(int)indices[i]];
            var vec2 = vertices[(int)indices[i + 1]];
            var vec3 = vertices[(int)indices[i + 2]];

            normals[(int)indices[i]] = vec3.Cross(vec2 - vec1, vec3 - vec1);
            normals[(int)indices[i + 1]] = vec3.Cross(vec2 - vec1, vec3 - vec1);
            normals[(int)indices[i + 2]] = vec3.Cross(vec2 - vec1, vec3 - vec1);
        }

        for (var i = 0; i < normals.Count; ++i)
        {
            normals[i] = normals[i].Normalized;
        }

        return normals.ToArray();
    }
}

public record ModelBufferBuild(VertexArrayObject<float, uint>? Vao, uint IndicesAmount)
{
    public float[]? VertexData { get; init; }
    public BufferObject<float>? VertexBuffer { get; init; }

    /// <summary>
    /// Whether the corners lie in one plane (<see cref="MeshBuilder.IsFlat"/>), a rigid build's that doesn't move
    /// </summary>
    public bool Flat { get; init; }

    /// <summary>
    /// A hash of a rigid build's vertexes, which tells a model's parts from another's
    /// </summary>
    public int DataHash { get; init; }

    /// <summary>
    /// The buffers the vertex array draws from, freed with it
    /// </summary>
    public IReadOnlyList<IDisposable> OwnedBuffers { get; init; } = [];

    /// <summary>
    /// The pool the vertexes are in when the build has no vertex array of its own
    /// </summary>
    public VertexPool? Pool { get; init; }

    /// <summary>
    /// Where the build's vertexes start in what it's drawn from, the first vertex of its draws
    /// </summary>
    public int FirstVertex { get; init; }

    public uint VertexArray => Vao?.Handle ?? Pool!.VertexArray;

    /// <summary>
    /// Frees the vertex array and its buffers, on the render thread
    /// </summary>
    public void Release()
    {
        Vao?.Dispose();
        foreach (var buffer in OwnedBuffers)
        {
            buffer.Dispose();
        }

        Pool?.Remove(FirstVertex, (int)IndicesAmount);
    }
}
public record BlendSkinModelBufferBuild(ModelBufferBuild Model, BlendSkinShapeBuild ShapeBuild, vec3 BlendShape);
public record BlendSkinShapeBuild(int[] ShapesOffsets, int ShapeStart);