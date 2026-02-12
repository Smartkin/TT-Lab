using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.AssetData.Graphics;

[ReferencesAssets]
public class LodModelData : AbstractAssetData
{
    [System.Text.Json.Serialization.JsonConstructor]
    private LodModelData() : base(null) { Type = LodType.COMPRESSED; }
    
    public LodModelData(IAsset asset) : base(asset)
    {
        Type = LodType.COMPRESSED;
        ModelsDrawDistances = new Int32[3];
        Meshes = new List<LabURI>();
        MinDrawDistance = 0;
        MaxDrawDistance = UInt16.MaxValue;
    }

    public LodModelData(IAsset asset, ITwinLOD lod) : this(asset)
    {
        SetTwinItem(lod);
    }

    [JsonProperty(Required = Required.Always)]
    [System.Text.Json.Serialization.JsonIgnore]
    public LodType Type { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Int32 MinDrawDistance { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Int32 MaxDrawDistance { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Int32[] ModelsDrawDistances { get; set; }
    [JsonProperty(Required = Required.Always)]
    [System.Text.Json.Serialization.JsonIgnore]
    public List<LabURI> Meshes { get; set; }

    public override String GetStringified()
    {
        var result = new StringBuilder();
        result.AppendLine(MinDrawDistance.ToString());
        result.AppendLine(MaxDrawDistance.ToString());
        foreach (var modelsDrawDistance in ModelsDrawDistances)
        {
            result.AppendLine(modelsDrawDistance.ToString());
        }
        foreach (var mesh in Meshes)
        {
            result.AppendLine(AssetManager.Get().GetAsset(mesh).GetDataHash().ToString());
        }
        return result.ToString();
    }

    protected override void Dispose(Boolean disposing)
    {
        Meshes.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var lod = GetTwinItem<ITwinLOD>();
        Type = lod.Type;
        MinDrawDistance = lod.MinDrawDistance;
        MaxDrawDistance = lod.MaxDrawDistance;
        ModelsDrawDistances = CloneUtils.CloneArray(lod.ModelsDrawDistances);
        Meshes = new List<LabURI>();
        foreach (var mesh in lod.Meshes)
        {
            Meshes.Add(AssetManager.Get().GetUriByTwinId<Mesh>(Owner, mesh));
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((Int32)Type);
        switch (Type)
        {
            case LodType.FULL:
                writer.Write(Meshes.Count & 0xFF);
                writer.Write(MinDrawDistance);
                writer.Write(MaxDrawDistance);
                foreach (var dist in ModelsDrawDistances)
                {
                    writer.Write(dist);
                }
                writer.Write(0); // Unused value
                foreach (var mesh in Meshes)
                {
                    writer.Write(assetManager.GetAsset(mesh).ExportTwinID);
                }
                break;
            case LodType.COMPRESSED:
                writer.Write((Byte)Meshes.Count);
                writer.Write(MinDrawDistance);
                writer.Write(MaxDrawDistance);
                foreach (var dist in ModelsDrawDistances)
                {
                    writer.Write(dist);
                }
                foreach (var mesh in Meshes)
                {
                    writer.Write(assetManager.GetAsset(mesh).ExportTwinID);
                }
                break;
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateLOD(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetParent();
        var meshesSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);
        foreach (var mesh in Meshes)
        {
            assetManager.GetAsset(mesh).ResolveChunkResources(factory, meshesSection);
        }

        return base.ResolveChunkResources(factory, section, id, layoutID);
    }
}