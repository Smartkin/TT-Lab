using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.ViewModels.Editors.Code;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Assets.Code
{
    public class Animation : SerializableAsset
    {
        public override UInt32 Section => Constants.CODE_ANIMATIONS_SECTION;
        public override String IconPath => "Animation.png";

        public Animation() { }

        public Animation(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinAnimation? animation = null) : base(id, name, package, needVariant, variant)
        {
            if (animation == null)
            {
                return;
            }
            
            AssetData = new AnimationData(animation);
        }

        public override Type GetEditorType()
        {
            return typeof(AnimationViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new AnimationData();
                AssetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
            }
            return AssetData;
        }
    }
}
