using System;
using System.Collections.Generic;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.AssetResolvers;

public class BehaviourResolver(Dictionary<string, TwinBehaviourStarter> starterMap) : AssetResolver<ITwinBehaviourGraph>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        var code = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var behaviours = code.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
        for (var i = 0; i < behaviours.GetItemsAmount(); ++i)
        {
            var itemId = behaviours.GetItem(i).GetID();
            if (itemId % 2 == 0)
            {
                continue;
            }

            CreateAssetFromId(chunk, behaviours, package, itemId);
        }
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