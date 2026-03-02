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

public class AiPositionData : AbstractAssetData
{
    public AiPositionData(IAsset asset) : base(asset)
    {
        Coords = new Vector4(0, 0, 0, 1);
    }

    public AiPositionData(IAsset asset, ITwinAIPosition aiPosition) : this(asset)
    {
        SetTwinItem(aiPosition);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Vector4 Coords { get; set; }
        
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt16 Arg { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        return;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        ITwinAIPosition aiPosition = GetTwinItem<ITwinAIPosition>();
        Coords = CloneUtils.Clone(aiPosition.Position);
        Arg = aiPosition.UnkShort;
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        Coords.Write(writer);
        writer.Write(Arg);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateAIPosition(ms);
    }
}