namespace TT_Lab.ViewModels.Editors.Descs;

public record Vector3EditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new Vector3FieldViewModel(Document, Node);
}