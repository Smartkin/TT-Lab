using System;
using System.Collections.Generic;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.Assets.Instance
{
    public class Scenery : SerializableInstance
    {
        public override bool IsInScenery => true;
        public override UInt32 Section => Constants.SCENERY_SECENERY_ITEM;

        public Scenery()
        {
            Parameters = new Dictionary<string, object?>();
        }

        public Scenery(LabURI package, UInt32 id, String name, String chunk, ITwinScenery scenery) : base(package, id, name, chunk, null)
        {
            assetData = new SceneryData(scenery);
        }

        public override Type GetEditorType()
        {
            return typeof(SceneryViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || assetData.Disposed)
            {
                assetData = new SceneryData();
                assetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
                IsLoaded = true;
            }
            return assetData;
        }

        protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new SceneryElementViewModel(URI, parent);
        }
    }
}
