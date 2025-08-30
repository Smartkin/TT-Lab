using System;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace TT_Lab.AssetResolvers;

public class ParticleResolver : AssetResolver<ITwinParticle>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        CreateAssetFromId(chunk, chunk, package, Constants.LEVEL_PARTICLES_ITEM);
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinParticle item, bool needVariant, string variant)
    {
        return new Particles(package.URI, item.GetID(), "Particles", ChunkPath, item);
    }

    protected override String GetTwinItemHash(ITwinParticle item)
    {
        return "Particles";
    }
}