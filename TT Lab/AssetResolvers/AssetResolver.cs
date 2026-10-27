using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetResolvers;

public abstract class AssetResolver<TTwinItem> : IAssetResolver where TTwinItem : ITwinItem
{
    protected readonly Dictionary<string, uint> HashChecker = [];
    private readonly Dictionary<string, MetaAsset> _byHash = [];
    protected readonly List<MetaAsset> Assets = [];
    
    public IAsset CreatedAsset { get; private set; }
    public string? ChunkPathOverride { get; init; }
    public string ChunkPath => (string.IsNullOrEmpty(ChunkPathOverride) ? ResolverManager.ChunkPath[..] : ChunkPathOverride);
    public abstract void CreateAssetsFromChunk(ITwinSection chunk, Package package);

    public MetaAsset? CreateAssetFromId(ITwinSection chunk, ITwinSection itemSection, Package package, uint itemId)
    {
        var twinItem = itemSection.GetItem<TTwinItem>(itemId);
        // The game finds a resource among every loaded chunk's: a part a chunk names without having it (under an item an earlier chunk had
        // the same of, whose parts are resolved again in each chunk) is another chunk's
        if (twinItem == null)
        {
            return null;
        }

        var objectHash = GetTwinItemHash(twinItem);
        if (IsAlreadyContained(objectHash))
        {
            // An earlier chunk's item of the same content is this chunk's asset too, but what it names by IDs is the chunk's: the game reuses
            // IDs for other content in other chunks, a chunk's own picture of a texture ID is under a material, an OGI and an object other
            // chunks have the same of
            ResolveParts(chunk, package, twinItem);
            if (_byHash.TryGetValue(objectHash, out var known))
            {
                Resolved(known.Asset);
            }

            return null;
        }
        
        HashChecker.Add(objectHash, twinItem.GetID());

        var twinIdCollisions = HashChecker.Values.Count(e => e == twinItem.GetID());
        var needVariant = twinIdCollisions > 1;
        ResolveParts(chunk, package, twinItem);
        var labAsset = CreateAsset(chunk, package, twinItem, needVariant, ChunkPath);
        labAsset.RegenerateLinks();
        CreatedAsset = labAsset;
        Assets.Add(new MetaAsset(labAsset.URI, labAsset));
        _byHash[objectHash] = Assets[^1];
        Resolved(labAsset);
        return Assets[^1];
    }

    /// <summary>
    /// The asset an item became, or the one an item with the same content became
    /// </summary>
    public LabURI? GetResolvedUri(TTwinItem item)
    {
        return _byHash.TryGetValue(GetTwinItemHash(item), out var resolved) ? resolved.Uri : null;
    }

    // What the item names by IDs, resolved in the chunk before its asset is made and for an item an earlier chunk had the same of
    protected virtual void ResolveParts(ITwinSection chunk, Package package, TTwinItem item)
    {
    }

    // The asset an item of the chunk is, made of it or of an earlier chunk's item of the same content
    protected virtual void Resolved(IAsset asset)
    {
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