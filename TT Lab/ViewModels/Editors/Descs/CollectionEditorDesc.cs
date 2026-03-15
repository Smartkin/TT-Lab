namespace TT_Lab.ViewModels.Editors.Descs;

public record CollectionEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new DocumentCollectionViewModel(Document, Node);
}