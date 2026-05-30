using System;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.AssetResolvers;

public class BehaviourSequenceResolver : AssetResolver<ITwinBehaviourCommandsSequence>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        var code = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var behaviours = code.GetItem<ITwinSection>(Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION);
        for (var i = 0; i < behaviours.GetItemsAmount(); ++i)
        {
            var itemId = behaviours.GetItem(i).GetID();
            CreateAssetFromId(chunk, behaviours, package, itemId);
        }
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinBehaviourCommandsSequence item, bool needVariant, string variant)
    {
        return new BehaviourCommandsSequence(package.URI, needVariant, variant, item.GetID(), item.GetName(), item);
    }

    protected override String GetTwinItemHash(ITwinBehaviourCommandsSequence item)
    {
        return item.GetID().ToString();
    }
}