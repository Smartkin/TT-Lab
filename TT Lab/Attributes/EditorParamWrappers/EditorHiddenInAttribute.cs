using System;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes.EditorParamWrappers;

public class EditorHiddenInAttribute(string editorName, bool partialComparison = true) : EditorParamWrapperBaseAttribute
{
    public override void ApplyTo(DocumentNodeViewModel viewModel)
    {
        viewModel.IsVisible = !IsWithin(viewModel, editorName, PartialComparison);
    }
    
    public bool PartialComparison => partialComparison;

    // Whether a node above the editor's is named so (or has the name in its own)
    internal static bool IsWithin(DocumentNodeViewModel viewModel, string editorName, bool partialComparison)
    {
        if (string.IsNullOrEmpty(editorName))
        {
            return false;
        }

        var currentParent = viewModel.Property.Parent;
        while (currentParent != null)
        {
            var isWithin = partialComparison
                ? currentParent.Name.Contains(editorName, StringComparison.InvariantCultureIgnoreCase)
                : currentParent.Name == editorName;
            if (isWithin)
            {
                return true;
            }

            currentParent = currentParent.Parent;
        }

        return false;
    }
}