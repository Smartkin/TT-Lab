using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Graphics
{
    public class Material : SerializableAsset
    {
        public override UInt32 Section => Constants.GRAPHICS_MATERIALS_SECTION;
        public override String IconPath => "LOD.png";

        public Material(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinMaterial material) : base(id, name, package, needVariant, variant)
        {
            AssetData = new MaterialData(this, material);
        }

        public Material()
        {
        }

        public override Type GetEditorType()
        {
            return typeof(MaterialViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new MaterialData(this);
                AssetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
            }
            return AssetData;
        }
    }
}
