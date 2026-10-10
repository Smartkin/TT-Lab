using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetData.Graphics;

/// <summary>
/// A skin whose vertexes also move by shapes the facial animations blend together
/// </summary>
[ReferencesAssets]
public class BlendSkinData : AbstractAssetData
{
    public const string TlmAssetType = "BlendSkin";

    public BlendSkinData(IAsset asset) : base(asset)
    {
    }

    public BlendSkinData(IAsset asset, ITwinBlendSkin blendSkin) : this(asset)
    {
        SetTwinItem(blendSkin);
    }

    /// <summary>
    /// Amount of shapes every part has, the shapes of the parts read from the model file
    /// </summary>
    public Int32 BlendsAmount { get; set; }
    public List<SubBlendData> Blends { get; set; } = [];

    protected override void Dispose(Boolean disposing)
    {
        foreach (var blend in Blends)
        {
            blend.Dispose();
        }
        Blends.Clear();
    }

    public override String GetStringified()
    {
        return Convert.ToHexString(SHA256.HashData(WriteParts(SkinData.WriteMaterial)));
    }

    // The materials by their IDs: their data hashes are worked out of the file or the loaded data, whichever is there first
    public override Byte[] GetFingerprint()
    {
        return WriteParts((writer, material) => writer.Write(RigidModelData.MaterialId(material)));
    }

    private Byte[] WriteParts(Action<BinaryWriter, LabURI> writeMaterial)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(BlendsAmount);
        foreach (var blend in Blends)
        {
            writeMaterial(writer, blend.Material);
            ModelData.WritePart(writer, blend.Vertexes, blend.Faces, blend.Layout);
            SkinData.WriteCompression(writer, blend.Compression);
            foreach (var offset in blend.ShapeOffsets.SelectMany(s => s))
            {
                writer.Write(offset.X);
                writer.Write(offset.Y);
                writer.Write(offset.Z);
            }

            foreach (var shape in blend.Layout?.Batches.Select(b => b.BlendShape) ?? [])
            {
                writer.Write(shape?.X ?? 0);
                writer.Write(shape?.Y ?? 0);
                writer.Write(shape?.Z ?? 0);
            }
        }

        writer.Flush();
        return stream.ToArray();
    }

    public const string TlmKind = "shape";

    /// <summary>
    /// Every part as a part of one mesh, all of them with every shape
    /// </summary>
    public JsonObject WriteTlmMesh(TlmFile file, TlmMaterials materials)
    {
        return TlmMeshes.WriteMesh(file, Blends.Select(blend =>
        {
            var part = blend.ToModelPart();
            part.ShapeOffsets = [..part.ShapeOffsets];
            while (part.ShapeOffsets.Count < BlendsAmount)
            {
                part.ShapeOffsets.Add(blend.Vertexes.Select(_ => new Twinsanity.TwinsanityInterchange.Common.Vector4()).ToList());
            }

            return (part, materials.Use(blend.Material));
        }), true);
    }

    public void ReadTlmMesh(TlmFile file, JsonObject? mesh, TlmMaterials materials)
    {
        foreach (var (part, material) in TlmMeshes.ReadMesh(file, mesh, true))
        {
            Blends.Add(new SubBlendData(materials.GetRequired(material, TlmMaterialUse.Skin), part));
        }

        BlendsAmount = Math.Max(BlendsAmount, Blends.Select(b => b.ShapeOffsets.Count).DefaultIfEmpty(0).Max());
    }

    protected override void SaveInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        var file = new TlmFile(TlmAssetType, Owner.Name);
        var root = TlmNodes.Create(TlmKind, Owner.Name);
        root[TlmNodes.MeshKey] = WriteTlmMesh(file, new TlmMaterials(file));
        file.Root = root;
        file.Save(dataPath);
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        var file = TlmFile.Load(dataPath);
        var materials = new TlmMaterials(file, Owner);
        Blends = [];
        BlendsAmount = 0;
        if (file.Root != null)
        {
            ReadTlmMesh(file, file.Root[TlmNodes.MeshKey] as JsonObject, materials);
        }

        DisposedValue = false;
        if (materials.AddedToProject)
        {
            SaveInternal(dataPath, settings);
        }
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var blendSkin = GetTwinItem<ITwinBlendSkin>();
        BlendsAmount = blendSkin.BlendsAmount;
        foreach (var blend in blendSkin.SubBlends)
        {
            Blends.Add(new SubBlendData(Owner, blend, BlendsAmount));
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        TlmMaterials.CheckDrawsSkins(Owner, Blends.Select(b => b.Material));
        var assetManager = AssetManager.Get();
        return factory.GenerateBlendSkin(BlendsAmount, Blends.Select(b => new BlendPartExport(assetManager.GetAsset(b.Material).ExportTwinID, b.Vertexes, b.ShapeOffsets,
            StripParts.GetValidLayout(b.Layout, b.Vertexes, b.Faces, StripParts.SkinWinding), b.Compression)).ToList());
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetParent();
        var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        foreach (var blend in Blends)
        {
            assetManager.GetAsset(blend.Material).ResolveChunkResources(factory, materialsSection);
        }
        return base.ResolveChunkResources(factory, section, id, layoutId);
    }
}
