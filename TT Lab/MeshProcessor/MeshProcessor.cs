using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Libraries;
using Twinsanity.PS2Hardware;

namespace TT_Lab.MeshProcessor;

public static class MeshProcessor
{
    public static Mesh CreateMesh(List<Vertex> vertices, List<IndexedFace> faces, List<SubBlendFaceData>? blendFaces = null)
    {
        return new Mesh(vertices, faces, blendFaces);
    }

    /// <summary>
    /// Stripifies the entire mesh then splits it into substrips
    /// </summary>
    /// <param name="mesh">Mesh to process</param>
    /// <param name="useOptimalStrips">Uses a better algorithm to stripify but some meshes may look incorrectly</param>
    public static void ProcessMesh(Mesh mesh, bool useOptimalStrips = true)
    {
        StripifyMesh(mesh, useOptimalStrips);
        BuildMeshletsForStrip(mesh);
    }

    private static void StripifyMesh(Mesh mesh, bool useOptimalStrips = true)
    {
        var strip = MeshOptimizer.Stripify(GetMeshIndices(mesh), (UInt32)mesh.GetVertices().Count, useOptimalStrips);
        var meshlet = new Meshlet
        {
            Strip = strip,
            Vertexes = mesh.GetVertices(),
            BlendFaces = mesh.GetBlendFaces(),
            Indices = GetMeshIndices(mesh)
        };
        mesh.AddMeshlet(meshlet);
    }

    private static void BuildMeshletsForStrip(Mesh mesh)
    {
        var strip = mesh.Meshlets[0].Strip;
        var resultingMeshlets = new List<Meshlet>();
        var currentIdxs = new List<UInt32>();
        var meshIndices = GetMeshIndices(mesh);
        var finalIdx = strip.Count - 1;
        for (var i = 0; i < strip.Count; ++i)
        {
            var idx = strip[i];
            var needStitching = true;
            currentIdxs.Add(idx);

            var earlyFlush = idx == 0xFFFF && currentIdxs.Count >= TwinVIFCompiler.VertexStripCache - 6;
            if (i < finalIdx && currentIdxs.Count <= TwinVIFCompiler.VertexStripCache && !earlyFlush)
            {
                continue;
            }

            if (currentIdxs.Count < 3)
            {
                resultingMeshlets[^1].Strip.AddRange(currentIdxs);
                currentIdxs.Clear();
                continue;
            }

            // If we end where strip ends then no stitching is required which saves 2 vertices
            if (currentIdxs[^1] == 0xFFFF)
            {
                currentIdxs.RemoveAt(currentIdxs.Count - 1);
                needStitching = false;
            }
            
            var meshlet = new Meshlet
            {
                Strip = currentIdxs.ToList(),
                Vertexes = mesh.GetVertices(),
                BlendFaces = mesh.GetBlendFaces(),
                Indices = meshIndices.ToList()
            };
            meshlet.CalculateNormals();
            resultingMeshlets.Add(meshlet);
            
            var winding = currentIdxs.Count % 2 == 1;
            var v1 = currentIdxs[^1];
            var v2 = currentIdxs[^2];
            
            currentIdxs.Clear();

            if (!needStitching)
            {
                continue;
            }

            // TODO: Verify that Twins actually cares about winding, if not just leave adding 2 extra vertices in whatever order
            if (!winding)
            {
                currentIdxs.Add(v1);
                currentIdxs.Add(v2);
            }
            else
            {
                currentIdxs.Add(v2);
                currentIdxs.Add(v1);
            }
        }
        
        mesh.Meshlets = resultingMeshlets;
    }

    private static List<UInt32> GetMeshIndices(Mesh mesh)
    {
        List<UInt32> indices = new();
        foreach (var face in mesh.GetFaces())
        {
            indices.Add((ushort)face.Indexes![0]);
            indices.Add((ushort)face.Indexes[1]);
            indices.Add((ushort)face.Indexes[2]);
        }

        return indices;
    }
}