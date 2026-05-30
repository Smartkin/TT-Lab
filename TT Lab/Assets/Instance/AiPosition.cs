using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Assets.Instance
{
    public class AiPosition : SerializableInstance
    {
        public override UInt32 Section => Constants.LAYOUT_AI_POSITIONS_SECTION;
        public override String IconPath => "AI_Position.png";

        public AiPosition(LabURI package, UInt32 id, String name, String chunk, Int32 layId, ITwinAIPosition position) : base(package, id, name, chunk, layId)
        {
            AssetData = new AiPositionData(this, position);
        }

        public AiPosition()
        {
        }

        public override Type GetEditorType()
        {
            return typeof(AiPositionViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new AiPositionData(this);
                AssetData.Load(DataLoadPath);
            }
            return AssetData;
        }

        protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new InstanceElementGenericViewModel<AiPosition>(URI, parent);
        }
    }
}
