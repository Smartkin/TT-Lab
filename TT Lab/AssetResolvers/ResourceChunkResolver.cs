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
    

    public ResourceChunkResolver(string chunkPath, GameObjectResolver gameObjectResolver, BehaviourResolver behaviourResolver, BehaviourSequenceResolver behaviourSequenceResolver)
    {
        _gameObjectResolver = gameObjectResolver;
        _behaviourResolver = behaviourResolver;
        _behaviourSequenceResolver = behaviourSequenceResolver;
        
        ChunkPath = chunkPath[..];
        _collisionResolver.ChunkPath = chunkPath[..];
        _particleResolver.ChunkPath = chunkPath[..];
        
        _instanceSectionResolvers = new InstanceSectionResolver[8];
        for (var i = 0; i < _instanceSectionResolvers.Length; i++)
        {
            _instanceSectionResolvers[i] = new InstanceSectionResolver(i);
        }
    }
    
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
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
        _particleResolver.FinalizeResolve();
        foreach (var instanceSectionResolver in _instanceSectionResolvers)
        {
            instanceSectionResolver.FinalizeResolve();
        }
        
        base.FinalizeResolve();
    }
}