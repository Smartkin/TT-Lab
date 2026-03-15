using Newtonsoft.Json;
using System;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.AssetData.Instance;

public class PositionData : AbstractAssetData
{
    public PositionData(IAsset asset) : base(asset)
    {
        Coords = new Vector3(0, 0, 0);
    }

    public PositionData(IAsset asset, ITwinPosition position) : base(asset)
    {
        SetTwinItem(position);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable(IncludeAllProperties = true)]
    public Vector3 Coords { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        return;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var position = GetTwinItem<ITwinPosition>();
        Coords = new Vector3(position.Position.X, position.Position.Y, position.Position.Z);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        var coordsVec = new Vector4(Coords.X, Coords.Y, Coords.Z, 1.0f);
        coordsVec.Write(writer);

        writer.Flush();
        ms.Position = 0;
        return factory.GeneratePosition(ms);
    }
}