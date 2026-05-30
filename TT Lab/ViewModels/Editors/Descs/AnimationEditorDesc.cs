using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.ViewModels.Editors.Descs;

public record AnimationEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new AnimationViewModel(Document, Node);
}