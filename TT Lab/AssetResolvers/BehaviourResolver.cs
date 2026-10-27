using System;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.AssetResolvers;

public class BehaviourResolver(UnpackedScripts scripts) : AssetResolver<ITwinBehaviourGraph>
{
    private ChunkScripts? _chunkScripts;

    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        var code = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var behaviours = code.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
        _chunkScripts = scripts.Begin(ChunkPath, chunk);
        for (var i = 0; i < behaviours.GetItemsAmount(); ++i)
        {
            var itemId = behaviours.GetItem(i).GetID();
            if (itemId % 2 == 0)
            {
                continue;
            }

            CreateAssetFromId(chunk, behaviours, package, itemId);
            // Made of this chunk's graph or of an earlier chunk's of the same script, a graph of another script of the ID being a variant
            _chunkScripts.Resolved(itemId, GetResolvedUri(behaviours.GetItem<ITwinBehaviourGraph>(itemId))!);
        }
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinBehaviourGraph item, bool needVariant, string variant)
    {
        return new BehaviourGraph(package.URI, needVariant, variant, item.GetID(), item.GetName(), item, _chunkScripts!.StarterOf(item), _chunkScripts);
    }

    protected override String GetTwinItemHash(ITwinBehaviourGraph item)
    {
        return item.GetName();
    }
}