using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Global;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Assets.Global;

public class PTC : GlobalAsset
{
    protected override String TwinDataExt => "ptc";
    public override UInt32 Section => throw new NotImplementedException();
    public override String IconPath => "PTC.png";

    public PTC() { }

    public PTC(LabURI package, Boolean needVariant, String variant, String name, ITwinPTC ptc) : base((UInt32)Guid.NewGuid().GetHashCode(), name, package, needVariant, variant)
    {
        AssetData = new PTCData(this, ptc);
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new PTCData(this);
            AssetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
        }
        return AssetData;
    }

    public override void PreResolveResources()
    {
        base.PreResolveResources();
        
        var data = (PTCData)GetData();
        AssetManager.Get().GetAsset<Texture>(data.TextureID).PreResolveResources();
        AssetManager.Get().GetAsset<Material>(data.MaterialID).PreResolveResources();
    }

    public override Type GetEditorType()
    {
        throw new NotImplementedException();
    }
}