using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Enumerations;
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

    private readonly Dictionary<string, MetaAsset> _soundsByHash = new();
    private readonly Dictionary<(Type, uint), int> _versions = new();

    public MetaAsset? CreateAssetFromId<T>(ITwinSection itemSection, Package package, uint itemId) where T : SoundEffect
    {
        if (!itemSection.ContainsItem(itemId))
        {
            return null;
        }
        
        var twinSound = itemSection.GetItem<ITwinSound>(itemId);
        var objectHash = Hash<T>(twinSound);
        if (_soundsByHash.TryGetValue(objectHash, out var existing))
        {
            return existing;
        }

        var key = (typeof(T), twinSound.GetID());
        var needVariant = _versions.TryGetValue(key, out var versions) && versions > 0;
        _versions[key] = versions + 1;
        var soundAsset = (T)Activator.CreateInstance(typeof(T), package.URI, needVariant, ChunkPath, twinSound.GetID(), RetailNames.Of(DefaultHashes.Sounds, twinSound.GetID(), twinSound.GetName()), twinSound)!;
        soundAsset.RegenerateLinks();
        var meta = new MetaAsset(soundAsset.URI, soundAsset);
        Assets.Add(meta);
        _soundsByHash.Add(objectHash, meta);
        return meta;
    }

    public LabURI? GetResolvedUri<T>(ITwinSection itemSection, uint itemId) where T : SoundEffect
    {
        if (!itemSection.ContainsItem(itemId))
        {
            return null;
        }

        return _soundsByHash.TryGetValue(Hash<T>(itemSection.GetItem<ITwinSound>(itemId)), out var meta) ? meta.Uri : null;
    }

    // Every language's sounds have the same IDs and often the same records, the item's hash doesn't cover the samples next to them
    private static string Hash<T>(ITwinSound sound) where T : SoundEffect
    {
        return $"{typeof(T).Name} {sound.GetHash()} {Hasher.ComputeHash(sound.Sound ?? [])}";
    }
}