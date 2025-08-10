using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.AssetData.Instance.DynamicScenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.AssetData.Instance
{
    [ReferencesAssets]
    public class DynamicSceneryData : AbstractAssetData
    {
        public DynamicSceneryData()
        {
            DynamicModels = new List<DynamicSceneryModelData>();
        }

        public DynamicSceneryData(ITwinDynamicScenery dynamicScenery) : this()
        {
            SetTwinItem(dynamicScenery);
        }

        [JsonProperty(Required = Required.Always)]
        public Int32 UnkInt { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<DynamicSceneryModelData> DynamicModels { get; set; }

        protected override void Dispose(Boolean disposing)
        {
            DynamicModels.Clear();
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            ITwinDynamicScenery dynamicScenery = GetTwinItem<ITwinDynamicScenery>();
            UnkInt = dynamicScenery.UnkInt;
            DynamicModels.Clear();
            foreach (var model in dynamicScenery.DynamicModels)
            {
                DynamicModels.Add(new DynamicSceneryModelData(package, variant, model));
            }
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(UnkInt);
            writer.Write((Int16)DynamicModels.Count);
            foreach (var model in DynamicModels)
            {
                model.Write(writer);
            }

            writer.Flush();
            ms.Position = 0;
            return factory.GenerateDynamicScenery(ms);
        }

        public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
        {
            var assetManager = AssetManager.Get();
            var graphicsSection = section.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION);
            var meshSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);

            foreach (var model in DynamicModels)
            {
                assetManager.GetAsset(model.Mesh).ResolveChunkResources(factory, meshSection);
            }

            return base.ResolveChunkResources(factory, section, id, layoutID);
        }
    }
}
