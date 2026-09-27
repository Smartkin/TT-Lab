using TT_Lab.Assets;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ViewModels;

public sealed class ResourcesEditorsViewModel() : EditorsViewerViewModel("ResourcesPane", "Resources")
{
    protected override TabbedEditorViewModel CreateTab(IAsset asset)
    {
        return new ResourceEditorTabViewModel(asset);
    }
}
