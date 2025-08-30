using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetResolvers;

public class LodResolver(MeshResolver meshResolver) : AssetResolver<ITwinLOD>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new System.NotImplementedException();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinLOD item, bool needVariant, string variant)
    {
        var meshSection = chunk.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION).GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);
        foreach (var itemMesh in item.Meshes)
        {
            meshResolver.CreateAssetFromId(chunk, null, package, itemMesh);
        }
        return new LodModel(package.URI, needVariant, variant, item.GetID(), item.GetName(), item);
    }
}