using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using GlmSharp;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Rendering.Objects.SceneInstances;

[Flags]
public enum SupportedTransforms
{
    None = 0,
    Translate = 1 << TransformMode.TRANSLATE,
    Rotate = 1 << TransformMode.ROTATE,
    Scale = 1 << TransformMode.SCALE,
}
    
public abstract class SceneInstance : IDisposable
{
    private readonly object? _userData;
        
    protected EditableObject AttachedEditableObject;
    protected readonly EditingContext EditingContext;
    protected readonly IAsset AttachedAsset;
    protected SupportedTransforms SupportedTransforms = SupportedTransforms.Translate | SupportedTransforms.Rotate;
        
    protected vec3 Position;
    protected vec3 Rotation;
    protected vec3 Offset;
    protected vec3 Size;
    protected Boolean IsSelected;

    private bool _editedDirectly = false;

    protected SceneInstance(EditingContext editingContext, IAsset attachedAsset, object? userData = null)
    {
        _userData = userData;
        AttachedAsset = attachedAsset;
        EditingContext = editingContext;
    }

    public void Init(Renderable? parentNode)
    {
        CreateEditableObject(parentNode);
        AttachedEditableObject.Init();
    }

    protected abstract void CreateEditableObject(Renderable? parentNode = null);

    protected T GetUserDataAs<T>() where T : class
    {
        Debug.Assert(_userData != null, "Attempting to get unexisting user data!");
        return (T)_userData;
    }

    public IAsset GetAttachedAsset()
    {
        return AttachedAsset;
    }

    public bool IsTransformSupported(TransformMode mode)
    {
        var supportCast = (SupportedTransforms)(1 << (int)mode);
        return SupportedTransforms.HasFlag(supportCast);
    }

    public void Translate(vec3 translation)
    {
        AttachedEditableObject.Translate(translation);
        Position += translation;
    }

    public void Rotate(vec3 rotation)
    {
        var rotQuat = new quat(rotation);
        AttachedEditableObject.Rotate(rotQuat, true);
    }

    public void Scale(vec3 scale)
    {
        AttachedEditableObject.Scale(scale);
    }

    public void Select()
    {
        AttachedEditableObject.Select();
        IsSelected = true;
    }

    public void Deselect()
    {
        IsSelected = false;
        AttachedEditableObject.Deselect();
    }

    public EditableObject GetEditableObject()
    {
        return AttachedEditableObject;
    }

    public vec3 GetOffset()
    {
        return Offset;
    }
        
    public vec3 GetSize()
    {
        return Size;
    }
        
    public vec3 GetPosition()
    {
        return AttachedEditableObject.GetPosition();
    }
        
    public vec3 GetRotation()
    {
        return AttachedEditableObject.GetRotation();
    }

    public vec3 GetScale()
    {
        return AttachedEditableObject.GetScale();
    }

    public mat4 GetTransform()
    {
        return mat4.Translate(Position) * (new quat(Rotation)).ToMat4;
    }

    public mat4 GetWorldTransform()
    {
        return AttachedEditableObject.WorldTransform;
    }

    public void Dispose()
    {
    }
}