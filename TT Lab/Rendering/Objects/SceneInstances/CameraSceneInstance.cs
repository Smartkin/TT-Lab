using GlmSharp;
using org.ogre;
using TT_Lab.AssetData.Instance;
using TT_Lab.Extensions;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Rendering.Objects.SceneInstances;

public sealed class CameraSceneInstance : SceneInstance
{
    public CameraSceneInstance(EditingContext editingContext, CameraData data, ResourceTreeElementViewModel attachedViewModel) : base(editingContext, data, attachedViewModel)
    {
        Position = new vec3(-data.Trigger.Position.X, data.Trigger.Position.Y, data.Trigger.Position.Z);
        var rotEuler = data.Trigger.Rotation.ToEulerAngles();
        Rotation = new vec3(rotEuler.X, -rotEuler.Y, -rotEuler.Z);
        Size = new vec3(data.Trigger.Scale.X, data.Trigger.Scale.Y, data.Trigger.Scale.Z);
        Offset = -Size;
        SupportedTransforms |= SupportedTransforms.Scale;
    }

    protected override void CreateEditableObject(SceneNode? parentNode = null)
    {
        var window = EditingContext.GetWindow();
        AttachedEditableObject = new Camera(window, $"{GetHashCode()}_Camera_{AttachedViewModel.Asset.ID}", parentNode!, window.GetSceneManager(), EditingContext.CreateCameraBillboard(), (CameraData)AssetData, Size);
    }
}