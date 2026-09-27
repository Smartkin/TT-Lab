using System;
using System.Collections.Generic;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.MeshProcessor;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.AssetData.Graphics.SubModels;

[ReferencesAssets]
public class SubSkinData : IDisposable
{
    public LabURI Material { get; set; }
    public List<Vertex> Vertexes { get; set; }
    public List<IndexedFace> Faces { get; set; }
    public StripLayout? Layout { get; set; }
    public TwinSkinCompression? Compression { get; set; }

    public SubSkinData(IAsset owner, ITwinSubSkin subSkin)
    {
        Material = AssetManager.Get().GetUriByTwinId<Material>(owner, subSkin.Material);
        if (Material == LabURI.Empty)
        {
            throw new Exception($"Couldn't find requested material 0x{subSkin.Material:X}!");
        }

        var part = StripParts.FromSkin(subSkin);
        Vertexes = part.Vertexes;
        Faces = part.Faces;
        Layout = part.Layout;
        Compression = subSkin.Compression;
    }

    public SubSkinData(LabURI material, ModelPart part)
    {
        Material = material;
        Vertexes = part.Vertexes;
        Faces = part.Faces;
        Layout = part.Layout;
        Compression = part.Compression;
    }

    public ModelPart ToModelPart()
    {
        return new ModelPart
        {
            Vertexes = Vertexes,
            Faces = Faces,
            Layout = Layout,
            Compression = Compression
        };
    }

    public void Dispose()
    {
        Vertexes.Clear();
        Faces.Clear();

        GC.SuppressFinalize(this);
    }
}
