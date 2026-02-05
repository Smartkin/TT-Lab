using System.Collections.Generic;
using TT_Lab.AssetData.Graphics.SubModels;

namespace TT_Lab.MeshProcessor;

public class Mesh
{
    List<Vertex> vertices;
    List<IndexedFace> faces;
    List<SubBlendFaceData>? blendFaces;

    public List<Meshlet> Meshlets { get; set; } = new();

    public Mesh(List<Vertex> vertices, List<IndexedFace> faces, List<SubBlendFaceData>? blendFaces = null)
    {
        this.vertices = vertices;
        this.faces = faces;
        this.blendFaces = blendFaces;
    }

    public void AddMeshlet(Meshlet meshlet)
    {
        Meshlets.Add(meshlet);
    }

    public List<Vertex> GetVertices()
    {
        return vertices;
    }

    public List<IndexedFace> GetFaces()
    {
        return faces;
    }

    public List<SubBlendFaceData>? GetBlendFaces()
    {
        return blendFaces;
    }

    public void Clear()
    {
        vertices.Clear();
        faces.Clear();
        Meshlets.Clear();
    }
}