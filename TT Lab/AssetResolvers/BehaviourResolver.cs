using System;
using System.Collections.Generic;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.AssetResolvers;

public class BehaviourResolver(Dictionary<string, TwinBehaviourStarter> starterMap) : AssetResolver<ITwinBehaviourGraph>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new System.NotImplementedException();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinBehaviourGraph item, bool needVariant, string variant)
    {
        starterMap.TryGetValue(item.GetID() + ChunkPath.ToLower(), out var starter);
        if (starter == null)
        {
            starterMap.TryGetValue(item.GetID().ToString(), out starter);
        }
        
        return new BehaviourGraph(package.URI, needVariant, variant, item.GetID(), item.GetName(), item, starter);
    }

    protected override String GetTwinItemHash(ITwinBehaviourGraph item)
    {
        return item.GetName();
    }
}