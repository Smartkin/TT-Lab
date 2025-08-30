using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.AssetResolvers;

public class OgiResolver : AssetResolver<ITwinOGI>
{
    private readonly AnimationResolver _animationResolver;
    private readonly BlendSkinResolver _blendSkinResolver;
    private readonly SkinResolver _skinResolver;
    private readonly RigidModelResolver _rigidModelResolver;

    public OgiResolver(AnimationResolver animationResolver)
    {
        _animationResolver = animationResolver;
        var modelResolver = new ModelResolver();
        var textureResolver = new TextureResolver();
        var materialResolver = new MaterialResolver(textureResolver, false);
        _blendSkinResolver = new BlendSkinResolver(materialResolver);
        _skinResolver = new SkinResolver(materialResolver);
        _rigidModelResolver = new RigidModelResolver(modelResolver, materialResolver);
    }
    
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        throw new System.NotImplementedException();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinOGI item, bool needVariant, string variant)
    {
        var graphicsSection = chunk.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var blendSkinsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_BLEND_SKINS_SECTION);
        var skinsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_SKINS_SECTION);
        var rigidModelsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_RIGID_MODELS_SECTION);
        if (item.BlendSkinID != 0)
        {
            _blendSkinResolver.CreateAssetFromId(chunk, blendSkinsSection, package, item.BlendSkinID);
        }

        if (item.SkinID != 0)
        {
            _skinResolver.CreateAssetFromId(chunk, skinsSection, package, item.SkinID);
        }

        foreach (var itemRigidModelId in item.RigidModelIds)
        {
            _rigidModelResolver.CreateAssetFromId(chunk, rigidModelsSection, package, itemRigidModelId);
        }
        
        return new OGI(package.URI, needVariant, variant, item.GetID(), item.GetName(), item);
    }

    public void AddAnimationLinks(Dictionary<ushort, List<ushort>> twinLinks)
    {
        foreach (var (ogiId, animations) in twinLinks)
        {
            var fittingOgis = Assets.Where(meta => meta.Asset.ID == ogiId);
            var animList = new List<LabURI>();
            foreach (var fittingAnims in animations.Select(animRef => _animationResolver.GetAssets().Where(meta => meta.Asset.ID == animRef)))
            {
                animList.AddRange(fittingAnims.Select(meta => meta.Uri));
            }
            foreach (var ogi in fittingOgis)
            {
                ((OGI)ogi.Asset).LinkAnimationsToData(animList);
            }
        }
    }

    public override void FinalizeResolve()
    {
        _rigidModelResolver.FinalizeResolve();
        _skinResolver.FinalizeResolve();
        _blendSkinResolver.FinalizeResolve();
        
        base.FinalizeResolve();
    }
}