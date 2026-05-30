using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.AssetResolvers;

public class InstanceSectionResolver : AssetResolver<ITwinSection>
{
    private readonly InstanceResolver<Trigger, ITwinTrigger> _triggerResolver;
    private readonly InstanceResolver<Position, ITwinPosition> _positionResolver;
    private readonly InstanceResolver<Path, ITwinPath> _pathResolver;
    private readonly InstanceResolver<ObjectInstance, ITwinInstance> _objectInstanceResolver;
    private readonly InstanceResolver<InstanceTemplate, ITwinTemplate> _templateResolver;
    private readonly InstanceResolver<CollisionSurface, ITwinSurface> _collisionSurfaceResolver;
    private readonly InstanceResolver<Camera, ITwinCamera> _cameraResolver;
    private readonly InstanceResolver<AiPosition, ITwinAIPosition> _aiPositionResolver;
    private readonly InstanceResolver<AiPath, ITwinAIPath> _aiPathResolver;
    private readonly IAssetResolver[] _assetResolvers;

    public InstanceSectionResolver(int layoutId)
    {
        _triggerResolver = new InstanceResolver<Trigger, ITwinTrigger>(layoutId, Constants.LAYOUT_TRIGGERS_SECTION);
        _positionResolver = new InstanceResolver<Position, ITwinPosition>(layoutId, Constants.LAYOUT_POSITIONS_SECTION);
        _pathResolver = new InstanceResolver<Path, ITwinPath>(layoutId, Constants.LAYOUT_PATHS_SECTION);
        _objectInstanceResolver = new InstanceResolver<ObjectInstance, ITwinInstance>(layoutId, Constants.LAYOUT_INSTANCES_SECTION);
        _templateResolver = new InstanceResolver<InstanceTemplate, ITwinTemplate>(layoutId, Constants.LAYOUT_TEMPLATES_SECTION);
        _collisionSurfaceResolver = new InstanceResolver<CollisionSurface, ITwinSurface>(layoutId, Constants.LAYOUT_SURFACES_SECTION);
        _cameraResolver = new InstanceResolver<Camera, ITwinCamera>(layoutId, Constants.LAYOUT_CAMERAS_SECTION);
        _aiPositionResolver = new InstanceResolver<AiPosition, ITwinAIPosition>(layoutId, Constants.LAYOUT_AI_POSITIONS_SECTION);
        _aiPathResolver = new InstanceResolver<AiPath, ITwinAIPath>(layoutId, Constants.LAYOUT_AI_PATHS_SECTION);

        _assetResolvers = [_triggerResolver, _positionResolver, _pathResolver, _objectInstanceResolver,
                            _templateResolver, _collisionSurfaceResolver, _cameraResolver, _aiPositionResolver, _aiPathResolver];
    }
    
    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        _triggerResolver.CreateAssetsFromChunk(chunk, package);
        _positionResolver.CreateAssetsFromChunk(chunk, package);
        _pathResolver.CreateAssetsFromChunk(chunk, package);
        _objectInstanceResolver.CreateAssetsFromChunk(chunk, package);
        _templateResolver.CreateAssetsFromChunk(chunk, package);
        _collisionSurfaceResolver.CreateAssetsFromChunk(chunk, package);
        _cameraResolver.CreateAssetsFromChunk(chunk, package);
        _aiPositionResolver.CreateAssetsFromChunk(chunk, package);
        _aiPathResolver.CreateAssetsFromChunk(chunk, package);
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinSection item, bool needVariant, string variant)
    {
        throw new System.NotImplementedException();
    }

    public override void FinalizeResolve()
    {
        _objectInstanceResolver.FinalizeResolve();
        _positionResolver.FinalizeResolve();
        _pathResolver.FinalizeResolve();
        _aiPositionResolver.FinalizeResolve();
        _aiPathResolver.FinalizeResolve();
        _triggerResolver.FinalizeResolve();
        _cameraResolver.FinalizeResolve();
        _templateResolver.FinalizeResolve();
        _collisionSurfaceResolver.FinalizeResolve();
    }

    public override IReadOnlyList<MetaAsset> GetAssets()
    {
        return _assetResolvers.SelectMany(resolver => resolver.GetAssets()).ToList();
    }
}