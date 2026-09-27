using TT_Lab.Assets;

namespace TT_Lab.ViewModels.Composite;

public sealed class ResourceEditorTabViewModel(IAsset asset) : TabbedEditorViewModel(asset);
