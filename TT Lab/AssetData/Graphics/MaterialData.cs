using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.AssetData.Graphics;

[ReferencesAssets]
public class MaterialData : AbstractAssetData
{
    public MaterialData(IAsset asset) : base(asset)
    {
        Shaders = [new LabShader()];
        Name = "NewMaterial";
    }

    public MaterialData(IAsset asset, ITwinMaterial material) : this(asset)
    {
        SetTwinItem(material);
    }

    public static MaterialData GetEmptyMaterial()
    {
        var material = new MaterialData(null);
        material.Shaders[0].TxtMapping = TwinShader.TextureMapping.ON;
        material.Shaders[0].ShaderType = TwinShader.Type.StandardLit;
        material.Shaders[0].TextureId = LabURI.BoatGuy;
        return material;
    }

    private class MaterialJsonData
    {
        public UInt32 DmaChainIndex { get; set; }
        public string Name { get; set; }
    }

    public JsonNode GetJsonFormat()
    {
        var jsonData = new MaterialJsonData
        {
            Name = Name,
            DmaChainIndex = DmaChainIndex,
        };
        
        return System.Text.Json.JsonSerializer.SerializeToNode(jsonData);
    }

    [JsonProperty(Required = Required.Always)]
    public AppliedShaders ActivatedShaders { get; set; }
    [JsonProperty(Required = Required.Always)]
    public UInt32 DmaChainIndex { get; set; }
    [JsonProperty(Required = Required.Always)]
    public String Name { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<LabShader> Shaders { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Shaders.Clear();
    }

    public static MaterialData LoadFromGltf(IAsset owner, SharpGLTF.Schema2.Node materialNode, List<SharpGLTF.Schema2.Material> gltfMaterials)
    {
        var materialData = new MaterialData(owner);
        var materialInfo = materialNode.Extras.Deserialize<MaterialJsonData>();
        materialData.DmaChainIndex = materialInfo!.DmaChainIndex;
        materialData.Name = materialInfo.Name;

        var assetManager = AssetManager.Get();
        materialData.Shaders = [];
        materialData.ActivatedShaders = 0;
        foreach (var gltfMaterial in gltfMaterials)
        {
            var shader = LabShader.GetShaderFromGltf(gltfMaterial);
            Debug.Assert(shader != null, "Shader must not be null!");
            var gltfTexture = gltfMaterial.FindChannel(nameof(SharpGLTF.Materials.KnownChannel.BaseColor))?.Texture;
            if (gltfTexture != null)
            {
                var texture = new Texture
                {
                    Package = owner.Package,
                    InvariantName = $"Texture_{owner.Name}",
                    Alias = $"Texture_{owner.Name}",
                    IsInternal = true
                };
                
                var textureData = TextureData.LoadFromGltf(texture, gltfTexture);
                texture.SetData(textureData);
                
                assetManager.TryAddAsset(texture);

                shader.TextureId = texture.URI;
            }
            
            materialData.ActivatedShaders |= Enum.Parse<AppliedShaders>(shader.ShaderType.ToString());
            materialData.Shaders.Add(shader);
        }
        
        return materialData;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var material = GetTwinItem<ITwinMaterial>();
        ActivatedShaders = material.ActivatedShaders;
        DmaChainIndex = material.DmaChainIndex;
        Name = new string(material.Name.ToCharArray());
        Shaders = [];
        foreach (var shader in material.Shaders)
        {
            Shaders.Add(new LabShader(Owner, shader));
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((UInt64)ActivatedShaders);
        writer.Write(DmaChainIndex);
        writer.Write(Name.Length);
        writer.Write(Name.ToCharArray());
        writer.Write(Shaders.Count);
        foreach (var shader in Shaders)
        {
            shader.Write(writer);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateMaterial(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetParent();
        var texturesSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_TEXTURES_SECTION);
        foreach (var shader in Shaders.Where(shader => shader.TextureId != LabURI.Empty))
        {
            assetManager.GetAsset(shader.TextureId).ResolveChunkResources(factory, texturesSection);
        }
        
        return base.ResolveChunkResources(factory, section, id, layoutID);
    }
}