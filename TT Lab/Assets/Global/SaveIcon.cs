using System;
using System.IO;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Global;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;

namespace TT_Lab.Assets.Global;

/// <summary>
/// The PS2 memory card icon the game's saves get (Startup\Crash.ico): a model of triangles the console's browser shows, with shapes its
/// animation blends and a 128x128 texture. Kept as a TT Lab model file the Blender add-on edits, built back into the icon
/// </summary>
[SupportsViewport]
public class SaveIcon : GlobalAsset
{
    protected override String DataExt => ".tlm";
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
            AssetData.Load(DataLoadPath);
        }
        return AssetData;
    }

    public override void ExportToFile(ITwinItemFactory factory, string directory)
    {
        File.WriteAllBytes(Path.Combine(directory, ExportFileName), ((SaveIconData)GetData()).ToIco());
    }

    public override Type GetEditorType()
    {
        throw new NotImplementedException();
    }
}
