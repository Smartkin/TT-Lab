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

// The lowest ID free among a kind's elements of a layout of one version's chunk: the PS2 and Xbox chunks of a path are numbered
// apart, an ID past the other version's elements left a gap in this one's (the default chunk's surfaces are found by their ID)
public class TwinIdGeneratorServiceInstance(Type type, Enums.Layouts layout, string chunk, LabURI package) : ITwinIdGeneratorService
{
    public UInt32 GenerateTwinId()
    {
        var taken = AssetManager.Get().GetAllAssetsOf(type).OfType<SerializableInstance>()
            .Where(a => a.LayoutID == (int)layout && a.Chunk == chunk && a.Package == package)
            .Select(a => a.ID).ToHashSet();
        var id = 0U;
        while (taken.Contains(id))
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