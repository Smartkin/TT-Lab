using GlmSharp;
using org.ogre;
using TT_Lab.AssetData.Instance;
using TT_Lab.Extensions;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Rendering.Objects.SceneInstances;

public sealed class TriggerSceneInstance : SceneInstance
{
    public TriggerSceneInstance(EditingContext editingContext, TriggerData data, ResourceTreeElementViewModel attachedViewModel) : base(editingContext, data, attachedViewModel)
    {
        Position = new vec3(-data.Position.X, data.Position.Y, data.Position.Z);
        var rotEuler = data.Rotation.ToEulerAngles();
        Rotation = new vec3(rotEuler.X, -rotEuler.Y, -rotEuler.Z);
        Size = new vec3(data.Scale.X, data.Scale.Y, data.Scale.Z);
        Offset = -Size;
        SupportedTransforms |= SupportedTransforms.Scale;
    }

    protected override void CreateEditableObject(SceneNode? parentNode = null)
    {
        var window = EditingContext.GetWindow();
        AttachedEditableObject = new Trigger(window, $"{GetHashCode()}_Trigger_{AttachedViewModel.Asset.ID}", parentNode!, window.GetSceneManager(), EditingContext.CreateTriggerBillboard(), (TriggerData)AssetData, Size);
    }
}