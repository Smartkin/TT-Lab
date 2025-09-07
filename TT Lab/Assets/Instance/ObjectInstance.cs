using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Assets.Instance
{
    public class ObjectInstance : SerializableInstance
    {
        public override UInt32 Section => Constants.LAYOUT_INSTANCES_SECTION;
        public override String IconPath => "Instance.png";

        public ObjectInstance(LabURI package, UInt32 id, String name, String chunk, Int32 layId, ITwinInstance instance) : base(package, id, name, chunk, layId)
        {
            AssetData = new ObjectInstanceData(this, instance);
        }

        public ObjectInstance()
        {
        }

        public override Type GetEditorType()
        {
            return typeof(ObjectInstanceViewModel);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new ObjectInstanceData(this);
                AssetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
            }
            return AssetData;
        }

        protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new InstanceElementGenericViewModel<ObjectInstance>(URI, parent);
        }
    }
}
