using System;
using GlmSharp;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ViewModels.Editors.Instance;

public abstract class ViewportEditableInstanceViewModel : InstanceSectionResourceEditorViewModel
{
    public event Action<quat>? Rotated;
    public event Action<vec3>? Translated;
    
    public virtual Vector4ViewModel Position { get; } = new();

    public virtual Vector3ViewModel Rotation { get; } = new();

    public virtual Vector3ViewModel Scale { get; } = new();

    public void Translate(vec3 translation)
    {
        Position.X += translation.x;
        Position.Y += translation.y;
        Position.Z += translation.z;
        
        Translated?.Invoke(translation);
    }

    public void Rotate(vec3 rotation)
    {
        Rotate(new quat(rotation));
    }

    public void Rotate(quat rotation)
    {
        var curQuat = new quat(new vec3(Rotation.X, Rotation.Y, Rotation.Z));
        var resultRotation = curQuat * rotation;
        var euler = resultRotation.EulerAngles;
        Rotation.X = glm.Degrees((float)euler.x);
        Rotation.Y = glm.Degrees((float)euler.y);
        Rotation.Z = glm.Degrees((float)euler.z);
        
        Rotated?.Invoke(rotation);
    }

    public void ScaleBy(vec3 scale)
    {
        Scale.X *= scale.x;
        Scale.Y *= scale.y;
        Scale.Z *= scale.z;
    }
}