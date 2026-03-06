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
    public static void ProcessMesh(Mesh mesh)
    {
        StripifyMesh(mesh);
        BuildMeshletsForStrip(mesh);
    }

    /// <summary>
    /// Stripifies the entire mesh and puts the strip in a single meshlet. Removes all the previously created meshlets
    /// </summary>
    /// <param name="mesh"></param>
    public static void StripifyMesh(Mesh mesh)
    {
        mesh.Meshlets.Clear();
        var strip = MeshOptimizer.Stripify(GetMeshIndices(mesh), (UInt32)mesh.GetVertices().Count);
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
            currentIdxs.Add(idx);

            if (i < finalIdx && currentIdxs.Count < TwinVIFCompiler.VertexStripCache)
            {
                continue;
            }
            
            var meshlet = new Meshlet
            {
                Strip = currentIdxs.ToList(),
                Vertexes = mesh.GetVertices(),
                BlendFaces = mesh.GetBlendFaces(),
                Indices = meshIndices.ToList()
            };
            resultingMeshlets.Add(meshlet);

            var v1 = currentIdxs[^2];
            var v2 = currentIdxs[^1];
            
            var winding = ((currentIdxs.Count - 2) % 2) == 1;
            currentIdxs.Clear();
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
            indices.Add((UInt32)face.Indexes![0]);
            indices.Add((UInt32)face.Indexes[1]);
            indices.Add((UInt32)face.Indexes[2]);
        }

        return indices;
    }
}