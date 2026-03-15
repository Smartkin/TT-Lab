using System;
using GlmSharp;
using Silk.NET.Maths;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Rendering.Objects.SceneInstances;

public sealed class CameraSceneInstance : SceneInstance
{
    public CameraSceneInstance(EditingContext editingContext, CameraData data, IAsset attachedAsset) : base(editingContext, attachedAsset, data)
    {
        Position = new vec3(data.Trigger.Position.X, data.Trigger.Position.Y, data.Trigger.Position.Z);
        var rotEuler = data.Trigger.Rotation;
        Rotation = new vec3(rotEuler.X, rotEuler.Y, rotEuler.Z);
        var scale = new vec3(Math.Abs(data.Trigger.Scale.X), Math.Abs(data.Trigger.Scale.Y), Math.Abs(data.Trigger.Scale.Z));
        var bbox = new Box3D<float>(new Vector3D<Single>(-scale.x / 2, -scale.y / 2, -scale.z / 2),
            new Vector3D<Single>(scale.x / 2, scale.y / 2, scale.z / 2));
        Size = scale;
        Offset = new vec3(bbox.Min.X, bbox.Min.Y, bbox.Min.Z);
        SupportedTransforms |= SupportedTransforms.Scale;
    }

    protected override void CreateEditableObject(Renderable? parentNode = null)
    {
        var window = EditingContext.GetRenderContext();
        AttachedEditableObject = new Camera(window, $"{GetHashCode()}_Camera_{AttachedAsset.ID}", parentNode!, EditingContext.CreateCameraBillboard(), GetUserDataAs<CameraData>(), Size);
    }
}