using System;
using System.Collections.Generic;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetResolvers;

public class SkydomeResolver : AssetResolver<ITwinSkydome>
{
    private readonly List<MeshResolver> _meshResolvers = [];

    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new System.NotImplementedException();
    }

    // Skies use the same texture IDs for other pictures (the sun of AltEarth's sky isn't the one of the others), so each keeps its
    // parts in a folder of its own, where its materials find its textures
    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinSkydome item, bool needVariant, string variant)
    {
        var resourcePath = $"Skydome/{item.GetID():X}";
        var textureResolver = new TextureResolver(true)
        {
            ChunkPathOverride = resourcePath
        };
        var meshResolver = new MeshResolver(new ModelResolver(true) { ChunkPathOverride = resourcePath },
            new MaterialResolver(textureResolver, true) { ChunkPathOverride = resourcePath })
        {
            ChunkPathOverride = resourcePath
        };
        _meshResolvers.Add(meshResolver);

        var meshSection = chunk.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION).GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);
        foreach (var itemMesh in item.Meshes)
        {
            meshResolver.CreateAssetFromId(chunk, meshSection, package, itemMesh);
        }

        return new Skydome(package.URI, needVariant, variant, item.GetID(), RetailNames.Of(DefaultHashes.Skydomes, item.GetID(), item.GetName()), item);
    }

    protected override String GetTwinItemHash(ITwinSkydome item)
    {
        return item.GetID().ToString();
    }

    public override void FinalizeResolve()
    {
        _meshResolvers.ForEach(resolver => resolver.FinalizeResolve());

        base.FinalizeResolve();
    }
}
