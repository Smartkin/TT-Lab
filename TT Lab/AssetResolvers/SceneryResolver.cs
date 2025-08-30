using System.Collections.Generic;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.AssetResolvers;

public class SceneryResolver : AssetResolver<ITwinScenery>
{
    private readonly SkydomeResolver _skydomeResolver;
    private readonly MeshResolver _meshResolver;
    private readonly LodResolver _lodResolver;

    public SceneryResolver(SkydomeResolver skydomeResolver)
    {
        _skydomeResolver = skydomeResolver;
        _meshResolver = new MeshResolver(new ModelResolver(), new MaterialResolver(new TextureResolver(), true));
        _lodResolver = new LodResolver(_meshResolver);
    }
    
    public MeshResolver MeshResolver => _meshResolver;
    
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        CreateAssetFromId(chunk, chunk, package, Constants.SCENERY_SECENERY_ITEM);
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinScenery item, bool needVariant, string variant)
    {
        var graphicsSection = chunk.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION);
        if (item.SkydomeID != 0)
        {
            var skydomeSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_SKYDOMES_SECTION);
            _skydomeResolver.CreateAssetFromId(chunk, skydomeSection, package, item.SkydomeID);
        }

        var meshSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);
        var lodSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_LODS_SECTION);
        foreach (var twinScenery in item.Sceneries)
        {
            foreach (var twinMesh in twinScenery.MeshIDs)
            {
                _meshResolver.CreateAssetFromId(chunk, meshSection, package, twinMesh);
            }

            foreach (var twinLod in twinScenery.LodIDs)
            {
                _lodResolver.CreateAssetFromId(chunk, lodSection, package, twinLod);
            }
        }
        
        return new Scenery(package.URI, item.GetID(), item.GetName(), ChunkPath, item);
    }

    public override void FinalizeResolve()
    {
        _meshResolver.FinalizeResolve();
        _lodResolver.FinalizeResolve();
        
        base.FinalizeResolve();
    }
}