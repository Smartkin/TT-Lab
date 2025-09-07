using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Graphics
{
    public class Skin : SerializableAsset
    {
        protected override String DataExt => ".glb";
        public override UInt32 Section => Constants.GRAPHICS_SKINS_SECTION;
        public override String IconPath => "Skin_Mesh.png";

        public Skin() { }

        public Skin(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinSkin skin) : base(id, name, package, needVariant, variant)
        {
            AssetData = new SkinData(this, skin);
            Raw = false;
        }

        public override Type GetEditorType()
        {
            return typeof(SkinModelViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new SkinData(this);
                AssetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
            }
            return AssetData;
        }
    }
}
