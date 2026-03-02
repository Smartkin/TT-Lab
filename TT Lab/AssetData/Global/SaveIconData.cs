using Newtonsoft.Json;
using System;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetData.Global;

public class SaveIconData : AbstractAssetData
{
    public SaveIconData(IAsset asset) : base(asset)
    {
        IconData = Array.Empty<Byte>();
    }

    public SaveIconData(IAsset asset, Byte[] iconData) : base(asset)
    {
        IconData = CloneUtils.CloneArray(iconData);
    }

    public Byte[] IconData { get; set; }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        throw new NotImplementedException();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        return;
    }

    protected override void SaveInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        using var fs = new FileStream(dataPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);
        writer.Write(IconData);
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        using var fs = new FileStream(dataPath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        IconData = reader.ReadBytes((Int32)fs.Length);
    }


    protected override void Dispose(Boolean disposing)
    {
        IconData = Array.Empty<Byte>();
    }
}