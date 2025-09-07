using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Global;
using TT_Lab.Assets.Factory;

namespace TT_Lab.Assets.Global;

/// <summary>
/// Save icons are Sony custom model formats that get displayed at PS2's memory card saves editor.
/// They are not just a 3D model but can also have animation data baked in.
/// <para>
/// TODO: Add support/write a separate library for working with these (Who wouldn't want a custom save icon for their mods?)
/// </para>
/// </summary>
public class SaveIcon : GlobalAsset
{
    protected override String DataExt => ".bin";
    protected override String TwinDataExt => "ico";
    public override UInt32 Section => throw new NotImplementedException();
    public override String IconPath => "Save.png";

    public SaveIcon()
    {
    }

    public SaveIcon(LabURI package, Boolean needVariant, String variant, String name, Byte[] data) : base((UInt32)Guid.NewGuid().GetHashCode(), name, package, needVariant, variant)
    {
        AssetData = new SaveIconData(this, data);
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new SaveIconData(this);
            AssetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
        }
        return AssetData;
    }

    public override void ExportToFile(ITwinItemFactory factory)
    {
        GetData().SaveInCurrentDirectory($"{Name}.{TwinDataExt}");
    }

    public override Type GetEditorType()
    {
        throw new NotImplementedException();
    }
}