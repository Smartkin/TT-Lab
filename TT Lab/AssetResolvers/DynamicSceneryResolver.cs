using System;
using System.Collections.Generic;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.AssetResolvers;

public class DynamicSceneryResolver(MeshResolver meshResolver) : AssetResolver<ITwinDynamicScenery>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        CreateAssetFromId(chunk, chunk, package, Constants.SCENERY_DYNAMIC_SECENERY_ITEM);
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinDynamicScenery item, bool needVariant, string variant)
    {
        var meshSection = chunk.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION).GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);
        foreach (var twinDynamicModel in item.DynamicModels)
        {
            meshResolver.CreateAssetFromId(chunk, meshSection, package, twinDynamicModel.MeshID);
        }
        
        return new DynamicScenery(package.URI, item.GetID(), "Dynamic Scenery", ChunkPath, item) { IsInternal = true };
    }

    protected override String GetTwinItemHash(ITwinDynamicScenery item)
    {
        return "Dynamic Scenery";
    }
}