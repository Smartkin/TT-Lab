using System.Collections.Generic;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.AssetResolvers;

public class AnimationResolver : AssetResolver<ITwinAnimation>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new System.NotImplementedException();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinAnimation item, bool needVariant, string variant)
    {
        return new Animation(package.URI, needVariant, variant, item.GetID(), item.GetName(), item);
    }
}