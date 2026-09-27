using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.MeshProcessor;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.AssetData.Graphics.SubModels;

/// <summary>
/// The part of a blend skin drawn with one material. The game splits it into models of one batch each, every one of them packing
/// the offsets of the shapes with its own scale
/// </summary>
[ReferencesAssets]
public class SubBlendData : IDisposable
{
    public LabURI Material { get; set; }
    public List<Vertex> Vertexes { get; set; } = [];
    public List<IndexedFace> Faces { get; set; } = [];
    /// <summary>
    /// Offset of every vertex for every shape
    /// </summary>
    public List<List<Vector4>> ShapeOffsets { get; set; } = [];
    /// <summary>
    /// The strips of the part, every batch being one of the game's models
    /// </summary>
    public StripLayout? Layout { get; set; }
    public TwinSkinCompression? Compression { get; set; }

    public SubBlendData(IAsset owner, ITwinSubBlendSkin blend, Int32 blendsAmount)
    {
        Material = AssetManager.Get().GetUriByTwinId<Assets.Graphics.Material>(owner, blend.Material);
        if (Material == LabURI.Empty)
        {
            throw new Exception($"Couldn't find requested material 0x{blend.Material:X}!");
        }

        var part = StripParts.FromBlend(blend.Models, blendsAmount, out var shapeOffsets);
        Vertexes = part.Vertexes;
        Faces = part.Faces;
        Layout = part.Layout;
        ShapeOffsets = shapeOffsets;
        Compression = blend.Models.FirstOrDefault()?.Compression;
    }

    public SubBlendData(LabURI material, ModelPart part)
    {
        Material = material;
        Vertexes = part.Vertexes;
        Faces = part.Faces;
        Layout = part.Layout;
        ShapeOffsets = part.ShapeOffsets;
        Compression = part.Compression;
    }

    public ModelPart ToModelPart()
    {
        return new ModelPart
        {
            Vertexes = Vertexes,
            Faces = Faces,
            Layout = Layout,
            Compression = Compression,
            ShapeOffsets = ShapeOffsets
        };
    }

    public void Dispose()
    {
        Vertexes.Clear();
        Faces.Clear();
        ShapeOffsets.Clear();

        GC.SuppressFinalize(this);
    }
}
