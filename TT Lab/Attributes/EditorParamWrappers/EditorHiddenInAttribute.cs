using System;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes.EditorParamWrappers;

public class EditorHiddenInAttribute(string editorName, bool partialComparison = true) : EditorParamWrapperBaseAttribute
{
    public override void ApplyTo(DocumentNodeViewModel viewModel)
    {
        var isHidden = false;
        if (!string.IsNullOrEmpty(editorName))
        {
            var currentParent = viewModel.Property.Parent;
            while (currentParent != null)
            {
                if (PartialComparison)
                {
                    isHidden = currentParent.Name.Contains(editorName,
                        StringComparison.InvariantCultureIgnoreCase);
                }
                else
                {
                    isHidden = currentParent.Name == editorName;
                }

                if (isHidden)
                {
                    break;
                }
                
                currentParent = currentParent.Parent;
            }
        }
        viewModel.IsVisible = !isHidden;
    }
    
    public bool PartialComparison => partialComparison;
}