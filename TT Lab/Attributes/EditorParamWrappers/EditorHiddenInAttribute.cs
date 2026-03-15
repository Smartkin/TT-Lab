using System;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes.EditorParamWrappers;

public class EditorHiddenInAttribute(string editorName, bool partialComparison = true) : EditorParamWrapperBaseAttribute
{
    public override void ApplyTo(DocumentNodeViewModel viewModel)
    {
        var isHidden = false;
        if (!string.IsNullOrEmpty(editorName) && viewModel.Property.Parent != null)
        {
            if (PartialComparison)
            {
                isHidden = viewModel.Property.Parent.Name.Contains(editorName,
                    StringComparison.InvariantCultureIgnoreCase);
            }
            else
            {
                isHidden = viewModel.Property.Parent.Name == editorName;
            }
        }
        viewModel.IsVisible = !isHidden;
    }
    
    public bool PartialComparison => partialComparison;
}