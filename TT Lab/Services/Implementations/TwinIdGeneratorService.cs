using System;
using System.Collections.Immutable;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.ServiceProviders;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.Services.Implementations;

public class TwinIdGeneratorService<T> : ITwinIdGeneratorService where T : IAsset
{
    public virtual UInt32 GenerateTwinId()
    {
        var currentlyRegistered = AssetManager.Get().GetAllAssetsOf<T>();
        var id = 1U;
        while (currentlyRegistered.Any(a => a.ID == id))
        {
            id++;
        }
        return id;
    }
}

public class TwinIdGeneratorServiceInstance<T> : TwinIdGeneratorService<SerializableInstance> where T : SerializableInstance
{
    private readonly Enums.Layouts _boundLayout;
    private readonly ChunkFolder _boundChunk;
    
    public TwinIdGeneratorServiceInstance(Enums.Layouts layout, ChunkFolder chunk)
    {
        _boundLayout = layout;
        _boundChunk = chunk;
    }

    public override UInt32 GenerateTwinId()
    {
        var currentlyRegistered = AssetManager.Get().GetAllAssetsOf<T>()
            .Where(a => a.LayoutID.HasValue && a.LayoutID == (int)_boundLayout)
            .Where(a => a.Chunk[..^3] == _boundChunk.Variation[..^3]).ToImmutableList();
        var id = 0U;
        while (currentlyRegistered.Any(a => a.ID == id))
        {
            id++;
        }
        
        return id;
    }
}

public class TwinIdGeneratorServiceFolder : TwinIdGeneratorService<Folder>
{
    public override UInt32 GenerateTwinId()
    {
        return (UInt32)Guid.NewGuid().GetHashCode();
    }
}

public class TwinIdGeneratorServiceBehaviour : TwinIdGeneratorService<BehaviourStarter>
{
    public override UInt32 GenerateTwinId()
    {
        var currentlyRegistered = AssetManager.Get().GetAllAssetsOf<BehaviourStarter>();
        var id = 2U;
        while (currentlyRegistered.Any(a => a.ID == id))
        {
            id += 2;
        }
        
        return id;
    }
}