namespace TT_Lab.ViewModels.Editors.Descs;

public record Matrix4EditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new Matrix4FieldViewModel(Document, Node);
}