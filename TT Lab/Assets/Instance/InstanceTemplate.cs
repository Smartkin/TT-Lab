using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Assets.Instance
{
    public class InstanceTemplate : SerializableInstance
    {
        public override UInt32 Section => Constants.LAYOUT_TEMPLATES_SECTION;
        public override String IconPath => "Instance_Template.png";

        public InstanceTemplate(LabURI package, UInt32 id, String name, String chunk, Int32 layId, ITwinTemplate template) : base(package, id, name, chunk, layId)
        {
            AssetData = new InstanceTemplateData(template);
        }

        public InstanceTemplate()
        {
        }

        public override Type GetEditorType()
        {
            throw new NotImplementedException();
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new InstanceTemplateData();
                AssetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
            }
            return AssetData;
        }

        protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new InstanceElementGenericViewModel<InstanceTemplate>(URI, parent);
        }
    }
}
