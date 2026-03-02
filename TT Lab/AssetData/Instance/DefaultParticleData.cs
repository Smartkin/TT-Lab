using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
public class DefaultParticleData : ParticleData
{

    public DefaultParticleData(IAsset asset) : base(asset)
    {
        UnkData = new Byte[4];
        UnkBlob = new Byte[0x420];
        UnkInts = new Int32[10];
    }

    public DefaultParticleData(IAsset asset, ITwinDefaultParticle particle) : base(asset, particle) { }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public List<LabURI> TextureIDs { get; set; } = new();
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public List<LabURI> MaterialIDs { get; set; } = new();
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public LabURI DecalTextureID { get; set; } = LabURI.Empty;
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public LabURI DecalMaterialID { get; set; } = LabURI.Empty;
    
    [JsonProperty(Required = Required.Always)]
    public Byte[] UnkData { get; private set; }
    
    [JsonProperty(Required = Required.Always)]
    public Byte[] UnkBlob { get; private set; }
    
    [JsonProperty(Required = Required.Always)]
    public Int32[] UnkInts { get; private set; }
    
    [JsonProperty(Required = Required.Always)]
    public List<Byte[]> UnkBlobs { get; private set; } = new();

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        for (Int32 i = 0; i < 3; i++)
        {
            writer.Write(assetManager.GetAsset(TextureIDs[i]).ExportTwinID);
            writer.Write(assetManager.GetAsset(MaterialIDs[i]).ExportTwinID);
        }

        WriteExport(writer);
        writer.Flush();
        ms.Position -= 4;

        writer.Write(assetManager.GetAsset(DecalTextureID).ExportTwinID);
        writer.Write(assetManager.GetAsset(DecalMaterialID).ExportTwinID);

        writer.Write(UnkData);
        writer.Write(UnkBlob);
        foreach (var @int in UnkInts)
        {
            writer.Write(@int);
        }

        foreach (var blob in UnkBlobs)
        {
            writer.Write(blob);
        }
        writer.Flush();

        ms.Position = 0;
        return factory.GenerateDefaultParticle(ms);
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        base.Import(package, variant, layoutId);

        var assetManager = AssetManager.Get();
        ITwinDefaultParticle particle = GetTwinItem<ITwinDefaultParticle>();

        foreach (var texture in particle.TextureIDs)
        {
            TextureIDs.Add(assetManager.GetUriByTwinId<Texture>(Owner, texture));
        }

        foreach (var material in particle.MaterialIDs)
        {
            MaterialIDs.Add(assetManager.GetUriByTwinId<Material>(Owner, material));
        }

        DecalTextureID = assetManager.GetUriByTwinId<Texture>(Owner, particle.DecalTextureID);
        DecalMaterialID = assetManager.GetUriByTwinId<Material>(Owner, particle.DecalMaterialID);

        UnkData = CloneUtils.CloneArray(particle.UnkData);
        UnkBlob = CloneUtils.CloneArray(particle.UnkBlob);
        UnkInts = CloneUtils.CloneArray(particle.UnkInts);

        foreach (var blob in particle.UnkBlobs)
        {
            UnkBlobs.Add(CloneUtils.CloneArray(blob));
        }
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var texturesSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_TEXTURES_SECTION);
        var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);

        foreach (var texture in TextureIDs)
        {
            assetManager.GetAsset(texture).ResolveChunkResources(factory, texturesSection);
        }

        foreach (var material in MaterialIDs)
        {
            assetManager.GetAsset(material).ResolveChunkResources(factory, materialsSection);
        }

        assetManager.GetAsset(DecalTextureID).ResolveChunkResources(factory, texturesSection);
        assetManager.GetAsset(DecalMaterialID).ResolveChunkResources(factory, materialsSection);

        return base.ResolveChunkResources(factory, section, id);
    }

    protected override void Dispose(Boolean disposing)
    {
        TextureIDs.Clear();
        MaterialIDs.Clear();
        UnkBlobs.Clear();

        base.Dispose(disposing);
    }
}