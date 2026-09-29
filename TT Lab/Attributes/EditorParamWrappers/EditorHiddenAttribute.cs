using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes.EditorParamWrappers;

// The value has no editor of its own, another one next to it edits it (a particle's texture rectangle, edited with its page)
public class EditorHiddenAttribute : EditorParamWrapperBaseAttribute
{
    public override void ApplyTo(DocumentNodeViewModel viewModel)
    {
        viewModel.IsVisible = false;
    }
}
