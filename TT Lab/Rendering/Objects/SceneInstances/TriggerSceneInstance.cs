using System;
using GlmSharp;
using Silk.NET.Maths;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Rendering.Objects.SceneInstances;

public sealed class TriggerSceneInstance : SceneInstance
{
    public TriggerSceneInstance(EditingContext editingContext, TriggerData data, IAsset attachedAsset) : base(editingContext, attachedAsset, data)
    {
        Position = new vec3(data.Position.X, data.Position.Y, data.Position.Z);
        var rotEuler = data.Rotation.ToEulerAngles();
        Rotation = new vec3(rotEuler.X, rotEuler.Y, rotEuler.Z);
        var scale = new vec3(Math.Abs(data.Scale.X), Math.Abs(data.Scale.Y), Math.Abs(data.Scale.Z));
        var bbox = new Box3D<float>(new Vector3D<Single>(-scale.x / 2, -scale.y / 2, -scale.z / 2),
            new Vector3D<Single>(scale.x / 2, scale.y / 2, scale.z / 2));
        Size = scale;
        Offset = new vec3(bbox.Min.X, bbox.Min.Y, bbox.Min.Z);
        SupportedTransforms |= SupportedTransforms.Scale;
    }

    protected override void CreateEditableObject(Renderable? parentNode = null)
    {
        var renderContext = EditingContext.GetRenderContext();
        AttachedEditableObject = new Trigger(renderContext, $"{GetHashCode()}_Trigger_{AttachedAsset.ID}", parentNode!, EditingContext.CreateTriggerBillboard(), GetUserDataAs<TriggerData>(), Size);
    }
}