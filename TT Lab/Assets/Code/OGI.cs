using System;
using System.Collections.Generic;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.Code;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Assets.Code;

[SupportsViewport]
public class OGI : SerializableAsset
{
    protected override String DataExt => ".tlm";
    public override UInt32 Section => Constants.CODE_OGIS_SECTION;
    public override String IconPath => "OGI.png";

    public OGI() { }

    public OGI(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinOGI ogi) : base(id, name, package, needVariant, variant)
    {
        AssetData = new OGIData(this, ogi);
    }

    public void SetAnimationsToData(List<AnimationData> animations)
    {
        ((OGIData)GetData()).SetAnimations(animations);
    }

    public override Type GetEditorType()
    {
        return typeof(OGIViewModel);
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new OGIData(this);
            AssetData.Load(DataLoadPath);
        }
        return AssetData;
    }
}