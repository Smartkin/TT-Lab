using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Graphics;

public class LodModel : SerializableAsset
{
    protected override bool SetIdFromDataHash => true;
    public override UInt32 Section => Constants.GRAPHICS_LODS_SECTION;
    public override String IconPath => "LOD.png";

    public LodModel(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinLOD lod) : base(id, name, package, needVariant, variant)
    {
        AssetData = new LodModelData(this, lod);
    }

    public LodModel()
    {
    }

    public override Type GetEditorType()
    {
        throw new NotImplementedException();
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new LodModelData(this);
            AssetData.Load(DataLoadPath);
        }
        return AssetData;
    }
}