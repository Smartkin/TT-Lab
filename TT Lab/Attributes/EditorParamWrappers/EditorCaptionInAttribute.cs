using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes.EditorParamWrappers;

// Another caption and hint for the property in the editors of what it means something else in (found like EditorHiddenIn finds them)
public class EditorCaptionInAttribute(string editorName, string? caption = null, string? hint = null) : EditorParamWrapperBaseAttribute
{
    public override void ApplyTo(DocumentNodeViewModel viewModel)
    {
        if (!EditorHiddenInAttribute.IsWithin(viewModel, editorName, true))
        {
            return;
        }

        if (caption != null)
        {
            viewModel.Caption = caption;
        }

        if (hint != null)
        {
            viewModel.Hint = hint;
        }
    }
}
