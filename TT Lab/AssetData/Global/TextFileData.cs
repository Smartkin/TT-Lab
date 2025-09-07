using Newtonsoft.Json;
using System;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetData.Global
{
    public class TextFileData : AbstractAssetData
    {
        public TextFileData(IAsset asset) : base(asset)
        {
            Text = "";
        }

        public TextFileData(IAsset asset, String text) : this(asset)
        {
            Text = $"{text}";
        }

        public String Text { get; set; }

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
            writer.Write(Text.ToCharArray());
        }

        protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
        {
            using var fs = new FileStream(dataPath, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(fs);
            Text = reader.ReadToEnd();
        }

        protected override void Dispose(Boolean disposing)
        {
            Text = "";
        }
    }
}
