using TT_Lab.Assets;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetResolvers;

public static class ResolverManager
{
    public static string ChunkPath { get; private set; } = string.Empty;

    public static void PerformResolve(Package package, string chunkPath, ITwinSection chunk, IAssetResolver chunkResolver)
    {
        ChunkPath = chunkPath[..];
        chunkResolver.CreateAssetsFromChunk(chunk, package);
    }
}