using TT_Lab.Assets;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ViewModels;

public sealed class ScenesEditorsViewModel() : EditorsViewerViewModel("ScenesPane", "Scenes")
{
    protected override TabbedEditorViewModel CreateTab(IAsset asset)
    {
        return new SceneEditorTabViewModel(asset);
    }
}
