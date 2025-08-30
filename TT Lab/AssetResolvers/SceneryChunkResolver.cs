using System;
using TT_Lab.Assets;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetResolvers;

public class SceneryChunkResolver : AssetResolver<ITwinSection>
{
    private readonly SceneryResolver _sceneryResolver;
    private readonly DynamicSceneryResolver _dynamicSceneryResolver;
    private readonly ChunkLinkResolver _chunkLinkResolver;

    public SceneryChunkResolver(string chunkPath, SkydomeResolver skydomeResolver)
    {
        ChunkPath = chunkPath[..];
        _sceneryResolver = new SceneryResolver(skydomeResolver)
        {
            ChunkPath = chunkPath[..]
        };
        _dynamicSceneryResolver = new DynamicSceneryResolver(_sceneryResolver.MeshResolver)
        {
            ChunkPath = chunkPath[..]
        };
        _chunkLinkResolver = new ChunkLinkResolver
        {
            ChunkPath = chunkPath[..]
        };
    }
    
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        _sceneryResolver.CreateAssetsFromChunk(chunk, package);
        _dynamicSceneryResolver.CreateAssetsFromChunk(chunk, package);
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
        _sceneryResolver.FinalizeResolve();
        _dynamicSceneryResolver.FinalizeResolve();
        _chunkLinkResolver.FinalizeResolve();
        
        base.FinalizeResolve();
    }
}