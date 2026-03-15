using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes.EditorParamWrappers;

public class EditorCollectionItemPrefixAttribute(string caption) : EditorParamWrapperBaseAttribute
{
    public override void ApplyTo(DocumentNodeViewModel viewModel)
    {
        viewModel.EditorParameters[DocumentCollectionViewModel.ItemCaptionPrefix] = caption;
    }
}