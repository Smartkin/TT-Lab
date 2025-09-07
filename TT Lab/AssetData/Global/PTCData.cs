using Newtonsoft.Json;
using System;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetData.Global
{
    [ReferencesAssets]
    public class PTCData : AbstractAssetData
    {
        public PTCData(IAsset asset) : base(asset)
        {
            TextureID = LabURI.Empty;
            MaterialID = LabURI.Empty;
        }

        public PTCData(IAsset asset, ITwinPTC ptc) : this(asset)
        {
            SetTwinItem(ptc);
        }

        [JsonProperty(Required = Required.Always)]
        public LabURI TextureID { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI MaterialID { get; set; }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            var assetManager = AssetManager.Get();

            var texture = (ITwinTexture)assetManager.GetAssetData<TextureData>(TextureID).Export(factory);
            var material = (ITwinMaterial)assetManager.GetAssetData<MaterialData>(MaterialID).Export(factory);

            return factory.GeneratePTC(assetManager.GetAsset(TextureID).ID, assetManager.GetAsset(MaterialID).ID, texture, material);
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            var assetManager = AssetManager.Get();
            var ptc = GetTwinItem<ITwinPTC>();
            var owner = (GlobalAsset)Owner;
            var texture = new Texture(package, false, "", ptc.TexID, $"{ptc.GetName()}_Texture", ptc.Texture)
            {
                AdditionalPath = $"{owner.GlobalPath}/{variant}"
            };
            texture.RegenerateLinks();
            assetManager.AddAssetToImport(texture);
            var material = new Material(package, false, "", ptc.MatID, $"{ptc.GetName()}_Material", ptc.Material)
            {
                AdditionalPath = $"{owner.GlobalPath}/{variant}"
            };
            material.RegenerateLinks();
            assetManager.AddAssetToImport(material);

            TextureID = texture.URI;
            MaterialID = material.URI;
        }

        protected override void Dispose(Boolean disposing)
        {
            return;
        }
    }
}
