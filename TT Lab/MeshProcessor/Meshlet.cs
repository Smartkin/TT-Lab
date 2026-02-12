using System;
using System.Collections.Generic;
using TT_Lab.AssetData.Graphics.SubModels;

namespace TT_Lab.MeshProcessor;

public class Meshlet
{
    public List<UInt32> Indices { get; set; } = new();
    public List<Vertex> Vertexes { get; set; } = new();
    public List<SubBlendFaceData>? BlendFaces { get; set; }
    public List<UInt32> Strip { get; set; } = new();
}