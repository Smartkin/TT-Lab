using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.AssetResolvers;

public class SoundResolver : AssetResolver<ITwinSound>
{
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new NotSupportedException();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinSound item, bool needVariant, string variant)
    {
        throw new NotSupportedException();
    }

    public MetaAsset? CreateAssetFromId<T>(ITwinSection itemSection, Package package, uint itemId) where T : SoundEffect
    {
        if (!itemSection.ContainsItem(itemId))
        {
            return null;
        }
        
        var twinSound = itemSection.GetItem<ITwinSound>(itemId);
        var objectHash = twinSound.GetHash();
        var alreadyContained = HashChecker.ContainsKey(objectHash);
        if (alreadyContained)
        {
            return Assets.First(a => a.Asset.ID == twinSound.GetID());
        }
        
        HashChecker.Add(objectHash, twinSound.GetID());

        var twinIdCollisions = HashChecker.Values.Count(e => e == twinSound.GetID());
        var needVariant = twinIdCollisions > 1;
        var soundAsset = (T)Activator.CreateInstance(typeof(T), package.URI, needVariant, ChunkPath, twinSound.GetID(), twinSound.GetName(), twinSound)!;
        Assets.Add(new MetaAsset(soundAsset.URI, soundAsset));
        return Assets[^1];
    }
}