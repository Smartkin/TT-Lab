using System.Linq;
using TT_Lab.Assets;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetResolvers;

public class ResourceChunkResolver : AssetResolver<ITwinSection>
{
    private readonly GameObjectResolver _gameObjectResolver;
    private readonly BehaviourResolver _behaviourResolver;
    private readonly BehaviourSequenceResolver _behaviourSequenceResolver;
    private readonly CollisionResolver _collisionResolver = new();
    private readonly ParticleResolver _particleResolver = new();
    private readonly InstanceSectionResolver[] _instanceSectionResolvers;

    private LevelChunk _levelChunk;
    
    private string ChunkName => ChunkPath.Split(System.IO.Path.DirectorySeparatorChar)[^1];

    public ResourceChunkResolver(GameObjectResolver gameObjectResolver, BehaviourResolver behaviourResolver, BehaviourSequenceResolver behaviourSequenceResolver)
    {
        _gameObjectResolver = gameObjectResolver;
        _behaviourResolver = behaviourResolver;
        _behaviourSequenceResolver = behaviourSequenceResolver;
        
        _instanceSectionResolvers = new InstanceSectionResolver[8];
        for (var i = 0; i < _instanceSectionResolvers.Length; i++)
        {
            _instanceSectionResolvers[i] = new InstanceSectionResolver(i);
        }
    }
    
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        var chunkAsset = new LevelChunk(package.URI, ChunkName)
        {
            AdditionalPath = ChunkPath
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

        _collisionResolver.CreateAssetsFromChunk(chunk, package);
        _particleResolver.CreateAssetsFromChunk(chunk, package);
        _behaviourResolver.CreateAssetsFromChunk(chunk, package);
        _behaviourSequenceResolver.CreateAssetsFromChunk(chunk, package);
        _gameObjectResolver.CreateAssetsFromChunk(chunk, package);
        foreach (var instanceSectionResolver in _instanceSectionResolvers)
        {
            instanceSectionResolver.CreateAssetsFromChunk(chunk, package);
        }
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinSection item, bool needVariant, string variant)
    {
        throw new System.NotImplementedException();
    }

    public override void FinalizeResolve()
    {
        _collisionResolver.FinalizeResolve();
        _levelChunk.ChunkResources.AddRange(_collisionResolver.GetAssets().Select(m => m.Uri));
        _particleResolver.FinalizeResolve();
        _levelChunk.ChunkResources.AddRange(_particleResolver.GetAssets().Select(m => m.Uri));
        foreach (var instanceSectionResolver in _instanceSectionResolvers)
        {
            instanceSectionResolver.FinalizeResolve();
            _levelChunk.ChunkResources.AddRange(instanceSectionResolver.GetAssets().Select(m => m.Uri));
        }
        
        base.FinalizeResolve();
    }
}