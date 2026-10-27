using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
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

        // The particles' data got saved, with its systems in their order and the names they were saved under, whichever editor changed them
        // (the particles' own and the default chunk's scene share the data)
        internal static event Action<DefaultParticleData, IReadOnlyList<(ParticleSystem System, String Name)>>? DataSaved;

        public override void Serialize(SerializationFlags serializationFlags = SerializationFlags.None)
        {
            // What's written, taken before the save lets go of the data unless it's kept
            var saved = serializationFlags.HasFlag(SerializationFlags.SaveData) && OverriddenAsset == null && AssetData is DefaultParticleData { Disposed: false } data
                ? data
                : null;
            var systems = saved?.ParticleSystems.Select(system => (system, system.Name)).ToList();
            base.Serialize(serializationFlags);
            if (saved != null)
            {
                DataSaved?.Invoke(saved, systems!);
            }
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
