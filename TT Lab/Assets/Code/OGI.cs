using System;
using System.Collections.Generic;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.ViewModels.Editors.Code;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Assets.Code
{
    public class OGI : SerializableAsset
    {
        public override UInt32 Section => Constants.CODE_OGIS_SECTION;
        public override String IconPath => "OGI.png";

        public OGI() { }

        public OGI(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinOGI ogi) : base(id, name, package, needVariant, variant)
        {
            assetData = new OGIData(ogi);
        }

        public void LinkAnimationsToData(List<LabURI> animations)
        {
            ((OGIData)assetData).LinkAnimations(animations);
        }

        public override Type GetEditorType()
        {
            return typeof(OGIViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || assetData.Disposed)
            {
                assetData = new OGIData();
                assetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
                IsLoaded = true;
            }
            return assetData;
        }
    }
}
