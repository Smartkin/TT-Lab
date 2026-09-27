using System.Collections.Generic;
using TT_Lab.MeshProcessor;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Graphics.SubModels;

/// <summary>
/// A part of a model: its vertexes and triangles, the strips the game draws them with and how a skin packs them
/// </summary>
public class ModelPart
{
    public List<Vertex> Vertexes { get; set; } = [];
    public List<IndexedFace> Faces { get; set; } = [];
    /// <summary>
    /// The strips the part was packed into, null when they didn't match the triangles anymore
    /// </summary>
    public StripLayout? Layout { get; set; }
    public TwinSkinCompression? Compression { get; set; }
    /// <summary>
    /// Offset of every vertex for every shape of a blend skin
    /// </summary>
    public List<List<Vector4>> ShapeOffsets { get; set; } = [];
}
