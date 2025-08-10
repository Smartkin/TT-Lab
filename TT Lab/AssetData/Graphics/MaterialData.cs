using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.AssetData.Graphics
{
    [ReferencesAssets]
    public class MaterialData : AbstractAssetData
    {
        public MaterialData()
        {
            Shaders = [new LabShader()];
            Name = "NewMaterial";
        }

        public MaterialData(ITwinMaterial material) : this()
        {
            SetTwinItem(material);
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

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            var material = GetTwinItem<ITwinMaterial>();
            ActivatedShaders = material.ActivatedShaders;
            DmaChainIndex = material.DmaChainIndex;
            Name = new string(material.Name.ToCharArray());
            Shaders = [];
            foreach (var shader in material.Shaders)
            {
                Shaders.Add(new LabShader(package, variant, shader));
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
            foreach (var shader in Shaders)
            {
                if (shader.TextureId == LabURI.Empty) continue;

                assetManager.GetAsset(shader.TextureId).ResolveChunkResources(factory, texturesSection);
            }
            return base.ResolveChunkResources(factory, section, id, layoutID);
        }
    }
}
