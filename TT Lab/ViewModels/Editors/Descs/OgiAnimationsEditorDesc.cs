using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.ViewModels.Editors.Descs;

public record OgiAnimationsEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new OgiAnimationsViewModel(Document, Node);
}
