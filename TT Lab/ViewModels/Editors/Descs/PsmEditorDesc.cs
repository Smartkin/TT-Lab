using TT_Lab.ViewModels.Editors.Global;

namespace TT_Lab.ViewModels.Editors.Descs;

public record PsmEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new PsmEditorViewModel(Document, Node);
}
