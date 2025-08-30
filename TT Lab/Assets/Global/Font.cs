using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Global;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Assets.Global
{
    public class Font : SerializableAsset
    {
        protected override String TwinDataExt => "psf";
        public override UInt32 Section => throw new NotImplementedException();
        public override String IconPath => "Font.png";

        public Font() { }

        public Font(LabURI package, Boolean needVariant, String variant, String name, ITwinPSF psf) : base((UInt32)Guid.NewGuid().GetHashCode(), name, package, needVariant, variant)
        {
            AssetData = new FontData(psf);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new FontData();
                AssetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
            }
            return AssetData;
        }

        public override void PreResolveResources()
        {
            base.PreResolveResources();
            var assetManager = AssetManager.Get();
            FontData data = (FontData)GetData();
            foreach (var page in data.FontPages)
            {
                assetManager.GetAsset<PTC>(page).PreResolveResources();
            }
        }

        public override Type GetEditorType()
        {
            throw new NotImplementedException();
        }
    }
}
