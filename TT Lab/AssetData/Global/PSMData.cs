using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Global;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetData.Global
{
    [ReferencesAssets]
    public class PSMData : AbstractAssetData
    {
        public PSMData()
        {
            PTCs = new();
        }

        public PSMData(ITwinPSM psm) : this()
        {
            SetTwinItem(psm);
        }

        [JsonProperty(Required = Required.Always)]
        public List<LabURI> PTCs { get; set; }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            var assetManager = AssetManager.Get();
            var ptcs = new List<ITwinPTC>();
            foreach (var ptc in PTCs)
            {
                ptcs.Add((ITwinPTC)assetManager.GetAssetData<PTCData>(ptc).Export(factory));
            }

            return factory.GeneratePSM(ptcs);
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            var psm = GetTwinItem<ITwinPSM>();
            var ptcIndex = 0;
            foreach (var ptc in psm.PTCs)
            {
                var asset = new PTC(package, true, $"{psm.GetName()}_{variant}_{ptcIndex++}", $"{psm.GetName()}_{ptcIndex++}", ptc)
                {
                    GlobalPath = $"PTC/{psm.GetName()}/{variant!}"
                };
                asset.RegenerateLinks();
                AssetManager.Get().AddAssetToImport(asset);
                PTCs.Add(asset.URI);
            }
        }

        protected override void Dispose(Boolean disposing)
        {
            PTCs.Clear();
        }
    }
}
