namespace TT_Lab.ViewModels.Editors.Descs;

public record VectorCharacterDataEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new VectorCharacterDataViewModel(Document, Node);
}