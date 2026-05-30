using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetResolvers;

public static class ResolverManager
{
    private static readonly Dictionary<string, Dictionary<Type, IAssetResolver>> ChunkResolvers = new();
    
    public static string ChunkPath { get; private set; } = string.Empty;

    public static void Start()
    {
        ChunkResolvers.Clear();
    }

    public static void Stop()
    {
        ChunkResolvers.Clear();
    }

    public static void PerformResolve(Package package, string chunkPath, ITwinSection chunk, IAssetResolver chunkResolver)
    {
        ChunkPath = chunkPath[..];
        if (ChunkResolvers.TryGetValue(ChunkPath, out var resolvers))
        {
            resolvers.Add(chunkResolver.GetType(), chunkResolver);
        }
        else
        {
            ChunkResolvers[ChunkPath] = [];
            ChunkResolvers[ChunkPath].Add(chunkResolver.GetType(), chunkResolver);
        }
        
        chunkResolver.CreateAssetsFromChunk(chunk, package);
    }

    public static T? GetChunkResolver<T>(string chunkPath) where T : IAssetResolver
    {
        if (ChunkResolvers.TryGetValue(chunkPath, out var resolvers))
        {
            return resolvers.ContainsKey(typeof(T)) ? (T)resolvers[typeof(T)] : default;
        }

        return default;
    }
}