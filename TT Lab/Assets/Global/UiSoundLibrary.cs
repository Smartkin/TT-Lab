using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Global;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Assets.Global;

public class UiSoundLibrary : GlobalAsset
{
    public override UInt32 Section => throw new NotImplementedException();
    public override String IconPath => "UI_Sound_Library.png";

    public UiSoundLibrary() { }

    public UiSoundLibrary(LabURI package, Boolean needVariant, String variant, String name, ITwinSection frontend) : base((UInt32)Guid.NewGuid().GetHashCode(), name, package, needVariant, variant)
    {
        AssetData = new UiSoundLibraryData(this, frontend);
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded)
        {
            AssetData = new UiSoundLibraryData(this);
            AssetData.Load(DataLoadPath);
        }
            
        return AssetData;
    }

    public override Type GetEditorType()
    {
        throw new NotImplementedException();
    }
}