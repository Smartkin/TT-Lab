using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Assets.Instance
{
    public class Position : SerializableInstance
    {
        public override UInt32 Section => Constants.LAYOUT_POSITIONS_SECTION;
        public override String IconPath => "Position.png";

        public Position(LabURI package, UInt32 id, String name, String chunk, Int32 layId, ITwinPosition position) : base(package, id, name, chunk, layId)
        {
            AssetData = new PositionData(position);
        }

        public Position()
        {
        }

        public override Type GetEditorType()
        {
            return typeof(PositionViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new PositionData();
                AssetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
            }
            
            return AssetData;
        }

        protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new InstanceElementGenericViewModel<Position>(URI, parent);
        }
    }
}
