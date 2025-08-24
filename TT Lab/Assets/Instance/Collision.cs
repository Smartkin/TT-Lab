using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace TT_Lab.Assets.Instance
{
    public class Collision : SerializableInstance
    {
        protected override String DataExt => ".glb";
        public override UInt32 Section => Constants.LEVEL_COLLISION_ITEM;
        public override String IconPath => "Collision.png";

        public Collision()
        {
        }

        public Collision(LabURI package, UInt32 id, String name, String chunk, ITwinCollision collisionData) : base(package, id, name, chunk, null)
        {
            assetData = new CollisionData(collisionData);
        }

        public override Type GetEditorType()
        {
            throw new NotImplementedException();
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || assetData.Disposed)
            {
                assetData = new CollisionData();
                assetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
                IsLoaded = true;
            }
            return assetData;
        }

        protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new CollisionElementViewModel(URI, parent);
        }
    }
}
