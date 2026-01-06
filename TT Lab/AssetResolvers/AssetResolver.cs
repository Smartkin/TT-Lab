using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetResolvers;

public abstract class AssetResolver<TTwinItem> : IAssetResolver where TTwinItem : ITwinItem
{
    protected readonly Dictionary<string, uint> HashChecker = [];
    protected readonly List<MetaAsset> Assets = [];

    public string? ChunkPathOverride { get; init; }
    public string ChunkPath => (string.IsNullOrEmpty(ChunkPathOverride) ? ResolverManager.ChunkPath[..] : ChunkPathOverride);
    public abstract void CreateAssetsFromChunk(ITwinSection chunk, Package package);

    public MetaAsset? CreateAssetFromId(ITwinSection chunk, ITwinSection itemSection, Package package, uint itemId)
    {
        var twinItem = itemSection.GetItem<TTwinItem>(itemId);
        var objectHash = GetTwinItemHash(twinItem);
        if (IsAlreadyContained(objectHash))
        {
            return null;
        }
        
        HashChecker.Add(objectHash, twinItem.GetID());

        var twinIdCollisions = HashChecker.Values.Count(e => e == twinItem.GetID());
        var needVariant = twinIdCollisions > 1;
        var labAsset = CreateAsset(chunk, package, twinItem, needVariant, ChunkPath);
        labAsset.RegenerateLinks();
        Assets.Add(new MetaAsset(labAsset.URI, labAsset));
        return Assets[^1];
    }

    protected virtual bool IsAlreadyContained(string hash)
    {
        return HashChecker.ContainsKey(hash);
    }

    protected virtual string GetTwinItemHash(TTwinItem item)
    {
        return item.GetHash();
    }

    protected abstract IAsset CreateAsset(ITwinSection chunk, Package package, TTwinItem item, bool needVariant, string variant);

    public virtual void FinalizeResolve()
    {
        var assetManager = AssetManager.Get();
        Assets.ForEach(asset =>
        {
            asset.Asset.RegenerateLinks();
            assetManager.AddAsset(asset.Asset);
        });
    }

    public virtual IReadOnlyList<MetaAsset> GetAssets() => Assets;
}