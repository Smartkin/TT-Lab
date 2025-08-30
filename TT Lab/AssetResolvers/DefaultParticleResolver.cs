using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace TT_Lab.AssetResolvers;

public class DefaultParticleResolver(MaterialResolver materialResolver, TextureResolver textureResolver) : AssetResolver<ITwinDefaultParticle>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        CreateAssetFromId(chunk, chunk, package, Constants.LEVEL_PARTICLES_ITEM);
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinDefaultParticle item, bool needVariant, string variant)
    {
        var graphicsSection = chunk.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var materialSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        var textureSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_TEXTURES_SECTION);
        
        materialResolver.CreateAssetFromId(chunk, materialSection, package, item.DecalMaterialID);
        textureResolver.CreateAssetFromId(chunk, textureSection, package, item.DecalTextureID);

        foreach (var itemMaterialId in item.MaterialIDs)
        {
            materialResolver.CreateAssetFromId(chunk, materialSection, package, itemMaterialId);
        }

        foreach (var itemTextureId in item.TextureIDs)
        {
            textureResolver.CreateAssetFromId(chunk, textureSection, package, itemTextureId);
        }
        
        return new DefaultParticles(package.URI, item.GetID(), "Global Particles", ChunkPath, item);
    }
}