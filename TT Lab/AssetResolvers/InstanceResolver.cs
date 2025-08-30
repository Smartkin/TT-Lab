using System;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetResolvers;

public class InstanceResolver<TInstance, TTwinItem>(int layoutId, int sectionId) : AssetResolver<TTwinItem>
    where TInstance : SerializableInstance
    where TTwinItem : ITwinItem
{

    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        var instanceSection = chunk.GetItem<ITwinSection>((uint)layoutId);
        if (instanceSection.GetItemsAmount() <= 0)
        {
            return;
        }
        
        var instancesSection = instanceSection.GetItem<ITwinSection>((uint)sectionId);
        for (var i = 0; i < instancesSection.GetItemsAmount(); i++)
        {
            var item = instancesSection.GetItem(i);
            CreateAssetFromId(chunk, instancesSection, package, item.GetID());
        }
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, TTwinItem item, bool needVariant, string variant)
    {
        return CreateInstance(chunk, package, item);
    }

    private TInstance CreateInstance(ITwinSection chunk, Package package, TTwinItem item)
    {
        return (TInstance)Activator.CreateInstance(typeof(TInstance), package.URI, item.GetID(), item.GetName(), ChunkPath, layoutId, item)!;
    }
}