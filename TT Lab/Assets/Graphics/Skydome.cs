using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Graphics;

public class Skydome : SerializableAsset
{
    protected override bool SetIdFromDataHash => true;
    protected override string DataExt => ".glb";
    
    public override UInt32 Section => Constants.GRAPHICS_SKYDOMES_SECTION;
    public override String IconPath => "Skybox.png";

    public Skydome(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinSkydome skydome) : base(id, name, package, needVariant, variant)
    {
        AssetData = new SkydomeData(this, skydome);
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
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new SkydomeData(this);
            AssetData.Load(DataLoadPath);
        }
        return AssetData;
    }
}