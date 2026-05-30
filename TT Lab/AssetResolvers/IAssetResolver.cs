using System.Collections.Generic;
using TT_Lab.Assets;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetResolvers;

public record MetaAsset(LabURI Uri, IAsset Asset);

public interface IAssetResolver
{
    string ChunkPath { get; }
    void CreateAssetsFromChunk(ITwinSection chunk, Package package);
    MetaAsset? CreateAssetFromId(ITwinSection chunk, ITwinSection itemSection, Package package, uint itemId);
    void FinalizeResolve();
    IReadOnlyList<MetaAsset> GetAssets();
}