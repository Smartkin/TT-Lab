using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Assets.Graphics
{
    public class Mesh : RigidModel
    {
        public override UInt32 Section => Constants.GRAPHICS_MESHES_SECTION;

        public Mesh() { }

        public Mesh(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinMesh mesh) : base(package, needVariant, variant, id, name, mesh)
        {
            AssetData = new MeshData(this, mesh);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new MeshData(this);
                AssetData.Load(DataLoadPath);
            }
            return AssetData;
        }
    }
}
