using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Rendering.Buffers;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering;

public class MeshBuilder(RenderContext renderContext)
{
    public ModelBufferBuild BuildRigidVaoFromVertexes(List<Vertex> vertexes, List<IndexedFace> faces, Func<int, Vector4>? colorSelector = null)
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

        var ebo = new BufferObject<uint>(renderContext, indices, BufferTargetARB.ElementArrayBuffer);
        var vbo = new BufferObject<float>(renderContext, rawData.ToArray(), BufferTargetARB.ArrayBuffer);
        var vao = new VertexArrayObject<float, uint>(renderContext, vbo, ebo);
        const uint vertexSize = 22U;
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
        
        return new ModelBufferBuild(vao, (uint)indices.Length);
    }

    public ModelBufferBuild BuildSkinnedVaoFromVertexes(List<Vertex> vertexes, List<IndexedFace> faces)
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

        var ebo = new BufferObject<uint>(renderContext, indices, BufferTargetARB.ElementArrayBuffer);
        var vbo = new BufferObject<float>(renderContext, rawData.ToArray(), BufferTargetARB.ArrayBuffer);
        var vao = new VertexArrayObject<float, uint>(renderContext, vbo, ebo);
        
        const uint vertexSize = 22U;
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
        
        return new ModelBufferBuild(vao, (uint)indices.Length);
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

public record ModelBufferBuild(VertexArrayObject<float, uint> Vao, uint IndicesAmount);
public record BlendSkinModelBufferBuild(ModelBufferBuild Model, BlendSkinShapeBuild ShapeBuild, vec3 BlendShape);
public record BlendSkinShapeBuild(int[] ShapesOffsets, int ShapeStart);