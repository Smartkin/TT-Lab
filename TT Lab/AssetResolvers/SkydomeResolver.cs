using System;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetResolvers;

public class SkydomeResolver : AssetResolver<ITwinSkydome>
{
    private MeshResolver _meshResolver;
    
    public SkydomeResolver()
    {
        const string resourcePath = "Skydome";
        var modelResolver = new ModelResolver(true)
        {
            ChunkPathOverride = resourcePath
        };
        var textureResolver = new TextureResolver(true)
        {
            ChunkPathOverride = resourcePath
        };
        var materialResolver = new MaterialResolver(textureResolver, true)
        {
            ChunkPathOverride = resourcePath
        };
        _meshResolver = new MeshResolver(modelResolver, materialResolver)
        {
            ChunkPathOverride = resourcePath
        };
    }
    
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new System.NotImplementedException();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinSkydome item, bool needVariant, string variant)
    {
        var meshSection = chunk.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION).GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);
        foreach (var itemMesh in item.Meshes)
        {
            _meshResolver.CreateAssetFromId(chunk, meshSection, package, itemMesh);
        }
        
        return new Skydome(package.URI, needVariant, variant, item.GetID(), item.GetName(), item);
    }

    protected override String GetTwinItemHash(ITwinSkydome item)
    {
        return item.GetID().ToString();
    }
}