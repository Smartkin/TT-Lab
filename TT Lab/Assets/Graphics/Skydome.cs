using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Graphics
{
    public class Skydome : SerializableAsset
    {
        public override UInt32 Section => Constants.GRAPHICS_SKYDOMES_SECTION;
        public override String IconPath => "Skybox.png";

        public Skydome(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinSkydome skydome) : base(id, name, package, needVariant, variant)
        {
            assetData = new SkydomeData(skydome);
        }

        public Skydome()
        {
        }

        public override Type GetEditorType()
        {
            return typeof(SkydomeViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || assetData.Disposed)
            {
                assetData = new SkydomeData();
                assetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
                IsLoaded = true;
            }
            return assetData;
        }
    }
}
