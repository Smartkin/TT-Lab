using TT_Lab.ViewModels.Editors.Global;

namespace TT_Lab.ViewModels.Editors.Descs;

public record SaveIconAnimationEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new SaveIconAnimationViewModel(Document, Node);
}
