using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace TT_Lab.Assets.Instance
{
    public class DefaultParticles : Particles
    {
        public DefaultParticles() : base() { }

        public DefaultParticles(LabURI package, UInt32 id, String name, String chunk, ITwinDefaultParticle particleData) : base(package, id, name, chunk, null)
        {
            AssetData = new DefaultParticleData(this, particleData);
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || AssetData.Disposed)
            {
                AssetData = new DefaultParticleData(this);
                AssetData.Load(DataLoadPath);
            }
            return AssetData;
        }
    }
}
