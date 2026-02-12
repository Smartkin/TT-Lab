using System;
using System.Collections.Generic;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Libraries;

namespace TT_Lab.MeshProcessor;

public static class MeshProcessor
{
    public static Mesh CreateMesh(List<Vertex> vertices, List<IndexedFace> faces, List<SubBlendFaceData>? blendFaces = null)
    {
        return new Mesh(vertices, faces, blendFaces);
    }

    /// <summary>
    /// Builds meshlets of the mesh that would fit in a VIF packet and stripifies them
    /// </summary>
    /// <param name="mesh">Mesh to process</param>
    public static void ProcessMesh(Mesh mesh)
    {
        BuildMeshlets(mesh);
        StripifyMeshlets(mesh);
    }

    /// <summary>
    /// Creates meshlets of the mesh and stores them in it
    /// </summary>
    /// <param name="mesh"></param>
    /// <returns></returns>
    public static void BuildMeshlets(Mesh mesh)
    {
        var meshlets = MeshOptimizer.BuildMeshlets(GetMeshIndices(mesh), mesh.GetVertices(), mesh.GetBlendFaces());
        foreach (var meshlet in meshlets)
        {
            mesh.AddMeshlet(meshlet);
        }
    }

    /// <summary>
    /// Stripifies all the meshlets
    /// </summary>
    /// <param name="mesh"></param>
    /// <param name="meshlets"></param>
    public static void StripifyMeshlets(Mesh mesh)
    {
        foreach (var meshlet in mesh.Meshlets)
        {
            var strip = MeshOptimizer.Stripify(meshlet.Indices, (UInt32)meshlet.Vertexes.Count);
            meshlet.Strip = strip;
        }
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