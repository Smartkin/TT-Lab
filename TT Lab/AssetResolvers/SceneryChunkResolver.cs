using System;
using System.Linq;
using TT_Lab.Assets;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetResolvers;

public class SceneryChunkResolver : AssetResolver<ITwinSection>
{
    private readonly SceneryResolver _sceneryResolver;
    private readonly DynamicSceneryResolver _dynamicSceneryResolver;
    private readonly ChunkLinkResolver _chunkLinkResolver;
    
    private LevelChunk _levelChunk;
    
    private string ChunkName => ChunkPath.Split(System.IO.Path.DirectorySeparatorChar)[^1];

    public SceneryChunkResolver(SkydomeResolver skydomeResolver)
    {
        _sceneryResolver = new SceneryResolver(skydomeResolver);
        _dynamicSceneryResolver = new DynamicSceneryResolver(_sceneryResolver.MeshResolver);
        _sceneryResolver.SetDynamicSceneryResolver(_dynamicSceneryResolver);
        _chunkLinkResolver = new ChunkLinkResolver();
    }
    
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        var chunkAsset = new LevelChunk(package.URI, ChunkName)
        {
            AdditionalPath = ChunkPath[..]
        };
        chunkAsset.RegenerateLinks();
        var assetManager = AssetManager.Get();
        if (!assetManager.DoesAssetExist(chunkAsset.URI))
        {
            _levelChunk = chunkAsset;
            assetManager.AddAsset(chunkAsset);
        }
        else
        {
            _levelChunk = assetManager.GetAsset<LevelChunk>(chunkAsset.URI);
        }
        
        _dynamicSceneryResolver.CreateAssetsFromChunk(chunk, package);
        _sceneryResolver.CreateAssetsFromChunk(chunk, package);
        _chunkLinkResolver.CreateAssetsFromChunk(chunk, package);
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinSection item, bool needVariant, string variant)
    {
        throw new System.NotImplementedException();
    }

    protected override String GetTwinItemHash(ITwinSection item)
    {
        return "Scenery";
    }

    public override void FinalizeResolve()
    {
        _dynamicSceneryResolver.FinalizeResolve();
        _sceneryResolver.FinalizeResolve();
        _levelChunk.ChunkResources.AddRange(_sceneryResolver.GetAssets().Select(m => m.Uri));
        _chunkLinkResolver.FinalizeResolve();
        _levelChunk.ChunkResources.AddRange(_chunkLinkResolver.GetAssets().Select(m => m.Uri));
        
        base.FinalizeResolve();
    }
}