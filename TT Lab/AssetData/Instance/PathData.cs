using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.AssetData.Instance;

public class PathData : AbstractAssetData
{
    public PathData(IAsset asset) : base(asset)
    {
        Points = [];
        Parameters = [];
    }

    public PathData(IAsset asset, ITwinPath path) : this(asset)
    {
        SetTwinItem(path);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public List<Vector3> Points { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public List<Vector2> Parameters { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Points.Clear();
        Parameters.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var path = GetTwinItem<ITwinPath>();
        Points = [];
        foreach (var point in path.PointList)
        {
            Points.Add(new Vector3(point.X, point.Y, point.Z));
        }
        Parameters = CloneUtils.CloneList(path.ParameterList);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(Points.Count);
        foreach (var pos in Points.Select(point => new Vector4(point.X, point.Y, point.Z, 1.0f)))
        {
            pos.Write(writer);
        }

        writer.Write(Parameters.Count);
        foreach (var parameter in Parameters)
        {
            parameter.Write(writer);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GeneratePath(ms);
    }
}