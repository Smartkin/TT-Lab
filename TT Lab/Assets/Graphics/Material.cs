using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Graphics;

[SupportsViewport]
public class Material : SerializableAsset
{
    protected override bool SetIdFromDataHash => true;
    protected override bool KeepsRetailId => true;

    // Its URI: two materials of byte-identical data files had one ID, and the parts drawing them, named after their materials' hashes
    // (RigidModelData.GetStringified), became one part drawing the first, so a model drawing the other from Blender lost it
    protected override string DataSalt => HashSalt.Length > 0 ? HashSalt : URI?.ToString() ?? string.Empty;
    public override UInt32 Section => Constants.GRAPHICS_MATERIALS_SECTION;
    public override String IconPath => "Material.png";

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
            AssetData.Load(DataLoadPath);
        }
        return AssetData;
    }
}