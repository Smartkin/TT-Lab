namespace TT_Lab.ViewModels.Editors.Descs;

public record Vector4EditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new Vector4FieldViewModel(Document, Node);
}