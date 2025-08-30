using System;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace TT_Lab.AssetResolvers;

public class CollisionResolver : AssetResolver<ITwinCollision>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        CreateAssetFromId(chunk, chunk, package, Constants.LEVEL_COLLISION_ITEM);
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinCollision item, bool needVariant, string variant)
    {
        return new Collision(package.URI, item.GetID(), "Static Collision", ChunkPath, item);
    }

    protected override string GetTwinItemHash(ITwinCollision item)
    {
        return "Static Collision";
    }
}