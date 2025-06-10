using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.Assets.Instance
{
    public class DynamicScenery : SerializableInstance
    {
        public override bool IsInScenery => true;
        public override UInt32 Section => Constants.SCENERY_DYNAMIC_SECENERY_ITEM;

        public DynamicScenery()
        {
        }

        public DynamicScenery(LabURI package, UInt32 id, String name, String chunk, ITwinDynamicScenery dynamicScenery) : base(package, id, name, chunk, null)
        {
            assetData = new DynamicSceneryData(dynamicScenery);
        }

        public override Type GetEditorType()
        {
            throw new NotImplementedException();
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || assetData.Disposed)
            {
                assetData = new DynamicSceneryData();
                assetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
                IsLoaded = true;
            }
            return assetData;
        }

        protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new DynamicSceneryElementViewModel(URI, parent);
        }
    }
}
