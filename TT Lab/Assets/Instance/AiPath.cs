using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Assets.Instance
{
    public class AiPath : SerializableInstance
    {
        public override UInt32 Section => Constants.LAYOUT_AI_PATHS_SECTION;
        public override String IconPath => "AI_Path.png";

        public AiPath(LabURI package, UInt32 id, String name, String chunk, Int32 layId, ITwinAIPath path) : base(package, id, name, chunk, layId)
        {
            AssetData = new AiPathData(this, path);
        }

        public AiPath()
        {
        }

        public override Type GetEditorType()
        {
            return typeof(AiPathViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new AiPathData(this);
                AssetData.Load(DataLoadPath);
            }
            return AssetData;
        }

        protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new InstanceElementGenericViewModel<AiPath>(URI, parent);
        }
    }
}
