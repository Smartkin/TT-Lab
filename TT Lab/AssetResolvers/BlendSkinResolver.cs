using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetResolvers;

public class BlendSkinResolver(MaterialResolver materialResolver) : AssetResolver<ITwinBlendSkin>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new System.NotImplementedException();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinBlendSkin item, bool needVariant, string variant)
    {
        var materialSection = chunk.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION).GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        foreach (var subBlend in item.SubBlends)
        {
            materialResolver.CreateAssetFromId(chunk, materialSection, package, subBlend.Material);
        }

        return new BlendSkin(package.URI, needVariant, variant, item.GetID(), item.GetName(), item) { IsInternal = true };
    }
}