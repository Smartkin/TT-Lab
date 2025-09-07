using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.AssetData.Instance
{
    public class PathData : AbstractAssetData
    {
        public PathData(IAsset asset) : base(asset)
        {
            Points = new List<Vector4>();
            Parameters = new List<Vector2>();
        }

        public PathData(IAsset asset, ITwinPath path) : this(asset)
        {
            SetTwinItem(path);
        }

        [JsonProperty(Required = Required.Always)]
        public List<Vector4> Points { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<Vector2> Parameters { get; set; }

        protected override void Dispose(Boolean disposing)
        {
            Points.Clear();
            Parameters.Clear();
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            ITwinPath path = GetTwinItem<ITwinPath>();
            Points = CloneUtils.CloneList(path.PointList);
            Parameters = CloneUtils.CloneList(path.ParameterList);
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(Points.Count);
            foreach (var point in Points)
            {
                point.Write(writer);
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
}
