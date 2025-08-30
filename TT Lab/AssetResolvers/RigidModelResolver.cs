using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetResolvers;

public class RigidModelResolver(ModelResolver modelResolver, MaterialResolver materialResolver) : AssetResolver<ITwinRigidModel>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new System.NotImplementedException();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinRigidModel item, bool needVariant, string variant)
    {
        var graphicsSection = chunk.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var modelSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MODELS_SECTION);
        var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        modelResolver.CreateAssetFromId(chunk, modelSection, package, item.Model);
        foreach (var itemMaterial in item.Materials)
        {
            materialResolver.CreateAssetFromId(chunk, materialsSection, package, itemMaterial);
        }

        return new RigidModel(package.URI, needVariant, variant, item.GetID(), item.GetName(), item);
    }
}