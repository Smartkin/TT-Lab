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

public class TwinIdGeneratorServiceInstance<T>(Enums.Layouts layout, LevelChunk chunk)
    : TwinIdGeneratorService<SerializableInstance>
    where T : SerializableInstance
{
    public override UInt32 GenerateTwinId()
    {
        var currentlyRegistered = AssetManager.Get().GetAllAssetsOf<T>()
            .Where(a => a.LayoutID.HasValue && a.LayoutID == (int)layout)
            .Where(a => a.Chunk == chunk.AdditionalPath).ToImmutableList();
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

public class TwinIdGeneratorServiceBehaviour : TwinIdGeneratorService<BehaviourGraph>
{
    public override UInt32 GenerateTwinId()
    {
        var currentlyRegistered = AssetManager.Get().GetAllAssetsOf<BehaviourGraph>();
        var id = 1U;
        while (currentlyRegistered.Any(a => a.ID == id))
        {
            id += 2;
        }
        
        return id;
    }
}