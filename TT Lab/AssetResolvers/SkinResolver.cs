using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetResolvers;

public class SkinResolver(MaterialResolver materialResolver) : AssetResolver<ITwinSkin>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new System.NotImplementedException();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinSkin item, bool needVariant, string variant)
    {
        var materialSection = chunk.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION).GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        foreach (var subSkin in item.SubSkins)
        {
            materialResolver.CreateAssetFromId(chunk, materialSection, package, subSkin.Material);
        }
        
        return new Skin(package.URI, needVariant, variant, item.GetID(), item.GetName(), item) { IsInternal = true };
    }
}