using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.ViewModels.Editors.Descs;

public record OgiAnimationEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new OgiAnimationFieldViewModel(Document, Node);
}
