using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.AssetResolvers;

public class InstanceSectionResolver(int layoutId) : AssetResolver<ITwinSection>
{
    private readonly InstanceResolver<Trigger, ITwinTrigger> _triggerResolver = new(layoutId, Constants.LAYOUT_TRIGGERS_SECTION);
    private readonly InstanceResolver<Position, ITwinPosition> _positionResolver = new(layoutId, Constants.LAYOUT_POSITIONS_SECTION);
    private readonly InstanceResolver<Path, ITwinPath> _pathResolver = new(layoutId, Constants.LAYOUT_PATHS_SECTION);
    private readonly InstanceResolver<ObjectInstance, ITwinInstance> _objectInstanceResolver = new(layoutId, Constants.LAYOUT_INSTANCES_SECTION);
    private readonly InstanceResolver<InstanceTemplate, ITwinTemplate> _templateResolver = new(layoutId, Constants.LAYOUT_TEMPLATES_SECTION);
    private readonly InstanceResolver<CollisionSurface, ITwinSurface> _collisionSurfaceResolver = new(layoutId, Constants.LAYOUT_SURFACES_SECTION);
    private readonly InstanceResolver<Camera, ITwinCamera> _cameraResolver = new(layoutId, Constants.LAYOUT_CAMERAS_SECTION);
    private readonly InstanceResolver<AiPosition, ITwinAIPosition> _aiPositionResolver = new(layoutId, Constants.LAYOUT_AI_POSITIONS_SECTION);
    private readonly InstanceResolver<AiPath, ITwinAIPath> _aiPathResolver = new(layoutId, Constants.LAYOUT_AI_PATHS_SECTION);
    
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
}