using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Graphics;

public class RigidModel : SerializableAsset
{
    protected override bool SetIdFromDataHash => true;
    public override UInt32 Section => Constants.GRAPHICS_RIGID_MODELS_SECTION;
    public override String IconPath => "Mesh.png";

    public RigidModel(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinRigidModel rigidModel) : base(id, name, package, needVariant, variant)
    {
        AssetData = new RigidModelData(this, rigidModel);
    }

    public RigidModel()
    {
    }

    public override Type GetEditorType()
    {
        return typeof(RigidModelViewModel);
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new RigidModelData(this);
            AssetData.Load(DataLoadPath);
        }
        return AssetData;
    }
}