namespace TT_Lab.ViewModels.Editors.Descs;

public record Vector2EditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new Vector2FieldViewModel(Document, Node);
}