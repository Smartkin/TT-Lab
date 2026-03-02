using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes.EditorParamWrappers;

public class EditorReadOnlyAttribute : EditorParamWrapperBaseAttribute
{
    public override void ApplyTo(DocumentPartViewModel viewModel)
    {
        viewModel.IsReadOnly = true;
    }
}