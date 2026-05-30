using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Assets.Instance
{
    public class Trigger : SerializableInstance
    {
        public override UInt32 Section => Constants.LAYOUT_TRIGGERS_SECTION;
        public override String IconPath => "Trigger.png";

        public Trigger(LabURI package, UInt32 id, String name, String chunk, Int32 layId, ITwinTrigger trigger) : base(package, id, name, chunk, layId)
        {
            AssetData = new TriggerData(this, trigger);
        }

        public Trigger()
        {
        }

        public override Type GetEditorType()
        {
            return typeof(TriggerViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new TriggerData(this);
                AssetData.Load(DataLoadPath);
            }
            
            return AssetData;
        }

        protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new InstanceElementGenericViewModel<Trigger>(URI, parent);
        }
    }
}
