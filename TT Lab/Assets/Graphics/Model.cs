using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Graphics
{
    public class Model : SerializableAsset
    {
        protected override String DataExt => ".glb";
        public override UInt32 Section => Constants.GRAPHICS_MODELS_SECTION;
        public override String IconPath => "Model.png";

        public Model(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinModel model) : base(id, name, package, needVariant, variant)
        {
            AssetData = new ModelData(this, model);
            Raw = false;
        }

        public Model()
        {
        }

        public override Type GetEditorType()
        {
            return typeof(ModelViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new ModelData(this);
                AssetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
            }
            return AssetData;
        }
    }
}
