namespace TT_Lab.ViewModels.Editors.Descs;

public record BoolEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new BoolFieldViewModel(Document, Node);
}