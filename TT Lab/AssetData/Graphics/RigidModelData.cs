using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetData.Graphics;

[ReferencesAssets]
public class RigidModelData : AbstractAssetData
{
    public RigidModelData(IAsset asset) : base(asset)
    {
        Materials = new List<LabURI>();
        Model = LabURI.Empty;
    }

    public RigidModelData(IAsset asset, ITwinRigidModel rigidModel) : this(asset)
    {
        SetTwinItem(rigidModel);
    }

    [JsonProperty(Required = Required.Always)]
    public List<LabURI> Materials { get; set; }
    [JsonProperty(Required = Required.Always)]
    public LabURI Model { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Materials.Clear();
    }

    public override String GetStringified()
    {
        var assetManager = AssetManager.Get();
        var result = new StringBuilder();
        result.AppendLine(assetManager.GetAsset(Model).GetDataHash().ToString());
        foreach (var mat in Materials)
        {
            result.AppendLine(assetManager.GetAsset(mat).GetDataHash().ToString());
        }
        
        return result.ToString();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        ITwinRigidModel rigidModel = GetTwinItem<ITwinRigidModel>();
        Materials = new List<LabURI>();
        foreach (var mat in rigidModel.Materials)
        {
            Materials.Add(AssetManager.Get().GetUriByTwinId<Material>(Owner, mat));
        }
        Model = AssetManager.Get().GetUriByTwinId<Model>(Owner, rigidModel.Model);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(257); // Unused header
        writer.Write(Materials.Count);
        foreach (var mat in Materials)
        {
            writer.Write(assetManager.GetAsset(mat).ExportTwinID);
        }
        writer.Write(assetManager.GetAsset(Model).ExportTwinID);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateRigidModel(ms);
    }

    protected virtual void ResolveResources(ITwinItemFactory factory, ITwinSection section)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetRoot().GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        var modelsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MODELS_SECTION);

        foreach (var material in Materials)
        {
            assetManager.GetAsset(material).ResolveChunkResources(factory, materialsSection);
        }

        assetManager.GetAsset(Model).ResolveChunkResources(factory, modelsSection);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
    {
        ResolveResources(factory, section);
        return base.ResolveChunkResources(factory, section, id, layoutID);
    }
}