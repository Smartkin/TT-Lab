namespace TT_Lab.ViewModels.Editors.Descs;

public record TextEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new TextFieldViewModel(Document, Node);
}