using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.AssetResolvers;

public class ChunkLinkResolver : AssetResolver<ITwinLink>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        CreateAssetFromId(chunk, chunk, package, Constants.SCENERY_LINK_ITEM);
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinLink item, bool needVariant, string variant)
    {
        return new ChunkLinks(package.URI, item.GetID(), "Chunk Links", ChunkPath, item);
    }

    protected override string GetTwinItemHash(ITwinLink item)
    {
        return "Chunk Links";
    }
}