namespace TT_Lab.ViewModels.Editors.Descs;

public record EnumEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new EnumFieldViewModel(Document, Node);
}