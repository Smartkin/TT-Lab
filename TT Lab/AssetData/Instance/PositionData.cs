using Newtonsoft.Json;
using System;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.AssetData.Instance
{
    public class PositionData : AbstractAssetData
    {
        public PositionData(IAsset asset) : base(asset)
        {
            Coords = new Vector4(0, 0, 0, 1);
        }

        public PositionData(IAsset asset, ITwinPosition position) : base(asset)
        {
            SetTwinItem(position);
        }

        [JsonProperty(Required = Required.Always)]
        public Vector4 Coords;

        protected override void Dispose(Boolean disposing)
        {
            return;
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            ITwinPosition position = GetTwinItem<ITwinPosition>();
            Coords = CloneUtils.Clone(position.Position);
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            Coords.Write(writer);

            writer.Flush();
            ms.Position = 0;
            return factory.GeneratePosition(ms);
        }
    }
}
