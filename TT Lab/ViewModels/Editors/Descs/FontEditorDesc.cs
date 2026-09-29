using TT_Lab.ViewModels.Editors.Global;

namespace TT_Lab.ViewModels.Editors.Descs;

public record FontEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new FontEditorViewModel(Document, Node);
}
