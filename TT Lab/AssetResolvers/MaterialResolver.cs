using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetResolvers;

public class MaterialResolver(TextureResolver textureResolver, bool isInScenery) : AssetResolver<ITwinMaterial>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new System.NotImplementedException();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinMaterial item, bool needVariant, string variant)
    {
        var textureSection = chunk.GetItem<ITwinSection>((uint)(isInScenery ? Constants.SCENERY_GRAPHICS_SECTION : Constants.LEVEL_GRAPHICS_SECTION)).GetItem<ITwinSection>(Constants.GRAPHICS_TEXTURES_SECTION);
        foreach (var itemShader in item.Shaders.Where(itemShader => itemShader.TextureId != 0))
        {
            textureResolver.CreateAssetFromId(chunk, textureSection, package, itemShader.TextureId);
        }

        return new Material(package.URI, needVariant, variant, item.GetID(), item.GetName(), item)
        {
            AdditionalPath = isInScenery ? ChunkPath : string.Empty,
            IsInternal = true
        };
    }

    public override void FinalizeResolve()
    {
        textureResolver.FinalizeResolve();
        
        base.FinalizeResolve();
    }
}