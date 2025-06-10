using System;
using TT_Lab.Assets.Factory;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Assets.Instance
{
    public abstract class SerializableInstance : SerializableAsset
    {
        public virtual bool IsInScenery => false;
        protected override String SavePath => $"Instance\\{Chunk}\\{Type.Name}{LayoutPath}";
        private String LayoutPath => LayoutID == null ? "" : $"\\Layout_{LayoutID}";

        protected SerializableInstance(LabURI package, UInt32 id, String name, String chunk, Int32? layId) : base(id, name, package, true, chunk)
        {
            Chunk = chunk;
            LayoutID = layId;
            RegenerateURI(true);
        }

        protected SerializableInstance()
        {
        }

        public override void ResolveChunkResources(ITwinItemFactory factory, ITwinSection section)
        {
            if (LayoutID != null)
            {
                section = section.GetItem<ITwinSection>((UInt32)LayoutID);
                section = section.GetItem<ITwinSection>(Section);
            }
            base.ResolveChunkResources(factory, section);
        }
    }
}
