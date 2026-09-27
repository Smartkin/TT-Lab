namespace TT_Lab.ViewModels.Editors.Descs;

public record LayoutEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new LayoutFieldViewModel(Document, Node);
}
