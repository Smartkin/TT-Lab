using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ViewModels.Editors.Instance;

public abstract class ViewportEditableInstanceViewModel : InstanceSectionResourceEditorViewModel
{
    public virtual Vector4ViewModel Position { get; set; } = new();

    public virtual Vector3ViewModel Rotation { get; set; } = new();

    public virtual Vector3ViewModel Scale { get; set; } = new();
}