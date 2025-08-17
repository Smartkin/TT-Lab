using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Rendering.Services;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Rendering.Objects.SceneInstances;

public sealed class ObjectSceneInstance : SceneInstance
{
    private readonly TwinSkeletonManager _skeletonManager;
    private readonly MeshService _meshService;

    public ObjectSceneInstance(EditingContext editingContext, TwinSkeletonManager skeletonManager, MeshService meshService, ObjectInstanceData instanceData, ResourceTreeElementViewModel viewModel) : base(editingContext, instanceData, viewModel)
    {
        _skeletonManager = skeletonManager;
        _meshService = meshService;
        Position = new vec3(instanceData.Position.X, instanceData.Position.Y, instanceData.Position.Z);
        Rotation = new vec3(instanceData.RotationX.GetRotation(), instanceData.RotationY.GetRotation(), instanceData.RotationZ.GetRotation());
        
        var assetManager = AssetManager.Get();
        var objData = assetManager.GetAssetData<GameObjectData>(instanceData.ObjectId);
        foreach (var ogiData in from uri in objData.OGISlots where uri != LabURI.Empty select assetManager.GetAssetData<OGIData>(uri))
        {
            Size = new vec3
            {
                x = ogiData.BoundingBox[1].X - ogiData.BoundingBox[0].X,
                y = ogiData.BoundingBox[1].Y - ogiData.BoundingBox[0].Y,
                z = ogiData.BoundingBox[1].Z - ogiData.BoundingBox[0].Z
            };
            Offset = new vec3(ogiData.BoundingBox[0].X, ogiData.BoundingBox[0].Y, ogiData.BoundingBox[0].Z);
            break;
        }

        var renderContext = EditingContext.GetRenderContext();
        // TextDisplay = new TextDisplay(window.GetSceneManager(), this, window.GetCamera());
        // TextDisplay.SetText($"{viewModel.Asset.ID.ToString()} ({viewModel.Asset.LayoutID!.Value.ToString()})");
        // TextDisplay.SetTextColour(ColourValue.Red);
        // TextDisplay.Enable(true);
        AttachedEditableObject = new ObjectInstance(renderContext, _skeletonManager, _meshService, $"{GetHashCode()} Instance of {objData.Name}", instanceData, Size);
    }

    protected override void CreateEditableObject(Renderable? parentNode = null)
    {
        var renderContext = EditingContext.GetRenderContext();
        var assetManager = AssetManager.Get();
        var objData = assetManager.GetAssetData<GameObjectData>(((ObjectInstanceData)AssetData).ObjectId);
        AttachedEditableObject = new ObjectInstance(renderContext, _skeletonManager, _meshService, $"{GetHashCode()} Instance of {objData.Name}", (ObjectInstanceData)AssetData, Size);
    }
}