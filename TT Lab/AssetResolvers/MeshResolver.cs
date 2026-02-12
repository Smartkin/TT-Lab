using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetResolvers;

public class MeshResolver(ModelResolver modelResolver, MaterialResolver materialResolver, bool isDefault = false) : AssetResolver<ITwinMesh>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        var graphicsSection = chunk.GetItem<ITwinSection>((uint)(isDefault ? Constants.LEVEL_GRAPHICS_SECTION : Constants.SCENERY_GRAPHICS_SECTION));
        var meshSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);
        for (var i = 0; i < meshSection.GetItemsAmount(); ++i)
        {
            var itemId = meshSection.GetItem(i).GetID();
            CreateAssetFromId(chunk, meshSection, package, itemId);
        }
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinMesh item, bool needVariant, string variant)
    {
        var graphicsSection = chunk.GetItem<ITwinSection>((uint)(isDefault ? Constants.LEVEL_GRAPHICS_SECTION : Constants.SCENERY_GRAPHICS_SECTION));
        var modelSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MODELS_SECTION);
        var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        modelResolver.CreateAssetFromId(chunk, modelSection, package, item.Model);
        foreach (var itemMaterial in item.Materials)
        {
            materialResolver.CreateAssetFromId(chunk, materialsSection, package, itemMaterial);
        }

        return new Mesh(package.URI, needVariant, variant, item.GetID(), item.GetName(), item)
        {
            AdditionalPath = ChunkPath,
            IsInternal = !isDefault
        };
    }

    public override void FinalizeResolve()
    {
        modelResolver.FinalizeResolve();
        materialResolver.FinalizeResolve();
        
        base.FinalizeResolve();
    }
}