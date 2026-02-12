using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetResolvers;

public class TextureResolver(bool isInScenery) : AssetResolver<ITwinTexture>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new System.NotImplementedException();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinTexture item, bool needVariant, string variant)
    {
        return new Texture(package.URI, needVariant, variant, item.GetID(), item.GetName(), item)
        {
            AdditionalPath = isInScenery ? ChunkPath : string.Empty,
            IsInternal = true
        };
    }
}